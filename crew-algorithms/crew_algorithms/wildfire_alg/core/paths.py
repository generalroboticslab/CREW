import os
from pathlib import Path, PurePath


def _coerce_path(path):
    if isinstance(path, PurePath):
        return path

    return Path(path)


def build_results_path(algorithm, level, seed, timestamp, root=None):
    """Build a results directory using the current platform's separators."""
    if root is None:
        root = Path("results")

    path = _coerce_path(root) / "logs" / algorithm / level / str(seed) / timestamp
    return os.fspath(path)


def build_agent_chat_path(results_path, agent_id):
    """Build the chat log path inside an agent's results directory."""
    path = _coerce_path(results_path) / f"Agent_{agent_id}" / "chats.txt"
    return os.fspath(path)
