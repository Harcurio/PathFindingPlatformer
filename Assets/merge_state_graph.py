"""
merge_state_graph.py
--------------------
Post-processes a reachability_state_graph.json exported by PlatformerReachabilityPlanner.

Clustering strategy:
  Nodes are sorted by (pos.x, pos.y). For each node i, only nodes j in a
  forward sliding window where pos.x[j] - pos.x[i] <= pos_threshold are
  checked. Within that window, pos.y distance is also checked as a fast
  early-exit. This turns the naive O(n^2) into roughly O(n * w) where w
  is the average window size — typically very small for spatial game data.

  Two nodes are merged when ALL of:
    - |pos.x - pos.x| <= pos_threshold  (guaranteed by window)
    - |pos.y - pos.y| <= pos_threshold
    - velocity distance  <= vel_threshold
    - grounded flag matches
    - airJumpsUsed matches
    - |qt_a - qt_b|      <= qt_threshold

Usage:
    python merge_state_graph.py --input reachability_state_graph.json --output merged.json
    python merge_state_graph.py --input reachability_state_graph.json --output merged.json \\
        --pos-threshold 0.15 --vel-threshold 0.75 --qt-threshold 1
"""

import argparse
import json
import math
import sys
import time
from collections import defaultdict


# ---------------------------------------------------------------------------
#  Progress bar (no dependencies)
# ---------------------------------------------------------------------------

class ProgressBar:
    def __init__(self, total: int, label: str = "", width: int = 40):
        self.total   = max(total, 1)
        self.label   = label
        self.width   = width
        self.current = 0
        self.start   = time.time()
        self._draw(0)

    def update(self, n: int = 1):
        self.current = min(self.current + n, self.total)
        self._draw(self.current)

    def finish(self):
        self.current = self.total
        self._draw(self.total, done=True)
        print()

    def _draw(self, current: int, done: bool = False):
        frac    = current / self.total
        filled  = int(self.width * frac)
        bar     = "█" * filled + "░" * (self.width - filled)
        pct     = frac * 100
        elapsed = time.time() - self.start

        if current > 0 and not done:
            eta     = elapsed / current * (self.total - current)
            eta_str = f"  ETA {eta:5.1f}s"
        else:
            eta_str = f"  {elapsed:.1f}s" if done else ""

        sys.stdout.write(f"\r  {self.label}  [{bar}] {pct:5.1f}%{eta_str}")
        sys.stdout.flush()


# ---------------------------------------------------------------------------
#  Union-Find
# ---------------------------------------------------------------------------

class UnionFind:
    def __init__(self, n: int):
        self.parent = list(range(n))
        self.rank   = [0] * n

    def find(self, x: int) -> int:
        while self.parent[x] != x:
            self.parent[x] = self.parent[self.parent[x]]
            x = self.parent[x]
        return x

    def union(self, a: int, b: int) -> bool:
        ra, rb = self.find(a), self.find(b)
        if ra == rb:
            return False
        if self.rank[ra] < self.rank[rb]:
            ra, rb = rb, ra
        self.parent[rb] = ra
        if self.rank[ra] == self.rank[rb]:
            self.rank[ra] += 1
        return True


# ---------------------------------------------------------------------------
#  Cluster — sorted sliding window
# ---------------------------------------------------------------------------

def build_clusters(nodes: list, pos_thr: float, vel_thr: float, qt_thr: int) -> UnionFind:
    n  = len(nodes)
    uf = UnionFind(n)

    # Sort by (pos.x, pos.y) so we can use a 1D sliding window on x,
    # with a cheap y early-exit inside the window.
    order = sorted(range(n), key=lambda i: (nodes[i]["pos"]["x"], nodes[i]["pos"]["y"]))

    bar       = ProgressBar(n, label="Clustering")
    max_win   = 0   # track largest window seen (diagnostic)
    total_cmp = 0   # total comparisons actually made

    for ii in range(n):
        i = order[ii]
        a = nodes[i]
        ax, ay = a["pos"]["x"], a["pos"]["y"]

        # Advance window: only nodes where x difference <= pos_thr are candidates.
        jj = ii + 1
        while jj < n:
            j = order[jj]
            b = nodes[j]
            bx = b["pos"]["x"]

            if bx - ax > pos_thr:
                break   # everything further right is also too far — stop

            # Fast scalar checks before the sqrt.
            if abs(b["pos"]["y"] - ay)          > pos_thr: jj += 1; continue
            if a["grounded"]     != b["grounded"]:         jj += 1; continue
            if a["airJumpsUsed"] != b["airJumpsUsed"]:     jj += 1; continue
            if abs(a["qt"]       -  b["qt"])     > qt_thr: jj += 1; continue

            # Velocity distance (still cheap — two subtractions + sqrt).
            dvx = a["vel"]["x"] - b["vel"]["x"]
            dvy = a["vel"]["y"] - b["vel"]["y"]
            if math.sqrt(dvx * dvx + dvy * dvy) > vel_thr: jj += 1; continue

            # Full position distance (x already within thr, just confirm 2D).
            dx = bx - ax
            dy = b["pos"]["y"] - ay
            if math.sqrt(dx * dx + dy * dy) <= pos_thr:
                uf.union(i, j)

            total_cmp += 1
            jj += 1

        win = jj - ii - 1
        if win > max_win:
            max_win = win

        bar.update()

    bar.finish()
    print(f"  window: max={max_win}  comparisons={total_cmp:,}  "
          f"(vs {n*(n-1)//2:,} naive)")
    return uf


# ---------------------------------------------------------------------------
#  Merge cluster members into one representative node
# ---------------------------------------------------------------------------

def merge_cluster(members: list) -> dict:
    anchor = min(members, key=lambda n: n["minTime"])

    avg_pos = {
        "x": sum(n["pos"]["x"] for n in members) / len(members),
        "y": sum(n["pos"]["y"] for n in members) / len(members),
    }
    avg_vel = {
        "x": sum(n["vel"]["x"] for n in members) / len(members),
        "y": sum(n["vel"]["y"] for n in members) / len(members),
    }

    return {
        "id":           -1,
        "pos":          avg_pos,
        "vel":          avg_vel,
        "grounded":     anchor["grounded"],
        "airJumpsUsed": anchor["airJumpsUsed"],
        "move":         anchor["move"],
        "facing":       anchor["facing"],
        "qt":           anchor["qt"],
        "minTime":      anchor["minTime"],
        "aliveMask":    anchor["aliveMask"],
        "visits":       sum(n["visits"] for n in members),
        "mergedCount":  len(members),
    }


# ---------------------------------------------------------------------------
#  Remap and combine edges
# ---------------------------------------------------------------------------

def merge_edges(edges: list, old_to_new: dict) -> list:
    combined = {}

    bar = ProgressBar(len(edges), label="Edges    ")
    for e in edges:
        new_from = old_to_new[e["fromId"]]
        new_to   = old_to_new[e["toId"]]

        if new_from != new_to:
            key = (new_from, new_to, e["action"])
            if key not in combined:
                combined[key] = {
                    "fromId":      new_from,
                    "toId":        new_to,
                    "action":      e["action"],
                    "costSeconds": e["costSeconds"],
                    "count":       e["count"],
                    "killedMask":  e["killedMask"],
                }
            else:
                ex = combined[key]
                ex["count"]       += e["count"]
                ex["costSeconds"]  = min(ex["costSeconds"], e["costSeconds"])
                ex["killedMask"]   = ex["killedMask"] | e["killedMask"]

        bar.update()

    bar.finish()
    return list(combined.values())


# ---------------------------------------------------------------------------
#  Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Merge near-duplicate nodes in a reachability state graph.")
    parser.add_argument("--input",         required=True,        help="Input JSON path")
    parser.add_argument("--output",        required=True,        help="Output JSON path")
    parser.add_argument("--pos-threshold", type=float, default=0.15,
                        help="Max position distance to merge two nodes (default: 0.15)")
    parser.add_argument("--vel-threshold", type=float, default=0.75,
                        help="Max velocity distance to merge two nodes (default: 0.75)")
    parser.add_argument("--qt-threshold",  type=int,   default=1,
                        help="Max quantized-time difference to merge two nodes (default: 1)")
    args = parser.parse_args()

    print(f"\n[merge] Loading {args.input} ...")
    with open(args.input, "r", encoding="utf-8") as f:
        graph = json.load(f)

    nodes = graph.get("nodes", [])
    edges = graph.get("edges", [])
    print(f"[merge] Input:  {len(nodes)} nodes,  {len(edges)} edges\n")

    if not nodes:
        print("[merge] No nodes found — nothing to merge.")
        sys.exit(0)

    print(f"[merge] pos<={args.pos_threshold}  "
          f"vel<={args.vel_threshold}  qt<={args.qt_threshold}\n")

    uf = build_clusters(nodes, args.pos_threshold, args.vel_threshold, args.qt_threshold)

    clusters: dict[int, list] = defaultdict(list)
    for node in nodes:
        clusters[uf.find(node["id"])].append(node)

    print(f"\n[merge] {len(nodes)} nodes -> {len(clusters)} clusters\n")

    merged_nodes = []
    old_to_new: dict[int, int] = {}

    for new_id, members in enumerate(clusters.values()):
        rep       = merge_cluster(members)
        rep["id"] = new_id
        merged_nodes.append(rep)
        for m in members:
            old_to_new[m["id"]] = new_id

    merged_edges = merge_edges(edges, old_to_new)
    print(f"\n[merge] {len(edges)} edges -> {len(merged_edges)} edges\n")

    output = {
        "version":        graph.get("version", 1),
        "stepDt":         graph.get("stepDt"),
        "stepsPerAction": graph.get("stepsPerAction"),
        "timeQuant":      graph.get("timeQuant"),
        "maxExpansions":  graph.get("maxExpansions"),
        "maxTimeSeconds": graph.get("maxTimeSeconds"),
        "startPos":       graph.get("startPos"),
        "maxAirJumps":    graph.get("maxAirJumps"),
        "enemyNames":     graph.get("enemyNames", []),
        "mergeParams": {
            "posThreshold": args.pos_threshold,
            "velThreshold": args.vel_threshold,
            "qtThreshold":  args.qt_threshold,
        },
        "nodes": merged_nodes,
        "edges": merged_edges,
    }

    with open(args.output, "w", encoding="utf-8") as f:
        json.dump(output, f, indent=2)

    print(f"[merge] Saved -> {args.output}\n")


if __name__ == "__main__":
    main()
