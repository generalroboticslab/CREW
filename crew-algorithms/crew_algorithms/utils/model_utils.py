import torch.nn as nn


def conv2d_bn_relu(inch, outch, kernel_size, stride=1, padding=1):
    convlayer = nn.Sequential(
        nn.Conv2d(inch, outch, kernel_size=kernel_size, stride=stride, padding=padding),
        nn.BatchNorm2d(outch),
        nn.ReLU(),
    )
    return convlayer


def conv2d_bn_sigmoid(inch, outch, kernel_size, stride=1, padding=1):
    convlayer = nn.Sequential(
        nn.Conv2d(inch, outch, kernel_size=kernel_size, stride=stride, padding=padding),
        nn.BatchNorm2d(outch),
        nn.Sigmoid(),
    )
    return convlayer


def deconv_sigmoid(inch, outch, kernel_size, stride=1, padding=1):
    convlayer = nn.Sequential(
        nn.ConvTranspose2d(
            inch, outch, kernel_size=kernel_size, stride=stride, padding=padding
        ),
        nn.Sigmoid(),
    )
    return convlayer


def deconv_only(inch, outch, kernel_size, stride=1, padding=1):
    convlayer = nn.Sequential(
        nn.ConvTranspose2d(
            inch, outch, kernel_size=kernel_size, stride=stride, padding=padding
        ),
    )
    return convlayer


def deconv_relu(inch, outch, kernel_size, stride=1, padding=1):
    convlayer = nn.Sequential(
        nn.ConvTranspose2d(
            inch, outch, kernel_size=kernel_size, stride=stride, padding=padding
        ),
        nn.BatchNorm2d(outch),
        nn.ReLU(),
    )
    return convlayer
