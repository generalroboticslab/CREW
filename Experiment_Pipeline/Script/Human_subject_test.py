import subprocess
import threading

from utils import close_client, play_video, run_client, run_python_script

# List of paths to your Unity builds
build_paths = [
    # "Image_and_video/Congnitive Test Intro.mp4",
    # "Image_and_video/Alignment_Intro.mp4",
    # "game_build/Eye_alignment_test/game.x86_64",
    # "Image_and_video/Reflection_intro.mp4",
    # "game_build/Reflex_test/game.x86_64",
    # "Image_and_video/Theory_of_mind_intro.mp4",
    # "game_build/Theory_of_behavior_test/game.x86_64",
    # "Image_and_video/Test Intro.mp4",
    # "game_build/Puzzle_solving/game.x86_64",
    # "Image_and_video/Video Test Intro.mp4",
    # "game_build/Spatial_mapping/game.x86_64",
]

# Iterate over each build path and run the build
for build_path in build_paths:
    try:
        if build_path.split(".")[-1] == "x86_64":
            process = subprocess.Popen([build_path])
            process.wait()

        elif build_path.split(".")[-1] == "mp4":
            # play_video(build_path)
            pass

        else:
            # Start two threads for running each command
            thread1 = threading.Thread(target=run_python_script, args=(build_path,))
            thread2 = threading.Thread(target=run_client, args=(build_path,))
            thread3 = threading.Thread(target=close_client)

            # Start both threads
            thread1.start()
            thread2.start()
            thread3.start()

            # Wait for both threads to finish
            thread1.join()
            thread2.join()
            thread3.join()

    except FileNotFoundError:
        print(f"File not found: {build_path}")
    except Exception as e:
        print(f"Error occurred while running {build_path}: {e}")
