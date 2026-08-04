import os
import tempfile
import unittest
from pathlib import Path, PurePosixPath, PureWindowsPath

from crew_algorithms.wildfire_alg.core.paths import (
    build_agent_chat_path,
    build_results_path,
)


class WildfirePathTests(unittest.TestCase):
    def test_results_path_uses_posix_separators(self):
        path = build_results_path(
            "MANUAL",
            "Cut_Trees_Sparse_small",
            483,
            "2026-07-31-10-54-36",
            root=PurePosixPath("results"),
        )

        self.assertEqual(
            path,
            "results/logs/MANUAL/Cut_Trees_Sparse_small/483/" "2026-07-31-10-54-36",
        )

    def test_results_path_uses_windows_separators(self):
        path = build_results_path(
            "CAMON",
            "Cut_Trees_Sparse_small",
            483,
            "2026-07-31-10-54-36",
            root=PureWindowsPath("results"),
        )

        self.assertEqual(
            path,
            "results\\logs\\CAMON\\Cut_Trees_Sparse_small\\483\\" "2026-07-31-10-54-36",
        )

    def test_chat_path_is_nested_under_agent_directory(self):
        posix_path = build_agent_chat_path(PurePosixPath("run"), 3)
        windows_path = build_agent_chat_path(PureWindowsPath("run"), 3)

        self.assertEqual(posix_path, "run/Agent_3/chats.txt")
        self.assertEqual(windows_path, "run\\Agent_3\\chats.txt")

    def test_results_and_chat_logs_share_one_directory_tree(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory) / "results"
            results_path = build_results_path(
                "MANUAL",
                "Cut_Trees_Sparse_small",
                483,
                "2026-07-31-10-54-36",
                root=root,
            )
            chat_path = build_agent_chat_path(results_path, 3)

            Path(results_path, "Agent_3").mkdir(parents=True)
            Path(results_path, "data.csv").touch()
            Path(chat_path).touch()

            self.assertTrue(Path(results_path, "data.csv").is_file())
            self.assertTrue(Path(results_path, "Agent_3", "chats.txt").is_file())
            self.assertEqual(
                os.path.commonpath([results_path, chat_path]), results_path
            )


if __name__ == "__main__":
    unittest.main()
