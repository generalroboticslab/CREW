from attrs import define


@define(auto_attribs=True)
class OptimizationConfig:
    utd_ratio: float = 1.0
    gamma: float = 0.99
    lr: float = 1e-4
    weight_decay: float = 0.0
    target_update_polyak: float = 0.995
    target_entropy_weight: float = 0.2
    max_grad_norm: float = 1.0
    alpha_init: float = 0.1
    target_entropy: float = -6.0


@define(auto_attribs=True)
class NetworkConfig:
    hidden_sizes: list = [256, 256]
    activation: str = "relu"
    default_policy_scale: float = 1.0
    scale_lb: float = 0.1
    device: str = "cpu"
