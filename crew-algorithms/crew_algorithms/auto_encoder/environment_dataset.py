import os
from collections.abc import Callable

from PIL import Image
from torch.utils.data import Dataset


class EnvironmentDataset(Dataset):
    """A dataset of frames collected from the environment for training the auto-encoder.

    Args:
        root: The root directory containing the images.
        transform: A callable function to transform the images.
        split: The split of the dataset to use, either 'train' or 'val'.
        val_ratio: The ratio of the dataset to use for validation.
    """

    def __init__(
        self,
        root: str,
        transform: Callable[..., any] | None = None,
        split: str = "train",
        val_ratio: float = 0.1,
    ) -> None:
        assert split in ["train", "val"]
        self.img_list = [f for f in os.listdir(root)]
        self.len_all = len(self.img_list)
        # seuqential split, assuming data is collected by random policy. This makes sure the validation data are from diifferent episodes than training.
        self.data = (
            self.img_list[: int(self.len_all * (1 - val_ratio))]
            if split == "train"
            else self.img_list[int(self.len_all * val_ratio) :]
        )
        self.transform = transform
        self.root = root

    def __len__(self) -> int:
        return len(self.data)

    def __getitem__(self, index: int) -> any:
        img_path = self.root + self.data[index]
        image = Image.open(img_path).convert("RGB")
        if self.transform is not None:
            image = self.transform(image)
        return image
