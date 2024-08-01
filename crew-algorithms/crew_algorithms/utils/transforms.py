from typing import Sequence

import torch
from tensordict.tensordict import TensorDictBase
from tensordict.utils import NestedKey
from torchrl.data.tensor_specs import TensorSpec
from torchrl.data.utils import DEVICE_TYPING
from torchrl.envs.transforms.transforms import (
    Compose,
    FlattenObservation,
    ObservationTransform,
    ToTensorImage,
    _apply_to_composite,
)


class CatUnitySensorsAlongChannelDimTransform(Compose):
    def __init__(
        self,
        in_keys: list[str] | None = None,
        out_keys: list[str] | None = None,
    ):
        self._device = None
        self._dtype = None
        in_keys = in_keys if in_keys is not None else ["observation"]
        out_keys = out_keys if out_keys is not None else ["observation"]

        transforms = []

        # ToTensor

        # Input data of the form:
        # (num_agents, num_sensors, width, height, num_stacks * channels).
        # Converts it to Tensor format.
        totensor = ToTensorImage(in_keys=in_keys)
        transforms.append(totensor)

        # Our data is currently in the form (num_agents, num_sensors,
        # num_stacks * channels, width, height). We want our data to
        # be stored in the form (num_agents,
        # num_sensors * num_stacks * channels, width, height).
        #
        # We flatten the observation so that multiple sensors per observation
        # are stored along the channel dimension rather than on the observation
        # dimension.
        flatten = FlattenObservation(in_keys=in_keys, first_dim=-4, last_dim=-3)
        transforms.append(flatten)

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


class RandomShift(ObservationTransform):
    def __init__(
        self,
        a: int,
        b: int,
        in_keys: Sequence[NestedKey] | None = None,
        out_keys: Sequence[NestedKey] | None = None,
    ):
        if in_keys is None:
            in_keys = IMAGE_KEYS  # default
        if out_keys is None:
            out_keys = copy(in_keys)
        super().__init__(in_keys=in_keys, out_keys=out_keys)
        self.a = a
        self.b = b

        # self.trans = transforms.Compose([
        #     transforms.Pad((a, b), padding_mode='edge'),
        #     transforms.RandomCrop((100, 100))
        # ])

        self.trans = RandomAffine(
            degrees=(0, 0),
            translate=(a, b),
            scale=(1.0, 1.0),
            interpolation=InterpolationMode.BILINEAR,
        )

    def _apply_transform(self, observation: torch.Tensor) -> torch.Tensor:
        observation = self.trans(observation)
        return observation

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

    def __repr__(self) -> str:
        return (
            f"{self.__class__.__name__}("
            f"a={float(self.a):4.4f}, b={float(self.b):4.4f}, "
        )


class RandRotate(ObservationTransform):
    def __init__(
        self,
        d: int,
        in_keys: Sequence[NestedKey] | None = None,
        out_keys: Sequence[NestedKey] | None = None,
    ):
        if in_keys is None:
            in_keys = IMAGE_KEYS  # default
        if out_keys is None:
            out_keys = copy(in_keys)
        super().__init__(in_keys=in_keys, out_keys=out_keys)
        self.d = d

        self.trans = RandomRotation(
            degrees=(-d, d), interpolation=InterpolationMode.BILINEAR
        )

    def _apply_transform(self, observation: torch.Tensor) -> torch.Tensor:
        observation = self.trans(observation)
        return observation

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

    def __repr__(self) -> str:
        return f"{self.__class__.__name__}(" f"d={float(self.d):4.4f}, "


class RandRotateChoice(ObservationTransform):
    def __init__(
        self,
        ds: Sequence[float],
        in_keys: Sequence[NestedKey] | None = None,
        out_keys: Sequence[NestedKey] | None = None,
    ):
        if in_keys is None:
            in_keys = IMAGE_KEYS  # default
        if out_keys is None:
            out_keys = copy(in_keys)
        super().__init__(in_keys=in_keys, out_keys=out_keys)
        self.ds = ds

    def _apply_transform(self, observation: torch.Tensor) -> torch.Tensor:
        observation = transforms.functional.rotate(
            observation,
            random.choice(self.ds),
            interpolation=InterpolationMode.BILINEAR,
        )
        return observation

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

    def __repr__(self) -> str:
        return f"{self.__class__.__name__}(" f"d={float(self.d):4.4f}, "


class AddCoordinates(ObservationTransform):
    def __init__(
        self,
        in_keys: Sequence[NestedKey] | None = None,
        out_keys: Sequence[NestedKey] | None = None,
    ):
        if in_keys is None:
            in_keys = IMAGE_KEYS  # default
        if out_keys is None:
            out_keys = copy(in_keys)
        super().__init__(in_keys=in_keys, out_keys=out_keys)
        self.d = 1.0

    def _apply_transform(self, observation: torch.Tensor) -> torch.Tensor:
        x_lin = torch.linspace(1, -1, steps=observation.shape[-2])
        y_lin = torch.linspace(10, -10, steps=observation.shape[-1])

        X, Y = torch.meshgrid(x_lin, y_lin)
        grid = torch.stack((X, Y), dim=0)

        batch_grid = (
            grid.unsqueeze(0)
            .repeat(observation.shape[0], 1, 1, 1)
            .to(observation.device)
        )
        if len(observation.shape) == 5:
            observation = observation.squeeze(1)

        return torch.cat((observation, batch_grid), dim=-3)

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

    def __repr__(self) -> str:
        return f"{self.__class__.__name__}(" f"d={float(self.d):4.4f}, "
