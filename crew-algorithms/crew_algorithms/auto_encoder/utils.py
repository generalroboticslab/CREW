from time import time

import torch
import torch.nn as nn
import torch.optim as optim
from crew_algorithms.auto_encoder import AutoEncoder
from crew_algorithms.auto_encoder.environment_dataset import EnvironmentDataset
from torch.utils.data import DataLoader
from torchvision.transforms import Compose, Resize, ToTensor


def make_dataloaders(cfg):
    """Makes a training and validation dataloader.

    Args:
        cfg: The configuration to use for making the dataloaders.

    Returns:
        The train dataloader.
        The validation dataloader.
    """
    transform = Compose([Resize((cfg.envs.crop_h, cfg.envs.crop_h)), ToTensor()])
    train_set = EnvironmentDataset(
        cfg.data_root, transform, split="train", val_ratio=cfg.val_ratio
    )
    val_set = EnvironmentDataset(
        cfg.data_root, transform, split="val", val_ratio=cfg.val_ratio
    )

    train_dataloader = DataLoader(
        train_set, batch_size=cfg.batch_size, shuffle=True, num_workers=cfg.num_workers
    )
    val_dataloader = DataLoader(
        val_set, batch_size=cfg.batch_size, shuffle=False, num_workers=cfg.num_workers
    )
    return train_dataloader, val_dataloader


def make_model(num_channels: int, embedding_dim: int):
    """Makes the AutoEncoder model.

    Args:
        num_channels: The number of channels the AutoEncoder should be
            created with.
        embedding_dim: The size of the output embedding.

    Returns:
        The AutoEncoder model.
    """
    net = AutoEncoder(num_channels, embedding_dim)
    return net


def make_optim(cfg, model: AutoEncoder):
    """Creates the optimizer used for the AutoEncoder.

    Args:
        cfg: The configuration settings.
        model: The model to optimize.

    Returns:
        The optimizer to be used with the AutoEncoder.
        The scheduler to be used with the AutoEncoder.
    """
    optimizer = optim.Adam(model.parameters(), lr=cfg.learning_rate)
    scheduler = optim.lr_scheduler.MultiStepLR(
        optimizer, milestones=cfg.scheduler_milestones, gamma=cfg.scheduler_gamma
    )
    return optimizer, scheduler


def train(
    cfg, train_loader, val_loader, model, loss_fn, optimizer, lr_scheduler, device
):
    model.to(device)
    """Trains the AutoEncoder model.
    """

    tic = time()
    for e in range(cfg.max_epochs):
        model.train()
        run_train, run_val = 0, 0
        for i, data in enumerate(train_loader):
            data = data.to(device)
            data_hat = model(data)
            loss = loss_fn(data_hat, data)
            optimizer.zero_grad()
            loss.backward()
            optimizer.step()
            run_train += loss.item()
            if (i + 1) % cfg.log_freq == 0:
                print(
                    "Ep %d| It %d/%d| train loss: %.7f| %dmin%ds"
                    % (
                        e,
                        i,
                        len(train_loader),
                        run_train / cfg.log_freq,
                        int((time() - tic) // 60),
                        int((time() - tic) % 60),
                    )
                )
                run_train = 0

        for i, data in enumerate(val_loader):
            model.eval()
            data = data.to(device)
            with torch.inference_mode():
                data_hat = model(data)
            loss = loss_fn(data_hat, data)
            run_val += loss.item()
        print(
            "EP %d| It %d/%d| val loss: %.7f"
            % (e, i, len(val_loader), run_val / (len(val_loader)))
        )

        if (e + 1) % cfg.save_freq == 0:
            torch.save(
                model.state_dict(), "crew_algorithms/auto_encoder/weights/%d.pth" % e
            )

        lr_scheduler.step()
