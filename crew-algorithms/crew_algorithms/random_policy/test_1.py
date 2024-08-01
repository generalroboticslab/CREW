import socket
from contextlib import closing

import torch
from crew_algorithms.envs.unity import UnityEnv
from torchrl.collectors import SyncDataCollector
from torchrl.collectors.collectors import RandomPolicy


def find_free_port():
    """Finds an open port on the system.

    Returns:
        The open port.
    """
    with closing(socket.socket(socket.AF_INET, socket.SOCK_STREAM)) as s:
        s.bind(("", 0))
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        return s.getsockname()[1]


def random_policy():
    """An implementation of a random policy."""
    device = "cpu" if not torch.has_cuda else "cuda:0"

    env = UnityEnv(
        "/Users/hyerra/Desktop/3DBalanceBall.app",
        log_folder="/Users/hyerra/Desktop/UnityLogs",
        # "<PATH TO UNITY GAME>",
        # log_folder="<PATH TO WHERE YOU WANT LOGS>",
        base_port=find_free_port(),
        timeout_wait=60 * 60 * 24,
        device=device,
        frame_skip=1,
    )
    policy = RandomPolicy(env.action_spec, action_key=env.action_key)
    collector = SyncDataCollector(
        env,
        policy,
        frames_per_batch=1,
        total_frames=10_000,
        device=device,
        split_trajs=False,
        exploration_mode="random",
    )

    for batch, data in enumerate(collector):
        pass


if __name__ == "__main__":
    random_policy()
