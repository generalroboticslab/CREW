import os
from datetime import datetime
from torchvision.utils import save_image
import torch
from crew_algorithms.auto_encoder import EncoderTransform
from crew_algorithms.auto_encoder.model import Encoder, StateEncoder
from crew_algorithms.deep_tamer.loss import TamerLoss
from crew_algorithms.deep_tamer.policy import (
    ContinuousActorNet,
    ContinuousQValueNet,
    HFNet,
)
from crew_algorithms.envs.channels import ToggleTimestepChannel
from crew_algorithms.envs.configs import EnvironmentConfig
from crew_algorithms.utils.rl_utils import make_base_env
from crew_algorithms.utils.transforms import CatUnitySensorsAlongChannelDimTransform
from sortedcontainers import SortedList
from tensordict.nn import TensorDictModule
from torch import nn
from torchrl.data.replay_buffers import (
    LazyMemmapStorage,
    RandomSampler,
    TensorDictReplayBuffer,
)
from torchrl.data.tensor_specs import ContinuousBox, DiscreteTensorSpec
from torchrl.envs import Compose, EnvBase, StepCounter, TransformedEnv
from torchrl.envs.transforms.transforms import (
    CatFrames,
    CenterCrop,
    Resize,
    ToTensorImage,
    UnsqueezeTransform,
)
from torchrl.modules import (
    AdditiveGaussianWrapper,
    QValueActor,
    SafeModule,
    SafeSequential,
    TanhModule,
)
from torchrl.objectives import SoftUpdate
from torchvision.utils import save_image


def make_env(
    cfg: EnvironmentConfig,
    toggle_timestep_channel: ToggleTimestepChannel,
    device: str,
):
    """Creates an environment based on the configuration that can be used for
    a random policy.

    Args:
        cfg: The environment configuration to be used.
        written_feedback_channel: A Unity side channel that can be used
            to share written feedback at the end of each episode.
        device: The device to perform environment operations on.

    Returns:
        The environment that can be used for the random policy.
    """

    env = TransformedEnv(
        make_base_env(cfg, device, toggle_timestep_channel=toggle_timestep_channel),
        Compose(
            ToTensorImage(in_keys=[("agents", "observation", "obs_0")], unsqueeze=True),
            CenterCrop(
                cfg.crop_h, cfg.crop_w, in_keys=[("agents", "observation", "obs_0")]
            ),
            Resize(100, 100, in_keys=[("agents", "observation", "obs_0")]),
            UnsqueezeTransform(
                unsqueeze_dim=-3, in_keys=[("agents", "observation", "obs_1")]
            ),
        ),
    )

    if cfg.pretrained_encoder:
        env.append_transform(
            EncoderTransform(
                env_name=cfg.name,
                num_channels=cfg.num_stacks * cfg.num_channels,
                in_keys=[("agents", "observation", "obs_0")],
                out_keys=[(("agents", "observation", "encoded_vec"))],
                version="latest",
            )
        )
    env.append_transform(
        CatFrames(
            N=cfg.num_stacks, dim=-3, in_keys=[("agents", "observation", "obs_0")]
        )
    )
    env.append_transform(
        CatFrames(
            N=cfg.num_stacks, dim=-2, in_keys=[("agents", "observation", "obs_1")]
        )
    )
    env.append_transform(StepCounter())

    return env


def make_agent(cfg, proof_env, device):
    action_spec = proof_env.action_spec
    if isinstance(action_spec.space, ContinuousBox):
        model, actor = make_agent_continuous(proof_env, cfg, device)
    else:
        model, actor = make_agent_discrete(proof_env, cfg, device)

    return model, actor


def make_agent_continuous(proof_env, cfg, device):
    action_dims = proof_env.action_spec.space.low.shape[-1]
    additional_in_keys = {tuple(v): k for k, v in cfg.envs.additional_in_keys.items()}

    if cfg.envs.pretrained_encoder:
        in_keys = {("agents", "observation", "encoded_vec"): "obs"}
        in_keys.update(additional_in_keys)
        encoder = nn.Identity()
        in_dims = 64 * cfg.envs.num_stacks + (cfg.envs.additional_in_keys != {})
    elif cfg.from_states:
        in_keys = {("agents", "observation", "obs_1"): "obs"}
        in_keys.update(additional_in_keys)
        encoder = StateEncoder(cfg.envs.state_start_dim, cfg.envs.state_end_dim)
        in_dims = (
            cfg.envs.state_end_dim - cfg.envs.state_start_dim
        ) * cfg.envs.num_stacks + (cfg.envs.additional_in_keys != {})
    else:
        in_keys = {("agents", "observation", "obs_0"): "obs"}
        in_keys.update(additional_in_keys)
        encoder = Encoder(cfg.envs.num_channels, 64)
        in_dims = 64 * cfg.envs.num_stacks + (cfg.envs.additional_in_keys != {})

    actor_net = ContinuousActorNet(
        encoder=encoder,
        n_agent_inputs=in_dims,
        num_cells=256,
        out_dims=action_dims,
    )

    actor_module = TensorDictModule(
        actor_net,
        in_keys=in_keys,
        out_keys=[
            ("agents", "param"),
        ],
    )
    actor = SafeSequential(
        actor_module,
        TanhModule(
            in_keys=[("agents", "param")],
            out_keys=[("agents", "action")],
            low=cfg.envs.action_low,
            high=cfg.envs.action_high,
        ),
    ).to(device)

    value_net = ContinuousQValueNet(
        encoder=encoder,
        n_agent_inputs=in_dims + action_dims,
        num_cells=256,
    )

    value_module = SafeModule(
        value_net,
        in_keys={
            **in_keys,
            **{("agents", "action"): "action"},
            **{("feedback"): "feedback"},
            **{("time"): "time"},
            **{("feedback_time"): "feedback_time"},
        },
        out_keys=["pred_feedback"],
    ).to(device)

    model = nn.Sequential(actor, value_module)

    actor_model_explore = AdditiveGaussianWrapper(
        model[0],
        sigma_end=1.0,
        sigma_init=1.0,
        mean=0.0,
        std=0.1,
        safe=False,
        action_key=("agents", "action"),
    ).to(device)

    return model, actor_model_explore


def make_agent_discrete(proof_env, encoder, in_keys, in_dims, cfg, device):
    num_actions = proof_env.action_spec.space.n
    hf_net = HFNet(
        encoder=encoder,
        in_dims=in_dims,
        num_actions=num_actions,
    )

    hf_module = SafeModule(
        hf_net,
        in_keys=in_keys,
        out_keys=["pred_feedback"],
    )

    actor = QValueActor(
        module=hf_module,
        spec=proof_env.action_spec,
        action_value_key="pred_feedback",
    )
    return actor, actor


def make_data_buffer(cfg, exp_name):
    """Makes the data storage elements required for the DeepTamer policy.

    Args:
        cfg: The DeepTamer configuration.

    Returns:
        A sorted list to store recent experiences.
        A replay buffer which can be used to store experiences.
    """
    sample_storage = SortedList()
    replay_buffer = TensorDictReplayBuffer(
        storage=LazyMemmapStorage(
            cfg.buffer_storage, scratch_dir="../Data/Buffer/prb_dptm_%s_" % exp_name
        ),
        sampler=RandomSampler(),
        batch_size=cfg.mini_batch_size,
    )
    return sample_storage, replay_buffer


def make_loss(model, device, credit_window_right=1.0):
    """Creates the loss used in DeepTamer.

    Args:
        model: The model to use to compute the loss.
        device: The device to perform operations on.
        credit_window_right: The right side of the credit window.
    Returns:
        The TamerLoss.
    """
    importance_distribution = torch.distributions.Uniform(0.2, credit_window_right)
    loss = TamerLoss(
        actor_network=model[0],
        value_network=model[1],
        importance_distribution=importance_distribution,
        loss_function="l2",
        delay_actor=False,
        delay_value=True,
        device=device,
    )

    target_net_updater = SoftUpdate(loss, eps=0.995)
    return loss, target_net_updater


def make_optim(cfg, loss_module: TamerLoss):
    """Creates the SGD optimizer used in DeepTamer.

    Args:
        cfg: The DeepTamer configuration.
        loss_module: The loss module being used to update the policy.

    Returns:
        The SGD optimizer.
    """
    # policy_params = loss_module.policy_network_params.values(True, True)
    policy_params = loss_module.parameters()
    optim = torch.optim.Adam(policy_params, cfg.learning_rate)
    return optim


def feedback_applies_to_sample(sample, loss_module: TamerLoss):
    """Determines if the feedback given applies to the sample.

    Checks the importance weight of the sample, and returns False
    if it is close to zero.

    Args:
        sample: The sample to check if the feedback applies to.
        loss_module: The loss module to use to determine the importance
            weight.

    Returns:
        `True` if the feedback applies to the sample, otherwise `False`.
    """
    importance_weight = loss_module.get_importance_weight(sample)
    # print(sample)
    # print('Importance weight:', importance_weight)
    return not torch.allclose(importance_weight, torch.zeros_like(importance_weight))


def step_policy(sampled_tensordict, loss_module, optim, target_net_updater):
    """Performs one optimization step of the policy.

    Args:
        sampled_tensordict: The sampled data to optimize the policy with.
        loss_module: The loss module to compute the loss from.
        optim: The optimizer to optimize the policy with.
    """
    sampled_tensordict = sampled_tensordict.clone()
    optim.zero_grad()
    loss_td = loss_module(sampled_tensordict)
    loss = loss_td["loss_actor"] + loss_td["loss_value"]
    if loss != 0:
        # print('Loss: %.5f' %loss.item())
        print(
            "actor_loss: %.5f, value_loss: %.5f"
            % (loss_td["loss_actor"].item(), loss_td["loss_value"].item())
        )
    loss.backward()
    torch.nn.utils.clip_grad_norm_(loss_module.parameters(), 1)
    optim.step()
    target_net_updater.step()


def visualize(data, i):
    current_frame = data["agents", "observation", "obs_0"]
    next_frame = data["next", "agents", "observation", "obs_0"]

    name = datetime.now().strftime("%H%M%S")
    os.makedirs("visualize", exist_ok=True)
    save_image(current_frame, "visualize/frame_%d_%s.png" % (i, name))
