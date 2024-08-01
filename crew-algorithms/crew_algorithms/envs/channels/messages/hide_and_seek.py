"""Messages for communication with hide and seek game."""

import enum
from abc import ABC

from attrs import field, frozen
from cattrs.converters import UnstructureStrategy
from cattrs.preconf.json import make_converter

converter = make_converter(unstruct_strat=UnstructureStrategy.AS_TUPLE)


@frozen
class EventMessage(ABC):
    pass


class Identity(enum.Enum):
    Hider = "Hider"
    Seeker = "Seeker"


@frozen
class EpisodeStartEventMessage(EventMessage):
    pass


@frozen
class EpisodeStopEventMessage(EventMessage):
    winner: Identity = field(converter=Identity)
    episode_duration: float


@frozen
class RoleSelectionEventMessage(EventMessage):
    pass


@frozen
class SeekerCaughtHiderEventMessage(EventMessage):
    pass


def _obj_and_type_selector(o):
    message_type = o[0]
    if message_type == "E":
        event_type = o[1]
        obj = o[2:]
        if event_type == "EpisodeStart":
            return *obj, EpisodeStartEventMessage
        elif event_type == "EpisodeStop":
            return *obj, EpisodeStopEventMessage
        elif event_type == "RoleSelection":
            return obj, RoleSelectionEventMessage
        elif event_type == "SeekerHasCaught":
            return obj, SeekerCaughtHiderEventMessage
    raise ValueError("Unknown message passed!")


converter.register_structure_hook(
    EpisodeStartEventMessage
    | EpisodeStopEventMessage
    | RoleSelectionEventMessage
    | SeekerCaughtHiderEventMessage,
    lambda o, _: converter.structure(*_obj_and_type_selector(o)),
)
