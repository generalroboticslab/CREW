"""Messages for communication with the Tetris game."""

from abc import ABC

from attrs import frozen
from cattrs.converters import UnstructureStrategy
from cattrs.preconf.json import make_converter

converter = make_converter(unstruct_strat=UnstructureStrategy.AS_TUPLE)


@frozen
class EventMessage(ABC):
    pass


@frozen
class ObjectSpawnedEventMessage(EventMessage):
    pass


class NoMessage:
    pass


def _obj_and_type_selector(o):
    message_type = o[0]
    if message_type == "E":
        event_type = o[1]
        obj = o[2:]
        if event_type == "ObjectSpawned":
            return obj, ObjectSpawnedEventMessage
    raise ValueError("Unknown message passed!")


converter.register_structure_hook(
    ObjectSpawnedEventMessage | NoMessage,
    lambda o, _: converter.structure(*_obj_and_type_selector(o)),
)
