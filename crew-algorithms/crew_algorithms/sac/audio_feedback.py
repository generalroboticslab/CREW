import os
import wave

import numpy as np
import pyaudio
import torch


class Audio_Streamer:
    """Class to stream audio from the microphone and save it to a file

    Args:
        channels: Number of channels to record
        rate: Sampling rate
        frames_per_buffer: Number of frames per buffer
    """

    def __init__(self, channels=1, rate=44100, frames_per_buffer=1024):
        self.channels = channels
        self.rate = rate
        self.frames_per_buffer = frames_per_buffer
        self.buffer = []
        os.makedirs("crew_algorithm/sac/audio", exist_ok=True)

    def start_streaming(self):
        self.audio = pyaudio.PyAudio()
        self.stream = self.audio.open(
            format=pyaudio.paInt16,
            channels=self.channels,
            rate=self.rate,
            input=True,
            frames_per_buffer=self.frames_per_buffer,
        )

    def get_sample(self):
        data = self.stream.read(self.frames_per_buffer, exception_on_overflow=False)
        self.buffer.append(data)

    def stop_streaming(self):
        self.stream.stop_streaming()
        self.stream.close()
        self.audio.terminate()

    def save_to_file(self, file_name):
        sound_file = wave.open("crew_algorithms/ddpg/audio/%s.wav" % file_name, "wb")
        sound_file.setnchannels(1)
        sound_file.setsampwidth(pyaudio.get_sample_size(pyaudio.paInt16))
        sound_file.setframerate(44100)
        sound_file.writeframes(b"".join(self.buffer))
        sound_file.close()
        self.buffer = []

    def to_torch_tensor(self):
        final_buffer = []
        for data in self.buffer:
            npbuffer = np.frombuffer(data, dtype=np.int16).astype(np.float32)
            npbuffer = npbuffer / 32768  # normalize between -1 and 1
            npbuffer = np.reshape(npbuffer, (-1, 1))  # reshape to time-major frame
            final_buffer.append(npbuffer)  # append to final buffer

        # concatenate all the small buffers
        final_buffer_np = np.concatenate(final_buffer)
        # convert final numpy array to torch tensor
        torchbuffer = torch.from_numpy(final_buffer_np).to("cuda")
        return torchbuffer
