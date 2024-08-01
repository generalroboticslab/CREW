import json
from collections import defaultdict
from time import time

import hydra
from attrs import define
from crew_algorithms.envs.configs import EnvironmentConfig, register_env_configs
from crew_algorithms.utils.common_utils import get_time
from crew_algorithms.utils.wandb_utils import WandbConfig
from hydra.core.config_store import ConfigStore
from omegaconf import MISSING
from torchrl.envs.utils import ExplorationType, set_exploration_type
from torchrl.trainers.helpers.collectors import OffPolicyCollectorConfig


@define(auto_attribs=True)
class Config:
    envs: EnvironmentConfig = MISSING
    """Settings for the environment to use."""
    collector: OffPolicyCollectorConfig = OffPolicyCollectorConfig(
        exploration_mode="mode", frames_per_batch=1, total_frames=100_000
    )
    """Settings to use for the off-policy collector."""
    mini_batch_size: int = 16
    """Size of the mini batches stored in the Replay Buffer."""
    buffer_storage: int = 50_000
    """Maximium size of the Replay Buffer."""
    buffer_update_interval: int = 20
    """Interval by which to update the buffer."""
    learning_rate: float = 2.5e-2
    """Rate at which to learn."""
    from_states: bool = False
    """Whether to use the states as input to the policy."""
    seed: int = 42
    """Seed to use for reproducibility."""
    exp_path: str = "none"
    """Path to the experiment weights folder."""
    eval_weights: list[int] = []


cs = ConfigStore.instance()
cs.store(name="base_config", node=Config)
register_env_configs()


@hydra.main(version_base=None, config_path="../conf", config_name="deep_tamer")
def eval(cfg: Config):
    """An implementation of the DeepTamer algorithm.

    For more details, see the DeepTamer paper: https://arxiv.org/pdf/1709.10163.pdf.
    """
    import uuid
    from collections import defaultdict

    import torch
    from crew_algorithms.deep_tamer.utils import make_agent, make_env
    from crew_algorithms.envs.channels import ToggleTimestepChannel
    from crew_algorithms.utils.common_utils import SortedItem
    from crew_algorithms.utils.rl_utils import log_policy, make_collector

    device = "cpu" if not torch.has_cuda else "cuda:0"
    toggle_timestep_channel = ToggleTimestepChannel(uuid.uuid4())

    env_fn = lambda: make_env(cfg.envs, toggle_timestep_channel, False, device)
    env = env_fn()

    model, actor = make_agent(cfg, env, device)
    env.close()

    eval_envs = 10 if cfg.envs.name == "bowling" else 100

    path = f"../Data/{cfg.exp_path}/"
    scores = defaultdict(int)

    for m in cfg.eval_weights:
        scores[m] = 0
        with set_exploration_type(ExplorationType.MODE), torch.no_grad():
            try:
                if cfg.envs.name == "bowling":
                    weights = torch.load(path + "%d.0.pth" % (m))
                else:
                    weights = torch.load(path + "%d.0.pth" % (m * 2))
            except:
                if cfg.envs.name == "bowling":
                    weights = torch.load(path + "%d.1.pth" % (m))
                else:
                    weights = torch.load(path + "%d.1.pth" % (m * 2))

            try:
                model.load_state_dict(weights)
            except:
                weights = {k.replace("0.module", "0.td_module.module", 1): v for k, v in weights.items()}
                model.load_state_dict(weights)

            collector = make_collector(cfg.collector, env_fn, model[0], device)
            collector.set_seed(cfg.seed)
            for data in collector:
                if data["collector", "traj_ids"].max() >= eval_envs:
                    print(scores)
                    with open(path + f"results.json", "w") as f:
                        json.dump(scores, f)
                    collector.shutdown()
                    break
                score = data[("next", "agents", "reward")]
                if score != 0:
                    scores[m] += score.item()
                    print(scores)
                    with open(path + f"results.json", "w") as f:
                        json.dump(scores, f)
                    if cfg.envs.name == "bowling":
                        collector.shutdown()
                        break


if __name__ == "__main__":
    eval()
