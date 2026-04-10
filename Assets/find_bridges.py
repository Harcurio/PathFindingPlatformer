"""
find_bridges.py
---------------
Finds bridge edges in a platformer reachability graph exported by
PlatformerReachabilityPlanner (StateGraphExport JSON format).

A "bridge" is an edge whose removal disconnects the graph — meaning
the mechanic on that edge (e.g. DoubleJump) is strictly required to
reach part of the level.

MoveActionType enum (from PlayerMovement.cs):
    0 = None
    1 = Left
    2 = Right
    3 = Jump
    4 = DoubleJump
    (5 = Dash, if ever uncommented)

Usage:
    python find_bridges.py <path_to_graph.json> [options]

Options:
    --start-id INT      Override the start node id (default: 0)
    --min-visits INT    Only consider nodes with visits >= N (default: 0)
    --action INT/NAME   Filter bridges to only those using a specific action
                        e.g. --action 4  or  --action DoubleJump
    --output FILE       Write results to a JSON file
    --verbose           Print extra info about each bridge partition
"""

import argparse
import json
import sys
import os
from collections import defaultdict, deque
from typing import Optional

# ── Action enum from PlayerMovement.cs ────────────────────────────────────────

ACTION_NAMES = {
    0: "None",
    1: "Left",
    2: "Right",
    3: "Jump",
    4: "DoubleJump",
    5: "Dash",       # uncommented in the future
}

def action_name(action_int: int) -> str:
    return ACTION_NAMES.get(action_int, f"Action({action_int})")

def parse_action_filter(value: str) -> Optional[int]:
    """Accept either an int string '4' or a name 'DoubleJump'."""
    if value is None:
        return None
    try:
        return int(value)
    except ValueError:
        inv = {v.lower(): k for k, v in ACTION_NAMES.items()}
        key = value.strip().lower()
        if key in inv:
            return inv[key]
        raise ValueError(f"Unknown action '{value}'. "
                         f"Use an int or one of: {list(ACTION_NAMES.values())}")

# ── Graph loading ──────────────────────────────────────────────────────────────

def load_graph(path: str, min_visits: int = 0):
    """
    Load the JSON and return:
        node_ids  : set of all node ids present
        adj       : dict[node_id] -> list of (neighbor_id, edge_index)
                    (undirected: each edge appears in both directions)
        edges     : list of raw edge dicts
        meta      : top-level metadata dict
    """
    print(f"[load] Reading {path} …")
    file_size = os.path.getsize(path)
    print(f"[load] File size: {file_size / 1_048_576:.1f} MB")

    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    print(f"[load] JSON parsed.")

    nodes_raw = data.get("nodes", [])
    edges_raw = data.get("edges", [])

    print(f"[load] {len(nodes_raw):,} nodes, {len(edges_raw):,} edges")

    # Build active node set (optionally filtered by visits)
    if min_visits > 0:
        active_nodes = {
            n["id"] for n in nodes_raw if n.get("visits", 0) >= min_visits
        }
        print(f"[load] After visits>={min_visits} filter: {len(active_nodes):,} nodes")
    else:
        active_nodes = {n["id"] for n in nodes_raw}

    # Build adjacency list (undirected for bridge detection)
    # Multiple parallel edges between the same pair are kept as separate entries
    # so that Tarjan's algorithm can correctly track the parent edge by index
    # rather than by node id (avoids false bridges on multigraphs).
    adj = defaultdict(list)  # node_id -> [(neighbor_id, edge_index)]

    for i, e in enumerate(edges_raw):
        u, v = e["fromId"], e["toId"]
        if u == v:
            continue  # self-loops are never bridges
        if u not in active_nodes or v not in active_nodes:
            continue
        adj[u].append((v, i))
        adj[v].append((u, i))

    meta = {k: v for k, v in data.items() if k not in ("nodes", "edges")}

    # Attach a lookup for node data
    node_by_id = {n["id"]: n for n in nodes_raw}

    return active_nodes, adj, edges_raw, meta, node_by_id


# ── Tarjan's bridge algorithm (iterative, safe for large graphs) ───────────────

def find_bridges_tarjan(node_ids, adj):
    """
    Iterative DFS-based bridge detection (Tarjan 1974).

    Returns a set of edge indices (into the original edges list) that are bridges.

    We track parent by *edge index* (not node id) so that multi-edges
    between the same pair of nodes are handled correctly — two parallel edges
    between u and v mean neither is a bridge, and this handles that.
    """
    disc  = {}   # node -> discovery time
    low   = {}   # node -> lowest discovery time reachable
    timer = [0]
    bridge_edge_indices = set()

    # Iterative DFS to avoid Python recursion limit on large graphs
    for start in node_ids:
        if start in disc:
            continue

        # Stack entries: (node, parent_edge_index, adj_iterator_index)
        stack = [(start, -1, 0)]
        disc[start] = low[start] = timer[0]
        timer[0] += 1

        while stack:
            u, parent_edge_idx, adj_idx = stack[-1]

            neighbors = adj[u]

            if adj_idx < len(neighbors):
                # Advance iterator
                stack[-1] = (u, parent_edge_idx, adj_idx + 1)

                v, edge_idx = neighbors[adj_idx]

                if edge_idx == parent_edge_idx:
                    # This is the exact edge we came from — skip it
                    # (handles simple graphs; multi-edge case handled below)
                    continue

                if v in disc:
                    # Back edge: update low
                    low[u] = min(low[u], disc[v])
                else:
                    # Tree edge: push child
                    disc[v] = low[v] = timer[0]
                    timer[0] += 1
                    stack.append((v, edge_idx, 0))
            else:
                # Done with u — pop and update parent's low
                stack.pop()
                if stack:
                    parent_node = stack[-1][0]
                    low[parent_node] = min(low[parent_node], low[u])

                    # Bridge condition
                    if low[u] > disc[parent_node]:
                        bridge_edge_indices.add(parent_edge_idx)

    return bridge_edge_indices


# ── Partition helper ───────────────────────────────────────────────────────────

def get_partition(edge_idx, adj, node_ids, edges_raw):
    """
    BFS from edge.fromId excluding edge_idx.
    Returns (group_A, group_B) as sets of node ids.
    """
    e = edges_raw[edge_idx]
    start = e["fromId"]

    visited = set()
    q = deque([start])
    visited.add(start)

    while q:
        u = q.popleft()
        for v, idx in adj[u]:
            if idx == edge_idx:
                continue
            if v not in visited:
                visited.add(v)
                q.append(v)

    group_a = visited & node_ids
    group_b = node_ids - visited
    return group_a, group_b


# ── Reporting ──────────────────────────────────────────────────────────────────

def summarize_partition(group, node_by_id, max_show=8):
    """Return a short readable summary of a node group."""
    positions = []
    for nid in sorted(group):
        n = node_by_id.get(nid)
        if n:
            p = n.get("pos", {})
            positions.append((p.get("x", 0), p.get("y", 0)))

    if not positions:
        return "(empty)"

    xs = [p[0] for p in positions]
    ys = [p[1] for p in positions]
    x_min, x_max = min(xs), max(xs)
    y_min, y_max = min(ys), max(ys)
    return (f"{len(group):,} nodes | "
            f"x=[{x_min:.2f}, {x_max:.2f}] "
            f"y=[{y_min:.2f}, {y_max:.2f}]")


def print_results(bridge_indices, edges_raw, adj, node_ids, node_by_id,
                  action_filter=None, verbose=False):
    if not bridge_indices:
        print("\n✓  No bridges found — the graph is 2-edge-connected.")
        print("   No single mechanic is strictly required to reach any area.")
        return

    # Group bridges by action type
    by_action = defaultdict(list)
    for idx in bridge_indices:
        e = edges_raw[idx]
        a = e.get("action", -1)
        by_action[a].append(idx)

    print(f"\n{'='*60}")
    print(f"  BRIDGE EDGES FOUND: {len(bridge_indices)}")
    print(f"{'='*60}")

    for action_int in sorted(by_action):
        if action_filter is not None and action_int != action_filter:
            continue

        name = action_name(action_int)
        indices = by_action[action_int]
        print(f"\n  Action [{action_int}] {name}  —  {len(indices)} bridge(s)")
        print(f"  {'─'*50}")

        for idx in indices:
            e = edges_raw[idx]
            cost = e.get("costSeconds", 0)
            count = e.get("count", 0)
            killed = e.get("killedMask", 0)

            from_node = node_by_id.get(e["fromId"], {})
            to_node   = node_by_id.get(e["toId"],   {})
            fp = from_node.get("pos", {}); tp = to_node.get("pos", {})

            print(f"\n  Edge #{idx}")
            print(f"    {e['fromId']} ({fp.get('x',0):.3f}, {fp.get('y',0):.3f})"
                  f"  →[{name}]→  "
                  f"{e['toId']} ({tp.get('x',0):.3f}, {tp.get('y',0):.3f})")
            print(f"    cost={cost:.3f}s  count={count}"
                  + (f"  killedMask={killed:#010x}" if killed else ""))

            if verbose:
                ga, gb = get_partition(idx, adj, node_ids, edges_raw)
                print(f"    Group A (reachable without): {summarize_partition(ga, node_by_id)}")
                print(f"    Group B (isolated):          {summarize_partition(gb, node_by_id)}")

    print(f"\n{'='*60}")
    print("  SUMMARY BY ACTION")
    print(f"{'='*60}")
    for action_int in sorted(by_action):
        if action_filter is not None and action_int != action_filter:
            continue
        print(f"  [{action_int}] {action_name(action_int):15s}  {len(by_action[action_int]):>5} bridge(s)")


# ── Output ─────────────────────────────────────────────────────────────────────

def build_output(bridge_indices, edges_raw, adj, node_ids, node_by_id):
    """Build a JSON-serialisable results dict."""
    results = []
    for idx in sorted(bridge_indices):
        e = edges_raw[idx]
        ga, gb = get_partition(idx, adj, node_ids, edges_raw)

        from_node = node_by_id.get(e["fromId"], {})
        to_node   = node_by_id.get(e["toId"],   {})

        results.append({
            "edge_index":   idx,
            "fromId":       e["fromId"],
            "toId":         e["toId"],
            "action":       e.get("action", -1),
            "action_name":  action_name(e.get("action", -1)),
            "costSeconds":  e.get("costSeconds", 0),
            "count":        e.get("count", 0),
            "killedMask":   e.get("killedMask", 0),
            "from_pos":     from_node.get("pos", {}),
            "to_pos":       to_node.get("pos", {}),
            "partition_A_size": len(ga),
            "partition_B_size": len(gb),
        })
    return {"bridge_count": len(bridge_indices), "bridges": results}


# ── Main ───────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(
        description="Find bridge edges in a platformer reachability graph JSON."
    )
    parser.add_argument("json_file", help="Path to the exported graph JSON")
    parser.add_argument("--min-visits", type=int, default=0,
                        help="Ignore nodes with fewer than N visits (default: 0)")
    parser.add_argument("--action", type=str, default=None,
                        help="Filter output to a specific action (int or name, e.g. 4 or DoubleJump)")
    parser.add_argument("--output", type=str, default=None,
                        help="Write bridge results to this JSON file")
    parser.add_argument("--verbose", action="store_true",
                        help="Print partition summaries for each bridge")
    args = parser.parse_args()

    # Parse optional action filter
    action_filter = None
    if args.action:
        try:
            action_filter = parse_action_filter(args.action)
            print(f"[filter] Only showing bridges with action "
                  f"{action_filter} ({action_name(action_filter)})")
        except ValueError as ex:
            print(f"[error] {ex}")
            sys.exit(1)

    # Load
    node_ids, adj, edges_raw, meta, node_by_id = load_graph(
        args.json_file, min_visits=args.min_visits
    )

    print(f"\n[meta] version={meta.get('version')}  "
          f"maxAirJumps={meta.get('maxAirJumps')}  "
          f"maxTimeSeconds={meta.get('maxTimeSeconds')}  "
          f"maxExpansions={meta.get('maxExpansions')}")

    if not node_ids:
        print("[error] No nodes to analyse.")
        sys.exit(1)

    # Run Tarjan
    print(f"\n[tarjan] Running bridge detection on "
          f"{len(node_ids):,} nodes …")
    bridge_indices = find_bridges_tarjan(node_ids, adj)
    print(f"[tarjan] Done. {len(bridge_indices)} bridge(s) found.")

    # Print
    print_results(bridge_indices, edges_raw, adj, node_ids, node_by_id,
                  action_filter=action_filter,
                  verbose=args.verbose)

    # Save output JSON if requested
    if args.output:
        out = build_output(bridge_indices, edges_raw, adj, node_ids, node_by_id)
        with open(args.output, "w", encoding="utf-8") as f:
            json.dump(out, f, indent=2)
        print(f"\n[output] Results written to {args.output}")


if __name__ == "__main__":
    main()