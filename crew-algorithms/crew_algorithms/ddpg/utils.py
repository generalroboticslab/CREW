import math
import os
import pickle
import re
from datetime import datetime

import torch
from crew_algorithms.auto_encoder import EncoderTransform
from crew_algorithms.auto_encoder.model import Encoder, StateEncoder
from crew_algorithms.ddpg.imitation_learning import ImitationLearningWrapper
from crew_algorithms.ddpg.policy import (
    ContinuousActorNet,
    ContinuousQValueNet,
    FeedbackNet,
)
from crew_algorithms.envs.channels import WrittenFeedbackChannel
from crew_algorithms.envs.configs import EnvironmentConfig
from crew_algorithms.utils.rl_utils import make_base_env
from PIL import Image
from tensordict import TensorDict
from tensordict.nn import TensorDictModule
from torch import nn, optim
from torch.nn import functional as F
from torchrl.data import (
    LazyMemmapStorage,
    TensorDictPrioritizedReplayBuffer,
    TensorDictReplayBuffer,
)
from torchrl.data.replay_buffers.samplers import PrioritizedSampler
from torchrl.data.tensor_specs import ContinuousBox
from torchrl.envs.transforms.transforms import (
    CatFrames,
    CenterCrop,
    Compose,
    Resize,
    StepCounter,
    ToTensorImage,
    TransformedEnv,
    UnsqueezeTransform,
)
from torchrl.modules import (
    AdditiveGaussianWrapper,
    SafeSequential,
    TanhModule,
    ValueOperator,
)
from torchrl.objectives import DDPGLoss, SoftUpdate
from torchvision import transforms
from torchvision.utils import save_image


def make_env(
    cfg: EnvironmentConfig,
    written_feedback_channel: WrittenFeedbackChannel,
    written_feedback: bool,
    device: str,
):
    """Creates an environment based on the configuration.

    Args:
        cfg: The environment configuration to be used.
        written_feedback_channel: A Unity side channel that can be used
            to share written feedback at the end of each episode.
        device: The device to perform environment operations on.

    Returns:
        The environment that can be used for the random policy.
    """

    env = TransformedEnv(
        make_base_env(
            cfg,
            device,
            written_feedback_channel=written_feedback_channel,
            use_written_feedback=written_feedback,
        ),
        Compose(
            ToTensorImage(in_keys=[("agents", "observation", "obs_0")], unsqueeze=True),
            CenterCrop(
                cfg.crop_h, cfg.crop_w, in_keys=[("agents", "observation", "obs_0")]
            ),
            Resize(100, 100, in_keys=[("agents", "observation", "obs_0")]),
            UnsqueezeTransform(
                unsqueeze_dim=-3, in_keys=[("agents", "observation", "obs_1")]
            ),
        ),
    )

    if cfg.pretrained_encoder:
        env.append_transform(
            EncoderTransform(
                env_name=cfg.name,
                num_channels=cfg.num_stacks * cfg.num_channels,
                in_keys=[("agents", "observation", "obs_0")],
                out_keys=[(("agents", "observation", "encoded_vec"))],
                version="latest",
            )
        )

    env.append_transform(
        CatFrames(
            N=cfg.num_stacks, dim=-3, in_keys=[("agents", "observation", "obs_0")]
        )
    )
    env.append_transform(
        CatFrames(
            N=cfg.num_stacks, dim=-2, in_keys=[("agents", "observation", "obs_1")]
        )
    )
    env.append_transform(StepCounter())

    return env


def make_agent(cfg, proof_env, device):
    print("Making agent")
    action_spec = proof_env.action_spec

    if isinstance(action_spec.space, ContinuousBox):
        model, actor, fb_model = make_agent_continuous(proof_env, cfg, device)
    else:
        raise NotImplementedError("Only continuous action spaces are supported.")

    return model, actor, fb_model


def make_agent_continuous(proof_env, cfg, device):
    action_dims = proof_env.action_spec.space.low.shape[-1]
    additional_in_keys = {tuple(v): k for k, v in cfg.envs.additional_in_keys.items()}

    if cfg.envs.pretrained_encoder:
        in_keys = {("agents", "observation", "encoded_vec"): "obs"}
        in_keys.update(additional_in_keys)
        encoder = nn.Identity()
        in_dims = 64 * cfg.envs.num_stacks + (cfg.envs.additional_in_keys != {})
    elif cfg.from_states:
        in_keys = {("agents", "observation", "obs_1"): "obs"}
        in_keys.update(additional_in_keys)
        encoder = StateEncoder(cfg.envs.state_start_dim, cfg.envs.state_end_dim)
        in_dims = (
            cfg.envs.state_end_dim - cfg.envs.state_start_dim
        ) * cfg.envs.num_stacks + (cfg.envs.additional_in_keys != {})
    else:
        in_keys = {("agents", "observation", "obs_0"): "obs"}
        in_keys.update(additional_in_keys)
        encoder = Encoder(cfg.envs.num_channels, 64)
        in_dims = 64 * cfg.envs.num_stacks + (cfg.envs.additional_in_keys != {})

    actor_net = ContinuousActorNet(
        encoder=encoder,
        n_agent_inputs=in_dims,
        num_cells=256,
        out_dims=action_dims,
    )

    actor_module = TensorDictModule(
        actor_net,
        in_keys=in_keys,
        out_keys=[("agents", "param")],
    )
    actor = SafeSequential(
        actor_module,
        TanhModule(
            in_keys=[("agents", "param")],
            out_keys=[("agents", "action")],
            low=cfg.envs.action_low,
            high=cfg.envs.action_high,
        ),
    )
    actor = ImitationLearningWrapper(
        actor,
        action_key=("agents", "action"),
        il_enabled_key=("agents", "observation", "obs_1"),
        il_action_key=("agents", "observation", "obs_1"),
    )

    qvalue_net = ContinuousQValueNet(
        encoder=encoder,
        n_agent_inputs=in_dims + action_dims,
        num_cells=256,
    )

    qvalue = ValueOperator(
        in_keys={**in_keys, **{("agents", "action"): "action"}},
        module=qvalue_net,
        out_keys=["state_action_value"],
    )

    model = nn.ModuleList([actor, qvalue]).to(device)

    if cfg.feedback_model:
        if cfg.history:
            feedback_in_dims = (in_dims // 3) * 7 + 6 * 2 + 6 * 1
        else:
            feedback_in_dims = (
                64 * cfg.envs.num_stacks + action_dims
            )
        print("Feedback in dims:", feedback_in_dims)
        feedback_model = ContinuousQValueNet(
            encoder=encoder,
            n_agent_inputs=feedback_in_dims,
            num_cells=256,
        )
    else:
        feedback_model = None

    feedback_model = feedback_model.to(device) if feedback_model else None

    actor_model_explore = AdditiveGaussianWrapper(
        model[0],
        sigma_end=1.0,
        sigma_init=1.0,
        mean=0.0,
        std=cfg.optimization.exploration_noise,
        safe=False,
        action_key=("agents", "action"),
    ).to(device)

    return model, actor_model_explore, feedback_model


def make_data_buffer(cfg, run_name):
    p_sampler = PrioritizedSampler(
        max_capacity=cfg.buffer_size, alpha=0.7, beta=0.9, reduction="max"
    )

    replay_buffer = TensorDictReplayBuffer(
        pin_memory=False,
        storage=LazyMemmapStorage(
            cfg.buffer_size,
            scratch_dir="../Data/Buffer/prb_%s" % run_name,
            device="cpu",
        ),
        batch_size=cfg.batch_size,
        sampler=p_sampler,
        priority_key=("agents", "priority_weight"),
    )

    if cfg.use_expert:
        replay_buffer_expert = TensorDictPrioritizedReplayBuffer(
            alpha=0.7,
            beta=0.5,
            pin_memory=False,
            storage=LazyMemmapStorage(
                cfg.buffer_size, scratch_dir="../Data/Buffer/prb_%s" % run_name
            ),
            batch_size=cfg.batch_size,
            priority_key="rank_adjusted_td_error",
        )
        replay_buffer_expert = load_prb(replay_buffer_expert)
    else:
        replay_buffer_expert = None

    return replay_buffer, replay_buffer_expert


def make_loss_module(cfg, env, model):
    """Make loss module and target network updater."""
    # Create DDPG loss
    if isinstance(env.action_spec.space, ContinuousBox):
        print("DDPG")
        loss_module = DDPGLoss(
            actor_network=model[0],
            value_network=model[1],
            loss_function="l2",
            delay_actor=False,
            delay_value=True,
        )
        loss_module.set_keys(
            reward=env.reward_key,
            done=("agents", "done"),
            priority=("agents", "td_error"),
        )

    else:
        print("DDPG does not support discrete action space")
        exit(0)

    loss_module.make_value_estimator(gamma=cfg.optimization.gamma)
    target_net_updater = SoftUpdate(
        loss_module, eps=cfg.optimization.target_update_polyak
    )
    return loss_module, target_net_updater


def make_optimizer(cfg, loss_module):
    optimizer = optim.Adam(
        loss_module.parameters(),
        lr=cfg.optimization.lr,
        weight_decay=cfg.optimization.weight_decay,
    )

    return optimizer


def make_sac_optimizer(cfg, loss_module):
    critic_params = list(loss_module.value_network_params.flatten_keys().values())
    actor_params = list(loss_module.actor_network_params.flatten_keys().values())

    optimizer_actor = optim.Adam(
        actor_params,
        lr=cfg.optimization.lr,
        weight_decay=cfg.optimization.weight_decay,
    )
    optimizer_critic = optim.Adam(
        critic_params,
        lr=cfg.optimization.lr,
        weight_decay=cfg.optimization.weight_decay,
    )
    optimizer_alpha = optim.Adam(
        [loss_module.log_alpha],
        lr=3.0e-4,
    )

    return optimizer_actor, optimizer_critic, optimizer_alpha


def lr_scheduler(
    optimizer_actor,
    optimizer_critic,
    step,
    phase_1=40,
    phase_2=60,
    lr_1=5e-5,
    lr_2=1.5e-5,
):
    if step < phase_1:
        optimizer_actor.param_groups[0]["lr"] = 0
        optimizer_critic.param_groups[0]["lr"] = lr_1
        phase = "critic learning"

    elif step < phase_2:
        optimizer_actor.param_groups[0]["lr"] = (
            (step - phase_1) * lr_2 / (phase_2 - phase_1)
        )
        optimizer_critic.param_groups[0]["lr"] = lr_1 + (step - phase_1) * (
            lr_2 - lr_1
        ) / (phase_2 - phase_1)
        phase = "warmup"

    else:
        optimizer_actor.param_groups[0]["lr"] = lr_2
        optimizer_critic.param_groups[0]["lr"] = lr_2
        phase = "interactive learning"

    return optimizer_actor, optimizer_critic, phase


def human_delay_transform(td, shift_key, N):
    """ Shifts the human feedback by N steps """
    data = td.get(shift_key)
    human_feedback = data[..., -1, 0]

    human_feedback_0 = (
        torch.ones((N, *human_feedback.shape[1:]), device=human_feedback.device) * 0
    )

    human_feedback = torch.cat([human_feedback, human_feedback_0], 0)
    human_feedback = human_feedback[N:]

    data[..., -1, 0] = human_feedback
    td.set(shift_key, data)
    # Return the tensors with valid data and the tensors with placeholder separately
    return td[:-N], td[-N:]


def gradient_weighted_average_transform(td, key, N, step_count_key="step_count"):
    if td.batch_size[0] <= 1:
        return td

    data = td.get(key)
    step_count = td[step_count_key].squeeze()
    feedback = data[..., 0].squeeze()

    coordinates = (step_count,)
    try:
        grads = torch.gradient(feedback, spacing=coordinates)[0]
    except Exception as e:
        print(step_count.shape)
        print(step_count)
        print(td)
        raise e

    total_padding = N - 1

    front_padding = torch.zeros(math.floor(total_padding / 2), device=feedback.device)
    back_padding = torch.zeros(math.ceil(total_padding / 2), device=feedback.device)

    feedback = torch.cat([front_padding, feedback, back_padding], -1)
    grads = torch.cat([front_padding, grads, back_padding], -1)

    feedback = feedback.unfold(-1, N, 1)
    grads = grads.unfold(-1, N, 1)

    weighted_average = torch.sum(feedback * grads, dim=-1) / (grads.sum(dim=-1) + 1e-10)
    data[..., 0] = torch.reshape(weighted_average, data[..., 0].shape)
    td.set(key, data)
    return td


def override_il_feedback(td, il_enabled_key, feedback_key, il_feedback):
    """ Sets feedback value of human controlled steps to il_feedback """
    # https://arxiv.org/pdf/1905.06750.pdf
    # https://arxiv.org/pdf/2108.04763.pdf
    original_feedback = td.get(feedback_key)
    feedback = original_feedback[..., -1, 0]

    il_enabled = td.get(il_enabled_key)[..., -1, 3].bool()
    feedback[il_enabled] = il_feedback
    original_feedback[..., -1, 0] = feedback
    td.set(feedback_key, original_feedback)
    return td


def audio_feedback(stream, time_stamp, action_key, reward_key, prb):
    if len(stream.buffer) < 128:
        return prb
    effected_data_idx = prb["_data", "time_stamp"] > time_stamp - 3

    file_name = "audio_%.2f" % time_stamp
    stream.save_to_file(file_name)

    return prb


def combine_feedback_and_rewards(td, feedback_key, reward_key, scale_feedback=1.0):
    td[reward_key] = td[reward_key] + td.get(feedback_key) * scale_feedback
    return td


def get_time():
    now = datetime.now()
    return now.strftime("%m%d_%H%M")


def fill_prb(
    act_folder="../Data/Offline/Actions",
    obs_folder="../Data/Offline/Observations",
    ending_steps_path="../Data/Offline/ending_steps.pth",
):
    """Fill a replay buffer with offline data.
    Args:
        act_folder: The folder containing the action data.
        obs_folder: The folder containing the observation data.
        ending_steps_path: A torch tensor array containing the ending step number of each episode.
    """

    to_tensor = transforms.ToTensor()

    def sorting_key(string):
        numbers = re.findall(r"\d+", string)
        return int(numbers[0]), int(numbers[1])

    o_list = [f for f in os.listdir(obs_folder)]
    o_list = sorted(o_list, key=sorting_key)

    a_list = [f for f in os.listdir(act_folder)]
    a_list = sorted(a_list, key=sorting_key)

    ending_steps = torch.load(ending_steps_path)
    traj_ID = 0

    experiences = []

    for i in range(len(o_list) - 1):
        print("Processing %d/%d" % (i, len(o_list) - 1), end="\r")

        o_i = o_list[i][3:-4]
        a_i = a_list[i][3:-3]
        assert o_i == a_i, "Not match: %s, %s" % (o_i, a_i)

        obs_i = to_tensor(
            Image.open(os.path.join(obs_folder, o_list[i])).convert("RGB")
        ).unsqueeze(0)
        act_i = torch.tensor(
            torch.load(os.path.join(act_folder, a_list[i])), dtype=torch.int64
        ).unsqueeze(0)
        logits_i = torch.zeros((1, 4))
        logits_i[0, int(act_i.item())] = 1

        next_obs_i = to_tensor(
            Image.open(os.path.join(obs_folder, o_list[i + 1])).convert("RGB")
        ).unsqueeze(0)
        next_act_i = torch.tensor(
            torch.load(os.path.join(act_folder, a_list[i + 1])), dtype=torch.int64
        ).unsqueeze(0)
        next_logits_i = torch.zeros((1, 4))
        next_logits_i[0, int(next_act_i.item())] = 1

        reward = torch.tensor([[[0.000]]])
        done = torch.tensor([[[False]]])

        if i in ending_steps:
            reward = torch.tensor([[[1.0]]])
        if i - 1 in ending_steps:
            continue
        if i - 2 in ending_steps:
            traj_ID += 1

        data_dict = TensorDict(
            {
                "agents": TensorDict(
                    {
                        "action": act_i.unsqueeze(0),
                        "done": torch.tensor([[[False]]]),
                        "logits": logits_i.unsqueeze(1),
                        "observation": TensorDict(
                            {
                                "obs_0": obs_i.unsqueeze(0),
                                "obs_1": torch.zeros((1, 1, 15)),
                            },
                            batch_size=[1, 1],
                            device="cuda",
                        ),
                    },
                    batch_size=[1, 1],
                    device="cuda",
                ),
                "collector": TensorDict(
                    {"traj_ids": torch.tensor([traj_ID])}, batch_size=[1], device="cuda"
                ),
                "next": TensorDict(
                    {
                        "agents": TensorDict(
                            {
                                "done": done,
                                "observation": TensorDict(
                                    {
                                        "obs_0": next_obs_i.unsqueeze(0),
                                        "obs_1": torch.zeros((1, 1, 15)),
                                    },
                                    batch_size=[1, 1],
                                    device="cuda",
                                ),
                                "reward": reward,
                            },
                            batch_size=[1, 1],
                            device="cuda",
                        ),
                        "step_count": torch.zeros((1, 1, 1), dtype=torch.int64),
                    },
                    batch_size=[],
                    device="cuda",
                ),
                "step_count": torch.zeros((1, 1, 1), dtype=torch.int64),
                "time_stamp": -torch.ones((1)) * 100,
                "is_expert": torch.tensor([False]),
            },
            batch_size=[1],
            device="cuda",
        )

        experiences.append(data_dict)

        if (i + 1) % 10000 == 0:
            torch.save(experiences, "../Data/Offline/experiences_%d.pt" % i)
            experiences = []


def load_prb(prb, path="../Data/Offline/Replay_nodone", chunk_size=1024):
    from time import time

    tic = time()
    exp_list = [f for f in os.listdir(path) if f[-2:] == "pt"]
    for i, f in enumerate(exp_list):
        print("Loading Expert Experiences: %d/%d" % (i, len(exp_list)), end="\r")
        experiences = torch.load(os.path.join(path, f))
        for j in range(0, len(experiences), chunk_size):
            print("Loading Expert Experiences: %d/%d" % (j, len(experiences)), end="\r")
            e = torch.cat(experiences[j : j + chunk_size], dim=0)
            prb.extend(e.cpu())

    print("Loading finished in %.1f seconds" % (time() - tic))
    return prb


def visualize(data, i, num_channels, fpb, hf=False, continuous=True):
    for j in range(len(data)):
        r = data["next", "agents", "reward"][j].item()
        d = data["next", "agents", "done"][j].item()

        if hf:
            r_hf = data["agents", "observation", "obs_1"][j][..., -1, 0].item()
        else:
            r_hf = data["next", "agents", "feedback"][j].item()

        os.makedirs("visualize", exist_ok=True)
        save_image(
            data["agents", "observation", "obs_0"][j, ..., -num_channels:, :, :],
            "visualize/frame_%d_r%.2f_d%d_rhf%.2f.png" % (i * fpb + j, r, d, r_hf),
        )
        save_image(
            data["next", "agents", "observation", "obs_0"][
                j, ..., -num_channels:, :, :
            ],
            "visualize/frame_%d_next.png" % (i * fpb + j),
        )


class heuristic_feedback:
    """ A hardcoded heuristic feedback model for FindTreasure and 1v1 Hide and Seek. Provides positive feedback for moving closer to the treasure, negative feedback for moving away from the treasure, and positive feedback for exploring new areas.
    Args:
        template_path: The path to the template image of the treasure.
        threshold: The threshold for the treasure detection.
        batch_size: The batch size of the model.
        device: The device to perform operations on.
    """
    def __init__(self, template_path, threshold, batch_size, device):
        template = Image.open(template_path)
        self.totens = transforms.ToTensor()
        self.template = self.totens(template).unsqueeze(0).to(device) + 1e-6

        torch._assert(len(self.template.shape) == 4, "Template should be 4D")
        self.template_morm = ((self.template**2).sum() ** 0.5).repeat(
            batch_size, 1, 1, 1
        )
        self.one_kernel = torch.ones_like(self.template, device=device)
        self.threshold = threshold

    def treasure_in_view(self, frame):
        torch._assert(len(frame.shape) == 4, "Frame should be 4D")

        frame = frame + 1e-6

        brightness = F.conv2d(frame**2, self.one_kernel) ** 0.5
        heat_map = F.conv2d(frame, self.template)
        heat_map = heat_map / (brightness * self.template_morm)

        out = (heat_map > self.threshold).sum(dim=(1, 2, 3)) > 0
        return out

    def moved_closer(self, td):
        next_agent = td["next", "agents", "observation", "obs_1"][..., -1, 6:9]
        next_treasure = td["next", "agents", "observation", "obs_1"][..., -1, 12:15]
        next_agent[..., 1], next_treasure[..., 1] = 0, 0
        next_distance = ((next_agent - next_treasure) ** 2).mean(dim=-1, keepdim=True)

        current_agent = td["agents", "observation", "obs_1"][..., -1, 6:9]
        current_treasure = td["agents", "observation", "obs_1"][..., -1, 12:15]
        current_agent[..., 1], current_treasure[..., 1] = 0, 0
        current_distance = ((current_agent - current_treasure) ** 2).mean(
            dim=-1, keepdim=True
        )

        r_dis = (current_distance - next_distance) / 10
        return r_dis.squeeze(-1).squeeze(-1)

    def explored(self, f_current, f_next):
        non_black_current = (f_current > 0).float().sum(dim=(1, 2, 3))
        non_black_next = (f_next > 0).float().sum(dim=(1, 2, 3))
        explored = (non_black_next - non_black_current - 300) / 1000
        return explored

    def treasure_appeared(self, f_current, f_next):
        current_treasure = self.treasure_in_view(f_current)
        next_treasure = self.treasure_in_view(f_next)
        return (next_treasure & ~current_treasure).float() * 2 - 1

    def get_treasure_in_view(self, f_current, f_next):
        in_current = self.treasure_in_view(f_current)
        in_next = self.treasure_in_view(f_next)
        return in_current, in_next

    def provide_feedback(self, td):
        f_current = td.get(("agents", "observation", "obs_0")).squeeze(1)[:, -3:]
        f_next = td.get(("next", "agents", "observation", "obs_0")).squeeze(1)[:, -3:]

        treasure_in_view, treasure_in_next = self.get_treasure_in_view(
            f_current, f_next
        )
        treasure_not_in_view = ~treasure_in_view

        moved_closer = self.moved_closer(td)
        explored = self.explored(f_current, f_next)

        feedback = (
            treasure_in_view.float() * moved_closer
            + treasure_not_in_view.float() * explored
            + (treasure_in_next.float() - treasure_in_view.float()) * 3
        )

        """ To experiment with random feedback"""
        # feedback = torch.rand_like(feedback) * 2 - 1 
        feedback = feedback

        return feedback.unsqueeze(-1).unsqueeze(-1)


def save_training(
    model,
    feedback_model,
    prb,
    episode_success,
    all_hf,
    all_heu,
    loss_module,
    run_name,
    iter,
    fpb,
):
    """Saves training status for continual training"""
    os.makedirs("../Data/Saved_Training/%s" % run_name, exist_ok=True)
    meta = {"fpb": fpb, "sr": episode_success}
    with open("../Data/Saved_Training/%s/meta.pkl" % run_name, "wb") as f:
        pickle.dump(meta, f)
    if len(all_hf) > 0:
        with open("../Data/Saved_Training/%s/hf_values.pkl" % run_name, "wb") as f:
            pickle.dump(all_hf, f)
    if len(all_heu) > 0:
        with open("../Data/Saved_Training/%s/heuristic_values.pkl" % run_name, "wb") as f:
            pickle.dump(all_heu, f)

    torch.save(
        model.state_dict(), "../Data/Saved_Training/%s/weights_Iter_%d.pth" % (run_name, iter)
    )
    torch.save(
        feedback_model.state_dict(),
        "../Data/Saved_Training/%s/feedback_model_Iter_%d.pth" % (run_name, iter),
    )
    torch.save(
        loss_module.state_dict(),
        "../Data/Saved_Training/%s/loss_module_Iter_%d.pth" % (run_name, iter),
    )
    prb.dumps("../Data/Saved_Training/%s/prb.pkl" % run_name)


def load_training(model, prb, loss_module, run_name, global_start_time, iter, device):
    model.load_state_dict(
        torch.load("../Data/Saved_Training/%s/weights_Iter_%d.pth" % (run_name, iter))
    )
    loss_module.load_state_dict(
        torch.load("../Data/Saved_Training/%s/loss_module_Iter_%d.pth" % (run_name, iter))
    )
    prb.loads("../Data/Saved_Training/%s/prb.pkl" % run_name)

    with open("../Data/Saved_Training/%s/meta.pkl" % run_name, "rb") as f:
        meta = pickle.load(f)
        episode_success = meta["sr"]
        fpb = meta["fpb"]
    collected_frames = iter * fpb

    actor_model_explore = AdditiveGaussianWrapper(
        model[0],
        sigma_end=1.0,
        sigma_init=1.0,
        mean=0.0,
        std=0.1,
        safe=False,
        action_key=("agents", "action"),
    ).to(device)

    time_stamp = collected_frames / 2
    # prb.update_priority(index=torch.arange(len(prb)), priority=torch.ones(len(prb)) * 1)
    return (
        model,
        actor_model_explore,
        prb,
        episode_success,
        loss_module,
        time_stamp + global_start_time,
        collected_frames,
    )


def feedback_model_train_step(history, model, data, optim, val=False):
    """Trains the feedback model for one step"""
    if history:
        obs = data["agents", "history", "obs"]  # [bs, 1, 3x7, 100, 100]
        action = data["agents", "history", "actions"]  # [bs, 6, 2]
        feedback = data["agents", "history", "feedbacks"]  # [bs, 6, 1]

    else:
        obs = data["next", "agents", "observation", "obs_0"]  # a stack of t-1, t, t+1
        action = data["agents", "action"]
        feedback = data["next", "agents", "feedback"]

    predicted_feedback = model(obs=obs, action=action)

    if val:
        return (predicted_feedback - feedback).pow(2).mean().item()

    optim.zero_grad()
    loss = (predicted_feedback - feedback).pow(2).mean()
    loss.backward()
    optim.step()

    return loss.item()


def provide_learned_feedback(history, model, data):
    """Provides feedback using the learned feedback model"""
    if history:
        obs = data["agents", "history", "obs"]  # [bs, 1, 3x7, 100, 100]
        action = data["agents", "history", "actions"]  # [bs, 6, 2]

    else:
        obs = data["next", "agents", "observation", "obs_0"]  # a stack of t-1, t, t+1
        action = data["agents", "action"]

    with torch.inference_mode():
        predicted_feedback = model(obs=obs, action=action)
    return predicted_feedback
