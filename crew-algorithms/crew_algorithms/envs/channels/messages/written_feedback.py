"""Messages for communication with written feedback."""

from attrs import field, frozen
from cattrs.converters import UnstructureStrategy
from cattrs.preconf.json import make_converter

converter = make_converter(unstruct_strat=UnstructureStrategy.AS_TUPLE)


@frozen
class WrittenFeedbackMessage:
    message: str


class NoMessage:
    pass


def _obj_and_type_selector(o):
    message_type = o[0]
    if message_type == "WF":
        obj = o[1:]
        return *obj, WrittenFeedbackMessage
    else:
        raise ValueError("Unknown message passed!")


converter.register_structure_hook(
    WrittenFeedbackMessage | NoMessage,
    lambda o, _: converter.structure(*_obj_and_type_selector(o)),
)
