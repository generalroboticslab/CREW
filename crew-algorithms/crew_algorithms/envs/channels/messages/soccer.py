"""Messages for communication with soccer game."""

import enum
from abc import ABC

from attrs import field, frozen
from cattrs.converters import UnstructureStrategy
from cattrs.preconf.json import make_converter

converter = make_converter(unstruct_strat=UnstructureStrategy.AS_TUPLE)


@frozen
class EventMessage(ABC):
    pass


@frozen
class GameStartedEventMessage(EventMessage):
    pass


@frozen
class GameEndedEventMessage(EventMessage):
    pass


class Team(enum.Enum):
    Blue = "blue"
    Purple = "purple"


@frozen
class GameScoredEventMessage(EventMessage):
    team: Team = field(converter=Team)


def _obj_and_type_selector(o):
    message_type = o[0]
    if message_type == "E":
        event_type = o[1]
        obj = o[2:]

        if event_type == "GameStarted":
            return obj, GameStartedEventMessage
        elif event_type == "GameEnded":
            return obj, GameEndedEventMessage
        elif event_type == "GameScored":
            return obj, GameScoredEventMessage
    raise ValueError("Unknown message passed!")


converter.register_structure_hook(
    GameStartedEventMessage | GameEndedEventMessage | GameScoredEventMessage,
    lambda o, _: converter.structure(*_obj_and_type_selector(o)),
)
