#!/bin/bash

# Ensure the script is run with bash, not sh
if [ -z "$BASH_VERSION" ]; then
    echo "Please run this script using bash, not sh" >&2
    exit 1
fi

# Check if conda is installed
if ! command -v conda &> /dev/null; then
    echo "conda could not be found, please install Anaconda or Miniconda first."
    exit 1
fi

# Initialize conda (this handles paths as well)
eval "$(conda shell.bash hook)"

# Create UnityLogs directory
echo "Creating UnityLogs directory..."
mkdir -p ../UnityLogs

# Create a conda environment with Python 3.10.11
echo "Creating conda environment 'crew' with Python 3.10.11..."
conda create -n crew python=3.10.11 -y

# Activate the environment and install PyAudio
echo "Activating the 'crew' environment and installing PyAudio..."
conda activate crew
conda install -c conda-forge pyaudio -y


