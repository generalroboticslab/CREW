from cattrs.preconf.json import JsonConverter
from crew_algorithms.envs.channels.game_event_channel import GameEventChannel
from crew_algorithms.envs.channels.messages.hide_and_seek import (
    EpisodeStartEventMessage,
    EpisodeStopEventMessage,
    RoleSelectionEventMessage,
    SeekerCaughtHiderEventMessage,
    converter,
)
from mlagents_envs.side_channel import IncomingMessage


class HideAndSeekEventChannel(
    GameEventChannel[
        EpisodeStartEventMessage
        | EpisodeStopEventMessage
        | RoleSelectionEventMessage
        | SeekerCaughtHiderEventMessage
    ]
):
    @property
    def converter(self) -> JsonConverter:
        return converter

    def decode_message(
        self, msg: IncomingMessage
    ) -> (
        EpisodeStartEventMessage
        | EpisodeStopEventMessage
        | RoleSelectionEventMessage
        | SeekerCaughtHiderEventMessage
        | SeekerCaughtHiderEventMessage
    ):
        return self.converter.loads(
            msg.read_string(),
            EpisodeStartEventMessage
            | EpisodeStopEventMessage
            | RoleSelectionEventMessage
            | SeekerCaughtHiderEventMessage,
        )
