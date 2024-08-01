import uuid

from mlagents_envs.side_channel.side_channel import (
    IncomingMessage,
    OutgoingMessage,
    SideChannel,
)


class ToggleTimestepChannel(SideChannel):
    def __init__(self, id: uuid) -> None:
        super().__init__(id)

    def on_message_received(self, msg: IncomingMessage) -> None:
        pass

    def send_toogle_timestep(self) -> None:
        msg = OutgoingMessage()
        msg.write_string("Toggle Timestep")
        super().queue_message_to_send(msg)
