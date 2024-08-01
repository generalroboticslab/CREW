import torch
import torch.nn as nn
from crew_algorithms.auto_encoder import Encoder_Nature
from tensordict import TensorDictBase
from torchrl.data.tensor_specs import ContinuousBox, TensorSpec
from torchrl.data.utils import DEVICE_TYPING
from torchrl.envs.transforms.transforms import (
    Compose,
    ObservationTransform,
    _apply_to_composite,
)
from torchrl.envs.transforms.utils import _set_missing_tolerance


class _EncoderNet(ObservationTransform):
    """A torchrl env transform that converts pixel inputs to encoded vectors.

    Args:
        in_keys: The input keys to transform.
        out_keys: The keys where the output will be stored.
        num_channels: The number of channels that will be fed to the encoder.
        env_name: The name of the environment that the encoder was trained on.
            Currently only supports 'bowling'. (You can use __main__ to train encoders for other environments.)
        del_keys: Whether to delete the input keys after transformation.
        outdim: The dimension of the output vector.
    """

    def __init__(
        self,
        in_keys,
        out_keys,
        num_channels,
        env_name: str = "bowling",
        del_keys: bool = True,
        outdim: int = 64,
    ):
        super().__init__(in_keys=in_keys, out_keys=out_keys)
        self.outdim = outdim
        self.encoder = Encoder_Nature(num_channels, self.outdim)
        self.encoder.eval()
        self.del_keys = del_keys
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.env_name = env_name

    def load_weights(self):
        """Loads the weights for the encoder.

        Args:
            env_name: Name of the environment to load weights for.
        """

        if self.env_name == "bowling":
            state_dict = torch.load(
                "crew_algorithms/auto_encoder/weights/encoder_bowling.pth"
            )
            print("Loaded bowling encoder")
        else:
            print(
                "Currently only supports bowling encoder, random initializaed encoder will be used."
            )
            return

        self.encoder.load_state_dict(state_dict)

    def _apply_transform(self, obs: torch.Tensor) -> torch.Tensor:
        obs = obs.to(self.device)
        out = self.encoder(obs).detach()
        out = nn.Flatten()(out)
        return out

    def _reset(
        self, tensordict: TensorDictBase, tensordict_reset: TensorDictBase
    ) -> TensorDictBase:
        with _set_missing_tolerance(self, True):
            tensordict_reset = self._call(tensordict_reset)
        return tensordict_reset

    @_apply_to_composite
    def transform_observation_spec(self, observation_spec: TensorSpec) -> TensorSpec:
        space = observation_spec.space
        if isinstance(space, ContinuousBox):
            space.low = self._apply_transform(space.low)
            space.high = self._apply_transform(space.high)
            observation_spec.shape = space.low.shape
        else:
            observation_spec.shape = self._apply_transform(
                torch.zeros(observation_spec.shape)
            ).shape
        return observation_spec


class EncoderTransform(Compose):
    def __init__(
        self,
        env_name: str,
        num_channels: int,
        in_keys: list[str] | None = None,
        out_keys: list[str] | None = None,
        out_dim: int = 64,
    ):
        """A torchrl compose transform that encodes raw observations into a smaller dimension by using a
        pretrained Encoder.

        This Encoder transform uses the pretrained Encoder part of the
        AutoEncoder outlined here:
        https://www.nature.com/articles/s41598-020-77918-x.

        Args:
            env_name: The name of the environment to use the encoder for.
            num_channels: The number of channels that will be fed to the
                encoder.
            in_keys: The input keys to transform.
            out_keys: The keys where the output will be stored.
        """
        self._device = None
        self._dtype = None
        self.env_name = env_name
        in_keys = in_keys if in_keys is not None else ["observation"]
        out_keys = out_keys if out_keys is not None else ["encoder_vec"]

        transforms = []
        network = _EncoderNet(
            in_keys=in_keys,
            out_keys=out_keys,
            num_channels=num_channels,
            del_keys=True,
            env_name=env_name,
            outdim=out_dim,
        )

        network.load_weights(env_name)
        transforms.append(network)

        super().__init__(*transforms)

        if self._device is not None:
            self.to(self._device)
        if self._dtype is not None:
            self.to(self._dtype)

    def to(self, dest: DEVICE_TYPING | torch.dtype):
        if isinstance(dest, torch.dtype):
            self._dtype = dest
        else:
            self._device = dest
        return super().to(dest)

    @property
    def device(self):
        return self._device

    @property
    def dtype(self):
        return self._dtype
