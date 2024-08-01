from pathlib import Path
from typing import Callable

from attrs import define
from hydra.core.config_store import ConfigStore
from omegaconf import MISSING


@define(auto_attribs=True)
class EnvironmentConfig:
    name: str = MISSING
    num_channels: int = 3
    num_stacks: int = MISSING
    action_space: str = MISSING
    action_dims: int = MISSING
    unity_server_build_path: Path = MISSING
    unity_server_build_path_osx: Path = MISSING
    unity_server_build_path_linux: Path = MISSING
    log_folder_path: Path = "../UnityLogs"
    human_delay_steps: int = 1  # number of steps to shift human feedback
    no_graphics: bool = False
    time_scale: float = 1.0
    seed: int = 42
    pretrained_encoder: bool = False  # whether to use a pretrained encoder
    additional_in_keys: dict = {}  # additional keys to include in the input
    target_img: Path = "assets/treasure.png"
    scale_reward: float = 1.0
    shift_reward: float = 0.0
    dense_reward_scale: float = 1.0
    credit_window_right: float = 4.0


@define(auto_attribs=True)
class BowlingConfig(EnvironmentConfig):
    name: str = "bowling"
    num_agents: int = 1
    num_channels: int = 1
    num_stacks: int = 1
    action_space: str = "continuous"
    action_dims: int = 3
    unity_server_build_path_linux: Path = (
        "../crew-dojo/Builds/Bowling-StandaloneLinux64-Server/Unity.x86_64"
    )
    unity_server_build_path_osx: Path = (
        "../crew-dojo/Builds/Bowling-StandaloneOSX-Server/Unity"
    )
    unity_server_build_path_windows: Path = (
        "../crew-dojo/Builds/Bowling-StandaloneWindows-Server/Unity.exe"
    )
    log_folder_path: Path = "../UnityLogs"
    human_delay_steps: int = 4
    no_graphics: bool = False
    time_scale: float = 1.0
    seed: int = 42
    state_start_dim: int = 4
    state_end_dim: int = 36
    pretrained_encoder: bool = False
    crop_h: int = 60
    crop_w: int = 120
    action_low: float = -1.0
    action_high: float = 1.0
    scale_reward: float = 1.0
    shift_reward: float = 0.0
    dense_reward_scale: float = 5.0
    credit_window_right: float = 4.0


@define(auto_attribs=True)
class FindTreasureConfig(EnvironmentConfig):
    name: str = "find_treasure"
    num_agents: int = 1
    num_channels: int = 3
    num_stacks: int = 3
    rand_maze: bool = False
    action_space: str = "continuous"
    action_dims: int = 2
    unity_server_build_path_linux: Path = (
        "../crew-dojo/Builds/FindTreasure-StandaloneLinux64-Server/Unity.x86_64"
    )
    unity_server_build_path_osx: Path = (
        "../crew-dojo/Builds/FindTreasure-StandaloneOSX-Server/Unity"
    )
    unity_server_build_path_windows: Path = (
        "../crew-dojo/Builds/FindTreasure-StandaloneWindows-Server/Unity.exe"
    )
    log_folder_path: Path = "../UnityLogs"
    human_delay_steps: int = 2
    no_graphics: bool = False
    time_scale: float = 1.0
    seed: int = 42
    state_start_dim: int = 5
    state_end_dim: int = 9
    pretrained_encoder: bool = False
    additional_in_keys: dict = {"step_count": ("agents", "step_count")}
    crop_h: int = 100
    crop_w: int = 100
    action_low: float = -10.0
    action_high: float = 10.0
    target_img: Path = "assets/treasure.png"
    scale_reward: float = 11.0
    shift_reward: float = -1.0
    dense_reward_scale: float = 1.0
    credit_window_right: float = 1.0


@define(auto_attribs=True)
class HideAndSeek1V1Config(EnvironmentConfig):
    name: str = "hide_and_seek_1v1"
    num_agents: int = 1
    num_channels: int = 3
    num_stacks: int = 3
    rand_maze: bool = False
    action_space: str = "continuous"
    action_dims: int = 2
    unity_server_build_path_linux: Path = (
        "../crew-dojo/Builds/HideAndSeek_Single-StandaloneLinux64-Server/Unity.x86_64"
    )
    unity_server_build_path_osx: Path = (
        "../crew-dojo/Builds/HideAndSeek_Single-StandaloneOSX-Server/Unity"
    )
    unity_server_build_path_windows: Path = (
        "../crew-dojo/Builds/HideAndSeek_Single-StandaloneWindows-Server/Unity.exe"
    )
    log_folder_path: Path = "../UnityLogs"
    human_delay_steps: int = 2
    no_graphics: bool = False
    time_scale: float = 1.0
    seed: int = 42
    state_start_dim: int = 5
    state_end_dim: int = 9
    pretrained_encoder: bool = False
    additional_in_keys: dict = {"step_count": ("agents", "step_count")}
    crop_h: int = 100
    crop_w: int = 100
    action_low: float = -10.0
    action_high: float = 10.0
    target_img: Path = "assets/hider.png"
    scale_reward: float = 11.0
    shift_reward: float = -1.0
    dense_reward_scale: float = 1.0
    credit_window_right: float = 1.0


@define(auto_attribs=True)
class HideAndSeekConfig(EnvironmentConfig):
    name: str = "hide_and_seek"
    num_hiders: int = 2
    num_seekers: int = 2
    num_channels: int = 3
    num_stacks: int = 3
    rand_maze: bool = False
    action_space: str = "continuous"
    action_dims: int = 2
    unity_server_build_path_linux: Path = (
        "../crew-dojo/Builds/HideAndSeek-StandaloneLinux64-Server/Unity.x86_64"
    )
    unity_server_build_path_osx: Path = (
        "../crew-dojo/Builds/HideAndSeek-StandaloneOSX-Server/Unity"
    )
    unity_server_build_path_windows: Path = (
        "../crew-dojo/Builds/HideAndSeek-StandaloneWindows-Server/Unity.exe"
    )
    log_folder_path: Path = "../UnityLogs"
    human_delay_steps: int = 2
    no_graphics: bool = False
    time_scale: float = 1.0
    seed: int = 42
    state_start_dim: int = 3
    state_end_dim: int = 9
    pretrained_encoder: bool = False
    additional_in_keys: dict = {"step_count": ("agents", "step_count")}
    crop_h: int = 100
    crop_w: int = 100
    action_low: float = -10.0
    action_high: float = 10.0
    target_img: Path = "assets/hider.png"
    scale_reward: float = 11.0
    shift_reward: float = -1.0
    dense_reward_scale: float = 1.0
    credit_window_right: float = 1.0

    @property
    def num_player_args(self) -> list[str]:
        return [
            "-NumHiders",
            f"{self.num_hiders}",
            "-NumSeekers",
            f"{self.num_seekers}",
        ]


def register_env_configs() -> None:
    cs = ConfigStore.instance()
    cs.store(group="envs", name="base_bowling", node=BowlingConfig)
    cs.store(group="envs", name="base_find_treasure", node=FindTreasureConfig)
    cs.store(group="envs", name="base_hide_and_seek_1v1", node=HideAndSeek1V1Config)
    cs.store(group="envs", name="base_hide_and_seek", node=HideAndSeekConfig)
