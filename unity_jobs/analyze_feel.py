"""Analyze the newest telemetry/feel_*.csv into feel metrics.
Usage: python unity_jobs/analyze_feel.py [path]
Metrics: press->hit latency, chop cadence, time-to-max-speed, stop time,
top speed, pickup chain gaps, battery drain per swing.
"""
import csv
import glob
import os
import sys


def main() -> int:
    if len(sys.argv) > 1:
        path = sys.argv[1]
    else:
        files = sorted(glob.glob("telemetry/feel_*.csv"))
        if not files:
            print("no telemetry files")
            return 1
        path = files[-1]

    rows = []
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            try:
                rows.append((r["type"], float(r["t"]),
                             float(r["x"] or 0), float(r["y"] or 0),
                             float(r["speed"]) if r.get("speed") else None,
                             r.get("extra", "")))
            except (ValueError, KeyError):
                continue

    out = [f"file={os.path.basename(path)} rows={len(rows)} "
           f"span={rows[-1][1] - rows[0][1]:.1f}s" if rows else "empty"]

    # press -> hit latency
    lat = []
    presses = [t for ty, t, *_ in rows if ty == "press_lmb"]
    hits = [t for ty, t, *_ in rows if ty == "swing_hit_fired"]
    for p in presses:
        nxt = [h for h in hits if 0 <= h - p < 1.0]
        if nxt:
            lat.append(nxt[0] - p)
    if lat:
        out.append(f"press->hit: n={len(lat)} avg={sum(lat)/len(lat)*1000:.0f}ms "
                   f"min={min(lat)*1000:.0f} max={max(lat)*1000:.0f} (target <=120ms incl anim start)")

    # chop cadence
    if len(hits) > 2:
        gaps = [b - a for a, b in zip(hits, hits[1:]) if b - a < 2.0]
        if gaps:
            out.append(f"chop cadence: avg gap={sum(gaps)/len(gaps):.2f}s (cooldown 0.35)")

    # speed profile
    speeds = [(t, s) for ty, t, x, y, s, e in rows if ty == "pos" and s is not None]
    if speeds:
        vals = sorted(s for _, s in speeds if s < 20)
        top = vals[int(len(vals) * 0.95)] if vals else 0
        out.append(f"top speed (p95)={top:.2f} u/s (config 3.6)")
        # time-to-max: longest run below->above 90% top
        thresh = top * 0.9
        t_start, ramps, stops = None, [], []
        prev_s, prev_t = 0.0, None
        for t, s in speeds:
            if prev_t is not None:
                if prev_s < 0.2 and s >= 0.2:
                    t_start = t
                if t_start is not None and s >= thresh:
                    ramps.append(t - t_start)
                    t_start = None
                if prev_s >= thresh and s < 0.2:
                    stops.append(t - prev_t)
            prev_s, prev_t = s, t
        if ramps:
            out.append(f"time-to-max: avg={sum(ramps)/len(ramps):.2f}s n={len(ramps)} (target <=0.15)")
        if stops:
            out.append(f"stop time: avg={sum(stops)/len(stops):.2f}s n={len(stops)} (target <=0.1)")

    # pickups
    picks = [t for ty, t, *_ in rows if ty == "pickup"]
    if len(picks) > 1:
        gaps = [b - a for a, b in zip(picks, picks[1:])]
        chained = sum(1 for g in gaps if g <= 1.5)
        out.append(f"pickups={len(picks)} chained(<=1.5s)={chained} (combo pitch should ramp)")

    # battery
    batt = [(t, int(e)) for ty, t, x, y, s, e in rows if ty == "battery" and e.isdigit()]
    if len(batt) > 1:
        out.append(f"battery {batt[0][1]} -> {batt[-1][1]} over {batt[-1][0]-batt[0][0]:.0f}s, "
                   f"swings={len(hits)}")

    print("\n".join(out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
