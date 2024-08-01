import random

import torch
import torch.nn as nn
from crew_algorithms.utils.model_utils import (
    conv2d_bn_relu,
    deconv_relu,
    deconv_sigmoid,
)
from PIL import Image
from torchvision.models import mobilenet_v3_small, resnet18
from torchvision.transforms import RandomAffine, ToTensor, v2

random.seed(0)


class Encoder_Nature(nn.Module):
    def __init__(self, in_channels: int, embedding_dim: int = 128):
        """The implementation of the encoder outlined here:
        https://www.nature.com/articles/s41598-020-77918-x.

        Args:
            in_channels: Number of input channels.
            embedding_dim: The size of the output embedding.
        """
        super().__init__()
        self.conv_stack1 = nn.Sequential(
            conv2d_bn_relu(in_channels, 32, 4, stride=2), conv2d_bn_relu(32, 32, 3)
        )

        self.conv_stack2 = nn.Sequential(
            conv2d_bn_relu(32, 32, 4, stride=2), conv2d_bn_relu(32, 32, 3)
        )

        self.conv_stack3 = nn.Sequential(
            conv2d_bn_relu(32, 64, 4, stride=2), conv2d_bn_relu(64, 64, 3)
        )
        self.conv_stack4 = nn.Sequential(
            conv2d_bn_relu(64, embedding_dim, 4, stride=2),
            conv2d_bn_relu(embedding_dim, embedding_dim, 3),
        )

        self.conv_stack5 = nn.Sequential(
            conv2d_bn_relu(embedding_dim, embedding_dim, 4, stride=2),
            conv2d_bn_relu(embedding_dim, embedding_dim, 3),
        )

        self.conv_stack6 = nn.Sequential(
            conv2d_bn_relu(embedding_dim, embedding_dim, 4, stride=2),
            conv2d_bn_relu(embedding_dim, embedding_dim, 3),
        )

        self.conv_stack7 = nn.Sequential(
            conv2d_bn_relu(embedding_dim, embedding_dim, 4, stride=2),
            conv2d_bn_relu(embedding_dim, embedding_dim, 3),
        )

    def forward(self, x: torch.Tensor):
        conv1_out = self.conv_stack1(x)
        conv2_out = self.conv_stack2(conv1_out)
        conv3_out = self.conv_stack3(conv2_out)
        conv4_out = self.conv_stack4(conv3_out)
        conv5_out = self.conv_stack5(conv4_out)
        conv6_out = self.conv_stack6(conv5_out)
        conv7_out = self.conv_stack7(conv6_out)
        return conv7_out


class Encoder(nn.Module):
    """3 layer CNN encoder, with random shift augmentation.

    Args:
        in_channels: Number of input channels.
        embedding_dim: The size of the output embedding.
    """

    def __init__(self, in_channels: int = 3, embedding_dim: int = 128):
        super().__init__()

        self.cnn = nn.Sequential(
            nn.Conv2d(in_channels, 64, kernel_size=8, stride=4),
            nn.BatchNorm2d(64),
            nn.ReLU(inplace=True),
            nn.MaxPool2d(kernel_size=3, stride=2),
            nn.Conv2d(64, 64, kernel_size=3, stride=2, padding=1),
            nn.BatchNorm2d(64),
            nn.ReLU(inplace=True),
            nn.Conv2d(64, 64, kernel_size=3, stride=2, padding=1),
            nn.BatchNorm2d(64),
            nn.Flatten(),
        )

        self.fc = nn.Sequential(
            nn.Linear(576, embedding_dim),
        )

    def forward(self, x):
        """Random shift augmentation is common practice in visual RL.
        A random shift is only applied at training time, with the
        same shift applied to all frames in the batch.
        """
        if len(x.shape) < 4:
            x = x.unsqueeze(1)
        if x.shape[0] > 1:
            pix = 8
            a, b = random.uniform(-pix, pix), random.uniform(-pix, pix)
            x = v2.functional.affine(x, angle=0, translate=[a, b], scale=1, shear=0)

        x = self.cnn(x)
        x = self.fc(x)
        return x


class StateEncoder(nn.Module):
    """Encoder for structured state input.

    Args:
        start_dim: The starting dimension to include in the output.
        end_dim: The ending dimension to include in the output.
    """

    def __init__(self, start_dim=4, end_dim=None):
        super().__init__()
        self.start_dim = start_dim
        self.end_dim = end_dim

    def forward(self, x):
        x = x[..., self.start_dim : self.end_dim]
        return x


class Decoder(nn.Module):
    """Decoder for the AutoEncoder.

    Args:
        out_channels: The number of output channels.
        embedding_dim: The size of the output embedding.
    """

    def __init__(self, out_channels: str, embedding_dim: int = 128):
        super().__init__()
        self.deconv_7 = deconv_relu(embedding_dim, embedding_dim, 4, stride=2)
        self.deconv_6 = deconv_relu(embedding_dim + 3, embedding_dim, 4, stride=2)
        self.deconv_5 = deconv_relu(embedding_dim + 3, embedding_dim, 4, stride=2)
        self.deconv_4 = deconv_relu(embedding_dim + 3, 64, 4, stride=2)
        self.deconv_3 = deconv_relu(67, 32, 4, stride=2)
        self.deconv_2 = deconv_relu(35, 16, 4, stride=2)
        self.deconv_1 = deconv_sigmoid(19, out_channels, 4, stride=2)

        self.predict_7 = nn.Conv2d(embedding_dim, 3, 3, stride=1, padding=1)
        self.predict_6 = nn.Conv2d(embedding_dim + 3, 3, 3, stride=1, padding=1)
        self.predict_5 = nn.Conv2d(embedding_dim + 3, 3, 3, stride=1, padding=1)
        self.predict_4 = nn.Conv2d(embedding_dim + 3, 3, 3, stride=1, padding=1)
        self.predict_3 = nn.Conv2d(67, 3, 3, stride=1, padding=1)
        self.predict_2 = nn.Conv2d(35, 3, 3, stride=1, padding=1)

        self.up_sample_7 = nn.Sequential(
            nn.ConvTranspose2d(3, 3, 4, stride=2, padding=1, bias=False), nn.Sigmoid()
        )
        self.up_sample_6 = nn.Sequential(
            nn.ConvTranspose2d(3, 3, 4, stride=2, padding=1, bias=False), nn.Sigmoid()
        )
        self.up_sample_5 = nn.Sequential(
            nn.ConvTranspose2d(3, 3, 4, stride=2, padding=1, bias=False), nn.Sigmoid()
        )
        self.up_sample_4 = nn.Sequential(
            nn.ConvTranspose2d(3, 3, 4, stride=2, padding=1, bias=False), nn.Sigmoid()
        )
        self.up_sample_3 = nn.Sequential(
            nn.ConvTranspose2d(3, 3, 4, stride=2, padding=1, bias=False), nn.Sigmoid()
        )
        self.up_sample_2 = nn.Sequential(
            nn.ConvTranspose2d(3, 3, 4, stride=2, padding=1, bias=False), nn.Sigmoid()
        )

    def forward(self, x: torch.Tensor):
        deconv7_out = self.deconv_7(x)
        predict_7_out = self.up_sample_7(self.predict_7(x))

        concat_6 = torch.cat([deconv7_out, predict_7_out], dim=1)
        deconv6_out = self.deconv_6(concat_6)
        predict_6_out = self.up_sample_6(self.predict_6(concat_6))

        concat_5 = torch.cat([deconv6_out, predict_6_out], dim=1)
        deconv5_out = self.deconv_5(concat_5)
        predict_5_out = self.up_sample_5(self.predict_5(concat_5))

        concat_4 = torch.cat([deconv5_out, predict_5_out], dim=1)
        deconv4_out = self.deconv_4(concat_4)
        predict_4_out = self.up_sample_4(self.predict_4(concat_4))

        concat_3 = torch.cat([deconv4_out, predict_4_out], dim=1)
        deconv3_out = self.deconv_3(concat_3)
        predict_3_out = self.up_sample_3(self.predict_3(concat_3))

        concat2 = torch.cat([deconv3_out, predict_3_out], dim=1)
        deconv2_out = self.deconv_2(concat2)
        predict_2_out = self.up_sample_2(self.predict_2(concat2))

        concat1 = torch.cat([deconv2_out, predict_2_out], dim=1)
        predict_out = self.deconv_1(concat1)

        return predict_out


class AutoEncoder(nn.Module):
    def __init__(self, channels: int, embedding_dim: int) -> None:
        """The AutoEncoder implementation of the algorithm
        outlined here: https://www.nature.com/articles/s41598-020-77918-x.

        Args:
            channels: Number of channels.
            embedding_dim: The size of the bottleneck layer.
        """
        super().__init__()
        self.encoder = Encoder_Nature(channels, embedding_dim)
        self.decoder = Decoder(channels, embedding_dim)

    def forward(self, x: torch.Tensor):
        encoder_out = self.encoder(x)
        return self.decoder(encoder_out)
