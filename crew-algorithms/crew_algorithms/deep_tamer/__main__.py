import json
from collections import defaultdict
from time import time

import hydra
import numpy as np
from attrs import define
from crew_algorithms.envs.configs import EnvironmentConfig, register_env_configs
from crew_algorithms.utils.common_utils import get_time
from crew_algorithms.utils.wandb_utils import WandbConfig
from hydra.core.config_store import ConfigStore
from omegaconf import MISSING
from torchrl.trainers.helpers.collectors import OffPolicyCollectorConfig


@define(auto_attribs=True)
class Config:
    envs: EnvironmentConfig = MISSING
    """Settings for the environment to use."""
    collector: OffPolicyCollectorConfig = OffPolicyCollectorConfig(
        exploration_mode="mode", frames_per_batch=8, total_frames=100_000
    )
    """Settings to use for the off-policy collector."""
    wandb: WandbConfig = WandbConfig(project="crew-deeptamer")
    """WandB logger configuration."""
    mini_batch_size: int = 16
    """Size of the mini batches stored in the Replay Buffer."""
    buffer_storage: int = 15_000
    """Maximium size of the Replay Buffer."""
    buffer_update_interval: int = 1
    """Interval by which to update the buffer."""
    learning_rate: float = 1e-4
    """Rate at which to learn."""
    from_states: bool = False
    """Whether to use the states as input to the policy."""
    log_smoothing: int = 100
    """Number of episodes to smooth the log over."""
    train_minutes: int = 10
    """Train for this many minutes."""


cs = ConfigStore.instance()
cs.store(name="base_config", node=Config)
register_env_configs()


@hydra.main(version_base=None, config_path="../conf", config_name="deep_tamer")
def deep_tamer(cfg: Config):
    """An implementation of the DeepTamer algorithm.

    For more details, see the DeepTamer paper: https://arxiv.org/pdf/1709.10163.pdf.
    """
    import os
    import uuid

    import torch
    import wandb
    from crew_algorithms.deep_tamer.utils import (
        feedback_applies_to_sample,
        make_agent,
        make_data_buffer,
        make_env,
        make_loss,
        make_optim,
        step_policy,
        visualize,
    )
    from crew_algorithms.envs.channels import ToggleTimestepChannel
    from crew_algorithms.utils.common_utils import SortedItem
    from crew_algorithms.utils.rl_utils import log_policy, make_collector
    from torchrl.record.loggers import generate_exp_name, get_logger

    wandb.login()
    exp_name = generate_exp_name("DeepTamer", f"deep-tamer-{cfg.envs.name}")
    logger = get_logger(
        "wandb",
        logger_name=os.getcwd(),
        experiment_name=exp_name,
        wandb_kwargs=dict(
            project=cfg.wandb.project,
            settings=wandb.Settings(start_method="thread"),
            tags=[cfg.envs.name],
        ),
    )
    logger.log_hparams(cfg)

    device = "cpu" if not torch.has_cuda else "cuda:0"
    toggle_timestep_channel = ToggleTimestepChannel(uuid.uuid4())

    run_name = get_time() + "_" + cfg.envs.name + "_deep_tamer"
    os.makedirs("../Data/deep_tamer/" + run_name, exist_ok=True)

    env_fn = lambda: make_env(cfg.envs, toggle_timestep_channel, device)
    env = env_fn()

    model, actor = make_agent(cfg, env, device)
    env.close()

    collector = make_collector(cfg.collector, env_fn, actor, device)
    sample_storage, replay_buffer = make_data_buffer(cfg, run_name)
    loss_module, target_net_updater = make_loss(
        model, device, cfg.envs.credit_window_right
    )
    optim = make_optim(cfg, loss_module)

    log_reward = defaultdict(float)
    log_feedback = defaultdict(float)

    episode_reward= defaultdict(float)
    last_traj_ID = 0
    collected_frames = 0

    episode_success = []

    i = 0
    save_weights_timer = 0

    begin_time = time()

    torch.save(model.state_dict(), "../Data/deep_tamer/" + run_name + "/" + "0.0.pth")

    for data in collector:
        collected_frames += data.numel()
        episode_success.extend(
            data["next", "agents", "reward"][
                data["next", "agents", "done"] == True
            ].tolist()
        )
        for single_data_view in data.unbind(0):
            single_data_view["feedback"] = single_data_view[
                ("agents", "observation", "obs_1")
            ][:, -1, 0:1]

            start_time = single_data_view[("agents", "observation", "obs_1")][:, -1, 1]
            end_time = single_data_view[("next", "agents", "observation", "obs_1")][
                :, -1, 1
            ]
            single_data_view["time"] = torch.cat([start_time, end_time], dim=-1)
            traj_ID = single_data_view[("agents", "observation", "obs_1")][
                :, -1, 2
            ].item()
            next_ID = single_data_view[("next", "agents", "observation", "obs_1")][
                :, -1, 2
            ].item()

            episode_reward[traj_ID] += single_data_view[
                "next", "agents", "reward"
            ].item()
            if traj_ID != last_traj_ID:
                r_list = [episode_reward[ep] for ep in episode_reward]
                avg_ep_r = np.array(
                    r_list[max(0, len(r_list) - cfg.log_smoothing - 1) : -1]
                ).mean()
                logger.log_scalar("avg_episode_reward", avg_ep_r, step=collected_frames)

                last_traj_ID = traj_ID

            if len(episode_success) > 10:
                avg_sr = sum(episode_success[-cfg.log_smoothing - 1 :]) / min(
                    cfg.log_smoothing, len(episode_success)
                )
                logger.log_scalar("success_rate", avg_sr, step=collected_frames)

            i += 1
            single_data_view["feedback"] = single_data_view["feedback"].clamp(-1, 1)

            if single_data_view["feedback"] != 0:
                print(
                    "Feedback Received: %.1f| Time: %dmin%ds"
                    % (
                        single_data_view["feedback"].item(),
                        single_data_view["time"][-1].item() // 60,
                        single_data_view["time"][-1].item() % 60,
                    )
                )

            sorted_item = SortedItem(
                single_data_view["time"][-1].item(), single_data_view
            )

            logger.log_scalar(
                "feedback",
                single_data_view["feedback"].item(),
                single_data_view["agents", "step_count"].item(),
            )
            logger.log_scalar(
                "reward",
                single_data_view["next", "agents", "reward"].item(),
                single_data_view["agents", "step_count"].item(),
            )
            sample_storage.add(sorted_item)
            if single_data_view["feedback"] != 0:
                current_buffer = []

                for j in range(len(sample_storage) - 1, -1, -1):
                    sample = sample_storage[j].item.clone(False)
                    sample["feedback_time"] = single_data_view["time"][-1].clone()
                    sample["feedback"] = single_data_view["feedback"].clone()

                    # Clear old samples from the sample storage
                    if sample["time"][-1].item() < sample["feedback_time"].item() - 10:
                        sample_storage.pop(j)
                        continue

                    if feedback_applies_to_sample(sample, loss_module):
                        current_buffer.append(sample)
                        replay_buffer.add(sample)

                if current_buffer:
                    step_policy(
                        torch.stack(current_buffer),
                        loss_module,
                        optim,
                        target_net_updater,
                    )

            if i % cfg.buffer_update_interval == 0 and len(replay_buffer):
                step_policy(
                    replay_buffer.sample(), loss_module, optim, target_net_updater
                )

            if next_ID == traj_ID:
                log_reward[traj_ID] = single_data_view[
                    "next", "agents", "reward"
                ].item()
                log_feedback[traj_ID] += single_data_view["feedback"].item()

            if single_data_view["time"][-1].item() - save_weights_timer > 60:
                save_weights_timer = single_data_view["time"][-1].item()
                torch.save(
                    model.state_dict(),
                    "../Data/deep_tamer/"
                    + run_name
                    + "/"
                    + str(round(single_data_view["time"][-1].item() / 60, 1))
                    + ".pth",
                )

            os.makedirs("crew_algorithms/deep_tamer/logs", exist_ok=True)
            with open(
                "crew_algorithms/deep_tamer/logs/" + run_name + "feedback.json", "w"
            ) as f:
                json.dump(log_feedback, f)

        if time() - begin_time > 60 * cfg.train_minutes:
            os.makedirs("../Data/saved_training/%s" % run_name, exist_ok=True)
            replay_buffer.dumps("../Data/saved_training/%s/prb.pkl" % run_name)
            collector.shutdown()
            return None


if __name__ == "__main__":
    deep_tamer()
