import cv2
import os
from pathlib import Path
import re
import numpy as np


def sort_key(filename):
    match = re.search(r'capture_(\d+)', filename)
    return int(match.group(1)) if match else float('inf')


def load_sorted_images(folder, repeat=1):
    image_files = sorted([f for f in os.listdir(folder) if f.endswith('.png')], key=sort_key)
    images = []
    for filename in image_files:
        img = cv2.imread(os.path.join(folder, filename))
        for _ in range(repeat):
            images.append(img)
    return images


def get_frame_or_last(images, index):
    if not images:
        return None
    if index < len(images):
        return images[index]
    else:
        return images[-1]  # Repeat last frame


def resize_to_match(image, target_size):
    return cv2.resize(image, target_size)


def compile_split_screen_video(base_folder, output_video_path, frame_rate=20):
    base_path = Path(base_folder)
    agents = [f for f in base_path.iterdir() if f.is_dir() and f.name.startswith("Agent_")]

    agent_feeds = []
    max_frames = 0

    for agent in agents:
        minimap_folder = agent / "Minimap"
        pov_folder = agent / "POV"

        if minimap_folder.exists() and pov_folder.exists():
            minimap_imgs = load_sorted_images(minimap_folder)
            pov_imgs = load_sorted_images(pov_folder)
            max_frames = max(max_frames, len(minimap_imgs), len(pov_imgs))
            agent_feeds.append((minimap_imgs, pov_imgs))

    # Server folders
    server_acc_imgs = load_sorted_images(base_path / "Server_Accumulative")
    server_map_imgs = load_sorted_images(base_path / "Server_Map")
    max_frames = max(max_frames, len(server_acc_imgs), len(server_map_imgs))

    # Get a reference size from the first available image
    ref_img = None
    for minimap, pov in agent_feeds:
        if minimap:
            ref_img = minimap[0]
            break
    # if not ref_img:
    #     ref_img = server_acc_imgs[0] if server_acc_imgs else server_map_imgs[0]

    height, width, _ = ref_img.shape
    size = (width, height)

    # Prepare video writer
    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    video = cv2.VideoWriter(str(output_video_path), fourcc, frame_rate, (width * 2, height * (len(agent_feeds) + 1)))

    for i in range(max_frames):
        rows = []
        for minimap_imgs, pov_imgs in agent_feeds:
            minimap_frame = resize_to_match(get_frame_or_last(minimap_imgs, i), size)
            pov_frame = resize_to_match(get_frame_or_last(pov_imgs, i), size)
            rows.append(cv2.hconcat([minimap_frame, pov_frame]))

        server_acc_frame = resize_to_match(get_frame_or_last(server_acc_imgs, i), size)
        server_map_frame = resize_to_match(get_frame_or_last(server_map_imgs, i), size)
        rows.append(cv2.hconcat([server_acc_frame, server_map_frame]))

        final_frame = cv2.vconcat(rows)
        video.write(final_frame)

    video.release()


if __name__ == "__main__":
    folder_path = (
        Path("crew-algorithms")
        / "crew_algorithms"
        / "wildfire_alg"
        / "results"
        / "logs"
        / "CAMON"
        / "Cut_Trees_Sparse_small"
        / "483"
        / "2025-05-30-16-18-19"
    )
    output_path = folder_path / "combined_output.mp4"
    compile_split_screen_video(folder_path, output_path)
