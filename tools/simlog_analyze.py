#!/usr/bin/env python3
import argparse
import csv
import math
import sys

import numpy as np

ACTIONS = ["idle", "move_to", "attack", "eat", "reproduce", "signal", "rest", "give", "take", "put", "move_nest"]
BEHAVIOUR_TAIL = ["distance", "speech_rate", "attacks", "food_eaten", "energy_surplus"]
LOG1P_COLUMNS = {"distance", "attacks", "food_eaten"}
NULL_ARM = "null_policy"
CONTROL_ARM = "control"
ARM_ORDER = [CONTROL_ARM, "speech_muted", "speech_shuffled", NULL_ARM]


def num(text):
    if text is None or text == "":
        return math.nan
    try:
        return float(text)
    except ValueError:
        return math.nan


def read_rows(path):
    with open(path, newline="", encoding="utf-8") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise SystemExit(f"{path}: empty file")
        return reader.fieldnames, list(reader)


def need(columns, names, path):
    missing = [n for n in names if n not in columns]
    if missing:
        raise SystemExit(f"{path}: missing columns {', '.join(missing)}")


def arm_sort_key(arm):
    return (ARM_ORDER.index(arm), arm) if arm in ARM_ORDER else (len(ARM_ORDER), arm)


def entropy_bits(counts):
    total = counts.sum()
    if total <= 0:
        return math.nan
    p = counts[counts > 0] / total
    return float(-(p * np.log2(p)).sum())


def nanmean(values):
    values = np.asarray(values, dtype=float)
    values = values[~np.isnan(values)]
    return float(values.mean()) if values.size else math.nan


def fmt(value, digits=3):
    if value is None or (isinstance(value, float) and math.isnan(value)):
        return "-"
    return f"{value:.{digits}f}"


def colony_tables(columns, rows):
    need(columns, ["time_s", "scope", "colony_id", "alive", "born", "died", "arm", "speech_mi_bits", "listener_shift"], "simulation log")
    started = [f"{a}_started" for a in ACTIONS]
    need(columns, started, "simulation log")

    by_arm = {}
    for row in rows:
        if row["scope"] != "colony":
            continue
        arm = row["arm"] or CONTROL_ARM
        by_arm.setdefault(arm, []).append(row)
    return by_arm, started


def print_population(arm_rows):
    print("== population per arm (sum of colony members at each sample) ==")
    curves = {}
    for arm in sorted(arm_rows, key=arm_sort_key):
        per_time = {}
        for row in arm_rows[arm]:
            t = num(row["time_s"])
            per_time[t] = per_time.get(t, 0.0) + num(row["alive"])
        times = sorted(per_time)
        curves[arm] = (times, [per_time[t] for t in times])

    for arm in sorted(curves, key=arm_sort_key):
        times, values = curves[arm]
        values = np.array(values)
        colonies = len({row["colony_id"] for row in arm_rows[arm]})
        print(f"{arm}: colonies={colonies} samples={len(times)} mean={fmt(values.mean(), 1)} min={fmt(values.min(), 0)} "
              f"max={fmt(values.max(), 0)} final={fmt(values[-1], 0)}")
        points = np.linspace(0, len(times) - 1, num=min(10, len(times))).astype(int)
        print("  curve: " + " ".join(f"{times[i]:.0f}s:{values[i]:.0f}" for i in points))


def print_behaviour(arm_rows, started):
    print()
    print("== per arm: births, deaths, action-mix entropy, decision source, speech ==")
    header = f"{'arm':<16}{'born':>8}{'died':>8}{'lifespan_s':>12}{'act_H_bits':>12}{'brain_sh':>10}{'speech_mi':>11}{'listen_shift':>14}"
    print(header)
    for arm in sorted(arm_rows, key=arm_sort_key):
        rows = arm_rows[arm]
        born = sum(num(r["born"]) for r in rows)
        died = sum(num(r["died"]) for r in rows)
        lifespan_sum = sum(num(r["mean_lifespan_s"]) * num(r["died"]) for r in rows if "mean_lifespan_s" in r and not math.isnan(num(r["mean_lifespan_s"])))
        lifespan = lifespan_sum / died if died > 0 else math.nan

        counts = np.zeros(len(started))
        for r in rows:
            for i, name in enumerate(started):
                value = num(r[name])
                if not math.isnan(value):
                    counts[i] += value
        entropy = entropy_bits(counts)

        brain = nanmean([num(r["brain_share"]) for r in rows]) if "brain_share" in rows[0] else math.nan
        mi = nanmean([num(r["speech_mi_bits"]) for r in rows])
        shift = nanmean([num(r["listener_shift"]) for r in rows])
        print(f"{arm:<16}{born:>8.0f}{died:>8.0f}{fmt(lifespan, 1):>12}{fmt(entropy):>12}{fmt(brain):>10}{fmt(mi):>11}{fmt(shift):>14}")


def print_world(columns, rows):
    world = [r for r in rows if r["scope"] == "world"]
    if not world or "avg_entropy" not in columns:
        return
    values = [num(r["avg_entropy"]) for r in world]
    print()
    print(f"world coordinator entropy (nats): mean={fmt(nanmean(values))} (all arms pooled; the log has one world row)")
    conflict = [c for c in columns if c.startswith("grad_cos_") or c.startswith("grad_norm_")]
    if conflict:
        print("== gradient conflict between castes (world rows, mean over samples) ==")
        for name in conflict:
            print(f"  {name}: {fmt(nanmean([num(r[name]) for r in world]))}")


def feature_names():
    return [f"{a}_share" for a in ACTIONS] + [f"goal_{g}_share" for g in ["survive", "hunt", "forage", "social"]] + BEHAVIOUR_TAIL


def load_descriptors(path, include_wild, min_age):
    columns, rows = read_rows(path)
    names = feature_names()
    need(columns, ["arm", "caste", "age_s"] + names, path)

    by_arm = {}
    for row in rows:
        if row["caste"] == "wild" and not include_wild:
            continue
        if num(row["age_s"]) < min_age:
            continue
        values = [num(row[n]) for n in names]
        if any(math.isnan(v) for v in values):
            continue
        for i, n in enumerate(names):
            if n in LOG1P_COLUMNS:
                values[i] = math.log1p(max(values[i], 0.0))
        by_arm.setdefault(row["arm"] or CONTROL_ARM, []).append(values)
    return names, {arm: np.array(v, dtype=float) for arm, v in by_arm.items() if v}


def kmeans(x, k, rng, n_init=5, iterations=100):
    best_labels, best_inertia = None, math.inf
    for _ in range(n_init):
        centres = np.empty((k, x.shape[1]))
        centres[0] = x[rng.integers(len(x))]
        closest = ((x - centres[0]) ** 2).sum(axis=1)
        for c in range(1, k):
            total = closest.sum()
            if total <= 0:
                centres[c] = x[rng.integers(len(x))]
            else:
                centres[c] = x[rng.choice(len(x), p=closest / total)]
            closest = np.minimum(closest, ((x - centres[c]) ** 2).sum(axis=1))

        labels = np.zeros(len(x), dtype=int)
        for _ in range(iterations):
            distances = ((x[:, None, :] - centres[None, :, :]) ** 2).sum(axis=2)
            new_labels = distances.argmin(axis=1)
            for c in range(k):
                members = x[new_labels == c]
                if len(members):
                    centres[c] = members.mean(axis=0)
                else:
                    centres[c] = x[rng.integers(len(x))]
            if np.array_equal(new_labels, labels):
                break
            labels = new_labels
        inertia = float(((x - centres[labels]) ** 2).sum())
        if inertia < best_inertia:
            best_inertia, best_labels = inertia, labels.copy()
    return best_labels


def silhouette(x, labels):
    k = labels.max() + 1
    if k < 2 or len(np.unique(labels)) < 2:
        return math.nan
    sq = (x ** 2).sum(axis=1)
    d = np.sqrt(np.maximum(sq[:, None] + sq[None, :] - 2.0 * x @ x.T, 0.0))
    scores = np.zeros(len(x))
    masks = [labels == c for c in range(k)]
    sizes = np.array([m.sum() for m in masks])
    for i in range(len(x)):
        own = labels[i]
        if sizes[own] <= 1:
            continue
        a = d[i, masks[own]].sum() / (sizes[own] - 1)
        b = min(d[i, masks[c]].mean() for c in range(k) if c != own and sizes[c] > 0)
        scores[i] = (b - a) / max(a, b, 1e-12)
    return float(scores.mean())


def subsample(x, limit, rng):
    if len(x) <= limit:
        return x
    return x[rng.choice(len(x), size=limit, replace=False)]


def subsample_index(count, limit, rng):
    if count <= limit:
        return np.arange(count)
    return np.sort(rng.choice(count, size=limit, replace=False))


def total_variation(p, q):
    return 0.5 * float(np.abs(p - q).sum())


def print_clusters(names, arms, k_max, max_rows, seed):
    pooled = np.vstack([arms[a] for a in arms])
    mean = pooled.mean(axis=0)
    std = pooled.std(axis=0)
    std[std < 1e-9] = 1.0
    scaled = {a: (v - mean) / std for a, v in arms.items()}
    ks = list(range(2, k_max + 1))

    print()
    print(f"== behavioural descriptors: {sum(len(v) for v in arms.values())} agent-rows, {len(names)} features, z-scored on the pooled rows ==")
    print("== per arm k-means silhouette (higher = more separated behavioural types) ==")
    table = {}
    for arm in sorted(arms, key=arm_sort_key):
        rng = np.random.default_rng(seed)
        x = subsample(scaled[arm], max_rows, rng)
        row = []
        for k in ks:
            if len(x) <= k:
                row.append(math.nan)
                continue
            row.append(silhouette(x, kmeans(x, k, rng)))
        table[arm] = row
        print(f"{arm:<16}rows={len(arms[arm]):<7}" + " ".join(f"k{k}={fmt(s)}" for k, s in zip(ks, row)))

    if NULL_ARM in table:
        print(f"== silhouette minus the {NULL_ARM} arm at the same k (positive: more structure than the zero model) ==")
        for arm in sorted(table, key=arm_sort_key):
            if arm == NULL_ARM:
                continue
            diff = [a - b for a, b in zip(table[arm], table[NULL_ARM])]
            print(f"{arm:<16}" + " ".join(f"k{k}={fmt(s)}" for k, s in zip(ks, diff)))
    else:
        print(f"(no {NULL_ARM} arm in the descriptor file: no comparison against the zero model)")

    valid = [(np.nanmean([table[a][i] for a in table]), k) for i, k in enumerate(ks) if not all(math.isnan(table[a][i]) for a in table)]
    if not valid:
        return
    best_k = max(valid)[1]
    rng = np.random.default_rng(seed)
    picked = {a: subsample_index(len(scaled[a]), max_rows, rng) for a in scaled}
    sample = {a: scaled[a][picked[a]] for a in scaled}
    stacked = np.vstack([sample[a] for a in sorted(sample, key=arm_sort_key)])
    labels = kmeans(stacked, best_k, rng)

    print()
    print(f"== shared clusters, k={best_k} (best mean silhouette over arms): share of each arm's agents per cluster ==")
    offset = 0
    shares = {}
    for arm in sorted(sample, key=arm_sort_key):
        n = len(sample[arm])
        counts = np.bincount(labels[offset:offset + n], minlength=best_k).astype(float)
        shares[arm] = counts / counts.sum()
        offset += n
        print(f"{arm:<16}" + " ".join(f"c{c}={shares[arm][c]:.2f}" for c in range(best_k)))

    if NULL_ARM in shares:
        print(f"== total variation distance of the cluster mix from the {NULL_ARM} arm ==")
        for arm in sorted(shares, key=arm_sort_key):
            if arm != NULL_ARM:
                print(f"{arm:<16}{fmt(total_variation(shares[arm], shares[NULL_ARM]))}")

    print("== cluster centres (original units: shares, log1p counts, rates), pooled over the sampled rows ==")
    raw = np.vstack([arms[a][picked[a]] for a in sorted(sample, key=arm_sort_key)])
    for c in range(best_k):
        members = raw[labels == c]
        if not len(members):
            continue
        centre = members.mean(axis=0)
        top = np.argsort(-np.abs((centre - mean) / std))[:4]
        print(f"c{c} n={len(members)}: " + ", ".join(f"{names[i]}={centre[i]:.2f}" for i in top))


def main():
    parser = argparse.ArgumentParser(description="Compare experiment arms in a TheRavine simulation log and its behavioural descriptor file")
    parser.add_argument("simlog", help="simulation_log_<stamp>.csv")
    parser.add_argument("descriptors", nargs="?", help="behaviour_descriptors_<stamp>.csv")
    parser.add_argument("--k-max", type=int, default=8)
    parser.add_argument("--max-rows", type=int, default=1500, help="per-arm cap for silhouette (the matrix is rows x rows)")
    parser.add_argument("--min-age", type=float, default=0.0, help="drop descriptor rows of agents younger than this many seconds")
    parser.add_argument("--include-wild", action="store_true")
    parser.add_argument("--seed", type=int, default=1)
    args = parser.parse_args()

    columns, rows = read_rows(args.simlog)
    arm_rows, started = colony_tables(columns, rows)
    if not arm_rows:
        raise SystemExit("no colony rows in the simulation log")

    print_population(arm_rows)
    print_behaviour(arm_rows, started)
    print_world(columns, rows)

    if args.descriptors:
        names, arms = load_descriptors(args.descriptors, args.include_wild, args.min_age)
        if not arms:
            print("\nno usable descriptor rows")
        else:
            print_clusters(names, arms, max(args.k_max, 2), args.max_rows, args.seed)
    else:
        print("\n(no descriptor file given: clustering skipped)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
