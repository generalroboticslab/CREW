import torch
import torch.nn as nn
from crew_algorithms.auto_encoder.model import Encoder
from torchrl.modules import MLP


class HFNet(nn.Module):
    def __init__(self, encoder, in_dims: int, num_actions: int) -> None:
        """The huamn feedback regression network specified by the DeepTamer paper.

        Args:
            num_actions: The number of possible actions the network
                should output.
        """
        super().__init__()
        self.mlp = MLP(
            in_features=in_dims,
            out_features=num_actions,
            num_cells=[16, 16],
            activation_class=nn.ReLU,
            activate_last_layer=False,
        )
        self.encoder = encoder

    def forward(self, obs: torch.Tensor):
        obs = self.encoder(obs).flatten(1)
        mlp_out = self.mlp(obs)
        return mlp_out


class ContinuousActorNet(nn.Module):
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
    def __init__(
        self, encoder, n_agent_inputs, num_cells, activation_class=nn.ReLU
    ) -> None:
        super().__init__()
        self.mlp = nn.Sequential(
            # nn.Linear(n_agent_inputs + 1, num_cells),
            nn.Linear(n_agent_inputs, num_cells),
            # nn.BatchNorm1d(num_cells),
            activation_class(),
            nn.Linear(num_cells, num_cells),
            # nn.BatchNorm1d(num_cells),
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
