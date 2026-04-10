
"""
visualize_graph_scc.py
------------------
Visualizes a platformer reachability graph exported by PlatformerReachabilityPlanner.

DIRECTED BRIDGE DETECTION
--------------------------
Uses Kosaraju's algorithm to find Strongly Connected Components (SCCs).
An edge (u -> v) is a DIRECTED BRIDGE if:
  - u and v belong to DIFFERENT SCCs, AND
  - it is the only edge crossing from u's SCC to v's SCC
    (i.e. collapsing all SCCs into a DAG of "super-nodes", the edge
     connecting those two super-nodes is a bridge in the condensation DAG)

This correctly handles directed graphs: a back-edge that only exists in the
reverse direction does NOT protect a forward edge from being a bridge.

Action types (PlayerMovement.cs MoveActionType enum):
    0=None  1=Left  2=Right  3=Jump  4=DoubleJump  5=Dash

Usage:
    python visualize_graph_scc.py graph.json
    python visualize_graph_scc.py graph.json --max-edges 100000 --grid-res 0.5
    python visualize_graph_scc.py graph.json --bridges-only
    python visualize_graph_scc.py graph.json --action-filter 4    # DoubleJump only
    python visualize_graph_scc.py graph.json --no-gui --output out.png
    python visualize_graph_scc.py graph.json --min-visits 2       # filter noise
"""

import argparse
import json
import os
import random
from collections import defaultdict

import numpy as np
import matplotlib
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from matplotlib.widgets import CheckButtons
from matplotlib.collections import LineCollection


# ---------------------------------------------------------------------------
# Action enum  (mirrors PlayerMovement.MoveActionType)
# ---------------------------------------------------------------------------

ACTION_NAMES = {0: "None", 1: "Left", 2: "Right", 3: "Jump", 4: "DoubleJump", 5: "Dash"}
ACTION_COLORS = {
    0: "#555566",   # None       grey
    1: "#4488ff",   # Left       blue
    2: "#44ddff",   # Right      cyan
    3: "#44ff88",   # Jump       green
    4: "#ffaa00",   # DoubleJump orange
    5: "#ff44ff",   # Dash       magenta
}

BG       = "#0a0c10"
PANEL    = "#0f1318"
BORDER   = "#1e2a3a"
TEXT     = "#c8d8e8"
DIM      = "#4a6070"
BRIDGE_C = "#ff2d55"
GROUND_C = "#3399ff"
AIRJ_C   = "#ffaa00"


# ---------------------------------------------------------------------------
# Loading
# ---------------------------------------------------------------------------

def load_graph(path, min_visits=0):
    size_mb = os.path.getsize(path) / 1_048_576
    print(f"[load] {path}  ({size_mb:.1f} MB) ...")
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    nodes_raw = data.get("nodes", [])
    edges_raw = data.get("edges", [])
    meta = {k: v for k, v in data.items() if k not in ("nodes", "edges")}
    print(f"[load] {len(nodes_raw):,} nodes, {len(edges_raw):,} edges")
    if min_visits > 0:
        nodes_raw = [n for n in nodes_raw if n.get("visits", 0) >= min_visits]
        print(f"[load] After min_visits={min_visits}: {len(nodes_raw):,} nodes")
    node_by_id = {n["id"]: n for n in nodes_raw}
    return set(node_by_id), node_by_id, edges_raw, meta


# ---------------------------------------------------------------------------
# Kosaraju's SCC  (iterative — safe for very large graphs)
#
# Returns: scc_id[node] -> int  (nodes in the same SCC share the same id)
# ---------------------------------------------------------------------------

def kosaraju_scc(node_ids, edges_raw):
    """
    Two-pass iterative Kosaraju:
      Pass 1 — DFS on forward graph, push nodes onto a stack in finish order.
      Pass 2 — DFS on reverse graph in reverse finish order, each DFS tree = one SCC.
    """
    print("[scc] Building forward + reverse adjacency ...")
    fwd = defaultdict(list)   # u -> [v, ...]
    rev = defaultdict(list)   # v -> [u, ...]

    for e in edges_raw:
        u, v = e["fromId"], e["toId"]
        if u == v or u not in node_ids or v not in node_ids:
            continue
        fwd[u].append(v)
        rev[v].append(u)

    # Pass 1: iterative DFS on forward graph, record finish order
    print("[scc] Pass 1 — finish order ...")
    visited = set()
    finish_stack = []

    for start in node_ids:
        if start in visited:
            continue
        # stack entries: (node, iterator_index, entered)
        dfs = [(start, 0, False)]
        while dfs:
            u, idx, entered = dfs[-1]
            if not entered:
                if u in visited:
                    dfs.pop()
                    continue
                visited.add(u)
                dfs[-1] = (u, idx, True)
            neighbors = fwd[u]
            if idx < len(neighbors):
                dfs[-1] = (u, idx + 1, True)
                v = neighbors[idx]
                if v not in visited:
                    dfs.append((v, 0, False))
            else:
                dfs.pop()
                finish_stack.append(u)

    # Pass 2: iterative DFS on reverse graph in reverse finish order
    print("[scc] Pass 2 — labelling SCCs ...")
    scc_id = {}
    scc_count = 0

    while finish_stack:
        start = finish_stack.pop()
        if start in scc_id:
            continue
        # BFS/DFS on reverse graph
        stack = [start]
        while stack:
            u = stack.pop()
            if u in scc_id:
                continue
            scc_id[u] = scc_count
            for v in rev[u]:
                if v not in scc_id:
                    stack.append(v)
        scc_count += 1

    print(f"[scc] {scc_count:,} SCCs found across {len(node_ids):,} nodes.")
    return scc_id, scc_count


# ---------------------------------------------------------------------------
# Directed bridge detection via SCC condensation
#
# After collapsing each SCC to a single "super-node", the graph becomes a DAG.
# An original edge (u->v) is a directed bridge if:
#   1. scc_id[u] != scc_id[v]  — crosses SCC boundary
#   2. It is the ONLY edge between those two super-nodes in the condensation
#      (i.e. the condensation edge is a bridge in the DAG)
#
# Because the condensation is a DAG (no cycles), every edge in it is trivially
# a bridge of the DAG itself. So condition 1 alone is sufficient:
#   any edge that crosses an SCC boundary is a directed bridge.
#
# Why? Inside an SCC every node can reach every other node, so no single
# intra-SCC edge can disconnect anything. Only inter-SCC edges matter.
# And since the condensation is a DAG, removing any condensation edge
# disconnects the super-nodes (there's no other directed path between them).
# ---------------------------------------------------------------------------

def find_directed_bridges(node_ids, edges_raw, scc_id):
    print("[bridges] Finding directed bridges (inter-SCC edges) ...")

    # Count edges between each pair of SCCs
    inter_scc_edges = defaultdict(list)   # (scc_u, scc_v) -> [edge_index, ...]

    for i, e in enumerate(edges_raw):
        u, v = e["fromId"], e["toId"]
        if u == v or u not in node_ids or v not in node_ids:
            continue
        su = scc_id.get(u)
        sv = scc_id.get(v)
        if su is None or sv is None:
            continue
        if su != sv:
            inter_scc_edges[(su, sv)].append(i)

    # An inter-SCC edge is a directed bridge if it is the ONLY edge
    # between that pair of SCCs (parallel edges between the same SCC pair
    # mean neither alone disconnects the condensation).
    bridge_set = set()
    for (su, sv), edge_indices in inter_scc_edges.items():
        if len(edge_indices) == 1:
            bridge_set.add(edge_indices[0])

    print(f"[bridges] {len(bridge_set)} directed bridge(s) found.")
    return bridge_set


# ---------------------------------------------------------------------------
# Heatmap
# ---------------------------------------------------------------------------

def build_heatmap(node_by_id, grid_res):
    xs = np.array([n["pos"]["x"] for n in node_by_id.values()], dtype=np.float32)
    ys = np.array([n["pos"]["y"] for n in node_by_id.values()], dtype=np.float32)
    ws = np.array([max(1, n.get("visits", 1)) for n in node_by_id.values()], dtype=np.float32)
    x0, x1 = float(xs.min()), float(xs.max())
    y0, y1 = float(ys.min()), float(ys.max())
    nx = max(4, int((x1 - x0) / grid_res) + 1)
    ny = max(4, int((y1 - y0) / grid_res) + 1)
    print(f"[heatmap] {nx} x {ny} grid cells  (res={grid_res} world units)")
    xi = np.clip(((xs - x0) / grid_res).astype(np.int32), 0, nx - 1)
    yi = np.clip(((ys - y0) / grid_res).astype(np.int32), 0, ny - 1)
    grid = np.zeros((ny, nx), dtype=np.float32)
    np.add.at(grid, (yi, xi), ws)
    return grid, np.linspace(x0, x1, nx + 1), np.linspace(y0, y1, ny + 1)


# ---------------------------------------------------------------------------
# Edge list -> LineCollection segments
# ---------------------------------------------------------------------------

def to_segments(edge_list, node_by_id):
    segs = []
    for e in edge_list:
        a = node_by_id.get(e["fromId"])
        b = node_by_id.get(e["toId"])
        if a and b:
            segs.append([
                (a["pos"]["x"], a["pos"]["y"]),
                (b["pos"]["x"], b["pos"]["y"]),
            ])
    return segs


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def visualize(args):
    node_ids, node_by_id, edges_raw, meta = load_graph(args.json_file, args.min_visits)

    # --- Directed bridge detection via Kosaraju SCC -------------------------
    scc_id, scc_count = kosaraju_scc(node_ids, edges_raw)
    bridge_idx        = find_directed_bridges(node_ids, edges_raw, scc_id)

    bridge_edges = [edges_raw[i] for i in bridge_idx]
    normal_edges = [
        e for i, e in enumerate(edges_raw)
        if i not in bridge_idx
        and e["fromId"] != e["toId"]
        and e["fromId"] in node_ids
        and e["toId"]   in node_ids
    ]

    if args.action_filter is not None:
        normal_edges = [e for e in normal_edges if e.get("action") == args.action_filter]
        aname = ACTION_NAMES.get(args.action_filter, str(args.action_filter))
        print(f"[filter] {len(normal_edges):,} normal edges  (action={args.action_filter} / {aname})")

    if len(normal_edges) > args.max_edges:
        print(f"[sample] Sampling {args.max_edges:,} / {len(normal_edges):,} normal edges")
        normal_edges = random.sample(normal_edges, args.max_edges)

    # --- Node arrays --------------------------------------------------------
    print("[arrays] Preparing node arrays ...")
    all_nodes = list(node_by_id.values())
    axs  = np.array([n["pos"]["x"] for n in all_nodes], dtype=np.float32)
    ays  = np.array([n["pos"]["y"] for n in all_nodes], dtype=np.float32)
    grnd = np.array([n.get("grounded", False) for n in all_nodes])
    airj = np.array([n.get("airJumpsUsed", 0) > 0 for n in all_nodes])

    grid, xe, ye = build_heatmap(node_by_id, args.grid_res)
    grid_log = np.log1p(grid)

    bnids = set()
    for e in bridge_edges:
        bnids.add(e["fromId"])
        bnids.add(e["toId"])
    bn  = [node_by_id[i] for i in bnids if i in node_by_id]
    bx  = [n["pos"]["x"] for n in bn]
    bby = [n["pos"]["y"] for n in bn]

    # --- Figure -------------------------------------------------------------
    print("[plot] Composing figure ...")
    fig = plt.figure(figsize=(18, 10), facecolor=BG)
    try:
        fig.canvas.manager.set_window_title("Platformer Graph Visualizer")
    except Exception:
        pass

    ax_main = fig.add_axes([0.01, 0.05, 0.73, 0.90])
    ax_chk  = fig.add_axes([0.77, 0.22, 0.21, 0.60])
    ax_main.set_facecolor(BG)
    ax_chk.set_facecolor(PANEL)
    for sp in ax_chk.spines.values():
        sp.set_color(BORDER)

    # 1. Heatmap
    cmap = matplotlib.colormaps.get_cmap("inferno").copy()
    cmap.set_under(BG)
    im = ax_main.pcolormesh(xe, ye, grid_log, cmap=cmap, vmin=0.01,
                            shading="flat", rasterized=True, zorder=1)

    # 2. Normal edge sample (coloured by action)
    nsegs = to_segments(normal_edges, node_by_id)
    ncols = [ACTION_COLORS.get(e.get("action", 0), "#555566")
             for e in normal_edges
             if node_by_id.get(e["fromId"]) and node_by_id.get(e["toId"])]
    lc_n = LineCollection(nsegs, colors=ncols, linewidths=0.4, alpha=0.25, zorder=2)
    ax_main.add_collection(lc_n)

    # 3. Bridge edges
    bsegs = to_segments(bridge_edges, node_by_id)
    lc_b  = LineCollection(bsegs, colors=BRIDGE_C, linewidths=2.2, alpha=0.95, zorder=5)
    ax_main.add_collection(lc_b)

    # 4. Grounded nodes
    sc_g  = ax_main.scatter(axs[grnd], ays[grnd], s=1.2, c=GROUND_C,
                            alpha=0.35, linewidths=0, zorder=3)

    # 5. Air-jump nodes
    sc_a  = ax_main.scatter(axs[airj], ays[airj], s=2.0, c=AIRJ_C,
                            alpha=0.55, linewidths=0, zorder=4)

    # 6. Bridge node markers
    sc_bn = ax_main.scatter(bx, bby, s=22, c=BRIDGE_C, alpha=0.9,
                            linewidths=0.6, edgecolors="#ffffff", zorder=6)

    # 7. Bridge labels
    btexts = []
    if len(bridge_edges) <= 40:
        for e in bridge_edges:
            a = node_by_id.get(e["fromId"])
            b = node_by_id.get(e["toId"])
            if not a or not b:
                continue
            mx = (a["pos"]["x"] + b["pos"]["x"]) / 2
            my = (a["pos"]["y"] + b["pos"]["y"]) / 2
            lbl = ACTION_NAMES.get(e.get("action", -1), "?")
            t = ax_main.text(
                mx, my, lbl, fontsize=6.5, color="#ff8899", fontweight="bold",
                ha="center", va="bottom", zorder=7,
                bbox=dict(boxstyle="round,pad=0.15", fc="#1a0008", ec=BRIDGE_C, lw=0.6, alpha=0.88),
            )
            btexts.append(t)

    # --- Axes styling -------------------------------------------------------
    ax_main.set_aspect("equal")
    ax_main.autoscale_view()
    ax_main.set_xlabel("world X", color=DIM, fontsize=9)
    ax_main.set_ylabel("world Y", color=DIM, fontsize=9)
    ax_main.tick_params(colors=DIM, labelsize=8)
    for sp in ax_main.spines.values():
        sp.set_color(BORDER)

    ax_main.set_title(
        f"Platformer Reachability Graph  |  "
        f"{len(node_ids):,} nodes   {len(edges_raw):,} edges   "
        f"{len(bridge_idx)} directed bridge(s)   "
        f"{scc_count:,} SCCs   "
        f"maxAirJumps={meta.get('maxAirJumps', '?')}",
        color=TEXT, fontsize=10, pad=8,
    )

    cb = fig.colorbar(im, ax=ax_main, fraction=0.018, pad=0.01)
    cb.set_label("log(visit density)", color=DIM, fontsize=8)
    cb.ax.yaxis.set_tick_params(color=DIM, labelcolor=DIM, labelsize=7)
    cb.outline.set_edgecolor(BORDER)

    patches = (
        [mpatches.Patch(color=c, label=f"[{k}] {ACTION_NAMES[k]}")
         for k, c in sorted(ACTION_COLORS.items()) if k in ACTION_NAMES]
        + [mpatches.Patch(color=BRIDGE_C, label="Directed bridge"),
           mpatches.Patch(color=GROUND_C, label="Grounded node"),
           mpatches.Patch(color=AIRJ_C,   label="Air-jump used")]
    )
    ax_main.legend(handles=patches, loc="lower left", fontsize=7,
                   facecolor=PANEL, edgecolor=BORDER, labelcolor=TEXT, framealpha=0.92)

    by_act = defaultdict(int)
    for e in bridge_edges:
        by_act[e.get("action", -1)] += 1
    slines = [f"Bridges : {len(bridge_idx)}", f"SCCs    : {scc_count:,}", "-" * 24]
    for act, cnt in sorted(by_act.items()):
        slines.append(f"  [{act}] {ACTION_NAMES.get(act, '?'):12s} {cnt:>4}")
    slines += ["-" * 24,
               f"Nodes   : {len(node_ids):>10,}",
               f"Edges   : {len(edges_raw):>10,}",
               f"Sampled : {len(nsegs):>10,}"]
    ax_main.text(
        0.995, 0.995, "\n".join(slines),
        transform=ax_main.transAxes, fontsize=7.5, color=TEXT,
        va="top", ha="right", fontfamily="monospace",
        bbox=dict(boxstyle="round,pad=0.5", fc=PANEL, ec=BORDER, lw=0.8, alpha=0.92),
        zorder=10,
    )

    # --- Overlay toggles ----------------------------------------------------
    layer_labels  = ["Heatmap", "Normal edges", "Bridge edges",
                     "Grounded nodes", "Air-jump nodes", "Bridge nodes", "Bridge labels"]
    layer_artists = [im, lc_n, lc_b, sc_g, sc_a, sc_bn, btexts]
    ax_chk.set_title("Overlays", color=TEXT, fontsize=9, pad=6)
    chk = CheckButtons(ax_chk, layer_labels, [True] * len(layer_labels))

    # .rectangles was renamed to .patches in matplotlib 3.7+
    _boxes = getattr(chk, "patches", None) or getattr(chk, "rectangles", [])
    for r in _boxes:
        try: r.set_edgecolor(BORDER); r.set_facecolor(BG)
        except Exception: pass
    for lbl in chk.labels:
        lbl.set_color(TEXT)
        lbl.set_fontsize(8)

    def toggle(label):
        idx    = layer_labels.index(label)
        artist = layer_artists[idx]
        if isinstance(artist, list):
            for t in artist:
                t.set_visible(not t.get_visible())
        else:
            artist.set_visible(not artist.get_visible())
        fig.canvas.draw_idle()

    chk.on_clicked(toggle)

    # --- Output -------------------------------------------------------------
    if args.no_gui:
        out = args.output or "graph_viz.png"
        fig.savefig(out, dpi=150, facecolor=fig.get_facecolor(), bbox_inches="tight")
        print(f"[output] Saved -> {out}")
    else:
        print("[plot] Opening interactive window ...")
        print("       Scroll to zoom, drag to pan, checkboxes toggle layers")
        plt.show()


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    p = argparse.ArgumentParser(
        description="Visualize a PlatformerReachabilityPlanner state-graph JSON."
    )
    p.add_argument("json_file",
                   help="Path to the exported graph JSON")
    p.add_argument("--max-edges", type=int, default=50_000,
                   help="Max normal edges to sample for display (default: 50000)")
    p.add_argument("--grid-res", type=float, default=0.25,
                   help="Heatmap grid cell size in world units (default: 0.25)")
    p.add_argument("--min-visits", type=int, default=0,
                   help="Ignore nodes with fewer than N visits (default: 0)")
    p.add_argument("--no-gui", action="store_true",
                   help="Save PNG instead of showing interactive window")
    p.add_argument("--output", type=str, default=None,
                   help="Output PNG path when using --no-gui (default: graph_viz.png)")
    p.add_argument("--action-filter", type=int, default=None,
                   help="Only sample normal edges of this action int "
                        "(0=None 1=Left 2=Right 3=Jump 4=DoubleJump 5=Dash)")
    p.add_argument("--bridges-only", action="store_true",
                   help="Skip normal-edge sampling, show bridges only (fastest)")
    args = p.parse_args()
    if args.bridges_only:
        args.max_edges = 0
    visualize(args)


if __name__ == "__main__":
    main()