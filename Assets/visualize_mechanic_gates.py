"""
visualize_mechanic_gates.py
============================
Interactive Python visualizer for the platformer mechanic-gate analysis.
Run AFTER analyze_mechanic_gates.py — it reads both the original graph JSON
and the mechanic_gates.json output.

Usage:
    python visualize_mechanic_gates.py <graph.json> [gates.json] [options]

Options:
    --gates      Path to mechanic_gates.json  (default: mechanic_gates.json)
    --mechanic   Action ids if you want to re-run analysis inline (default: 4)
    --pos-quant  Position quantisation         (default: 0.10)
    --max-nodes  Cap on nodes drawn in bg scatter  (default: 80000)

Controls (interactive window):
    Click a cluster in the legend or scatter → highlight it
    Scroll  → zoom
    Middle-drag → pan
    Press 1-9 → jump to cluster by id
    Press A   → show all / reset view
    Press E   → export current view as PNG
    Press Q   → quit
"""

import argparse
import json
import sys
import os
from collections import defaultdict

import numpy as np
import matplotlib
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
import matplotlib.patheffects as pe
from matplotlib.colors import to_rgba
from matplotlib.widgets import Button, CheckButtons
import matplotlib.gridspec as gridspec


# ── Aesthetic constants (dark "game map" theme) ────────────────────────────────
BG_COLOR        = "#0d0f14"
GRID_COLOR      = "#1c2030"
TEXT_COLOR      = "#c8d0e0"
ACCENT_COLOR    = "#ffffff"
NODE_ALL_COLOR  = "#2a3550"          # unreachable-without-mechanic background nodes
NODE_FREE_COLOR = "#3d6b8e"          # freely reachable nodes
START_COLOR     = "#00ffc8"          # start node
GATED_PALETTE   = [                  # per-cluster colours (cycles)
    "#ff4d6d", "#ffd166", "#06d6a0", "#a64dff",
    "#f77f00", "#00b4d8", "#fb5607", "#8338ec",
    "#e63946", "#43aa8b", "#f4a261", "#577590",
    "#ef233c", "#b5e48c", "#48cae4", "#ff99c8",
]

FONT_TITLE  = {"fontsize": 14, "color": TEXT_COLOR, "fontweight": "bold",
               "fontfamily": "monospace"}
FONT_LABEL  = {"fontsize": 9,  "color": TEXT_COLOR, "fontfamily": "monospace"}
FONT_TICK   = {"color": TEXT_COLOR, "fontsize": 7}


# ─────────────────────────────────────────────────────────────────────────────
#  Data loading
# ─────────────────────────────────────────────────────────────────────────────

def load_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def build_adjacency(edges, mechanic_actions):
    """Returns (adj_full, adj_restricted)."""
    adj_full       = defaultdict(list)
    adj_restricted = defaultdict(list)
    for e in edges:
        fid, tid, action = e["fromId"], e["toId"], e["action"]
        adj_full[fid].append((tid, action))
        if action not in mechanic_actions:
            adj_restricted[fid].append((tid, action))
    return adj_full, adj_restricted


def bfs(adj, start=0):
    from collections import deque
    visited = {start}
    q = deque([start])
    while q:
        n = q.popleft()
        for nb, _ in adj.get(n, ()):
            if nb not in visited:
                visited.add(nb)
                q.append(nb)
    return visited


def run_analysis(graph, mechanic_actions, pos_quant):
    """Inline re-analysis so we don't need the gates JSON."""
    nodes  = {n["id"]: n for n in graph["nodes"]}
    adj_full, adj_restricted = build_adjacency(graph["edges"], mechanic_actions)

    reach_full    = bfs(adj_full)
    reach_without = bfs(adj_restricted)
    exclusive     = reach_full - reach_without

    pos_to_all     = defaultdict(list)
    pos_to_without = defaultdict(set)

    for nid in reach_full:
        n = nodes[nid]
        cx = round(n["pos"]["x"] / pos_quant)
        cy = round(n["pos"]["y"] / pos_quant)
        pos_to_all[(cx, cy)].append(nid)

    for nid in reach_without:
        n = nodes[nid]
        cx = round(n["pos"]["x"] / pos_quant)
        cy = round(n["pos"]["y"] / pos_quant)
        pos_to_without[(cx, cy)].add(nid)

    gated_cells = set()
    for cell in pos_to_all:
        if cell not in pos_to_without:
            gated_cells.add(cell)

    # 8-connected clustering
    from collections import deque
    remaining = set(gated_cells)
    directions = [(-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)]
    clusters_raw = []
    while remaining:
        seed = next(iter(remaining))
        comp = set()
        q = deque([seed])
        remaining.remove(seed)
        while q:
            cx, cy = q.popleft()
            comp.add((cx, cy))
            for dx, dy in directions:
                nb = (cx+dx, cy+dy)
                if nb in remaining:
                    remaining.remove(nb)
                    q.append(nb)
        clusters_raw.append(frozenset(comp))

    clusters_raw.sort(key=len, reverse=True)

    clusters_out = []
    for i, comp in enumerate(clusters_raw):
        xs = [cx * pos_quant for cx, cy in comp]
        ys = [cy * pos_quant for cx, cy in comp]
        exc_nids = []
        for cx, cy in comp:
            exc_nids.extend([nid for nid in pos_to_all[(cx,cy)] if nid in exclusive])
        clusters_out.append({
            "cluster_id": i,
            "position_count": len(comp),
            "exclusive_node_count": len(exc_nids),
            "bbox": {"x_min": min(xs), "x_max": max(xs),
                     "y_min": min(ys), "y_max": max(ys)},
            "centroid": {"x": np.mean(xs), "y": np.mean(ys)},
            "cells": list(comp),
        })

    return {
        "clusters": clusters_out,
        "reach_full": reach_full,
        "reach_without": reach_without,
        "exclusive": exclusive,
        "gated_cells": gated_cells,
        "nodes": nodes,
        "pos_quant": pos_quant,
        "stats": {
            "total_nodes": len(graph["nodes"]),
            "total_edges": len(graph["edges"]),
            "reachable_full": len(reach_full),
            "reachable_without_mechanic": len(reach_without),
            "exclusive_nodes": len(exclusive),
            "gated_positions": len(gated_cells),
            "gated_clusters": len(clusters_out),
        }
    }


# ─────────────────────────────────────────────────────────────────────────────
#  Point-cloud builders
# ─────────────────────────────────────────────────────────────────────────────

def node_point_clouds(nodes, reach_full, reach_without, exclusive, max_nodes):
    """Returns arrays for background (free) and gated node positions."""
    free_x, free_y   = [], []
    gated_x, gated_y = [], []

    # Subsample background nodes if huge
    free_ids  = list(reach_without)
    if len(free_ids) > max_nodes:
        idx = np.random.choice(len(free_ids), max_nodes, replace=False)
        free_ids = [free_ids[i] for i in idx]

    for nid in free_ids:
        n = nodes[nid]
        free_x.append(n["pos"]["x"])
        free_y.append(n["pos"]["y"])

    for nid in exclusive:
        n = nodes[nid]
        gated_x.append(n["pos"]["x"])
        gated_y.append(n["pos"]["y"])

    return (np.array(free_x),  np.array(free_y),
            np.array(gated_x), np.array(gated_y))


def cluster_point_arrays(clusters, nodes_dict, exclusive_set):
    """
    For each cluster return (xs, ys) of the exclusive node positions.
    Falls back to cell centroids when node ids aren't in the analysis result.
    """
    out = []
    for c in clusters:
        if "cells" in c:
            pq = 0.10
            xs = [cx * pq for cx, cy in c["cells"]]
            ys = [cy * pq for cx, cy in c["cells"]]
        else:
            pos = c.get("positions", [])
            xs = [p["x"] for p in pos]
            ys = [p["y"] for p in pos]
        out.append((np.array(xs), np.array(ys)))
    return out


# ─────────────────────────────────────────────────────────────────────────────
#  Main visualizer
# ─────────────────────────────────────────────────────────────────────────────

def build_figure(graph, analysis, mechanic_names, max_nodes):
    nodes      = analysis["nodes"]
    clusters   = analysis["clusters"]
    reach_full = analysis["reach_full"]
    reach_wo   = analysis["reach_without"]
    exclusive  = analysis["exclusive"]
    stats      = analysis["stats"]

    start_node = graph["nodes"][0]   # id=0 always start
    start_x    = start_node["pos"]["x"]
    start_y    = start_node["pos"]["y"]

    # Build point clouds
    fx, fy, gx, gy = node_point_clouds(nodes, reach_full, reach_wo,
                                        exclusive, max_nodes)
    cluster_arrays = cluster_point_arrays(clusters, nodes, exclusive)

    # ── Figure layout ─────────────────────────────────────────────────────────
    fig = plt.figure(figsize=(16, 9), facecolor=BG_COLOR)
    fig.canvas.manager.set_window_title("Mechanic Gate Analyzer")

    gs = gridspec.GridSpec(
        1, 2, figure=fig,
        width_ratios=[3, 1],
        left=0.04, right=0.98,
        top=0.94, bottom=0.06,
        wspace=0.03,
    )
    ax_map  = fig.add_subplot(gs[0])
    ax_info = fig.add_subplot(gs[1])

    for ax in (ax_map, ax_info):
        ax.set_facecolor(BG_COLOR)
        ax.tick_params(colors=TEXT_COLOR)
        for spine in ax.spines.values():
            spine.set_edgecolor(GRID_COLOR)

    ax_map.grid(True, color=GRID_COLOR, linewidth=0.4, zorder=0)
    ax_map.set_axisbelow(True)
    ax_info.axis("off")

    # ── Title ─────────────────────────────────────────────────────────────────
    mechanic_str = " + ".join(mechanic_names)
    fig.suptitle(
        f"Mechanic Gate Map  ·  Gating mechanic: {mechanic_str}",
        **FONT_TITLE, y=0.98
    )

    # ── Draw: free (background) nodes ─────────────────────────────────────────
    if len(fx):
        ax_map.scatter(fx, fy, s=2, c=NODE_FREE_COLOR, alpha=0.25,
                       linewidths=0, zorder=1, label="Freely reachable")

    # ── Draw: per-cluster gated positions (coloured) ──────────────────────────
    cluster_scatters = []
    legend_patches   = []

    for i, (cxs, cys) in enumerate(cluster_arrays):
        color = GATED_PALETTE[i % len(GATED_PALETTE)]
        sc = ax_map.scatter(
            cxs, cys, s=14, c=color, alpha=0.85,
            linewidths=0, zorder=3,
            label=f"Cluster {i}  ({clusters[i]['position_count']} cells)",
            picker=True,
        )
        cluster_scatters.append(sc)

        # Cluster centroid label
        cx  = clusters[i]["centroid"]["x"]
        cy  = clusters[i]["centroid"]["y"]
        txt = ax_map.text(
            cx, cy, str(i),
            fontsize=7, fontfamily="monospace", fontweight="bold",
            color=color, ha="center", va="center", zorder=5,
            path_effects=[pe.withStroke(linewidth=2, foreground=BG_COLOR)],
        )

        patch = mpatches.Patch(color=color,
                               label=f"#{i}  {clusters[i]['position_count']} pos  "
                                     f"{clusters[i]['exclusive_node_count']} nodes")
        legend_patches.append(patch)

    # ── Draw: start node ─────────────────────────────────────────────────────
    ax_map.scatter([start_x], [start_y], s=120, c=START_COLOR,
                   marker="*", zorder=6, label="Start")
    ax_map.text(start_x, start_y + 0.25, "START",
                fontsize=6, color=START_COLOR, ha="center",
                fontfamily="monospace",
                path_effects=[pe.withStroke(linewidth=2, foreground=BG_COLOR)])

    ax_map.set_xlabel("World X", **FONT_LABEL)
    ax_map.set_ylabel("World Y", **FONT_LABEL)
    ax_map.tick_params(**FONT_TICK)

    # ── Legend (right panel) ──────────────────────────────────────────────────
    ax_info.text(0.05, 0.99, "CLUSTERS", transform=ax_info.transAxes,
                 fontsize=10, color=ACCENT_COLOR, fontfamily="monospace",
                 fontweight="bold", va="top")

    # Stats block
    s = stats
    stat_lines = [
        f"nodes total   {s['total_nodes']:>8,}",
        f"edges total   {s['total_edges']:>8,}",
        f"reach (full)  {s['reachable_full']:>8,}",
        f"reach (free)  {s['reachable_without_mechanic']:>8,}",
        f"excl. nodes   {s['exclusive_nodes']:>8,}",
        f"gated cells   {s['gated_positions']:>8,}",
        f"clusters      {s['gated_clusters']:>8,}",
    ]
    stat_block = "\n".join(stat_lines)
    ax_info.text(0.05, 0.93, stat_block, transform=ax_info.transAxes,
                 fontsize=8, color=TEXT_COLOR, fontfamily="monospace",
                 va="top", linespacing=1.6,
                 bbox=dict(boxstyle="round,pad=0.5", facecolor=GRID_COLOR,
                           edgecolor=GRID_COLOR, alpha=0.8))

    # Cluster list
    y_cursor = 0.68
    ax_info.text(0.05, y_cursor, "id  cells   excl.nodes  centroid",
                 transform=ax_info.transAxes, fontsize=7,
                 color="#607080", fontfamily="monospace", va="top")
    y_cursor -= 0.025

    for i, c in enumerate(clusters[:20]):   # cap at 20 rows
        color = GATED_PALETTE[i % len(GATED_PALETTE)]
        label = (f"{i:>2}  {c['position_count']:>6,}  "
                 f"{c['exclusive_node_count']:>10,}  "
                 f"({c['centroid']['x']:>6.1f},{c['centroid']['y']:>6.1f})")
        ax_info.text(0.05, y_cursor, label,
                     transform=ax_info.transAxes,
                     fontsize=7, color=color, fontfamily="monospace", va="top",
                     picker=True)
        y_cursor -= 0.022

    if len(clusters) > 20:
        ax_info.text(0.05, y_cursor,
                     f"  … {len(clusters)-20} more clusters",
                     transform=ax_info.transAxes,
                     fontsize=7, color="#607080", fontfamily="monospace", va="top")

    # ── Keybind hint ──────────────────────────────────────────────────────────
    hints = "[1-9] jump cluster  [A] reset  [E] export PNG  [Q] quit"
    fig.text(0.01, 0.01, hints, fontsize=7, color="#405060",
             fontfamily="monospace")

    # ── Interaction state ─────────────────────────────────────────────────────
    state = {"selected": None, "orig_lims": None}

    def save_lims():
        state["orig_lims"] = (ax_map.get_xlim(), ax_map.get_ylim())

    def highlight_cluster(i):
        """Zoom map to cluster i and dim others."""
        if i is None or i >= len(clusters):
            return
        c = clusters[i]
        pad_x = max(1.0, (c["bbox"]["x_max"] - c["bbox"]["x_min"]) * 0.5)
        pad_y = max(1.0, (c["bbox"]["y_max"] - c["bbox"]["y_min"]) * 0.5)
        ax_map.set_xlim(c["bbox"]["x_min"] - pad_x, c["bbox"]["x_max"] + pad_x)
        ax_map.set_ylim(c["bbox"]["y_min"] - pad_y, c["bbox"]["y_max"] + pad_y)

        for j, sc in enumerate(cluster_scatters):
            sc.set_alpha(0.9 if j == i else 0.15)
            sc.set_sizes([30 if j == i else 8])

        state["selected"] = i
        fig.canvas.draw_idle()

    def reset_view():
        if state["orig_lims"]:
            ax_map.set_xlim(state["orig_lims"][0])
            ax_map.set_ylim(state["orig_lims"][1])
        for sc in cluster_scatters:
            sc.set_alpha(0.85)
            sc.set_sizes([14])
        state["selected"] = None
        fig.canvas.draw_idle()

    def on_key(event):
        if event.key in "123456789":
            save_lims()
            highlight_cluster(int(event.key) - 1)
        elif event.key == "0":
            save_lims()
            highlight_cluster(9)
        elif event.key in ("a", "A"):
            reset_view()
        elif event.key in ("e", "E"):
            out = "mechanic_gates_view.png"
            fig.savefig(out, dpi=150, bbox_inches="tight", facecolor=BG_COLOR)
            print(f"[export] saved {out}")
        elif event.key in ("q", "Q"):
            plt.close(fig)

    def on_pick(event):
        for i, sc in enumerate(cluster_scatters):
            if event.artist is sc:
                save_lims()
                highlight_cluster(i)
                return

    fig.canvas.mpl_connect("key_press_event", on_key)
    fig.canvas.mpl_connect("pick_event",      on_pick)

    # Save initial limits after rendering
    def on_draw(event):
        if state["orig_lims"] is None:
            state["orig_lims"] = (ax_map.get_xlim(), ax_map.get_ylim())
    fig.canvas.mpl_connect("draw_event", on_draw)

    return fig


# ─────────────────────────────────────────────────────────────────────────────
#  Cluster detail figure (separate window)
# ─────────────────────────────────────────────────────────────────────────────

def build_cluster_detail_figure(cluster, nodes_dict, palette_idx, pos_quant):
    """Small popup showing one cluster's node properties."""
    color = GATED_PALETTE[palette_idx % len(GATED_PALETTE)]
    cid   = cluster["cluster_id"]

    if "cells" in cluster:
        xs = np.array([cx * pos_quant for cx, cy in cluster["cells"]])
        ys = np.array([cy * pos_quant for cx, cy in cluster["cells"]])
    else:
        pos = cluster.get("positions", [])
        xs  = np.array([p["x"] for p in pos])
        ys  = np.array([p["y"] for p in pos])

    fig, axes = plt.subplots(1, 2, figsize=(10, 5), facecolor=BG_COLOR)
    fig.canvas.manager.set_window_title(f"Cluster {cid} Detail")
    fig.suptitle(f"Cluster {cid}  —  {len(xs)} gated positions",
                 **FONT_TITLE, y=0.98)

    # Left: spatial scatter of the cluster
    ax = axes[0]
    ax.set_facecolor(BG_COLOR)
    ax.grid(True, color=GRID_COLOR, linewidth=0.4)
    ax.scatter(xs, ys, s=16, c=color, alpha=0.7, linewidths=0)
    ax.set_title("Gated positions", **FONT_LABEL)
    ax.set_xlabel("World X", **FONT_LABEL)
    ax.set_ylabel("World Y", **FONT_LABEL)
    ax.tick_params(**FONT_TICK)
    for spine in ax.spines.values():
        spine.set_edgecolor(GRID_COLOR)

    # Right: text summary of sample nodes
    ax2 = axes[1]
    ax2.set_facecolor(BG_COLOR)
    ax2.axis("off")

    bb = cluster["bbox"]
    lines = [
        f"Cluster ID       : {cid}",
        f"Positions        : {cluster['position_count']}",
        f"Exclusive nodes  : {cluster['exclusive_node_count']}",
        f"",
        f"Bounding box",
        f"  X  [{bb['x_min']:.2f} .. {bb['x_max']:.2f}]",
        f"  Y  [{bb['y_min']:.2f} .. {bb['y_max']:.2f}]",
        f"",
        f"Centroid  ({cluster['centroid']['x']:.2f}, {cluster['centroid']['y']:.2f})",
    ]

    samples = cluster.get("sample_nodes", [])
    if samples:
        lines += ["", "── Sample nodes ──────────────────"]
        for n in samples[:8]:
            lines.append(
                f"  id={n['id']:>5}  pos=({n['pos']['x']:.2f},{n['pos']['y']:.2f})"
                f"  gnd={str(n['grounded']):<5}  airJ={n['airJumpsUsed']}"
                f"  vis={n['visits']}"
            )

    ax2.text(0.04, 0.96, "\n".join(lines),
             transform=ax2.transAxes,
             fontsize=8, color=TEXT_COLOR, fontfamily="monospace",
             va="top", linespacing=1.55)

    plt.tight_layout()
    return fig


# ─────────────────────────────────────────────────────────────────────────────
#  CLI
# ─────────────────────────────────────────────────────────────────────────────

def parse_args():
    p = argparse.ArgumentParser(description="Visualize mechanic-gated map areas.")
    p.add_argument("graph_json", help="Exported state graph JSON.")
    p.add_argument("--gates",     default="mechanic_gates.json",
                   help="Mechanic gates JSON from analyze_mechanic_gates.py  "
                        "(optional; analysis will run inline if missing).")
    p.add_argument("--mechanic",  default="4",
                   help="Comma-separated action ids (default: 4 = DoubleJump).")
    p.add_argument("--pos-quant", type=float, default=0.10)
    p.add_argument("--max-nodes", type=int,   default=80000,
                   help="Max background node points to draw (performance cap).")
    p.add_argument("--detail",    type=int,   default=None,
                   help="Open detail popup for a specific cluster id on start.")
    return p.parse_args()


def main():
    args = parse_args()
    mechanic_actions = set(int(x) for x in args.mechanic.split(","))
    ACTION_NAMES = {0:"None",1:"Left",2:"Right",3:"Jump",4:"DoubleJump",5:"Dash"}
    mechanic_names = [ACTION_NAMES.get(a, str(a)) for a in sorted(mechanic_actions)]

    print(f"[viz] loading graph …")
    graph = load_json(args.graph_json)

    # Try loading pre-computed gates first, else run analysis inline
    if os.path.exists(args.gates):
        print(f"[viz] loading gates from {args.gates} …")
        gates_data = load_json(args.gates)
        # We still need reach_full / exclusive for node colouring — re-run BFS
        print(f"[viz] re-running BFS for node colours …")
        adj_full, adj_res = build_adjacency(graph["edges"], mechanic_actions)
        reach_full    = bfs(adj_full)
        reach_without = bfs(adj_res)
        exclusive     = reach_full - reach_without
        nodes_dict    = {n["id"]: n for n in graph["nodes"]}
        analysis = {
            "clusters":       gates_data["clusters"],
            "reach_full":     reach_full,
            "reach_without":  reach_without,
            "exclusive":      exclusive,
            "gated_cells":    set(),
            "nodes":          nodes_dict,
            "pos_quant":      gates_data.get("pos_quant", args.pos_quant),
            "stats":          gates_data["stats"],
        }
    else:
        print(f"[viz] gates file not found — running analysis inline …")
        analysis = run_analysis(graph, mechanic_actions, args.pos_quant)

    print(f"[viz] {len(analysis['clusters'])} clusters to render")

    # ── Main figure ────────────────────────────────────────────────────────────
    matplotlib.rcParams["toolbar"] = "toolbar2"
    fig = build_figure(graph, analysis, mechanic_names, args.max_nodes)

    # ── Optional detail popup ──────────────────────────────────────────────────
    if args.detail is not None and args.detail < len(analysis["clusters"]):
        build_cluster_detail_figure(
            analysis["clusters"][args.detail],
            analysis["nodes"],
            args.detail,
            analysis["pos_quant"],
        )

    plt.show()


if __name__ == "__main__":
    main()
