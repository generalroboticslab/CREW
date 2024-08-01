from copy import deepcopy
from dataclasses import dataclass
from typing import Tuple

import numpy as np
import torch
from tensordict import TensorDict, TensorDictBase, TensorDictParams
from tensordict.nn import TensorDictModule, dispatch
from tensordict.utils import NestedKey, unravel_key
from torchrl.modules.tensordict_module.actors import ActorCriticWrapper
from torchrl.objectives.common import LossModule
from torchrl.objectives.utils import (
    _GAMMA_LMBDA_DEPREC_ERROR,  # _reduce,
    _cache_values,
    default_value_kwargs,
    distance_loss,
)


class TamerLoss(LossModule):
    @dataclass
    class _AcceptedKeys:
        state_action_value: NestedKey = "pred_feedback"
        feedback: NestedKey = "feedback"
        priority: NestedKey = "pred_error"
        # reward: NestedKey = "reward"
        done: NestedKey = "done"
        terminated: NestedKey = "terminated"
        time: NestedKey = "time"
        feedback_time: NestedKey = "feedback_time"

    default_keys = _AcceptedKeys()
    out_keys = [
        "loss_actor",
        "loss_value",
        "pred_value",
        "target_value",
        "pred_value_max",
        "target_value_max",
    ]

    def __init__(
        self,
        actor_network: TensorDictModule,
        value_network: TensorDictModule,
        importance_distribution: torch.distributions.Distribution,
        *,
        loss_function: str = "l2",
        delay_actor: bool = False,
        delay_value: bool = True,
        gamma: float = None,
        separate_losses: bool = False,
        reduction: str = None,
        device: str = None,
    ) -> None:
        self._in_keys = None
        if reduction is None:
            reduction = "mean"
        super().__init__()
        self.delay_actor = delay_actor
        self.delay_value = delay_value

        actor_critic = ActorCriticWrapper(actor_network, value_network)
        params = TensorDict.from_module(actor_critic)
        params_meta = params.apply(self._make_meta_params, device=torch.device("meta"))
        with params_meta.to_module(actor_critic):
            self.__dict__["actor_critic"] = deepcopy(actor_critic)

        self.convert_to_functional(
            actor_network,
            "actor_network",
            create_target_params=self.delay_actor,
        )
        if separate_losses:
            # we want to make sure there are no duplicates in the params: the
            # params of critic must be refs to actor if they're shared
            policy_params = list(actor_network.parameters())
        else:
            policy_params = None
        self.convert_to_functional(
            value_network,
            "value_network",
            create_target_params=self.delay_value,
            compare_against=policy_params,
        )
        self.actor_critic.module[0] = self.actor_network
        self.actor_critic.module[1] = self.value_network

        self.actor_in_keys = actor_network.in_keys
        self.importance_distribution = importance_distribution
        self.device = device
        self.value_exclusive_keys = set(self.value_network.in_keys) - (
            set(self.actor_in_keys) | set(self.actor_network.out_keys)
        )

        self.loss_function = loss_function
        self.reduction = reduction
        if gamma is not None:
            raise TypeError(_GAMMA_LMBDA_DEPREC_ERROR)

    def _set_in_keys(self):
        in_keys = {
            # unravel_key(("next", self.tensor_keys.reward)),
            unravel_key(("next", self.tensor_keys.done)),
            unravel_key(("next", self.tensor_keys.terminated)),
            *self.actor_in_keys,
            *[unravel_key(("next", key)) for key in self.actor_in_keys],
            *self.value_network.in_keys,
            *[unravel_key(("next", key)) for key in self.value_network.in_keys],
        }
        self._in_keys = sorted(in_keys, key=str)

    @property
    def in_keys(self):
        if self._in_keys is None:
            self._set_in_keys()
        return self._in_keys

    @in_keys.setter
    def in_keys(self, values):
        self._in_keys = values

    @dispatch
    def forward(self, tensordict: TensorDictBase) -> TensorDict:
        """Computes the Tamer losses given a tensordict sampled from the replay buffer.

        This function will also write a "pred_error" key that can be used by prioritized replay buffers to assign
            a priority to items in the tensordict.

        Args:
            tensordict (TensorDictBase): a tensordict with keys ["done", "terminated", "reward"] and the in_keys of the actor
                and value networks.

        Returns:
            a tuple of 2 tensors containing the DDPG loss.

        """
        tensordict = tensordict.to(self.device)
        loss_value, metadata = self.loss_value(tensordict)
        loss_actor, metadata_actor = self.loss_actor(tensordict)
        metadata.update(metadata_actor)
        td_out = TensorDict(
            source={
                "loss_actor": loss_actor.mean(),
                "loss_value": loss_value.mean(),
                **metadata,
            },
            batch_size=[],
        )
        return td_out

    def get_importance_weight(self, tensordict: TensorDictBase):
        """Determines the importance weight for a given sample using
        the formula specified by DeepTamer.

        Computes the importance weight using the distribution specified.
        Requires the time and feedback_time keys to be specified in the
        tensordict.

        Args:
            tensordict: Sample to determine the importance weight for.
        """

        device = self.device if self.device is not None else tensordict.device
        tddevice = tensordict.to(device)

        start_time, end_time = torch.tensor_split(
            tddevice.get(self.tensor_keys.time), indices=[1], dim=-1
        )

        feedback_time = tddevice.get(self.tensor_keys.feedback_time)

        start_time_ = start_time.detach().cpu().numpy()
        end_time_ = end_time.detach().cpu().numpy()
        feedback_time_ = feedback_time.detach().cpu().numpy()

        # Generate multiple points for the range between feedback-end_time
        # and feedback-start_time for better integral approximation.
        num_samples = 100
        y = np.linspace(
            feedback_time_ - end_time_,
            feedback_time_ - start_time_,
            num=num_samples,
            axis=-1,
        )

        # Compute the probability density for each of the points.
        y = torch.from_numpy(y).to(device)
        y = torch.exp(self.importance_distribution.log_prob(y))

        # Integrate to get final importance weight.
        importance_weight = torch.trapz(y, dx=1 / num_samples).to(device)
        return importance_weight

    def loss_actor(
        self,
        tensordict: TensorDictBase,
    ) -> [torch.Tensor, dict]:
        td_copy = tensordict.select(
            *self.actor_in_keys, *self.value_exclusive_keys, strict=False
        ).detach()
        with self.actor_network_params.to_module(self.actor_network):
            td_copy = self.actor_network(td_copy)
        with self._cached_detached_value_params.to_module(self.value_network):
            td_copy = self.value_network(td_copy)
        loss_actor = (
            -td_copy.get(self.tensor_keys.state_action_value).squeeze(-1).mean()
        )
        metadata = {}
        # loss_actor = _reduce(loss_actor, self.reduction)

        return loss_actor, metadata

    def loss_value(
        self,
        tensordict: TensorDictBase,
    ) -> Tuple[torch.Tensor, dict]:
        # value loss
        importance_weight = self.get_importance_weight(tensordict)

        td_copy = tensordict.select(*self.value_network.in_keys, strict=False).detach()
        with self.value_network_params.to_module(self.value_network):
            self.value_network(td_copy)
        pred_val = td_copy.get(self.tensor_keys.state_action_value).squeeze(-1)

        target_value = td_copy.get(self.tensor_keys.feedback).squeeze(-1)

        # pred_error = pred_val - target_value
        loss_value = (
            importance_weight
            * distance_loss(
                pred_val, target_value, loss_function=self.loss_function
            ).mean()
        )

        pred_error = (pred_val - target_value).pow(2)
        pred_error = pred_error.detach()
        if tensordict.device is not None:
            pred_error = pred_error.to(tensordict.device)
        tensordict.set(
            self.tensor_keys.priority,
            pred_error,
            inplace=True,
        )
        with torch.no_grad():
            metadata = {
                "pred_error": pred_error,
                "pred_value": pred_val,
                "target_value": target_value,
                "target_value_max": target_value.max(),
                "pred_value_max": pred_val.max(),
            }

        # loss_value = _reduce(loss_value, self.reduction)
        return loss_value, metadata

    @property
    @_cache_values
    def _cached_target_params(self):
        target_params = TensorDict(
            {
                "module": {
                    "0": self.target_actor_network_params,
                    "1": self.target_value_network_params,
                }
            },
            batch_size=self.target_actor_network_params.batch_size,
            device=self.target_actor_network_params.device,
        )
        return target_params

    @property
    @_cache_values
    def _cached_detached_value_params(self):
        return self.value_network_params.detach()
