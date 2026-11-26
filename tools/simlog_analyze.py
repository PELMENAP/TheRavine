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


def print_behaviour(arm_rows, started, rows_world=None):
    print()
    print("== per arm: births, deaths, action-mix entropy, decision source, speech ==")
    header = f"{'arm':<16}{'born':>8}{'died':>8}{'lifespan_s':>12}{'act_H_bits':>12}{'brain_sh':>10}{'speech_mi':>11}{'listen_shift':>14}{'h_sat':>11}{'pre_dist':>11}"
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
        sat = nanmean([num(r.get(f"h_saturated_{arm}")) for r in rows_world]) if rows_world is not None else math.nan
        dist = nanmean([num(r.get("predisposition_dist")) for r in rows])
        print(f"{arm:<16}{born:>8.0f}{died:>8.0f}{fmt(lifespan, 1):>12}{fmt(entropy):>12}{fmt(brain):>10}{fmt(mi):>11}{fmt(shift):>14}{fmt(sat):>11}{fmt(dist):>11}")


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

    pre_names = sorted(c for c in columns if c.startswith("predisposition_"))
    by_arm = {}
    pre_by_arm = {}
    for row in rows:
        if row["caste"] == "wild" and not include_wild:
            continue
        if num(row["age_s"]) < min_age:
            continue
        values = [num(row[n]) for n in names]
        pre = [num(row[n]) for n in pre_names]
        if any(math.isnan(v) for v in values) or any(math.isnan(v) for v in pre):
            continue
        for i, n in enumerate(names):
            if n in LOG1P_COLUMNS:
                values[i] = math.log1p(max(values[i], 0.0))
        arm = row["arm"] or CONTROL_ARM
        by_arm.setdefault(arm, []).append(values)
        pre_by_arm.setdefault(arm, []).append(pre)
    arms = {arm: np.array(v, dtype=float) for arm, v in by_arm.items() if v}
    pres = {arm: np.array(pre_by_arm[arm], dtype=float) for arm in arms}
    return names, arms, pre_names, pres


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


def correlation_ratio(values, labels, k):
    total = ((values - values.mean()) ** 2).sum()
    if total <= 1e-12:
        return math.nan
    between = 0.0
    for c in range(k):
        members = values[labels == c]
        if len(members):
            between += len(members) * (members.mean() - values.mean()) ** 2
    return float(between / total)


def print_predisposition(names, raw, pre_raw, pre_names, labels, k, offsets_by_arm):
    if not pre_names:
        print("(no predisposition columns in the descriptor file)")
        return
    print(f"== inherited predisposition vs behaviour (shared clusters k={k}) ==")
    print("correlation ratio eta^2 of each predisposition dimension on the cluster label (0 = unrelated, 1 = fully determined):")
    for j, name in enumerate(pre_names):
        line = f"  {name}: all arms eta2={fmt(correlation_ratio(pre_raw[:, j], labels, k))}"
        for arm, (lo, hi) in offsets_by_arm.items():
            line += f" {arm}={fmt(correlation_ratio(pre_raw[lo:hi, j], labels[lo:hi], k))}"
        print(line)
    print("strongest Pearson correlation of each dimension with a behavioural feature (pooled rows):")
    for j, name in enumerate(pre_names):
        best, best_r = None, 0.0
        for i, feature in enumerate(names):
            if raw[:, i].std() < 1e-9 or pre_raw[:, j].std() < 1e-9:
                continue
            r = float(np.corrcoef(raw[:, i], pre_raw[:, j])[0, 1])
            if abs(r) > abs(best_r):
                best, best_r = feature, r
        print(f"  {name}: {best if best else '-'} r={fmt(best_r)}")


def print_clusters(names, arms, k_max, max_rows, seed, pre_names=(), pres=None):
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

    ordered = sorted(sample, key=arm_sort_key)
    raw = np.vstack([arms[a][picked[a]] for a in ordered])
    if pres is not None and pre_names:
        pre_raw = np.vstack([pres[a][picked[a]] for a in ordered])
        offsets, at = {}, 0
        for a in ordered:
            offsets[a] = (at, at + len(sample[a]))
            at += len(sample[a])
        print()
        print_predisposition(names, raw, pre_raw, list(pre_names), labels, best_k, offsets)
        print()
    print("== cluster centres (original units: shares, log1p counts, rates), pooled over the sampled rows ==")
    for c in range(best_k):
        members = raw[labels == c]
        if not len(members):
            continue
        centre = members.mean(axis=0)
        top = np.argsort(-np.abs((centre - mean) / std))[:4]
        print(f"c{c} n={len(members)}: " + ", ".join(f"{names[i]}={centre[i]:.2f}" for i in top))


def series_by_time(rows, column, scope="world", extra=None):
    out = {}
    for r in rows:
        if r["scope"] != scope:
            continue
        if extra is not None and not extra(r):
            continue
        t = num(r["time_s"])
        v = num(r.get(column))
        if not math.isnan(v):
            out[t] = out.get(t, []) + [v]
    times = sorted(out)
    return np.array(times), np.array([float(np.mean(out[t])) for t in times])


def lagged_corr(a, b, max_lag):
    result = {}
    n = min(len(a), len(b))
    for lag in range(-max_lag, max_lag + 1):
        if lag >= 0:
            x, y = a[:n - lag], b[lag:n]
        else:
            x, y = a[-lag:n], b[:n + lag]
        ok = ~(np.isnan(x) | np.isnan(y))
        if ok.sum() < 5 or x[ok].std() < 1e-12 or y[ok].std() < 1e-12:
            result[lag] = math.nan
        else:
            result[lag] = float(np.corrcoef(x[ok], y[ok])[0, 1])
    return result


def print_lagged(title, left, right, corr):
    valid = {k: v for k, v in corr.items() if not math.isnan(v)}
    print(f"== {title} ==")
    print(f"corr({left}[t], {right}[t+lag]); positive lag: {left} leads")
    if not valid:
        print("  not enough data")
        return
    print("  " + " ".join(f"{lag:+d}:{fmt(corr[lag], 2)}" for lag in sorted(corr)))
    best = max(valid, key=lambda k: abs(valid[k]))
    print(f"  strongest: lag {best:+d} r={fmt(valid[best])}")


def slope_per_ks(times, values):
    ok = ~np.isnan(values)
    if ok.sum() < 3:
        return math.nan
    return float(np.polyfit(times[ok], values[ok], 1)[0] * 1000.0)


def print_predator_prey(columns, rows, max_lag):
    needed = ["is_wild", "strike_success", "escape_rate", "gene_speed_mean", "gene_detect_mean"]
    if any(c not in columns for c in needed):
        print("\n(no predator-prey columns in the simulation log)")
        return
    print()
    print("== predator-prey ==")
    wild = lambda r: r.get("is_wild") == "1"
    civil = lambda r: r.get("is_wild") == "0"
    t, success = series_by_time(rows, "strike_success", "colony", wild)
    _, escape = series_by_time(rows, "escape_rate", "colony", wild)
    if len(t) == 0:
        print("no predator colony rows")
        return

    killed = {}
    alive = {}
    for r in rows:
        if r["scope"] != "colony" or not civil(r):
            continue
        tt = num(r["time_s"])
        killed[tt] = killed.get(tt, 0.0) + num(r["deaths_killed"])
        alive[tt] = alive.get(tt, 0.0) + num(r["alive"])
    prey_killed = np.array([killed.get(x, math.nan) / alive[x] if alive.get(x, 0) > 0 else math.nan for x in t])

    print(f"predator strike success mean={fmt(nanmean(success))} escape rate mean={fmt(nanmean(escape))} prey killed per capita per window mean={fmt(nanmean(prey_killed), 4)}")
    print_lagged("predator strike success vs prey escape rate", "success", "escape", lagged_corr(success, escape, max_lag))
    print_lagged("predator strike success vs prey killed per capita", "success", "killed", lagged_corr(success, prey_killed, max_lag))

    print("== trait means over time (slope per 1000 s; arms race: both sides rising) ==")
    for column, label in (("gene_speed_mean", "speed gene"), ("gene_detect_mean", "detection gene")):
        tp, pred = series_by_time(rows, column, "colony", wild)
        weighted = {}
        for r in rows:
            if r["scope"] != "colony" or not civil(r):
                continue
            v, w = num(r.get(column)), num(r["alive"])
            if math.isnan(v) or math.isnan(w):
                continue
            tt = num(r["time_s"])
            a, b = weighted.get(tt, (0.0, 0.0))
            weighted[tt] = (a + v * w, b + w)
        tq = np.array(sorted(weighted))
        prey = np.array([weighted[x][0] / weighted[x][1] for x in tq if weighted[x][1] > 0])
        tq = np.array([x for x in tq if weighted[x][1] > 0])
        sp, sq = slope_per_ks(tp, pred), slope_per_ks(tq, prey)
        verdict = "both rising" if sp > 0 and sq > 0 else ("both falling" if sp < 0 and sq < 0 else "diverging")
        print(f"  {label}: predators {fmt(nanmean(pred), 4)} slope {fmt(sp, 4)}; prey {fmt(nanmean(prey), 4)} slope {fmt(sq, 4)}; {verdict}")


def print_virus(columns, rows, max_lag):
    needed = ["virus_dominant_prevalence", "virus_immune_dominant_share", "virus_dominant_strain", "virus_strains"]
    if any(c not in columns for c in needed):
        print("\n(no virus columns in the simulation log)")
        return
    print()
    print("== virus-immune ==")
    world = [r for r in rows if r["scope"] == "world"]
    t = np.array([num(r["time_s"]) for r in world])
    prevalence = np.array([num(r["virus_dominant_prevalence"]) for r in world])
    immune = np.array([num(r["virus_immune_dominant_share"]) for r in world])
    strains = np.array([num(r["virus_strains"]) for r in world])
    fresh = np.array([num(r["virus_strains_new"]) for r in world])
    lost = np.array([num(r["virus_strains_lost"]) for r in world])
    dominant = [r["virus_dominant_strain"] for r in world]
    print(f"strains alive mean={fmt(nanmean(strains), 2)} lineages mean={fmt(nanmean([num(r['virus_lineages']) for r in world]), 2)} "
          f"dominant prevalence mean={fmt(nanmean(prevalence))} immune to dominant mean={fmt(nanmean(immune))} "
          f"segment integrity mean={fmt(nanmean([num(r['virus_mean_integrity']) for r in world]))}")
    print(f"infections={fmt(sum(num(r['virus_infections']) for r in world if not math.isnan(num(r['virus_infections']))), 0)} "
          f"recoveries={fmt(sum(num(r['virus_recoveries']) for r in world if not math.isnan(num(r['virus_recoveries']))), 0)} "
          f"blocked by immunity={fmt(sum(num(r['virus_infections_blocked']) for r in world if not math.isnan(num(r['virus_infections_blocked']))), 0)}")
    print_lagged("immunity to the dominant strain vs its prevalence", "immune", "prevalence", lagged_corr(immune, prevalence, max_lag))

    present = [d for d in dominant if d not in ("", "0")]
    changes = sum(1 for a, b in zip(dominant, dominant[1:]) if a != b and a not in ("", "0") and b not in ("", "0"))
    spells, run = [], 0
    for i, d in enumerate(dominant):
        if d in ("", "0"):
            if run:
                spells.append(run)
            run = 0
            continue
        if i > 0 and dominant[i - 1] == d:
            run += 1
        else:
            if run:
                spells.append(run)
            run = 1
    if run:
        spells.append(run)
    turnover = changes / max(len(present) - 1, 1)
    churn = nanmean(np.where(strains > 0, (fresh + lost) / np.maximum(strains, 1), math.nan))
    print(f"strain turnover: dominant strain changed in {fmt(turnover)} of consecutive samples ({changes} changes), "
          f"mean dominant tenure {fmt(float(np.mean(spells)) if spells else math.nan, 2)} samples, mean (new+lost)/alive per sample {fmt(churn)}")


def print_recovery(columns, rows, arm_rows, band, pre_window, hold):
    if "perturbation_active" not in columns:
        print("\n(no perturbation column in the simulation log)")
        return
    world = [r for r in rows if r["scope"] == "world"]
    t = np.array([num(r["time_s"]) for r in world])
    active = np.array([num(r["perturbation_active"]) for r in world])
    windows, start = [], None
    for i, a in enumerate(active):
        if a > 0 and start is None:
            start = i
        if (a <= 0 or i == len(active) - 1) and start is not None:
            end = i if a <= 0 else i + 1
            windows.append((start, end))
            start = None
    print()
    print("== environmental perturbation and recovery ==")
    if not windows:
        print("no perturbation window in the log")
        return

    def recovery(times, values, s, e):
        pre = values[max(0, s - pre_window):s]
        pre = pre[~np.isnan(pre)]
        if not len(pre):
            return math.nan, math.nan, "no pre-shock samples"
        base = pre.mean()
        tol = band * abs(base)
        dev = np.abs(values[s:] - base)
        depth = float(np.nanmax(np.abs(values[s:e] - base)) / abs(base)) if e > s and abs(base) > 1e-12 else math.nan
        i = e
        while i < len(values):
            if all(dev[j - s] <= tol for j in range(i, min(i + hold, len(values)))) and i + hold <= len(values):
                return float(times[i] - times[e - 1] if e > 0 else times[i]), depth, "recovered"
            i += 1
        return math.nan, depth, "not recovered"

    for s, e in windows:
        print(f"shock {t[s]:.0f}s–{t[min(e, len(t) - 1)]:.0f}s (band ±{band * 100:.0f}% of the mean of {pre_window} samples before; hold {hold} samples)")
        wt, wf = series_by_time(rows, "avg_fitness", "world")
        if len(wt):
            ws = int(np.searchsorted(wt, t[s]))
            we = int(np.searchsorted(wt, t[min(e, len(t) - 1)]))
            rt, depth, status = recovery(wt, wf, ws, max(we, ws + 1))
            print(f"  world mean fitness: {status}, recovery {fmt(rt, 1)} s, max deviation {fmt(depth)}")

        per_arm = {}
        for arm in sorted(arm_rows, key=arm_sort_key):
            colonies = {}
            for r in arm_rows[arm]:
                colonies.setdefault(r["colony_id"], []).append((num(r["time_s"]), num(r["alive"])))
            for cid, points in colonies.items():
                points.sort()
                ct = np.array([p[0] for p in points])
                cv = np.array([p[1] for p in points])
                cs = int(np.searchsorted(ct, t[s]))
                ce = int(np.searchsorted(ct, t[min(e, len(t) - 1)]))
                rt, depth, status = recovery(ct, cv, cs, max(ce, cs + 1))
                per_arm.setdefault(arm, []).append((rt, depth, status))
        print("  population recovery per arm (colonies; recovered; median recovery s; median max deviation):")
        for arm in sorted(per_arm, key=arm_sort_key):
            items = per_arm[arm]
            done = [x[0] for x in items if x[2] == "recovered"]
            depth = [x[1] for x in items if not math.isnan(x[1])]
            print(f"    {arm:<22}n={len(items)} recovered={len(done)} median={fmt(float(np.median(done)) if done else math.nan, 1)} deviation={fmt(float(np.median(depth)) if depth else math.nan)}")


def read_life(path):
    columns, rows = read_rows(path)
    need(columns, ["event", "time_s", "agent_id", "parent_id", "colony_id"], path)
    genes = [c for c in columns if c not in ("event", "time_s", "agent_id", "parent_id", "mate_id", "colony_id", "cause", "age_s")]
    births, deaths = [], {}
    for r in rows:
        if r["event"] == "birth":
            values = [num(r[g]) for g in genes]
            if any(math.isnan(v) for v in values):
                continue
            parent = int(num(r["parent_id"])) if r["parent_id"] != "" else -1
            births.append((num(r["time_s"]), int(num(r["agent_id"])), parent, r["colony_id"], values))
        elif r["event"] == "death":
            deaths[int(num(r["agent_id"]))] = num(r["time_s"])
    return genes, births, deaths


def activity_curves(birth_t, death_t, colony, bins, parent_row, grid, rng, shadow, mutation, n_bins, threshold):
    n, g = bins.shape
    order = np.argsort(birth_t, kind="stable")
    events = [(birth_t[i], 1, i) for i in range(n)] + [(death_t[i], 0, i) for i in range(n) if np.isfinite(death_t[i])]
    events.sort(key=lambda e: (e[0], e[1]))
    state = bins.copy()
    alive_lists, position, alive = {}, {}, np.zeros(n, bool)
    activity = np.zeros((g, n_bins))
    abundance = np.zeros((g, n_bins))
    cumulative_new = np.zeros((g, n_bins), bool)
    ptr = 0
    out = []
    for t in grid:
        while ptr < len(events) and events[ptr][0] <= t:
            _, kind, i = events[ptr]
            ptr += 1
            lst = alive_lists.setdefault(colony[i], [])
            if kind == 1:
                if shadow and parent_row[i] >= 0 and lst:
                    p = lst[rng.integers(len(lst))]
                    child = state[p].copy()
                    flip = rng.random(g) < mutation
                    child[flip] = rng.integers(n_bins, size=int(flip.sum()))
                    state[i] = child
                position[i] = len(lst)
                lst.append(i)
                alive[i] = True
            elif alive[i]:
                j = position.pop(i)
                last = lst[-1]
                lst[j] = last
                position[last] = j
                lst.pop()
                if last == i:
                    position.pop(i, None)
                alive[i] = False
        present = np.zeros((g, n_bins), bool)
        share = np.zeros((g, n_bins))
        if alive.any():
            rows_alive = state[alive]
            present[np.arange(g)[None, :], rows_alive] = True
            np.add.at(share, (np.broadcast_to(np.arange(g)[None, :], rows_alive.shape), rows_alive), 1.0)
            share /= len(rows_alive)
        activity += present
        abundance += share
        crossed = present & (activity > threshold) & ~cumulative_new
        cumulative_new |= crossed
        d = int(present.sum())
        mean_a = float(activity[present].sum() / d) if d else 0.0
        mean_w = float(abundance[present].sum() / d) if d else 0.0
        out.append((float(t), d, mean_a, int(crossed.sum()), int(cumulative_new.sum()), mean_w))
    return out


GENE_NOTE = "Founder genes usually cover every bin, so new activity saturates; read abundance and diversity."


def print_curves(real, shadow, note=GENE_NOTE):
    print(f"{'time_s':>10}{'D real':>9}{'D shadow':>10}{'meanA real':>12}{'meanA shad':>12}{'new real':>10}{'new shad':>10}{'cumNew real':>13}{'cumNew shad':>13}{'abund real':>12}{'abund shad':>12}")
    picks = np.linspace(0, len(real) - 1, num=min(10, len(real))).astype(int)
    for i in picks:
        r, s = real[i], shadow[i]
        print(f"{r[0]:>10.0f}{r[1]:>9d}{s[1]:>10d}{r[2]:>12.2f}{s[2]:>12.2f}{r[3]:>10d}{s[3]:>10d}{r[4]:>13d}{s[4]:>13d}{r[5]:>12.2f}{s[5]:>12.2f}")
    r_new, s_new = real[-1][4], shadow[-1][4]
    r_mean = float(np.mean([x[2] for x in real]))
    s_mean = float(np.mean([x[2] for x in shadow]))
    r_div = float(np.mean([x[1] for x in real]))
    s_div = float(np.mean([x[1] for x in shadow]))
    r_ab = float(np.mean([x[5] for x in real]))
    s_ab = float(np.mean([x[5] for x in shadow]))
    print(f"abundance-weighted mean activity (cumulative share of living agents carrying the component): real={fmt(r_ab, 2)} shadow={fmt(s_ab, 2)}")
    print(f"totals: new activity real={r_new} shadow={s_new} (ratio {fmt(r_new / s_new if s_new else math.nan, 2)}); "
          f"mean cumulative activity real={fmt(r_mean, 2)} shadow={fmt(s_mean, 2)}; mean diversity real={fmt(r_div, 1)} shadow={fmt(s_div, 1)}")
    print("real above shadow in new activity, mean cumulative activity and abundance-weighted activity, with lower diversity, indicates selected components; equal values indicate drift. " + note)


def print_activity(path, n_bins, steps, threshold, seed):
    genes, births, deaths = read_life(path)
    print()
    print("== Bedau-Packard evolutionary activity (real against neutral shadow) ==")
    if len(births) < 10 or not genes:
        print("not enough birth rows in the life log")
        return
    births.sort(key=lambda b: b[0])
    ids = {b[1]: i for i, b in enumerate(births)}
    n = len(births)
    birth_t = np.array([b[0] for b in births])
    death_t = np.array([deaths.get(b[1], math.inf) for b in births])
    colony = [b[3] for b in births]
    values = np.array([b[4] for b in births])
    lo, hi = values.min(axis=0), values.max(axis=0)
    span = np.where(hi - lo < 1e-12, 1.0, hi - lo)
    bins = np.minimum(((values - lo) / span * n_bins).astype(int), n_bins - 1)
    parent_row = np.array([ids.get(b[2], -1) for b in births])

    pairs = [(i, parent_row[i]) for i in range(n) if parent_row[i] >= 0]
    if pairs:
        child = np.array([p[0] for p in pairs])
        par = np.array([p[1] for p in pairs])
        mutation = np.clip((bins[child] != bins[par]).mean(axis=0), 0.0, 1.0)
    else:
        mutation = np.full(len(genes), 0.05)

    t0, t1 = float(birth_t[0]), float(max(birth_t[-1], np.nanmax(np.where(np.isfinite(death_t), death_t, 0.0))))
    grid = np.linspace(t0, t1, steps + 1)[1:]
    print(f"{n} births, {len(deaths)} deaths, {len(genes)} genes x {n_bins} bins = {len(genes) * n_bins} components, {steps} steps, activity threshold {threshold} steps")
    print(f"mean per-gene bin change between child and parent (shadow mutation rate): {fmt(float(mutation.mean()), 3)}")
    rng = np.random.default_rng(seed)
    real = activity_curves(birth_t, death_t, colony, bins, parent_row, grid, rng, False, mutation, n_bins, threshold)
    shadow = activity_curves(birth_t, death_t, colony, bins, parent_row, grid, np.random.default_rng(seed + 1), True, mutation, n_bins, threshold)

    print_curves(real, shadow)


GOALS = ["survive", "hunt", "forage", "social"]
BEHAVIOUR_MOTIF_SPACE = len(ACTIONS) ** 3 * 16
SPEECH_CONTEXTS = 64
SPEECH_VOCAB = 16


def js_divergence(p, q):
    p = np.asarray(p, dtype=float)
    q = np.asarray(q, dtype=float)
    sp, sq = p.sum(), q.sum()
    if sp <= 0 or sq <= 0:
        return math.nan
    p = p / sp
    q = q / sq
    m = 0.5 * (p + q)

    def kl(a):
        mask = a > 0
        return float((a[mask] * np.log2(a[mask] / m[mask])).sum())

    return 0.5 * kl(p) + 0.5 * kl(q)


def conditional_js(a, b):
    ra, rb = a.sum(axis=1), b.sum(axis=1)
    na, nb = ra.sum(), rb.sum()
    if na <= 0 or nb <= 0:
        return math.nan
    mask = (ra > 0) & (rb > 0)
    if not mask.any():
        return math.nan
    rows = np.nonzero(mask)[0]
    weights = 0.5 * (ra[rows] / na + rb[rows] / nb)
    values = np.array([js_divergence(a[c], b[c]) for c in rows])
    return float((weights * values).sum() / weights.sum())


def read_speech_tables(path):
    columns, rows = read_rows(path)
    need(columns, ["time_s", "colony_id", "cells"], path)
    tables = {}
    top_ctx, top_tok = SPEECH_CONTEXTS - 1, SPEECH_VOCAB - 1
    parsed = []
    for row in rows:
        cells = []
        for item in row["cells"].split(";"):
            if not item:
                continue
            ctx, tok, count = item.split(":")
            cells.append((int(ctx), int(tok), int(count)))
            top_ctx = max(top_ctx, int(ctx))
            top_tok = max(top_tok, int(tok))
        parsed.append((num(row["time_s"]), int(row["colony_id"]), cells))
    for t, cid, cells in parsed:
        table = tables.setdefault((round(t, 3), cid), np.zeros((top_ctx + 1, top_tok + 1)))
        for ctx, tok, count in cells:
            table[ctx, tok] += count
    return tables


def colony_series(columns, rows, block, speech_tables):
    need(columns, ["time_s", "scope", "colony_id", "arm"], "simulation log")
    colony_rows = [r for r in rows if r["scope"] == "colony" and r.get("is_wild", "0") != "1"]
    times = sorted({round(num(r["time_s"]), 3) for r in colony_rows})
    index = {t: i for i, t in enumerate(times)}
    blocks = (len(times) + block - 1) // block
    arms = {}
    actions = {}
    goals = {}
    goal_columns = [f"goal_{g}_started" for g in GOALS]
    has_goals = all(c in columns for c in goal_columns)
    for row in colony_rows:
        cid = int(row["colony_id"])
        arms[cid] = row["arm"] or CONTROL_ARM
        b = index[round(num(row["time_s"]), 3)] // block
        vec = actions.setdefault(cid, np.zeros((blocks, len(ACTIONS))))
        for i, a in enumerate(ACTIONS):
            value = num(row[f"{a}_started"])
            if not math.isnan(value):
                vec[b, i] += value
        if has_goals:
            gvec = goals.setdefault(cid, np.zeros((blocks, len(GOALS))))
            for i, name in enumerate(goal_columns):
                value = num(row[name])
                if not math.isnan(value):
                    gvec[b, i] += value
    speech = {}
    if speech_tables:
        top_ctx = max(t.shape[0] for t in speech_tables.values())
        top_tok = max(t.shape[1] for t in speech_tables.values())
        for (t, cid), table in speech_tables.items():
            if t not in index or cid not in arms:
                continue
            vec = speech.setdefault(cid, np.zeros((blocks, top_ctx, top_tok)))
            vec[index[t] // block, :table.shape[0], :table.shape[1]] += table
    return arms, actions, goals, speech


def divergence_matrix(series, ids, dist, total, min_count):
    blocks = next(iter(series.values())).shape[0]
    matrix = np.full((len(ids), len(ids)), math.nan)
    for i in range(len(ids)):
        for j in range(i + 1, len(ids)):
            values = []
            for t in range(blocks):
                a, b = series[ids[i]][t], series[ids[j]][t]
                if total(a) < min_count or total(b) < min_count:
                    continue
                values.append(dist(a, b))
            matrix[i, j] = matrix[j, i] = nanmean(values)
    return matrix


def self_lag(series, cid, dist, total, min_count):
    values = []
    for t in range(1, series[cid].shape[0]):
        a, b = series[cid][t], series[cid][t - 1]
        if total(a) < min_count or total(b) < min_count:
            continue
        values.append(dist(a, b))
    return nanmean(values)


def print_divergence(title, series, arms, dist, total, min_count):
    ids = sorted(series)
    print()
    print(f"== inter-colony divergence: {title} (Jensen-Shannon, bits) ==")
    if len(ids) < 2:
        print("fewer than two colonies with data")
        return
    matrix = divergence_matrix(series, ids, dist, total, min_count)
    labels = [f"{cid}{'*' if arms.get(cid) == NULL_ARM else ''}" for cid in ids]
    print("      " + "".join(f"{label:>7}" for label in labels))
    for i, label in enumerate(labels):
        print(f"{label:>6}" + "".join(f"{fmt(matrix[i, j], 3):>7}" for j in range(len(ids))))
    print("(* = null_policy colony)")

    real = [i for i, cid in enumerate(ids) if arms.get(cid) != NULL_ARM]
    null = [i for i, cid in enumerate(ids) if arms.get(cid) == NULL_ARM]
    pair_values = [matrix[i, j] for a, i in enumerate(real) for j in real[a + 1:]]
    pair_mean = nanmean(pair_values)
    lag_mean = nanmean([self_lag(series, ids[i], dist, total, min_count) for i in real])
    null_values = [matrix[i, j] for i in real for j in null]
    null_mean = nanmean(null_values) if null_values else math.nan
    print(f"mean pair divergence {fmt(pair_mean, 4)}; against itself one block earlier {fmt(lag_mean, 4)}; against null_policy colonies {fmt(null_mean, 4)}")
    ratio_lag = pair_mean / lag_mean if lag_mean and not math.isnan(lag_mean) else math.nan
    ratio_null = pair_mean / null_mean if null_mean and not math.isnan(null_mean) else math.nan
    print(f"ratio pair / self-lag = {fmt(ratio_lag, 2)}; ratio pair / null_policy = {fmt(ratio_null, 2)}"
          + ("" if null else " (no null_policy colony in this run)"))


def print_inter_colony(columns, rows, speech_path, block, min_count):
    tables = read_speech_tables(speech_path) if speech_path else None
    arms, actions, goals, speech = colony_series(columns, rows, max(block, 1), tables)
    if len(actions) < 2:
        print()
        print("== inter-colony divergence: fewer than two civil colonies in the log ==")
        return
    vector_total = lambda v: float(np.sum(v))
    print_divergence("action mix", actions, arms, js_divergence, vector_total, min_count)
    if goals:
        print_divergence("goal mix", goals, arms, js_divergence, vector_total, min_count)
    else:
        print("\n(no goal_<goal>_started columns: goal mix skipped)")
    if speech:
        print_divergence("token given context (context-weighted)", speech, arms, conditional_js, vector_total, min_count)
    elif speech_path:
        print("\n(speech table file has no usable rows)")
    else:
        print("\n(no --speech-tables file: token-given-context divergence skipped)")


def print_brain_distance(path):
    columns, rows = read_rows(path)
    need(columns, ["time_s", "colony_a", "colony_b", "distance"], path)
    by_time = {}
    for row in rows:
        by_time.setdefault(num(row["time_s"]), {})[(int(row["colony_a"]), int(row["colony_b"]))] = num(row["distance"])
    print()
    print("== brain distance (cosine distance of flattened executor weights) ==")
    if not by_time:
        print("no rows")
        return
    times = sorted(by_time)
    means = [nanmean(list(by_time[t].values())) for t in times]
    print(f"{len(times)} samples; mean pair distance first={fmt(means[0], 4)} last={fmt(means[-1], 4)} overall={fmt(nanmean(means), 4)}")
    last = by_time[times[-1]]
    ids = sorted({c for pair in last for c in pair})
    print("      " + "".join(f"{cid:>7}" for cid in ids))
    for a in ids:
        cells = []
        for b in ids:
            key = (a, b) if (a, b) in last else (b, a)
            cells.append(fmt(last[key], 3) if a != b and key in last else "-")
        print(f"{a:>6}" + "".join(f"{c:>7}" for c in cells))



def read_motifs(path):
    columns, rows = read_rows(path)
    need(columns, ["time_s", "agent_id", "colony_id", "motifs"], path)
    totals = {}
    for r in rows:
        agent = int(num(r["agent_id"]))
        table = totals.setdefault(agent, {})
        for item in r["motifs"].split(";"):
            if not item:
                continue
            motif, count = item.split(":")
            table[int(motif)] = table.get(int(motif), 0) + int(count)
    return totals


def motif_activity_curves(birth_t, death_t, colony, sets, parent_row, grid, rng, shadow, rate, pool, threshold):
    n = len(sets)
    events = [(birth_t[i], 1, i) for i in range(n)] + [(death_t[i], 0, i) for i in range(n) if np.isfinite(death_t[i])]
    events.sort(key=lambda e: (e[0], e[1]))
    state = [list(x) for x in sets]
    alive_lists, position, alive = {}, {}, np.zeros(n, bool)
    carriers = {}
    activity, abundance = {}, {}
    cumulative_new = set()
    living = 0
    ptr = 0
    out = []
    for t in grid:
        while ptr < len(events) and events[ptr][0] <= t:
            _, kind, i = events[ptr]
            ptr += 1
            lst = alive_lists.setdefault(colony[i], [])
            if kind == 1:
                if shadow and parent_row[i] >= 0 and lst and len(pool) > 0:
                    p = lst[rng.integers(len(lst))]
                    child = list(state[p])
                    for slot in range(len(child)):
                        if rng.random() < rate:
                            child[slot] = int(pool[rng.integers(len(pool))])
                    state[i] = sorted(set(child))
                position[i] = len(lst)
                lst.append(i)
                alive[i] = True
                living += 1
                for m in state[i]:
                    carriers[m] = carriers.get(m, 0) + 1
            elif alive[i]:
                j = position.pop(i)
                last = lst[-1]
                lst[j] = last
                position[last] = j
                lst.pop()
                if last == i:
                    position.pop(i, None)
                alive[i] = False
                living -= 1
                for m in state[i]:
                    carriers[m] -= 1
        present = [m for m, c in carriers.items() if c > 0]
        crossed = 0
        for m in present:
            activity[m] = activity.get(m, 0) + 1
            abundance[m] = abundance.get(m, 0.0) + carriers[m] / living
            if activity[m] > threshold and m not in cumulative_new:
                cumulative_new.add(m)
                crossed += 1
        d = len(present)
        mean_a = sum(activity[m] for m in present) / d if d else 0.0
        mean_w = sum(abundance[m] for m in present) / d if d else 0.0
        out.append((float(t), d, mean_a, crossed, len(cumulative_new), mean_w))
    return out


def print_motif_activity(motif_path, life_path, steps, threshold, seed, top_k):
    totals = read_motifs(motif_path)
    genes, births, deaths = read_life(life_path)
    print()
    print("== Bedau-Packard evolutionary activity of behavioural motifs (real against neutral shadow) ==")
    if len(births) < 10 or not totals:
        print("not enough birth rows in the life log or no motif rows")
        return
    births.sort(key=lambda b: b[0])
    ids = {b[1]: i for i, b in enumerate(births)}
    n = len(births)
    birth_t = np.array([b[0] for b in births])
    death_t = np.array([deaths.get(b[1], math.inf) for b in births])
    colony = [b[3] for b in births]
    parent_row = np.array([ids.get(b[2], -1) for b in births])

    limit = top_k if top_k > 0 else max((len(t) for t in totals.values()), default=1)
    sets = []
    for b in births:
        table = totals.get(b[1], {})
        top = sorted(table.items(), key=lambda kv: (-kv[1], kv[0]))[:limit]
        sets.append(sorted(m for m, _ in top))
    pool = np.array(sorted({m for s in sets for m in s}), dtype=int)
    if len(pool) == 0:
        print("no motifs for the agents of the life log")
        return

    rates = []
    for i in range(n):
        p = parent_row[i]
        if p >= 0 and sets[i] and sets[p]:
            rates.append(1.0 - len(set(sets[i]) & set(sets[p])) / len(sets[i]))
    rate = float(np.clip(np.mean(rates), 0.0, 1.0)) if rates else 0.05

    t0 = float(birth_t[0])
    t1 = float(max(birth_t[-1], np.nanmax(np.where(np.isfinite(death_t), death_t, 0.0))))
    grid = np.linspace(t0, t1, steps + 1)[1:]
    with_motifs = sum(1 for s in sets if s)
    print(f"{n} births, {with_motifs} agents with motif rows, {len(pool)} distinct motifs of {BEHAVIOUR_MOTIF_SPACE} possible ids, up to {limit} per agent, {steps} steps, activity threshold {threshold} steps")
    print(f"mean share of an agent's motifs absent from its parent's set (shadow re-draw rate): {fmt(rate, 3)}")
    real = motif_activity_curves(birth_t, death_t, colony, sets, parent_row, grid, np.random.default_rng(seed), False, rate, pool, threshold)
    shadow = motif_activity_curves(birth_t, death_t, colony, sets, parent_row, grid, np.random.default_rng(seed + 1), True, rate, pool, threshold)
    print_curves(real, shadow, "An agent's motif set is its most frequent motifs over its lifetime; the shadow copies the set of a random living agent of the colony at each birth with a known parent and re-draws each motif at the observed rate from the motifs seen in the run; founders keep their real sets.")


def main():
    parser = argparse.ArgumentParser(description="Compare experiment arms in a TheRavine simulation log and its behavioural descriptor file")
    parser.add_argument("simlog", help="simulation_log_<stamp>.csv")
    parser.add_argument("descriptors", nargs="?", help="behaviour_descriptors_<stamp>.csv")
    parser.add_argument("--k-max", type=int, default=8)
    parser.add_argument("--max-rows", type=int, default=1500, help="per-arm cap for silhouette (the matrix is rows x rows)")
    parser.add_argument("--min-age", type=float, default=0.0, help="drop descriptor rows of agents younger than this many seconds")
    parser.add_argument("--include-wild", action="store_true")
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--life", help="life_log_<stamp>.csv (births and deaths) for the Bedau-Packard activity")
    parser.add_argument("--bins", type=int, default=5, help="bins per gene for activity components")
    parser.add_argument("--life-steps", type=int, default=100)
    parser.add_argument("--activity-threshold", type=int, default=10, help="steps of presence after which a component counts as adaptive")
    parser.add_argument("--max-lag", type=int, default=6, help="lags (in samples) of the lagged correlations")
    parser.add_argument("--band", type=float, default=0.1, help="recovery band, fraction of the pre-shock mean")
    parser.add_argument("--pre-window", type=int, default=6)
    parser.add_argument("--hold", type=int, default=2)
    parser.add_argument("--motifs", help="behaviour_motifs_<stamp>.csv; with --life adds motif components to the Bedau-Packard activity")
    parser.add_argument("--motif-k", type=int, default=0, help="motifs kept per agent for the activity (0: as many as the file has per row)")
    parser.add_argument("--speech-tables", help="speech_tables_<stamp>.csv (per-colony context x token counts)")
    parser.add_argument("--brain-distance", help="brain_distance_<stamp>.csv (pairwise cosine distance of executor weights)")
    parser.add_argument("--div-block", type=int, default=1, help="samples pooled into one block for the inter-colony divergence (1: the log's own windows)")
    parser.add_argument("--div-min-count", type=float, default=20.0, help="skip a block with fewer events than this in either colony")
    args = parser.parse_args()

    columns, rows = read_rows(args.simlog)
    arm_rows, started = colony_tables(columns, rows)
    if not arm_rows:
        raise SystemExit("no colony rows in the simulation log")

    print_population(arm_rows)
    print_behaviour(arm_rows, started, [r for r in rows if r["scope"] == "world"])
    print_world(columns, rows)
    print_predator_prey(columns, rows, args.max_lag)
    print_virus(columns, rows, args.max_lag)
    print_recovery(columns, rows, arm_rows, args.band, args.pre_window, args.hold)
    print_inter_colony(columns, rows, args.speech_tables, args.div_block, args.div_min_count)
    if args.brain_distance:
        print_brain_distance(args.brain_distance)
    if args.life:
        print_activity(args.life, max(args.bins, 2), max(args.life_steps, 10), args.activity_threshold, args.seed)
        if args.motifs:
            print_motif_activity(args.motifs, args.life, max(args.life_steps, 10), args.activity_threshold, args.seed, args.motif_k)
    elif args.motifs:
        print("\n(--motifs needs --life for births, deaths and parents: motif activity skipped)")

    if args.descriptors:
        names, arms, pre_names, pres = load_descriptors(args.descriptors, args.include_wild, args.min_age)
        if not arms:
            print("\nno usable descriptor rows")
        else:
            print_clusters(names, arms, max(args.k_max, 2), args.max_rows, args.seed, pre_names, pres)
    else:
        print("\n(no descriptor file given: clustering skipped)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
