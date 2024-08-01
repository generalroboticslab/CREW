import hydra
from attrs import define
from crew_algorithms.envs.configs import EnvironmentConfig, register_env_configs
from crew_algorithms.utils.wandb_utils import WandbConfig
from hydra.core.config_store import ConfigStore
from omegaconf import MISSING
from torchrl.trainers.helpers.collectors import OnPolicyCollectorConfig


@define(auto_attribs=True)
class Config:
    envs: EnvironmentConfig = MISSING
    """Settings for the environment to use."""
    collector: OnPolicyCollectorConfig = OnPolicyCollectorConfig(frames_per_batch=1)
    """Settings to use for the on-policy collector."""
    wandb: WandbConfig = WandbConfig(project="random")
    """WandB logger configuration."""
    collect_data: bool = True
    """Whether or not to collect data and save a new dataset to WandB."""


cs = ConfigStore.instance()
cs.store(name="base_config", node=Config)
register_env_configs()


@hydra.main(version_base=None, config_path="../conf", config_name="random_policy")
def random_policy(cfg: Config):
    """An implementation of a random policy."""
    import os
    import uuid
    from pathlib import Path

    import torch
    import wandb
    from crew_algorithms.envs.channels import ToggleTimestepChannel
    from crew_algorithms.random_policy.utils import (
        make_env,
        make_policy,
        save_images,
        upload_dataset,
    )
    from crew_algorithms.utils.rl_utils import make_collector
    from torchrl.record.loggers import generate_exp_name, get_logger

    wandb.login()
    exp_name = generate_exp_name("Random", f"random-{cfg.envs.name}")
    logger = get_logger(
        "wandb",
        logger_name=os.getcwd(),
        experiment_name=exp_name,
        wandb_kwargs=dict(
            entity=cfg.wandb.entity,
            project=cfg.wandb.project,
            settings=wandb.Settings(start_method="thread"),
            tags=["baseline", cfg.envs.name],
        ),
    )
    logger.log_hparams(cfg)

    device = "cpu" if not torch.has_cuda else "cuda:0"
    toggle_timestep_channel = ToggleTimestepChannel(uuid.uuid4())

    env_fn = lambda: make_env(cfg.envs, toggle_timestep_channel, device)

    # env_fn = make_env(cfg.envs, toggle_timestep_channel, device)
    env = env_fn()

    policy = make_policy(env)

    env.close()
    collector = make_collector(cfg.collector, env_fn, policy, device)

    if cfg.collect_data:
        data_path = Path(logger.experiment.dir, "images")
        os.makedirs(data_path, exist_ok=True)

    for batch, data in enumerate(collector):
        # print(data)
        pass
    #     if cfg.collect_data:
    #         save_images(
    #             cfg.envs,
    #             data,
    #             data_path,
    #             cfg.collector.frames_per_batch,
    #             batch,
    #         )

    # if cfg.collect_data:
    #     upload_dataset(cfg, logger, data_path)


if __name__ == "__main__":
    random_policy()
