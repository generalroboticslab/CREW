import numpy as np
import torch
from torchrl.data.tensor_specs import CompositeSpec, UnboundedContinuousTensorSpec
from torchrl.data.utils import numpy_to_torch_dtype_dict

observation_spec = CompositeSpec(
    {
        "agents": CompositeSpec(
            {
                "observation": torch.stack(
                    [
                        CompositeSpec(
                            {
                                "obs_0": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_1": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_2": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_3": UnboundedContinuousTensorSpec(
                                    shape=[4],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                            }
                        ),
                        CompositeSpec(
                            {
                                "obs_0": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_1": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_2": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_3": UnboundedContinuousTensorSpec(
                                    shape=[4],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                            }
                        ),
                        CompositeSpec(
                            {
                                "obs_0": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_1": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_2": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_3": UnboundedContinuousTensorSpec(
                                    shape=[4],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                            }
                        ),
                        CompositeSpec(
                            {
                                "obs_0": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_1": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_2": UnboundedContinuousTensorSpec(
                                    shape=[128, 128, 3],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                                "obs_3": UnboundedContinuousTensorSpec(
                                    shape=[4],
                                    device="cpu",
                                    dtype=numpy_to_torch_dtype_dict[
                                        np.dtype("float32")
                                    ],
                                ),
                            }
                        ),
                    ],
                    dim=0,
                )
            },
            shape=(4,),
        )
    }
)
print(observation_spec)
print(observation_spec["agents", "observation"][0])

# specs = [CompositeSpec({
#     "observation_0": UnboundedContinuousTensorSpec(
#         shape=torch.Size([128, 128, 3]),
#         device="cpu",
#         dtype=torch.float32)}),
#     CompositeSpec({
#         "observation_1": UnboundedContinuousTensorSpec(
#         shape=torch.Size([128, 128, 3]),
#         device="cpu",
#         dtype=torch.float32)}), CompositeSpec({
#     "observation_2": UnboundedContinuousTensorSpec(
#         shape=torch.Size([128, 128, 3]),
#         device="cpu",
#         dtype=torch.float32)}), CompositeSpec({
#     "observation_3": UnboundedContinuousTensorSpec(
#         shape=torch.Size([4]),
#         device="cpu",
#         dtype=torch.float32)})]

# spec = torch.stack(specs, dim=0).unsqueeze(dim=0)
