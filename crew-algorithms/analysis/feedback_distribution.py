import os
import pickle

import matplotlib.pyplot as plt

# Visualize Feedback Value Distribution

find_treasure_all_feedback = []
hide_and_seek_all_feedback = []

root_path = "<ENTER PATH TO FEEDBACK FILES>"

subs = os.listdir(root_path)

for sub in subs:
    exps = os.listdir(f"{root_path}/{sub}/saved_training")
    for exp in exps:
        if "find_treasure" in exp:
            with open(
                f"{root_path}/{sub}/saved_training/{exp}/hf_values.pkl", "rb"
            ) as f:
                hf = pickle.load(f)
            find_treasure_all_feedback.extend(hf)
        elif "hide_and_seek" in exp:
            with open(
                f"{root_path}/{sub}/saved_training/{exp}/hf_values.pkl", "rb"
            ) as f:
                hf = pickle.load(f)
            hide_and_seek_all_feedback.extend(hf)

find_treasure_all_feedback = [f for f in find_treasure_all_feedback if f != 0.0]
hide_and_seek_all_feedback = [f for f in hide_and_seek_all_feedback if f != 0.0]

print(len(find_treasure_all_feedback))
print(len(hide_and_seek_all_feedback))


plt.figure(figsize=(15, 6))
plt.hist(find_treasure_all_feedback, bins=100, color="orange", alpha=0.4)
# plt.hist(hide_and_seek_all_feedback, bins=100, color='lightcoral', alpha=0.4)
plt.xlabel("Feedback Value")
plt.ylabel("Frequency")
plt.show()
