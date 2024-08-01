import uuid
from collections.abc import Callable

from crew_algorithms.envs.channels.messages.written_feedback import (
    NoMessage,
    WrittenFeedbackMessage,
    converter,
)
from mlagents_envs.side_channel.side_channel import IncomingMessage, SideChannel


class WrittenFeedbackChannel(SideChannel):
    def __init__(
        self,
        id: uuid,
        on_written_feedback: Callable[[WrittenFeedbackMessage], None] | None = None,
    ) -> None:
        super().__init__(id)
        self.on_written_feedback = on_written_feedback

    def on_message_received(self, msg: IncomingMessage) -> None:
        written_feedback = converter.loads(
            msg.read_string(), WrittenFeedbackMessage | NoMessage
        )
        print("MSG", written_feedback.message)
        if self.on_written_feedback:
            print("CALLING")
            self.on_written_feedback(written_feedback)
