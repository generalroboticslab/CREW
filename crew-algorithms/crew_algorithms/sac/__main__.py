import json
import pickle
from time import time
import hydra
from attrs import define
from crew_algorithms.sac.audio_feedback import Audio_Streamer
from crew_algorithms.sac.config import NetworkConfig, OptimizationConfig
from crew_algorithms.sac.logger import custom_logger
from crew_algorithms.envs.configs import EnvironmentConfig, register_env_configs
from crew_algorithms.utils.wandb_utils import WandbConfig
from hydra.core.config_store import ConfigStore
from omegaconf import MISSING
from tensordict import TensorDict
from torch.optim import Adam
from torchrl.trainers.helpers.collectors import OffPolicyCollectorConfig


@define(auto_attribs=True)
class Config:
    envs: EnvironmentConfig = MISSING
    """Settings for the environment to use."""
    optimization: OptimizationConfig = OptimizationConfig()
    network: NetworkConfig = NetworkConfig()
    collector: OffPolicyCollectorConfig = OffPolicyCollectorConfig(
        frames_per_batch=240, init_random_frames=0
    )
    """Settings to use for the off-policy collector."""
    wandb: WandbConfig = WandbConfig(project="crew-sac")
    """WandB logger configuration."""
    batch_size: int = 240
    """Batch size to use for training."""
    buffer_size: int = 15_000
    """Size of the replay buffer."""
    num_envs: int = 1
    """Number of parallel environments to use."""
    seed: int = 42
    """Seed to use for reproducibility."""
    from_states: bool = False
    """Whether to use structured states as input"""
    traj: bool = False
    """Whether to use trajectory feedback"""
    use_expert: bool = False
    """Whether to use expert data"""
    heuristic_feedback: bool = False
    """Whether to use heuristic feedback"""
    hf: bool = False
    """Whether to use human feedback"""
    history: bool = False
    """Whether to use past transitions to predict human feedback"""
    log_smoothing: int = 100
    """Number of episodes to smooth the log"""
    continue_training: str = "none"
    """Path to the model to continue training"""
    sub_name: str = "00"
    """Experiment Subject name"""
    audio_feedback: bool = False
    """Whether to use audio feedback"""
    written_feedback: bool = False
    """Whether to use written feedback"""
    train_batches: int = 10
    """Train for train_batches x frames_per_batch environment steps"""
    visualize_batches: int = 0
    """Visualize for visualize_batches x frames_per_batch environment steps"""


cs = ConfigStore.instance()
cs.store(name="base_config", node=Config)
register_env_configs()


@hydra.main(version_base=None, config_path="../conf", config_name="sac")
def sac(cfg: Config):
    import os
    import random
    import uuid
    from collections import defaultdict, deque

    import numpy as np
    import torch
    import wandb
    from crew_algorithms.envs.channels import WrittenFeedbackChannel
    from crew_algorithms.sac.trajectory_feedback import TrajectoryFeedback
    from crew_algorithms.sac.utils import (
        audio_feedback,
        combine_feedback_and_rewards,
        feedback_model_train_step,
        get_time,
        gradient_weighted_average_transform,
        heuristic_feedback,
        human_delay_transform,
        load_training,
        make_agent,
        make_data_buffer,
        make_env,
        make_loss_module,
        make_optimizer,
        override_il_feedback,
        provide_learned_feedback,
        save_training,
        visualize,
    )
    from crew_algorithms.utils.rl_utils import make_collector
    from sortedcontainers import SortedList
    from torchrl.record.loggers import get_logger

    torch.manual_seed(cfg.seed)
    torch.cuda.manual_seed(cfg.seed)
    random.seed(cfg.seed)
    np.random.seed(cfg.seed)
    cfg.envs.seed = cfg.seed

    wandb.login()

    exp = "sac_hf" if cfg.hf else "sac"
    is_heu = "heuristic" if cfg.heuristic_feedback else ""
    run_name = f"{get_time()}_{cfg.envs.name}_{is_heu}_{exp}_seed_{str(cfg.seed)}"

    logger = get_logger(
        "wandb",
        logger_name=os.getcwd(),
        experiment_name=run_name,
        wandb_kwargs=dict(
            project=cfg.wandb.project,
            settings=wandb.Settings(start_method="thread"),
            tags=[cfg.envs.name],
        ),
    )
    logger.log_hparams(cfg)

    device = "cuda" if torch.cuda.is_available() else "cpu"
    print("Device:", device)

    """Prepare trajectory que for written feedback"""
    if cfg.written_feedback:
        ranked_trajectories = SortedList()
        written_feedback_queue = deque()
        with open("crew_algorithms/sac/written_feedback_queue.pkl", "wb") as f:
            pickle.dump(written_feedback_queue, f)

    def append_feedback(w):
        with open("crew_algorithms/sac/written_feedback_queue.pkl", "rb") as f:
            written_feedback_queue = pickle.load(f)
        written_feedback_queue.append(w)
        with open("crew_algorithms/sac/written_feedback_queue.pkl", "wb") as f:
            pickle.dump(written_feedback_queue, f)

    written_feedback_channel = WrittenFeedbackChannel(
        uuid.uuid4(),
        append_feedback,
    )

    env_fn = lambda: make_env(cfg.envs, written_feedback_channel, False, device)
    env = env_fn()

    model, actor, feedback_model = make_agent(cfg, env, device)
    loss_module, target_net_updater = make_loss_module(cfg, env, model)
    env.close()

    prb, prb_e = make_data_buffer(cfg, run_name)
    collected_frames = 0
    episode_success = []
    global_start_time = time()

    local_logger = custom_logger(
        path="crew_algorithms/sac/logs/" + run_name + ".json",
        start_time=global_start_time,
    )

    collector = make_collector(cfg.collector, env_fn, actor, device, cfg.num_envs)
    collector.set_seed(cfg.seed)

    """A short buffer later used for handling human feedback delay"""
    human_delay_buffer_td = None

    """Dictionary to store episode rewards"""
    episode_reward, episode_reward_hf = defaultdict(float), defaultdict(float)
    """List to store human feedback and heuristic feedback values"""
    all_hf, all_heu = [], []

    num_success, num_trajs, last_traj = 0, 0, 0
    loss = None

    """Heuristic feedback provider"""
    heuristic = heuristic_feedback(
        cfg.envs.target_img, 0.95, cfg.collector.frames_per_batch, device
    )
    optimizer = make_optimizer(cfg, loss_module)

    deploy_learned_feedback = False

    os.makedirs(f"../Data/{cfg.sub_name}/{exp}/{run_name}", exist_ok=True)
    torch.save(model.state_dict(), f"../Data/{cfg.sub_name}/{exp}/{run_name}/0.pth")

    if cfg.audio_feedback:
        stream = Audio_Streamer()
        stream.start_streaming()

    """ Main training loop """
    for i, data in enumerate(collector):
        time_stamp = time() - global_start_time
        logger.log_scalar("time", time_stamp, step=collected_frames)
        local_logger.log(
            x_axis="steps",
            y_axis="learned_feedback_deployed",
            x_value=collected_frames,
            y_value=deploy_learned_feedback,
            log_time=True,
        )

        collector.update_policy_weights_()
        data = data.view(-1)

        num_trajs += data["next", "agents", "done"].sum().item()
        episode_success.extend(
            data["next", "agents", "reward"][
                data["next", "agents", "done"] == True
            ].tolist()
        )
        num_success += int(data["next", "agents", "reward"].sum().item())

        data["done"] = data["agents", "done"]

        current_frames = data.numel()
        collected_frames += current_frames

        if cfg.envs.name in ["find_treasure", "hide_and_seek_1v1"]:
            data.set(
                ("next", "agents", "heuristic_feedback"),
                heuristic.provide_feedback(data),
            )

        if cfg.heuristic_feedback:
            data.set(
                ("next", "agents", "feedback"),
                data[("next", "agents", "heuristic_feedback")],
            )
        else:
            data.set(
                ("next", "agents", "feedback"),
                torch.zeros_like(data[("next", "agents", "reward")]).to(device),
            )

        for j in range(len(data)):
            time_stamp = data[("next", "agents", "observation", "obs_1")][
                j, ..., -1, 1
            ].item()
            traj_j = data["agents", "observation", "obs_1"][j, ..., -1, 2].int().item()
            episode_reward[traj_j] += data["next", "agents", "reward"][j].item()

            if cfg.hf:
                episode_reward_hf[traj_j] += data["agents", "observation", "obs_1"][
                    j, ..., -1, 0
                ].item()

            if traj_j > last_traj:
                r_list = [episode_reward[ep] for ep in episode_reward]
                avg_ep_r = np.array(
                    r_list[max(0, len(r_list) - cfg.log_smoothing - 1) : -1]
                ).mean()
                logger.log_scalar("avg_episode_reward", avg_ep_r, step=collected_frames)
                local_logger.log(
                    x_axis="steps",
                    y_axis="avg_episode_reward",
                    x_value=collected_frames,
                    y_value=avg_ep_r,
                    log_time=True,
                )

                if cfg.hf:
                    r_list_hf = [episode_reward_hf[ep] for ep in episode_reward_hf]
                    avg_ep_r_hf = np.array(
                        r_list_hf[max(0, len(r_list_hf) - cfg.log_smoothing - 1) : -1]
                    ).mean()
                    logger.log_scalar(
                        "avg_episode_reward_hf", avg_ep_r_hf, step=collected_frames
                    )
                    local_logger.log(
                        x_axis="steps",
                        y_axis="avg_episode_reward_hf",
                        x_value=collected_frames,
                        y_value=avg_ep_r_hf,
                        log_time=True,
                    )

                sr_list = [1 if episode_reward[ep] > 0 else 0 for ep in episode_reward]
                avg_sr = np.array(
                    sr_list[max(0, len(sr_list) - cfg.log_smoothing - 1) : -1]
                ).mean()
                logger.log_scalar("success_rate", avg_sr, step=collected_frames)
                local_logger.log(
                    x_axis="steps",
                    y_axis="success_rate",
                    x_value=collected_frames,
                    y_value=avg_sr,
                    log_time=True,
                )
                last_traj = traj_j

        local_logger.data["all_rewards"] = {
            k: round(v, 4) for k, v in episode_reward.items()
        }
        data.set(
            ("next", "agents", "reward"),
            data[("next", "agents", "reward")] * cfg.envs.scale_reward
            + cfg.envs.shift_reward,
        )

        data["time_stamp"] = torch.tensor([round(time_stamp, 4)] * data.numel())

        data["agents", "observation", "obs_1"][
            data["agents", "observation", "obs_1"] == -9
        ] = 0
        data["next", "agents", "observation", "obs_1"][
            data["next", "agents", "observation", "obs_1"] == -9
        ] = 0

        if cfg.hf:
            if cfg.envs.name in ["find_treasure", "hide_and_seek_1v1"]:
                hf_values = data["agents", "observation", "obs_1"][..., -1, 0]
                all_hf.extend(hf_values.squeeze(1).tolist())
                all_heu.extend(
                    data[("next", "agents", "heuristic_feedback")]
                    .squeeze(1)
                    .squeeze(1)
                    .tolist()
                )

            if human_delay_buffer_td is not None:
                data = torch.cat([human_delay_buffer_td, data], dim=0)

            human_delay_td, human_delay_buffer_td = human_delay_transform(
                data, ("agents", "observation", "obs_1"), cfg.envs.human_delay_steps
            )

            # grad_average_td = gradient_weighted_average_transform(
            #     human_delay_td, ("agents", "observation", "obs_1"), 5
            # )
            grad_average_td = human_delay_td
            il_feedback_td = grad_average_td
            il_feedback_td = override_il_feedback(
                grad_average_td,
                ("agents", "observation", "obs_1"),
                ("agents", "observation", "obs_1"),
                1,
            )

            il_feedback_td.set(
                ("next", "agents", "feedback"),
                il_feedback_td.get(("agents", "observation", "obs_1"))[
                    ..., -1, 0
                ].unsqueeze(dim=-1),
            )  # write human feedback to feedback field
        else:
            il_feedback_td = data

        combined_rewards = combine_feedback_and_rewards(
            il_feedback_td,
            ("next", "agents", "feedback"),
            ("next", "agents", "reward"),
            cfg.envs.dense_reward_scale,
        )  # add feedback to reward


        print(
            "Rewards:", combined_rewards.get(("next", "agents", "reward")).sum().item()
        )
        if len(combined_rewards) > 0:
            prb.extend(combined_rewards.cpu())

        total_collected_epochs = (
            data[("agents", "observation", "obs_1")][..., -1, 2].int().max().item()
        )

        print(
            "\n------- Iter: %d | Traj %d| Time: %.2f -------"
            % (i, num_trajs, time() - global_start_time)
        )
        print("num_success: %d" % (num_success))

        t1, t2 = [], []
        if collected_frames >= cfg.collector.init_random_frames:
            (
                total_losses,
                actor_losses,
                q_losses,
                alpha_losses,
                alphas,
                entropies,
            ) = ([], [], [], [], [], [])

            for _ in range(
                int(cfg.collector.frames_per_batch * (cfg.optimization.utd_ratio))
            ):
                tic = time()

                if cfg.audio_feedback:
                    stream.get_sample()

                if cfg.use_expert:
                    sampled_expert = (
                        prb_e.sample(batch_size=cfg.batch_size // 2).clone().to(device)
                    )
                    sampled_new = (
                        prb.sample(batch_size=cfg.batch_size // 2).clone().to(device)
                    )
                    sampled_tensordict = torch.cat([sampled_expert, sampled_new], dim=0)
                else:
                    sampled_tensordict = prb.sample().clone().to(device)

                loss_td = loss_module(sampled_tensordict)

                actor_loss = loss_td["loss_actor"]
                q_loss = loss_td["loss_qvalue"]
                alpha_loss = loss_td["loss_alpha"]

                optimizer.zero_grad()
                loss = actor_loss + q_loss + alpha_loss
                loss.backward()
                torch.nn.utils.clip_grad_norm_(
                    model.parameters(), cfg.optimization.max_grad_norm
                )
                optimizer.step()

                target_net_updater.step()
                t1.append(time() - tic)
                if (_ + 1) % 1 == 0:
                    if cfg.use_expert:
                        sampled_expert = sampled_tensordict[:cfg.batch_size//2]
                        sampled_new = sampled_tensordict[cfg.batch_size//2:]
                        prb_e.update_tensordict_priority(sampled_expert)
                        prb.update_tensordict_priority(sampled_new)
                    else:
                        prb.update_tensordict_priority(sampled_tensordict)
                t2.append(time() - tic)

                total_losses.append(loss.item())
                actor_losses.append(actor_loss.item())
                q_losses.append(q_loss.item())
                alpha_losses.append(alpha_loss.item())
                alphas.append(loss_td["alpha"].item())
                entropies.append(loss_td["entropy"].item())

        metrics = {
            "collected_frames": collected_frames,
            "collected_traj": num_trajs,
            "time": time_stamp,
        }
        if loss is not None:
            metrics.update(
                {
                    "total_loss": np.mean(total_losses),
                    "actor_loss": np.mean(actor_losses),
                    "q_loss": np.mean(q_losses),
                    "alpha_loss": np.mean(alpha_losses),
                    "alpha": np.mean(alphas),
                    "entropy": np.mean(entropies),
                }
            )

        for key, value in metrics.items():
            logger.log_scalar(key, value, step=collected_frames)
            local_logger.log(
                x_axis="steps",
                y_axis=key,
                x_value=collected_frames,
                y_value=value,
                log_time=True,
            )

        local_logger.save_log()

        torch.save(
            model.state_dict(),
            f"../Data/{cfg.sub_name}/{exp}/{run_name}/{str(i+1)}.pth",
        )
        if (i + 1) >= cfg.train_batches:
            if cfg.hf:
                save_training(
                    model,
                    feedback_model,
                    prb,
                    episode_success,
                    all_hf,
                    all_heu,
                    loss_module,
                    f"{cfg.sub_name}/saved_training/{run_name}",
                    i + 1,
                    cfg.collector.frames_per_batch,
                )
            collector.shutdown()
            return 0

    if cfg.audio_feedback:
        stream.stop_streaming()
    print("end- ", round(time() - global_start_time, 4))


if __name__ == "__main__":
    sac()
