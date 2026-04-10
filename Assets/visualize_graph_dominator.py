"""
visualize_graph_dominator.py
------------------
Visualizes a platformer reachability graph exported by PlatformerReachabilityPlanner.

CRITICAL EDGE DETECTION — Lengauer-Tarjan Dominator Tree
---------------------------------------------------------
An edge (u -> v) is CRITICAL if removing it makes v unreachable from start.

This happens when BOTH conditions hold:
  1. idom(v) == u          — u is the immediate dominator of v
                             (last mandatory checkpoint before v)
  2. in-degree of v == 1   — no other edge enters v
     from outside u's      (no alternative path into v)
     dominator subtree

Algorithm: Lengauer-Tarjan (1979)  O(V + E * alpha(V)) — effectively linear.

Steps:
  1. DFS from start node, number nodes by discovery order
  2. Compute semi-dominators using path-compressed ancestor queries
  3. Derive immediate dominators from semi-dominators
  4. Build dominator tree, find critical edges

Nodes unreachable from start are ignored (they can never be visited anyway).

Action types (PlayerMovement.cs MoveActionType enum):
    0=None  1=Left  2=Right  3=Jump  4=DoubleJump  5=Dash

Usage:
    python visualize_graph_dominator.py graph.json
    python visualize_graph_dominator.py graph.json --max-edges 100000 --grid-res 0.5
    python visualize_graph_dominator.py graph.json --bridges-only
    python visualize_graph_dominator.py graph.json --action-filter 4    # DoubleJump edges only
    python visualize_graph_dominator.py graph.json --no-gui --output out.png
    python visualize_graph_dominator.py graph.json --min-visits 2       # filter noise
    python visualize_graph_dominator.py graph.json --start-id 0         # override start node
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
CRITICAL_C = "#ff2d55"
GROUND_C   = "#3399ff"
AIRJ_C     = "#ffaa00"


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
# Lengauer-Tarjan Dominator Tree
# ---------------------------------------------------------------------------

def lengauer_tarjan(node_ids, edges_raw, start_id):
    """
    Computes the immediate dominator of every node reachable from start_id.

    Returns:
        idom      : dict  node -> immediate dominator node  (start has no entry)
        dfs_order : list  nodes in DFS discovery order (reachable only)

    Implementation follows the original 1979 paper with iterative DFS
    (no recursion limit issues) and union-find path compression.
    """

    # --- Build forward adjacency (directed) ---------------------------------
    print("[dom] Building adjacency ...")
    fwd = defaultdict(list)   # node -> [successor, ...]
    for e in edges_raw:
        u, v = e["fromId"], e["toId"]
        if u == v or u not in node_ids or v not in node_ids:
            continue
        fwd[u].append(v)

    # --- Step 1: DFS from start, assign DFS numbers -------------------------
    print("[dom] DFS from start ...")
    # dfs_num[node]    = DFS discovery index (0 = start)
    # dfs_order[i]     = node with DFS index i
    # parent_dfs[node] = DFS parent node
    dfs_num   = {}
    dfs_order = []
    parent_dfs = {}

    stack = [(start_id, None, 0)]          # (node, parent, adj_ptr)
    while stack:
        u, par, ptr = stack[-1]
        if u not in dfs_num:
            dfs_num[u]   = len(dfs_order)
            dfs_order.append(u)
            if par is not None:
                parent_dfs[u] = par
        neighbors = fwd[u]
        if ptr < len(neighbors):
            stack[-1] = (u, par, ptr + 1)
            v = neighbors[ptr]
            if v not in dfs_num:
                stack.append((v, u, 0))
        else:
            stack.pop()

    n = len(dfs_order)
    print(f"[dom] {n:,} nodes reachable from start.")

    # Map node id -> DFS index and back for array-based operations
    idx_of = dfs_num                        # node  -> int index
    node_of = dfs_order                     # index -> node

    # --- Step 2: Semi-dominators (Lengauer-Tarjan) --------------------------
    # sdom[i] = DFS index of semi-dominator of node_of[i]
    sdom   = list(range(n))                 # initially each node is its own sdom
    idom_i = [0] * n                        # immediate dominator indices (filled later)

    # Union-Find with path compression for ancestor queries
    ancestor = list(range(n))              # forest for path compression
    best     = list(range(n))              # node with min sdom on path to root

    def link(v, w):
        ancestor[w] = v

    def eval_node(v):
        """Return the node with minimum sdom on the path from v to its root."""
        # Path compression
        if ancestor[v] == v:
            return v
        # Compress path
        path = []
        u = v
        while ancestor[u] != u:
            path.append(u)
            u = ancestor[u]
        # u is now the root
        # Update best values along the path
        min_best = u
        for node in reversed(path):
            if sdom[best[node]] >= sdom[best[min_best]]:
                # min_best is better (lower sdom), keep
                pass
            else:
                min_best = best[node]
            ancestor[node] = u
        return best[v] if sdom[best[v]] <= sdom[v] else v

    # Build reverse adjacency restricted to reachable nodes
    rev_reachable = defaultdict(list)      # v -> [u] (predecessors of v in reachable subgraph)
    for e in edges_raw:
        u, v = e["fromId"], e["toId"]
        if u == v:
            continue
        if u not in idx_of or v not in idx_of:
            continue
        rev_reachable[v].append(u)

    # Bucket: sdom[w] -> [w, ...] nodes whose sdom needs processing
    bucket = defaultdict(list)

    # Process nodes in reverse DFS order (skip start = index 0)
    print("[dom] Computing semi-dominators and immediate dominators ...")
    for i in range(n - 1, 0, -1):
        w = node_of[i]
        wi = i

        # Compute sdom(w): min DFS# of all vertices u such that there is a path
        # u -> v1 -> ... -> vk -> w where each vj has DFS# > DFS#(w)
        for u in rev_reachable[w]:
            ui = idx_of[u]
            if ui <= wi:
                # u is an ancestor of w in DFS tree — sdom candidate is u itself
                candidate = ui
            else:
                # u is a descendant — use eval to find min sdom on path to root
                ev = eval_node(ui)
                candidate = sdom[ev]
            if candidate < sdom[wi]:
                sdom[wi] = candidate

        # Add w to bucket of its semi-dominator
        bucket[sdom[wi]].append(wi)

        # Link w to its DFS parent
        par_w = parent_dfs.get(w)
        if par_w is not None:
            link(idx_of[par_w], wi)

        # Process bucket of parent: compute idom candidates
        par_wi = idx_of[par_w] if par_w is not None else 0
        for v in bucket[par_wi]:
            u = eval_node(v)
            if sdom[u] < sdom[v]:
                idom_i[v] = u        # idom is u (will be confirmed later)
            else:
                idom_i[v] = par_wi   # idom is the DFS parent
        bucket[par_wi].clear()

    # Final pass: resolve deferred idom entries
    for i in range(1, n):
        if idom_i[i] != sdom[i]:
            idom_i[i] = idom_i[idom_i[i]]

    # Convert index-based idom back to node ids
    idom = {}
    for i in range(1, n):
        idom[node_of[i]] = node_of[idom_i[i]]

    return idom, dfs_order


# ---------------------------------------------------------------------------
# Critical edge detection
# ---------------------------------------------------------------------------

def find_critical_edges(node_ids, edges_raw, idom, reachable_set):
    """
    Edge (u -> v) is critical iff:
      1. idom[v] == u            (u is the immediate dominator of v)
      2. v has no other incoming edge from outside idom's subtree

    Practically: count how many edges enter v from nodes that are NOT
    in the dominator subtree of u (i.e. not dominated by u).
    If that count is 0, the edge is critical.

    We approximate "outside u's subtree" as: any predecessor of v
    where the predecessor is NOT dominated by u (i.e. u does not appear
    on every path from start to that predecessor).
    """
    print("[critical] Finding critical edges ...")

    # Build: for each node v, list of all predecessor node ids
    preds = defaultdict(set)
    edge_indices_into = defaultdict(list)   # v -> [(edge_index, u), ...]

    for i, e in enumerate(edges_raw):
        u, v = e["fromId"], e["toId"]
        if u == v:
            continue
        if u not in reachable_set or v not in reachable_set:
            continue
        preds[v].add(u)
        edge_indices_into[v].append((i, u))

    # Build dominator ancestry: dom_ancestors[v] = set of all dominators of v
    # (all nodes on the path from v to start in the dominator tree)
    # We do this lazily with memoization.
    dom_path_cache = {}

    def dom_ancestors(v):
        if v in dom_path_cache:
            return dom_path_cache[v]
        ancestors = set()
        cur = v
        while cur in idom:
            cur = idom[cur]
            ancestors.add(cur)
        dom_path_cache[v] = ancestors
        return ancestors

    critical_set = set()

    for v, incoming in edge_indices_into.items():
        if v not in idom:
            continue                         # start node has no idom
        u = idom[v]                          # immediate dominator of v

        if len(incoming) == 1:
            # Only one edge enters v — it must be critical
            critical_set.add(incoming[0][0])
            continue

        # Multiple edges enter v.
        # Count edges coming from outside u's dominator subtree.
        # A predecessor p is "inside u's subtree" if u dominates p,
        # i.e. u is in dom_ancestors(p).
        outside_count = 0
        for edge_idx, p in incoming:
            if p == u:
                continue                     # this is the idom edge itself
            if u not in dom_ancestors(p):
                outside_count += 1
                break                        # no need to count further

        if outside_count == 0:
            # All other predecessors are inside u's subtree —
            # the idom edge is the only "real" entry from outside
            for edge_idx, p in incoming:
                if p == u:
                    critical_set.add(edge_idx)
                    break

    print(f"[critical] {len(critical_set)} critical edge(s) found.")
    return critical_set


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

    # Determine start node
    start_id = args.start_id
    if start_id is None:
        # Default: node with id=0, or the first node in the list
        start_id = 0 if 0 in node_by_id else next(iter(node_by_id))
    if start_id not in node_by_id:
        print(f"[error] Start node {start_id} not found. Available: {list(node_by_id)[:5]} ...")
        return
    print(f"[start] Using node {start_id} as start  "
          f"pos=({node_by_id[start_id]['pos']['x']:.2f}, {node_by_id[start_id]['pos']['y']:.2f})")

    # --- Dominator tree + critical edges ------------------------------------
    idom, dfs_order = lengauer_tarjan(node_ids, edges_raw, start_id)
    reachable_set   = set(dfs_order)
    critical_idx    = find_critical_edges(node_ids, edges_raw, idom, reachable_set)

    critical_edges = [edges_raw[i] for i in critical_idx]
    normal_edges   = [
        e for i, e in enumerate(edges_raw)
        if i not in critical_idx
        and e["fromId"] != e["toId"]
        and e["fromId"] in reachable_set
        and e["toId"]   in reachable_set
    ]

    if args.action_filter is not None:
        normal_edges = [e for e in normal_edges if e.get("action") == args.action_filter]
        aname = ACTION_NAMES.get(args.action_filter, str(args.action_filter))
        print(f"[filter] {len(normal_edges):,} normal edges  (action={aname})")

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

    cnids = set()
    for e in critical_edges:
        cnids.add(e["fromId"])
        cnids.add(e["toId"])
    cn  = [node_by_id[i] for i in cnids if i in node_by_id]
    cx  = [n["pos"]["x"] for n in cn]
    cy_ = [n["pos"]["y"] for n in cn]

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

    # 3. Critical edges
    csegs = to_segments(critical_edges, node_by_id)
    lc_c  = LineCollection(csegs, colors=CRITICAL_C, linewidths=2.2, alpha=0.95, zorder=5)
    ax_main.add_collection(lc_c)

    # 4. Grounded nodes
    sc_g  = ax_main.scatter(axs[grnd], ays[grnd], s=1.2, c=GROUND_C,
                            alpha=0.35, linewidths=0, zorder=3)

    # 5. Air-jump nodes
    sc_a  = ax_main.scatter(axs[airj], ays[airj], s=2.0, c=AIRJ_C,
                            alpha=0.55, linewidths=0, zorder=4)

    # 6. Critical edge node markers
    sc_cn = ax_main.scatter(cx, cy_, s=22, c=CRITICAL_C, alpha=0.9,
                            linewidths=0.6, edgecolors="#ffffff", zorder=6)

    # 7. Critical edge labels
    ctexts = []
    if len(critical_edges) <= 60:
        for e in critical_edges:
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
                bbox=dict(boxstyle="round,pad=0.15", fc="#1a0008",
                          ec=CRITICAL_C, lw=0.6, alpha=0.88),
            )
            ctexts.append(t)

    # --- Axes styling -------------------------------------------------------
    ax_main.set_aspect("equal")
    ax_main.autoscale_view()
    ax_main.set_xlabel("world X", color=DIM, fontsize=9)
    ax_main.set_ylabel("world Y", color=DIM, fontsize=9)
    ax_main.tick_params(colors=DIM, labelsize=8)
    for sp in ax_main.spines.values():
        sp.set_color(BORDER)

    n_unreachable = len(node_ids) - len(reachable_set)
    ax_main.set_title(
        f"Platformer Reachability Graph  |  "
        f"{len(reachable_set):,} reachable nodes   "
        f"{len(edges_raw):,} edges   "
        f"{len(critical_idx)} critical edge(s)   "
        f"start={start_id}   "
        f"maxAirJumps={meta.get('maxAirJumps', '?')}"
        + (f"   [{n_unreachable:,} unreachable]" if n_unreachable else ""),
        color=TEXT, fontsize=10, pad=8,
    )

    cb = fig.colorbar(im, ax=ax_main, fraction=0.018, pad=0.01)
    cb.set_label("log(visit density)", color=DIM, fontsize=8)
    cb.ax.yaxis.set_tick_params(color=DIM, labelcolor=DIM, labelsize=7)
    cb.outline.set_edgecolor(BORDER)

    patches = (
        [mpatches.Patch(color=c, label=f"[{k}] {ACTION_NAMES[k]}")
         for k, c in sorted(ACTION_COLORS.items()) if k in ACTION_NAMES]
        + [mpatches.Patch(color=CRITICAL_C, label="Critical edge"),
           mpatches.Patch(color=GROUND_C,   label="Grounded node"),
           mpatches.Patch(color=AIRJ_C,     label="Air-jump used")]
    )
    ax_main.legend(handles=patches, loc="lower left", fontsize=7,
                   facecolor=PANEL, edgecolor=BORDER, labelcolor=TEXT, framealpha=0.92)

    # Stats box
    by_act = defaultdict(int)
    for e in critical_edges:
        by_act[e.get("action", -1)] += 1
    slines = [f"Critical: {len(critical_idx)}", "-" * 24]
    for act, cnt in sorted(by_act.items()):
        slines.append(f"  [{act}] {ACTION_NAMES.get(act, '?'):12s} {cnt:>4}")
    slines += [
        "-" * 24,
        f"Reachable: {len(reachable_set):>9,}",
        f"Edges    : {len(edges_raw):>9,}",
        f"Sampled  : {len(nsegs):>9,}",
    ]
    ax_main.text(
        0.995, 0.995, "\n".join(slines),
        transform=ax_main.transAxes, fontsize=7.5, color=TEXT,
        va="top", ha="right", fontfamily="monospace",
        bbox=dict(boxstyle="round,pad=0.5", fc=PANEL, ec=BORDER, lw=0.8, alpha=0.92),
        zorder=10,
    )

    # --- Overlay toggles ----------------------------------------------------
    layer_labels  = ["Heatmap", "Normal edges", "Critical edges",
                     "Grounded nodes", "Air-jump nodes", "Critical nodes", "Edge labels"]
    layer_artists = [im, lc_n, lc_c, sc_g, sc_a, sc_cn, ctexts]
    ax_chk.set_title("Overlays", color=TEXT, fontsize=9, pad=6)
    chk = CheckButtons(ax_chk, layer_labels, [True] * len(layer_labels))

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
    p.add_argument("--start-id", type=int, default=None,
                   help="Start node id for dominator tree (default: node with id=0)")
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
                   help="Skip normal-edge sampling, show critical edges only (fastest)")
    args = p.parse_args()
    if args.bridges_only:
        args.max_edges = 0
    visualize(args)


if __name__ == "__main__":
    main()