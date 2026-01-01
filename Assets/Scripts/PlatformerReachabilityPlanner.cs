using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class PlatformerReachabilityPlanner : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [SerializeField] private Transform goal;

    private const float StepDt = 0.02f;
    private const int StepsPerAction = 6;
    [SerializeField] private int MaxExpansions = 20000;
    private const float GoalRadius = 0.35f;

    private const float PosQuant = 0.10f;
    private const float VelQuant = 0.50f;

    private readonly List<Vector2> explored = new List<Vector2>(50000);
    private readonly List<Vector2> lastPath = new List<Vector2>(512);

    [ContextMenu("Run A* (Reach Goal)")]
    public void RunAStar() => RunInternal(heuristicWeight: 1f);

    [ContextMenu("Run Dijkstra (Reachability Map)")]
    public void RunDijkstra() => RunInternal(heuristicWeight: 0f);

    private void RunInternal(float heuristicWeight)
    {
        if (player == null || goal == null)
        {
            Debug.LogError("Assign PlayerMovement and Goal Transform.");
            return;
        }

        explored.Clear();
        lastPath.Clear();

        PlayerMovement ghost;
        var saved = new PhysicsIsolationScope(player, out ghost);

        try
        {
            if (ghost == null)
            {
                Debug.LogError("Failed to create ghost.");
                return;
            }

            var startSnap = player.CaptureSnapshot();
            ghost.RestoreSnapshot(startSnap);

            bool reachable = Search(ghost, goal.position, heuristicWeight, lastPath);

            Debug.Log(reachable
                ? $"[Planner] Goal reachable: YES. Path points: {lastPath.Count}, explored: {explored.Count}"
                : $"[Planner] Goal reachable: NO. Explored: {explored.Count}");
        }
        finally
        {
            saved.Dispose();
        }
    }

    private bool Search(PlayerMovement ghost, Vector2 goalPos, float heuristicWeight, List<Vector2> path)
    {
        path.Clear();

        var open = new MinHeap(); //frontier
        var closed = new HashSet<Key>();
        var records = new Dictionary<Key, Record>(8192);

        var startSnap = ghost.CaptureSnapshot();
        var startKey = new Key(startSnap);

        records[startKey] = new Record { snapshot = startSnap, g = 0f, hasParent = false };
        open.Push(Heuristic(startSnap.position, goalPos, ghost) * heuristicWeight, startKey);

        int expansions = 0;

        while (open.Count > 0 && expansions < MaxExpansions)
        {
            var popped = open.Pop();
            var curKey = popped.key;

            if (closed.Contains(curKey)) continue;

            var curRec = records[curKey];
            var curSnap = curRec.snapshot;

            closed.Add(curKey);
            explored.Add(curSnap.position);

            if (Vector2.Distance(curSnap.position, goalPos) <= GoalRadius)
            {
                Reconstruct(records, curKey, path);
                return true;
            }

            expansions++;

            ghost.RestoreSnapshot(curSnap);
            var actions = ghost.GetCandidateActions();

            for (int i = 0; i < actions.Count; i++)
            {
                var nextSnap = SimulateAction(ghost, curSnap, actions[i]);
                var nextKey = new Key(nextSnap);

                if (closed.Contains(nextKey)) continue;

                float stepCost = StepDt * StepsPerAction;
                float tentativeG = curRec.g + stepCost;

                Record nextRec;
                if (!records.TryGetValue(nextKey, out nextRec) || tentativeG < nextRec.g)
                {
                    nextRec.snapshot = nextSnap;
                    nextRec.g = tentativeG;
                    nextRec.parent = curKey;
                    nextRec.hasParent = true;
                    records[nextKey] = nextRec;

                    float f = tentativeG + Heuristic(nextSnap.position, goalPos, ghost) * heuristicWeight;
                    open.Push(f, nextKey);
                }
            }
        }

        return false;
    }

    private PlayerMovement.MovementSnapshot SimulateAction(
        PlayerMovement ghost,
        PlayerMovement.MovementSnapshot from,
        PlayerMovement.MoveAction action)
    {
        ghost.RestoreSnapshot(from);
        ghost.ApplyAction(action);

        for (int i = 0; i < StepsPerAction; i++)
        {
            ghost.SimulateStep(StepDt);
            Physics2D.Simulate(StepDt);
        }

        return ghost.CaptureSnapshot();
    }

    private static float Heuristic(Vector2 pos, Vector2 goalPos, PlayerMovement ghost)
    {
        float dx = Mathf.Abs(goalPos.x - pos.x);
        float speed = Mathf.Max(0.01f, ghost.moveSpeed);
        return dx / speed;
    }

    private static void Reconstruct(Dictionary<Key, Record> records, Key goalKey, List<Vector2> path)
    {
        path.Clear();
        var k = goalKey;

        int safety = 0;
        while (records.TryGetValue(k, out var rec))
        {
            path.Add(rec.snapshot.position);
            if (!rec.hasParent) break;

            k = rec.parent;
            if (++safety > 200000) break;
        }

        path.Reverse();
    }

    // -------------------------- Keying / Records --------------------------

    private struct Key : IEquatable<Key>
    {
        private readonly int qx, qy, qvx, qvy, qg;

        public Key(PlayerMovement.MovementSnapshot s)
        {
            qx = Quant(s.position.x, PosQuant);
            qy = Quant(s.position.y, PosQuant);
            qvx = Quant(s.velocity.x, VelQuant);
            qvy = Quant(s.velocity.y, VelQuant);
            qg = s.isGrounded ? 1 : 0;
        }

        private static int Quant(float v, float q) => Mathf.RoundToInt(v / q);

        public bool Equals(Key other) =>
            qx == other.qx && qy == other.qy && qvx == other.qvx && qvy == other.qvy && qg == other.qg;

        public override bool Equals(object obj) => obj is Key other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = qx;
                h = (h * 397) ^ qy;
                h = (h * 397) ^ qvx;
                h = (h * 397) ^ qvy;
                h = (h * 397) ^ qg;
                return h;
            }
        }
    }

    private struct Record
    {
        public PlayerMovement.MovementSnapshot snapshot;
        public float g;
        public Key parent;
        public bool hasParent;
    }

    private sealed class MinHeap
    {
        private readonly List<(float pri, Key key)> heap = new List<(float, Key)>(4096);
        public int Count => heap.Count;

        public void Push(float pri, Key key)
        {
            heap.Add((pri, key));
            SiftUp(heap.Count - 1);
        }

        public (float pri, Key key) Pop()
        {
            var root = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            if (heap.Count > 0) SiftDown(0);
            return root;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (heap[p].pri <= heap[i].pri) break;
                (heap[p], heap[i]) = (heap[i], heap[p]);
                i = p;
            }
        }

        private void SiftDown(int i)
        {
            int n = heap.Count;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, s = i;
                if (l < n && heap[l].pri < heap[s].pri) s = l;
                if (r < n && heap[r].pri < heap[s].pri) s = r;
                if (s == i) break;
                (heap[s], heap[i]) = (heap[i], heap[s]);
                i = s;
            }
        }
    }

    // -------------------------- Physics isolation (ghost + manual sim) --------------------------

    private sealed class PhysicsIsolationScope : IDisposable
    {
        private readonly Rigidbody2D liveRb;
        private readonly bool liveRbSimulated;

        private readonly List<Rigidbody2D> otherBodies = new List<Rigidbody2D>(256);
        private readonly List<bool> otherSimStates = new List<bool>(256);

        private readonly object savedSimulationMode;
        private readonly object savedAutoSimulation;
        private readonly bool usedSimulationModeProperty;

        private readonly GameObject ghostGO;

        public PhysicsIsolationScope(PlayerMovement live, out PlayerMovement ghost)
        {
            ghost = null;

            liveRb = live != null ? live.GetComponent<Rigidbody2D>() : null;
            if (liveRb != null)
            {
                liveRbSimulated = liveRb.simulated;
                liveRb.simulated = false;
            }
            else
            {
                liveRbSimulated = false;
            }

            Rigidbody2D[] allBodies;

#if UNITY_2023_1_OR_NEWER || UNITY_2022_2_OR_NEWER
            allBodies = UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
#else
            allBodies = UnityEngine.Object.FindObjectsOfType<Rigidbody2D>();
#endif

            foreach (var b in allBodies)
            {
                if (b == null) continue;
                if (liveRb != null && b == liveRb) continue;

                otherBodies.Add(b);
                otherSimStates.Add(b.simulated);
                b.simulated = false;
            }

            (usedSimulationModeProperty, savedSimulationMode, savedAutoSimulation) = SetPhysics2DToManual();

            ghostGO = UnityEngine.Object.Instantiate(live.gameObject);
            ghostGO.name = live.gameObject.name + "_PLANNER_GHOST";

            foreach (var r in ghostGO.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;

            foreach (var mb in ghostGO.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is PlayerMovement) continue;
                mb.enabled = false;
            }

            ghost = ghostGO.GetComponent<PlayerMovement>();
            if (ghost == null) return;


            ghost.SetUseUnityInput(false);
            EnsurePrivateRbFieldInitialized(ghost);

            var grb = ghost.GetComponent<Rigidbody2D>();
            if (grb != null) grb.simulated = true;
        }

        public void Dispose()
        {
            if (ghostGO != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(ghostGO);
                else UnityEngine.Object.DestroyImmediate(ghostGO);
            }

            RestorePhysics2D(usedSimulationModeProperty, savedSimulationMode, savedAutoSimulation);

            for (int i = 0; i < otherBodies.Count; i++)
                if (otherBodies[i] != null)
                    otherBodies[i].simulated = otherSimStates[i];

            if (liveRb != null)
                liveRb.simulated = liveRbSimulated;
        }

        private static void EnsurePrivateRbFieldInitialized(PlayerMovement pm)
        {
            var t = pm.GetType();
            var f = t.GetField("rb", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(Rigidbody2D)) return;

            var current = f.GetValue(pm) as Rigidbody2D;
            if (current != null) return;

            var rb = pm.GetComponent<Rigidbody2D>();
            if (rb != null) f.SetValue(pm, rb);
        }

        private static (bool usedSimulationModeProperty, object savedSimulationMode, object savedAutoSimulation) SetPhysics2DToManual()
        {
            var physics2DType = typeof(Physics2D);

            var simModeProp = physics2DType.GetProperty("simulationMode", BindingFlags.Public | BindingFlags.Static);
            if (simModeProp != null)
            {
                object saved = simModeProp.GetValue(null);
                object scriptValue = Enum.Parse(simModeProp.PropertyType, "Script");
                simModeProp.SetValue(null, scriptValue);
                return (true, saved, null);
            }

            var autoSimProp = physics2DType.GetProperty("autoSimulation", BindingFlags.Public | BindingFlags.Static);
            if (autoSimProp != null)
            {
                object saved = autoSimProp.GetValue(null);
                autoSimProp.SetValue(null, false);
                return (false, null, saved);
            }

            return (false, null, null);
        }

        private static void RestorePhysics2D(bool usedSimulationModeProperty, object savedSimulationMode, object savedAutoSimulation)
        {
            var physics2DType = typeof(Physics2D);

            if (usedSimulationModeProperty)
            {
                var simModeProp = physics2DType.GetProperty("simulationMode", BindingFlags.Public | BindingFlags.Static);
                if (simModeProp != null && savedSimulationMode != null)
                    simModeProp.SetValue(null, savedSimulationMode);
                return;
            }

            var autoSimProp = physics2DType.GetProperty("autoSimulation", BindingFlags.Public | BindingFlags.Static);
            if (autoSimProp != null && savedAutoSimulation != null)
                autoSimProp.SetValue(null, savedAutoSimulation);
        }
    }

    // -------------------------- Gizmos --------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        for (int i = 0; i < explored.Count; i += 25)
            Gizmos.DrawSphere(explored[i], 0.05f);

        if (lastPath.Count > 1)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.9f);
            for (int i = 0; i < lastPath.Count - 1; i++)
                Gizmos.DrawLine(lastPath[i], lastPath[i + 1]);
        }

        if (goal != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
            Gizmos.DrawWireSphere(goal.position, GoalRadius);
        }
    }
}