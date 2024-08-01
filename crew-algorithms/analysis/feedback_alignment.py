import json
import os
import pickle

import matplotlib.pyplot as plt
import numpy as np
import seaborn as sns
from statsmodels.tsa.stattools import grangercausalitytests

# Measuring how similar the human feedback is to the heuristic feedback


def get_feedback(root_path):
    paths = os.listdir(root_path)
    for path in paths:
        if "find_treasure" in path:
            heu_path = f"{root_path}/{path}/hf_values.pkl"
            hf_path = f"{root_path}/{path}/heuristic_values.pkl"
            break
        else:
            continue

    with open(heu_path, "rb") as f:
        heu = pickle.load(f)
    with open(hf_path, "rb") as f:
        hf = pickle.load(f)
    return hf, heu


def process(feedback, normalize_mode="std"):
    new_feedback = []
    f_max, f_min, f_mean, f_std = (
        max(feedback),
        min(feedback),
        np.mean(feedback),
        np.std(feedback),
    )
    for f in feedback:
        if normalize_mode == "std":
            new_feedback.append((f - f_mean) / (f_std))
        elif normalize_mode == "minmax":
            if f > 0:
                # new_feedback.append(f/f_max)
                new_feedback.append((f - f_mean) / (f_std))
            else:
                new_feedback.append((f - f_mean) / (f_std))
                # new_feedback.append(-f/f_min)
    return new_feedback


def affine_invariant_sequence_similarity(hf, heu, shift=2):
    hf = process(hf)
    heu = process(heu)

    if shift == 0:
        hf = np.array(hf)
        heu = np.array(heu)
    else:
        hf = np.array(hf[shift:])
        heu = np.array(heu[:-shift])

    error = np.sum(np.abs(hf - heu)) / len(hf)
    print(error)

    return error


def granger_causality(hf, heu):
    x = np.array([hf, heu]).T
    return grangercausalitytests(x, [2], addconst=True, verbose=False)[2][0][
        "ssr_ftest"
    ][1]


def get_plot(sub_path):
    hf, heu = get_feedback(sub_path)
    shifts = []

    for s in range(1):
        shifts.append(affine_invariant_sequence_similarity(hf, heu, s))
        # out = granger_causality(hf, heu)
        # print(out)
        # shifts.append(out)
    return shifts
    # plt.plot(shifts)
    # plt.show()


with open(f"/home/grl/Desktop/guide_scores.json", "r") as f:
    scores = json.load(f)

import scipy

all_errors = []
all_scores = []
for sub in os.listdir("../Data/FINAL/feedback_analysis"):
    if sub[:2] == "12":
        continue
    hf, heu = get_feedback(sub)
    error = affine_invariant_sequence_similarity(hf, heu)
    print(sub, error, scores[sub[:2]])
    all_errors.append(error)
    all_scores.append(scores[sub[:2]])


score_v_error = np.array([all_scores, all_errors])
np.save("scores_v_errors.npy", score_v_error)

print(scipy.stats.pearsonr(all_errors, all_scores))

plt.figure(figsize=(5, 5))


sns.regplot(y=all_scores, x=all_errors)
# plt.scatter(all_errors, all_scores)
plt.xlabel("Heuristic feedback matching error")
plt.ylabel("Total Guide score")
plt.grid()
plt.show()
