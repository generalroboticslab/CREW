from attrs import define


@define(auto_attribs=True)
class OptimizationConfig:
    utd_ratio: float = 1.0
    gamma: float = 0.99
    lr: float = 1e-4
    weight_decay: float = 0.0
    target_update_polyak: float = 0.995
    max_grad_norm: float = 1.0
    exploration_noise: float = 0.1


@define(auto_attribs=True)
class NetworkConfig:
    hidden_sizes: list = [256, 256]
    activation: str = "relu"
    default_policy_scale: float = 1.0
    device: str = "cpu"
