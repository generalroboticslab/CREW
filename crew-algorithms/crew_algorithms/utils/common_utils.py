import socket
from contextlib import closing
from datetime import datetime

from attrs import define, field
from tensordict import TensorDict


@define(order=True)
class SortedItem:
    priority: int
    item: any = field(order=False)


def find_free_port():
    """Finds an open port on the system.

    Returns:
        The open port.
    """
    with closing(socket.socket(socket.AF_INET, socket.SOCK_STREAM)) as s:
        s.bind(("", 0))
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        return s.getsockname()[1]


class KeyManager:
    def __init__(self, env_name: str):
        self.env_name = env_name

    def pixel_obs(self, data: TensorDict):
        return data[("agents", "observation", "obs_0")]

    def encoded_obs(self, data: TensorDict):
        return data[("agents", "observation", "encoder_vec")]

    def feedback(self, data: TensorDict):
        pass


def get_time():
    now = datetime.now()
    return now.strftime("%m%d_%H%M")
