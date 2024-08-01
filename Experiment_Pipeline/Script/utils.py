import os
import time

from moviepy.editor import VideoFileClip


def play_video(video_path):
    # Load the video file
    video = VideoFileClip(video_path)

    # Play the video with audio
    video.preview(fullscreen=True)


def run_client(build_path):
    time.sleep(12)  # Wait for 6 seconds
    cd_command = "cd ~"
    client_command = f"./crew-dojo/Builds/{build_path}-StandaloneLinux64-Client/Unity.x86_64 -screen-width 1920 -screen-height 1080"
    os.system(f"{cd_command} && {client_command}")


def close_client():
    time.sleep(30)  # Wait for 6 seconds
    os.system(f"pkill Unity.x86_64")


def run_python_script(env_name):
    cd_command1 = "cd ~/crew-algorithms"
    wandb_command = "export WANDB_MODE=disabled"
    python_script_command = f"python crew_algorithms/ddpg"
    os.system(f"{cd_command1} && {wandb_command} && {python_script_command}")
