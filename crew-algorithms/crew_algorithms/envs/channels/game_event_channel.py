import uuid
from abc import ABC, abstractmethod
from collections.abc import Callable
from typing import Generic, TypeVar

from cattrs.preconf.json import JsonConverter
from mlagents_envs.side_channel.side_channel import IncomingMessage, SideChannel

Message = TypeVar("Message")


class GameEventChannel(SideChannel, Generic[Message], ABC):
    def __init__(self, id: uuid, handler: Callable[[Message], None] | None) -> None:
        super().__init__(id)
        self.handler = handler

    @property
    @abstractmethod
    def converter(self) -> JsonConverter:
        pass

    @abstractmethod
    def decode_message(self, msg: IncomingMessage) -> Message:
        pass

    def on_message_received(self, msg: IncomingMessage) -> None:
        decoded_message = self.decode_message(msg)
        if self.handler:
            self.handler(decoded_message)
