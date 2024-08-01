import json
import os
from collections import defaultdict
from time import time

import matplotlib.pyplot as plt
import matplotlib.ticker as ticker
import numpy as np
from crew_algorithms.sac.utils import get_time


class custom_logger:
    """ A custom logger for local data logging"""
    def __init__(self, path, start_time=None, read_mode=False):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.path = path
        self.start_time = start_time

        if read_mode:
            with open(path, "r") as f:
                self.data = json.load(f)
        else:
            self.data = defaultdict(list)

    def log(self, x_axis, y_axis, x_value, y_value, log_time):
        x_value, y_value = round(float(x_value), 4), round(float(y_value), 4)
        self.data[x_axis + "-" + y_axis].append(x_value)
        self.data[y_axis + "-" + x_axis].append(y_value)
        if log_time:
            self.data["time-" + y_axis].append(
                round(time() - self.start_time, 3)
            )  # x_axis
            self.data[y_axis + "-time"].append(y_value)

    def get_curve(self, x_axis, y_axis, smooth=1):
        x, y = self.data[x_axis + "-" + y_axis], self.data[y_axis + "-" + x_axis]
        if smooth > 1:
            x = [np.array(x[max(0, i - smooth) : i]).mean() for i in range(len(x))]
            y = [np.array(y[max(0, i - smooth) : i]).mean() for i in range(len(y))]
        return x, y

    def save_log(self):
        with open(self.path, "w") as f:
            json.dump(self.data, f)