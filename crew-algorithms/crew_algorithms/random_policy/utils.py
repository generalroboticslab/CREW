import os

# import torch
import wandb
from crew_algorithms.envs.channels import ToggleTimestepChannel
from crew_algorithms.envs.configs import EnvironmentConfig

# from crew_algorithms.multimodal.split_key_transform import IndexSelectTransform
from crew_algorithms.utils.rl_utils import (
    convert_tensor_to_pil_image,
    make_base_env,
    unsqueeze_images_from_channel_dimension,
)
from torchrl.collectors.collectors import RandomPolicy
from torchrl.envs import (
    Compose,
    EnvBase,  # , StepCounter, ToTensorImage
    TransformedEnv,
)


def make_env(
    cfg: EnvironmentConfig,
    toggle_timestep_channel: ToggleTimestepChannel,
    device: str,
):
    """Creates an environment based on the configuration that can be used for
    a random policy.

    Args:
        cfg: The environment configuration to be used.
        toggle_timestep_channel: A Unity side channel that can be used
            to play/pause games.
        device: The device to perform environment operations on.

    Returns:
        The environment that can be used for the random policy.
    """
    env = TransformedEnv(
        make_base_env(cfg, device, toggle_timestep_channel=toggle_timestep_channel),
        Compose(
            # IndexSelectTransform([[torch.tensor([0])]], [[1]],
            # in_keys=[("agents", "observation", "obs_0")],
            # out_keys=[("agents", "observation", "feedback")])
            # ToTensorImage(in_keys=[("agents", "observation")], unsqueeze=True),
        ),
    )
    return env


def make_policy(env: EnvBase):
    """Creates the random policy.

    Args:
        env: The environment that the policy will be used for.

    Returns:
        The random policy.
    """
    policy = RandomPolicy(env.action_spec, action_key=env.action_key)
    return policy


def save_images(env_cfg, data_view, data_path, frames_per_batch, batch):
    """Saves individual images collected from a random policy.

    Saves images in a directory structure where you have the root data directory,
    and then one subfolder for each observation. The subfolder contains all of the
    images associated with that observation.

    Args:
        env_cfg: The environment configuration to be used.
        data_view: The current data collected from the data collector.
        data_path: The root directory to store the data at.
        frames_per_batch: The number of frames in each batch from the collector.
        batch: The current batch number from the collector.
    """
    for i, single_data_view in enumerate(data_view.unbind(0)):
        obs = single_data_view.get(("agents", "observation", "obs_0_0"))
        # Unity stacks observations from the StackingSensor
        # noqa E501: (https://docs.unity3d.com/Packages/com.unity.ml-agents@2.0/api/Unity.MLAgents.Sensors.StackingSensor.html)
        # along the channel dimension. In order for the ToPILTransform to work
        # these images need to be separated out along the batch rather than
        # channel dimension.

        # pyobs = unsqueeze_images_from_channel_dimension(env_cfg, obs, dim=-4)
        obs = obs.squeeze(0).permute(2, 0, 1)
        # obs = obs.flatten(0, -4)
        j = frames_per_batch * batch + i
        obs_path = data_path / str(j)
        os.makedirs(obs_path, exist_ok=True)
        for k, img in enumerate(obs):
            img = convert_tensor_to_pil_image(img)
            img.save(obs_path / f"{k}.png")


def upload_dataset(cfg, logger, path):
    """Uploads the dataset of images collected from the random policy.

    Args:
        cfg: The configuration to be used.
        logger: The WandB logger to be used for logging the dataset.
        path: The path to the images.
    """
    # Unfortunately, wandb doesn't have a way to specify
    # files to exclude in add_dir. A workaround is to just
    # delete the DS_STORE file before uploading.
    for root, dirnames, filenames in os.walk(logger.experiment.dir):
        for filename in filenames:
            if filename.upper() == ".DS_STORE":
                os.remove(os.path.join(root, filename))

    dataset = wandb.Artifact(
        f"{cfg.wandb.project}-{cfg.envs.name}-dataset", type="dataset"
    )
    dataset.add_dir(path)
    logger.experiment.log_artifact(dataset)
