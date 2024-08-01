import torch
import torch.multiprocessing as mp
import torch.nn as nn
from crew_algorithms.auto_encoder.model import Encoder


class ContinuousActorNet(nn.Module):
    """ Actor network for continuous action space
    Args:
        encoder: Encoder network
        n_agent_inputs: Input dimension
        num_cells: Number of cells in hidden layers
        out_dims: Number of output dimensions
        activation_class: Activation function class
    """
    def __init__(
        self, encoder, n_agent_inputs, num_cells, out_dims, activation_class=nn.ReLU
    ) -> None:
        super().__init__()

        self.mlp = nn.Sequential(
            nn.Linear(n_agent_inputs, num_cells),
            nn.BatchNorm1d(num_cells),
            activation_class(),
            nn.Linear(num_cells, num_cells),
            nn.BatchNorm1d(num_cells),
            activation_class(),
            nn.Linear(num_cells, out_dims),
        )
        self.encoder = encoder
        self.init_weights()

        if isinstance(self.encoder, Encoder):
            self.num_channels = next(self.encoder.parameters()).shape[1]
        else:
            self.num_channels = None

    def init_weights(self):
        for m in self.mlp:
            if isinstance(m, nn.Linear):
                nn.init.orthogonal_(m.weight)
                m.bias.data.fill_(0.01)

    def forward(self, obs: torch.Tensor, **kwargs):
        bs = obs.shape[0]
        if bs == 1:
            self.mlp.eval()
        else:
            self.mlp.train()
        if len(obs.shape) == 5:
            obs = obs.squeeze(1)

        if self.num_channels is not None:
            obs = obs.view(
                obs.shape[0], -1, self.num_channels, obs.shape[-2], obs.shape[-1]
            ).view(-1, self.num_channels, obs.shape[-2], obs.shape[-1])

        obs = self.encoder(obs).flatten(1).view(bs, -1)

        if "step_count" in kwargs:
            step_count = kwargs["step_count"]
            while len(step_count.shape) > 2:
                step_count = step_count.squeeze(1)
            obs = torch.cat([obs, step_count], dim=-1).to(obs.device)

        mlp_out = self.mlp(obs)

        return mlp_out.unsqueeze(1)


class ContinuousQValueNet(nn.Module):
    """ Q-value network for continuous action space
    Args:
        encoder: Encoder network
        n_agent_inputs: Input dimension
        num_cells: Number of cells in hidden layers
        activation_class: Activation
    """
    def __init__(
        self, encoder, n_agent_inputs, num_cells, activation_class=nn.ReLU
    ) -> None:
        super().__init__()
        self.mlp = nn.Sequential(
            nn.Linear(n_agent_inputs, num_cells),
            activation_class(),
            nn.Linear(num_cells, num_cells),
            activation_class(),
            nn.Linear(num_cells, 1),
        )
        self.encoder = encoder
        self.init_weights()
        if isinstance(self.encoder, Encoder):
            self.num_channels = next(self.encoder.parameters()).shape[1]
        else:
            self.num_channels = None

    def init_weights(self):
        for m in self.mlp:
            if isinstance(m, nn.Linear):
                nn.init.orthogonal_(m.weight)
                m.bias.data.fill_(0.01)

    def forward(self, obs: torch.Tensor, action: torch.Tensor, **kwargs):
        bs = obs.shape[0]
        if bs == 1:
            self.mlp.eval()
        else:
            self.mlp.train()
        if len(obs.shape) == 5:
            obs = obs.squeeze(1)
        if self.num_channels is not None:
            obs = obs.view(
                obs.shape[0], -1, self.num_channels, obs.shape[-2], obs.shape[-1]
            ).view(-1, self.num_channels, obs.shape[-2], obs.shape[-1])

        obs = self.encoder(obs).flatten(1).view(bs, -1)
        while len(action.shape) > 2:
            action = action.squeeze(1)
        obs_action = torch.cat([obs, action], dim=-1).to(obs.device)

        if "step_count" in kwargs:
            step_count = kwargs["step_count"]
            while len(step_count.shape) > 2:
                step_count = step_count.squeeze(1)
            obs_action = torch.cat([obs_action, step_count], dim=-1).to(obs.device)

        mlp_out = self.mlp(obs_action)
        return mlp_out.unsqueeze(1)


class FeedbackNet(nn.Module):
    """ Feedback prediction network. Given obs and action, predict assigned feedback.
    Args:
        encoder: Encoder network
        n_agent_inputs: Input dimension
        num_cells: Number of cells in hidden layers
        activation_class: Activation function class
    """
    def __init__(
        self, encoder, n_agent_inputs, num_cells, activation_class=nn.ReLU
    ) -> None:
        super().__init__()
        self.mlp = nn.Sequential(
            nn.Linear(n_agent_inputs, num_cells),
            activation_class(),
            nn.Linear(num_cells, num_cells),
            activation_class(),
            nn.Linear(num_cells, 1),
        )
        self.encoder = encoder
        self.init_weights()
        if isinstance(self.encoder, Encoder):
            self.num_channels = next(self.encoder.parameters()).shape[1]
        else:
            self.num_channels = None

    def init_weights(self):
        for m in self.mlp:
            if isinstance(m, nn.Linear):
                nn.init.orthogonal_(m.weight)
                m.bias.data.fill_(0.01)

    def forward(self, obs: torch.Tensor, action: torch.Tensor):
        if bs == 1:
            self.mlp.eval()
        else:
            self.mlp.train()
        bs = obs.shape[0]
        if len(obs.shape) == 5:
            obs = obs.squeeze(1)
        obs = obs.view(
            obs.shape[0], -1, self.num_channels, obs.shape[-2], obs.shape[-1]
        ).view(-1, 3, obs.shape[-2], obs.shape[-1])

        obs = self.encoder(obs).flatten(1).view(bs, -1)
        action = action.view(bs, -1)

        obs_action = torch.cat([obs, action], dim=-1).to(obs.device)
        mlp_out = self.mlp(obs_action)

        return mlp_out.unsqueeze(1)
