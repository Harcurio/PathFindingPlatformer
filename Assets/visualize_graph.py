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

ACTION_NAMES  = {0:"None",1:"Left",2:"Right",3:"Jump",4:"DoubleJump",5:"Dash"}
ACTION_COLORS = {0:"#555566",1:"#4488ff",2:"#44ddff",3:"#44ff88",4:"#ffaa00",5:"#ff44ff"}


def load_graph(path, min_visits=0):
    size_mb = os.path.getsize(path)/1_048_576
    print(f"[load] {path}  ({size_mb:.1f} MB) ...")
    with open(path,"r",encoding="utf-8") as f:
        data = json.load(f)
    nodes_raw = data.get("nodes",[])
    edges_raw = data.get("edges",[])
    meta = {k:v for k,v in data.items() if k not in ("nodes","edges")}
    print(f"[load] {len(nodes_raw):,} nodes, {len(edges_raw):,} edges")
    if min_visits > 0:
        nodes_raw = [n for n in nodes_raw if n.get("visits",0) >= min_visits]
        print(f"[load] After visits>={min_visits}: {len(nodes_raw):,} nodes")
    node_by_id = {n["id"]:n for n in nodes_raw}
    return set(node_by_id), node_by_id, edges_raw, meta


def find_bridges(node_ids, node_by_id, edges_raw):
    print("[tarjan] Building adjacency ...")
    adj = defaultdict(list)
    for i,e in enumerate(edges_raw):
        u,v = e["fromId"],e["toId"]
        if u==v or u not in node_ids or v not in node_ids: continue
        adj[u].append((v,i)); adj[v].append((u,i))
    print(f"[tarjan] Running on {len(node_ids):,} nodes ...")
    disc,low = {},{}
    timer=[0]; bset=set()
    for start in node_ids:
        if start in disc: continue
        disc[start]=low[start]=timer[0]; timer[0]+=1
        stack=[(start,-1,0)]
        while stack:
            u,pe,ix = stack[-1]
            nb = adj[u]
            if ix < len(nb):
                stack[-1]=(u,pe,ix+1)
                v,ei = nb[ix]
                if ei==pe: continue
                if v in disc: low[u]=min(low[u],disc[v])
                else:
                    disc[v]=low[v]=timer[0]; timer[0]+=1
                    stack.append((v,ei,0))
            else:
                stack.pop()
                if stack:
                    p=stack[-1][0]; low[p]=min(low[p],low[u])
                    if low[u]>disc[p]: bset.add(pe)
    print(f"[tarjan] {len(bset)} bridge(s) found.")
    return bset


def build_heatmap(node_by_id, grid_res):
    xs=np.array([n["pos"]["x"] for n in node_by_id.values()],dtype=np.float32)
    ys=np.array([n["pos"]["y"] for n in node_by_id.values()],dtype=np.float32)
    ws=np.array([max(1,n.get("visits",1)) for n in node_by_id.values()],dtype=np.float32)
    x0,x1=float(xs.min()),float(xs.max()); y0,y1=float(ys.min()),float(ys.max())
    nx=max(4,int((x1-x0)/grid_res)+1); ny=max(4,int((y1-y0)/grid_res)+1)
    print(f"[heatmap] Grid {nx}x{ny} cells")
    xi=np.clip(((xs-x0)/grid_res).astype(np.int32),0,nx-1)
    yi=np.clip(((ys-y0)/grid_res).astype(np.int32),0,ny-1)
    hmap=np.zeros((ny,nx),dtype=np.float32)
    np.add.at(hmap,(yi,xi),ws)
    return hmap, np.linspace(x0,x1,nx+1), np.linspace(y0,y1,ny+1)


def edges_to_segs(elist, node_by_id):
    segs=[]
    for e in elist:
        a=node_by_id.get(e["fromId"]); b=node_by_id.get(e["toId"])
        if a and b: segs.append([(a["pos"]["x"],a["pos"]["y"]),(b["pos"]["x"],b["pos"]["y"])])
    return segs


def visualize(args):
    node_ids,node_by_id,edges_raw,meta = load_graph(args.json_file,args.min_visits)

    bridge_idx = find_bridges(node_ids,node_by_id,edges_raw)
    bridge_edges=[edges_raw[i] for i in bridge_idx]
    normal_edges=[e for i,e in enumerate(edges_raw)
                  if i not in bridge_idx and e["fromId"]!=e["toId"]
                  and e["fromId"] in node_ids and e["toId"] in node_ids]

    if args.action_filter is not None:
        normal_edges=[e for e in normal_edges if e.get("action")==args.action_filter]
        print(f"[filter] {len(normal_edges):,} normal edges after action filter")

    if len(normal_edges) > args.max_edges:
        print(f"[sample] Sampling {args.max_edges:,}/{len(normal_edges):,} normal edges")
        normal_edges=random.sample(normal_edges,args.max_edges)

    print("[arrays] Building node arrays ...")
    all_nodes=list(node_by_id.values())
    axs=np.array([n["pos"]["x"] for n in all_nodes],dtype=np.float32)
    ays=np.array([n["pos"]["y"] for n in all_nodes],dtype=np.float32)
    grnd=np.array([n.get("grounded",False) for n in all_nodes])
    airj=np.array([n.get("airJumpsUsed",0)>0 for n in all_nodes])

    hmap,xe,ye=build_heatmap(node_by_id,args.grid_res)
    hlog=np.log1p(hmap)

    bnids=set()
    for e in bridge_edges: bnids.add(e["fromId"]); bnids.add(e["toId"])
    bn=[node_by_id[i] for i in bnids if i in node_by_id]
    bx=[n["pos"]["x"] for n in bn]; bby=[n["pos"]["y"] for n in bn]

    print("[plot] Building figure ...")
    fig=plt.figure(figsize=(18,10),facecolor="#0a0c10")
    try: fig.canvas.manager.set_window_title("Platformer Graph Visualizer")
    except: pass

    ax =fig.add_axes([0.01,0.05,0.73,0.90])
    axc=fig.add_axes([0.77,0.22,0.21,0.60])
    ax.set_facecolor("#0a0c10")
    axc.set_facecolor("#0f1318")
    for sp in axc.spines.values(): sp.set_color("#1e2a3a")

    cmap=matplotlib.colormaps.get_cmap("inferno").copy()
    cmap.set_under("#0a0c10")
    im=ax.pcolormesh(xe,ye,hlog,cmap=cmap,vmin=0.01,shading="flat",rasterized=True,zorder=1)

    nsegs=edges_to_segs(normal_edges,node_by_id)
    ncols=[ACTION_COLORS.get(e.get("action",0),"#555566") for e in normal_edges
           if node_by_id.get(e["fromId"]) and node_by_id.get(e["toId"])]
    lc_n=LineCollection(nsegs,colors=ncols,linewidths=0.4,alpha=0.25,zorder=2)
    ax.add_collection(lc_n)

    bsegs=edges_to_segs(bridge_edges,node_by_id)
    lc_b=LineCollection(bsegs,colors="#ff2d55",linewidths=2.2,alpha=0.95,zorder=5)
    ax.add_collection(lc_b)

    sc_g=ax.scatter(axs[grnd],ays[grnd],s=1.2,c="#3399ff",alpha=0.35,linewidths=0,zorder=3)
    sc_a=ax.scatter(axs[airj],ays[airj],s=2.0,c="#ffaa00",alpha=0.55,linewidths=0,zorder=4)
    sc_bn=ax.scatter(bx,bby,s=22,c="#ff2d55",alpha=0.9,linewidths=0.6,edgecolors="#ffffff",zorder=6)

    btexts=[]
    if len(bridge_edges)<=40:
        for e in bridge_edges:
            a=node_by_id.get(e["fromId"]); b=node_by_id.get(e["toId"])
            if not a or not b: continue
            mx=(a["pos"]["x"]+b["pos"]["x"])/2; my=(a["pos"]["y"]+b["pos"]["y"])/2
            nm=ACTION_NAMES.get(e.get("action",-1),f"A{e.get('action')}")
            t=ax.text(mx,my,nm,fontsize=6.5,color="#ff8899",fontweight="bold",
                      ha="center",va="bottom",zorder=7,
                      bbox=dict(boxstyle="round,pad=0.15",fc="#1a0008",ec="#ff2d55",lw=0.6,alpha=0.88))
            btexts.append(t)

    ax.set_aspect("equal"); ax.autoscale_view()
    ax.set_xlabel("world X",color="#4a6070",fontsize=9)
    ax.set_ylabel("world Y",color="#4a6070",fontsize=9)
    ax.tick_params(colors="#4a6070",labelsize=8)
    for sp in ax.spines.values(): sp.set_color("#1e2a3a")
    ax.set_title(
        f"Platformer Reachability Graph  |  {len(node_ids):,} nodes  "
        f"{len(edges_raw):,} edges  {len(bridge_idx)} bridge(s)  "
        f"[maxAirJumps={meta.get('maxAirJumps','?')}]",
        color="#c8d8e8",fontsize=10,pad=8)

    cb=fig.colorbar(im,ax=ax,fraction=0.018,pad=0.01)
    cb.set_label("log(visit density)",color="#4a6070",fontsize=8)
    cb.ax.yaxis.set_tick_params(color="#4a6070",labelcolor="#4a6070",labelsize=7)
    cb.outline.set_edgecolor("#1e2a3a")

    apatches=[mpatches.Patch(color=c,label=f"[{k}] {ACTION_NAMES[k]}") for k,c in sorted(ACTION_COLORS.items()) if k in ACTION_NAMES]
    ax.legend(handles=apatches+[
        mpatches.Patch(color="#ff2d55",label="Bridge (critical)"),
        mpatches.Patch(color="#3399ff",label="Grounded node"),
        mpatches.Patch(color="#ffaa00",label="Air-jump used"),
    ],loc="lower left",fontsize=7,facecolor="#0f1318",edgecolor="#1e2a3a",labelcolor="#c8d8e8",framealpha=0.92)

    by_action=defaultdict(int)
    for e in bridge_edges: by_action[e.get("action",-1)]+=1
    slines=[f"Bridges : {len(bridge_idx)}","-"*24]
    for act,cnt in sorted(by_action.items()): slines.append(f"  [{act}] {ACTION_NAMES.get(act,'?'):12s} {cnt:>4}")
    slines+=["-"*24,f"Nodes   : {len(node_ids):>10,}",f"Edges   : {len(edges_raw):>10,}",f"Sampled : {len(nsegs):>10,}"]
    ax.text(0.995,0.995,"\n".join(slines),transform=ax.transAxes,fontsize=7.5,color="#c8d8e8",
            va="top",ha="right",fontfamily="monospace",
            bbox=dict(boxstyle="round,pad=0.5",fc="#0f1318",ec="#1e2a3a",lw=0.8,alpha=0.92),zorder=10)

    lbls=["Heatmap","Normal edges","Bridge edges","Grounded nodes","Air-jump nodes","Bridge nodes","Bridge labels"]
    arts=[im,lc_n,lc_b,sc_g,sc_a,sc_bn,btexts]
    axc.set_title("Overlays",color="#c8d8e8",fontsize=9,pad=6)
    chk=CheckButtons(axc,lbls,[True]*len(lbls))
    _boxes = getattr(chk, "patches", None) or getattr(chk, "rectangles", [])
    for r in _boxes:
        try: r.set_edgecolor("#1e2a3a"); r.set_facecolor("#0a0c10")
        except Exception: pass
    for l in chk.labels: l.set_color("#c8d8e8"); l.set_fontsize(8)

    def toggle(lbl):
        idx=lbls.index(lbl); art=arts[idx]
        if isinstance(art,list):
            for t in art: t.set_visible(not t.get_visible())
        else: art.set_visible(not art.get_visible())
        fig.canvas.draw_idle()
    chk.on_clicked(toggle)

    if args.no_gui:
        out=args.output or "graph_viz.png"
        fig.savefig(out,dpi=150,facecolor=fig.get_facecolor(),bbox_inches="tight")
        print(f"[output] Saved to {out}")
    else:
        print("[plot] Opening interactive window ...")
        print("       Pan/zoom with mouse | Toggle overlays with checkboxes")
        plt.show()


def main():
    p=argparse.ArgumentParser(description="Visualize a platformer reachability graph JSON.")
    p.add_argument("json_file")
    p.add_argument("--max-edges",type=int,default=50_000)
    p.add_argument("--grid-res",type=float,default=0.25)
    p.add_argument("--min-visits",type=int,default=0)
    p.add_argument("--no-gui",action="store_true")
    p.add_argument("--output",type=str,default=None)
    p.add_argument("--action-filter",type=int,default=None)
    p.add_argument("--bridges-only",action="store_true")
    args=p.parse_args()
    if args.bridges_only: args.max_edges=0
    visualize(args)

if __name__=="__main__":
    main()