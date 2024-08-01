import hydra
import torch.nn as nn
from attrs import define
from crew_algorithms.envs.configs import EnvironmentConfig, register_env_configs
from crew_algorithms.utils.wandb_utils import WandbConfig
from hydra.core.config_store import ConfigStore
from omegaconf import MISSING, OmegaConf


@define(auto_attribs=True)
class Config:
    envs: EnvironmentConfig = MISSING
    """Settings for the environment to use."""
    max_epochs: int = 200
    """Max number of epochs to train for."""
    log_freq: int = 300
    """How often (number of steps) to log."""
    save_freq: int = 1
    """How often (number of epochs) to save the model."""
    val_ratio: float = 0.05
    """Ratio of data that should be used for the validation set."""
    batch_size: int = 128
    """Batch size to use for the data loaders."""
    learning_rate: float = 1e-4
    """Learning rate to use for the optimizer."""
    scheduler_milestones: list[int] = [80, 160]
    """Milestones to use for the MultiStepLR scheduler."""
    scheduler_gamma: float = 0.1
    """Gamma to use for the MultiStepLR scheduler."""
    data_root: str = "../Data/auto_encoder/"
    """Root directory for the data."""
    num_workers: int = 8
    """Number of workers to use for the data loaders."""
    embed_dim: int = 128
    """Size of the output embedding."""


cs = ConfigStore.instance()
cs.store(name="base_config", node=Config)
register_env_configs()


@hydra.main(version_base=None, config_path="../conf", config_name="auto_encoder")
def autoencoder(cfg: Config):
    """An implementation of the AutoEncoder used in the Boombox project.

    For more details, see the paper: https://www.nature.com/articles/s41598-020-77918-x.
    """
    import torch
    from crew_algorithms.auto_encoder.utils import (
        make_dataloaders,
        make_model,
        make_optim,
        train,
    )

    device = "cpu" if not torch.has_cuda else "cuda:0"
    train_dataloader, val_dataloader = make_dataloaders(cfg)
    net = make_model(cfg.envs.num_channels, cfg.embed_dim)
    criterion = nn.MSELoss()
    optim, lr_scheduler = make_optim(cfg, net)

    train(
        cfg,
        train_dataloader,
        val_dataloader,
        net,
        criterion,
        optim,
        lr_scheduler,
        device,
    )


if __name__ == "__main__":
    autoencoder()
