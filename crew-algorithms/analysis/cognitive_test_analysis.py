import json
import os

import matplotlib.pyplot as plt
import numpy as np
import statsmodels.api as sm
from scipy.stats import linregress


def eye(path):
    eye_path = path + "Eye-alignment"
    files = os.listdir(eye_path)[0]
    with open(eye_path + "/" + files, "r") as f:
        data = f.readlines()

    errors = []
    for d in data:
        # d = 'Episode: 0, Target Position: 0, Ruler Position: -0.03888893\n'
        target = d.split("Target Position: ")[1].split(",")[0]
        ruler = d.split("Ruler Position: ")[1].split("\n")[0]
        errors.append(abs(float(target) - float(ruler)))

    return -sum(errors) / len(errors)


def theory(path):
    theory_path = path + "Theory_of_bahavior_test"
    files = os.listdir(theory_path)[0]
    with open(theory_path + "/" + files, "r") as f:
        data = f.readlines()

    errors = []
    for d in data:
        if "Fail" in d:
            continue
        true = d.split("true position: (")[1].split("),")[0]
        true = np.array([float(i) for i in true.split(",")])
        pred = d.split("predicted position: (")[1].split(")")[0]
        pred = np.array([float(i) for i in pred.split(",")])
        errors.append(((true - pred) ** 2).sum())

    return -sum(errors) / len(errors)


def fittness(path):
    puzzle_path = path + "Puzzle_solving"
    files = os.listdir(puzzle_path)[0]
    with open(puzzle_path + "/" + files, "r") as f:
        data = f.readlines()

    co = 0
    total_time = 0
    for i, d in enumerate(data):
        if i >= 6:
            total_time += float(d.split("Time: ")[1].split("\n")[0])
            if "Not Answered" in d:
                continue
            correct = d.split("Correct Answer: ")[1].split(",")[0]
            chosen = d.split("Chosen Answer: ")[1].split(",")[0]
            if correct == chosen:
                co += 1
    return co / 6, total_time / 6


def rotation(path):
    puzzle_path = path + "Puzzle_solving"
    files = os.listdir(puzzle_path)[0]
    with open(puzzle_path + "/" + files, "r") as f:
        data = f.readlines()

    co = 0
    total_time = 0
    for i, d in enumerate(data):
        if i < 6:
            total_time += float(d.split("Time: ")[1].split("\n")[0])
            if "Not Answered" in d:
                continue
            correct = d.split("Correct Answer: ")[1].split(",")[0]
            chosen = d.split("Chosen Answer: ")[1].split(",")[0]

            if correct == chosen:
                co += 1

    return co / 6, total_time / 6


def spatial(path):
    spatial_path = path + "Spatial_mapping"
    files = os.listdir(spatial_path)[0]
    with open(spatial_path + "/" + files, "r") as f:
        data = f.readlines()

    co = 0
    total_time = 0
    for d in data:
        total_time += float(d.split("Time: ")[1].split("\n")[0])
        if "Not Answered" in d:
            continue
        correct = d.split("Correct Answer: ")[1].split(",")[0]
        chosen = d.split("Chosen Answer: ")[1].split(",")[0]

        if correct == chosen:
            co += 1
    return co / 6, total_time / 6


def reflex(path):
    reflex_path = path + "Reflection_test"
    files = os.listdir(reflex_path)[0]
    with open(reflex_path + "/" + files, "r") as f:
        data = f.readlines()

    time = []
    for d in data:
        if "Fail " in d:
            continue
        t = d.split("Reflection Time: ")[1].split("\n")[0]
        time.append(float(t))

    if len(time) == 0:
        return -9999999

    return -sum(time) / len(time)


def overall(path):
    return {
        "sub": path.split("/")[-2][:2],
        "eye": eye(path),
        "theory": theory(path),
        "rotation": rotation(path),
        "fittness": fittness(path),
        "spatial": spatial(path),
        "reflex": reflex(path),
    }


def plot_and_save(
    x, y, xlabel, ylabel, title, filename, output_folder, xtick_interval=1
):
    if not os.path.exists(output_folder):
        os.makedirs(output_folder)

    # Perform linear regression
    slope, intercept, r_value, p_value, std_err = linregress(x, y)

    # Calculate regression line
    line = slope * np.array(x) + intercept

    # Calculate 95% confidence interval
    x = sm.add_constant(x)
    model = sm.OLS(y, x).fit()
    predictions = model.get_prediction(x)
    frame = predictions.summary_frame(alpha=0.05)  # 95% confidence interval

    plt.figure()
    plt.scatter(x[:, 1], y, label="Data")
    plt.plot(
        x[:, 1],
        line,
        color="blue",
        label="Fit: y={:.2f}x+{:.2f}".format(slope, intercept),
    )
    plt.fill_between(
        x[:, 1],
        frame["obs_ci_lower"],
        frame["obs_ci_upper"],
        color="blue",
        alpha=0.1,
        label="95% CI",
    )

    plt.xlabel(xlabel)
    plt.ylabel(ylabel)
    plt.title(f"{title}\nSlope: {slope:.3f}, p-value: {p_value:.3f}")
    plt.grid(True)
    plt.xticks(range(0, len(x[:, 1]), 10))
    plt.legend()

    plt.savefig(os.path.join(output_folder, filename))
    plt.close()


if __name__ == "__main__":
    cognitive_test_path = "<PLEASE INPUT PATH TO DATA>"
    subs = os.listdir(cognitive_test_path)
    sub_data = []
    for sub in subs:
        sub = sub + "/"
        sub_data.append(overall(cognitive_test_path + sub))

    num_subs = len(sub_data)

    sub_data.sort(key=lambda x: x["eye"], reverse=True)
    for i in range(num_subs):
        if i == 0:
            sub_last = sub_data[0]
        if sub_last["eye"] == sub_data[i]["eye"] and i != 0:
            sub_data[i]["eye_rank"] = i - 1
        else:
            sub_data[i]["eye_rank"] = i
        sub_last = sub_data[i]

    sub_data.sort(key=lambda x: x["theory"], reverse=True)
    for i in range(num_subs):
        if i == 0:
            sub_last = sub_data[0]
        if sub_last["theory"] == sub_data[i]["theory"] and i != 0:
            sub_data[i]["theory_rank"] = sub_last["theory_rank"]
        else:
            sub_data[i]["theory_rank"] = i
        sub_last = sub_data[i]

    sub_data.sort(key=lambda x: (-x["rotation"][0], x["rotation"][1]))
    for i in range(num_subs):
        if i == 0:
            sub_last = sub_data[0]
        if sub_last["rotation"] == sub_data[i]["rotation"] and i != 0:
            sub_data[i]["rotation_rank"] = sub_last["rotation_rank"]
        else:
            sub_data[i]["rotation_rank"] = i
        sub_last = sub_data[i]

    sub_data.sort(key=lambda x: (-x["fittness"][0], x["fittness"][1]))
    for i in range(num_subs):
        if i == 0:
            sub_last = sub_data[0]
        if sub_last["fittness"] == sub_data[i]["fittness"] and i != 0:
            sub_data[i]["fittness_rank"] = sub_last["fittness_rank"]
        else:
            sub_data[i]["fittness_rank"] = i
        sub_last = sub_data[i]

    sub_data.sort(key=lambda x: (-x["spatial"][0], x["spatial"][1]))
    for i in range(num_subs):
        if i == 0:
            sub_last = sub_data[0]
        if sub_last["spatial"] == sub_data[i]["spatial"] and i != 0:
            sub_data[i]["spatial_rank"] = sub_last["spatial_rank"]
        else:
            sub_data[i]["spatial_rank"] = i
        sub_last = sub_data[i]

    sub_data.sort(key=lambda x: x["reflex"], reverse=True)
    for i in range(num_subs):
        if i == 0:
            sub_last = sub_data[0]
        if sub_last["reflex"] == sub_data[i]["reflex"] and i != 0:
            sub_data[i]["reflex_rank"] = sub_last["reflex_rank"]
        else:
            sub_data[i]["reflex_rank"] = i
        sub_last = sub_data[i]

    sub_data.sort(
        key=lambda x: x["eye_rank"]
        + x["theory_rank"]
        + x["rotation_rank"]
        + x["fittness_rank"]
        + x["spatial_rank"]
        + x["reflex_rank"]
    )
    for i in range(num_subs):
        if i == 0:
            sub_last = sub_data[0]
        if (
            sub_last["eye_rank"]
            + sub_last["theory_rank"]
            + sub_last["rotation_rank"]
            + sub_last["fittness_rank"]
            + sub_last["spatial_rank"]
            + sub_last["reflex_rank"]
            == sub_data[i]["eye_rank"]
            + sub_data[i]["theory_rank"]
            + sub_data[i]["rotation_rank"]
            + sub_data[i]["fittness_rank"]
            + sub_data[i]["spatial_rank"]
            + sub_data[i]["reflex_rank"]
            and i != 0
        ):
            sub_data[i]["overall_rank"] = sub_last["overall_rank"]
        else:
            sub_data[i]["overall_rank"] = i
        sub_last = sub_data[i]
        sub_data[i]["overall_rank"] = i

    outer_folder = "/home/grl/Desktop/corr_analysis"
    with open("/home/grl/Desktop/guide_scores.json", "r") as f:
        guide_scores = json.load(f)
    criteria = [
        "eye_rank",
        "theory_rank",
        "spatial_rank",
        "rotation_rank",
        "fittness_rank",
        "reflex_rank",
        "overall_rank",
    ]

    for id in range(7):
        sub_id_list = [s["sub"] for s in sub_data]
        sub_id_ranking = [s[criteria[id]] for s in sub_data]

        bowling_score = [guide_scores[s][0] for s in sub_id_list]
        FT_score = [guide_scores[s][1] for s in sub_id_list]
        HS_score = [guide_scores[s][2] for s in sub_id_list]
        total_score = [
            guide_scores[s][0] + guide_scores[s][1] + guide_scores[s][2]
            for s in sub_id_list
        ]

        for score in ["bowling_score", "FT_score", "HS_score", "total_score"]:
            plot_and_save(
                sub_id_ranking,
                eval(score),
                "Rank",
                "Score",
                f"{criteria[id]}_vs_{score}",
                f"{criteria[id]}_vs_{score}.png",
                outer_folder,
            )
