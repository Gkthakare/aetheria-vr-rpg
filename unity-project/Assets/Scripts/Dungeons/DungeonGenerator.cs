using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aetheria.Dungeons
{
    /// <summary>
    /// Deterministic SAO-flavored dungeon generator.
    /// Produces a small room-graph per (seed, floorIndex).
    /// Graph is solvable with Start -> (Hubs/Keys) -> Boss.
    /// </summary>
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("Generation Settings")]
        [SerializeField] private int seed = 12345;
        [SerializeField] private int floorIndex = 1;
        [SerializeField] private int minRooms = 8;
        [SerializeField] private int maxRooms = 16;

        [Header("Debug")]
        [SerializeField] private bool logGraph = true;

        private DeterministicRng rng;

        public DungeonGraph CurrentGraph { get; private set; }

        private void Awake()
        {
            rng = new DeterministicRng(seed ^ (floorIndex * 73471));
        }

        private void Start()
        {
            CurrentGraph = GenerateGraph();
            if (logGraph)
            {
                Debug.Log(CurrentGraph.ToDebugString());
            }
        }

        public void SetSeedAndFloor(int newSeed, int newFloor)
        {
            seed = newSeed;
            floorIndex = newFloor;
            rng = new DeterministicRng(seed ^ (floorIndex * 73471));
            CurrentGraph = GenerateGraph();
        }

        public DungeonGraph GenerateGraph()
        {
            int roomCount = rng.Range(minRooms, maxRooms + 1);
            var graph = new DungeonGraph();

            // Create nodes
            graph.AddNode(new DungeonRoom(RoomType.Start));
            graph.AddNode(new DungeonRoom(RoomType.Boss));
            graph.AddNode(new DungeonRoom(RoomType.Training));

            int hubs = Mathf.Max(1, roomCount / 4);
            int keys = Mathf.Clamp(roomCount / 5, 1, 3);

            for (int i = 0; i < hubs; i++) graph.AddNode(new DungeonRoom(RoomType.Hub));
            for (int i = 0; i < keys; i++) graph.AddNode(new DungeonRoom(RoomType.KeyRoom));

            int fillers = roomCount - graph.Nodes.Count;
            for (int i = 0; i < fillers; i++)
            {
                var t = rng.Choice(new[] { RoomType.Corridor, RoomType.TrapRoom, RoomType.MiniBoss });
                graph.AddNode(new DungeonRoom(t));
            }

            // Connect: Start -> hubs/paths -> keys -> boss, ensuring solvable path
            var start = graph.GetFirst(RoomType.Start);
            var boss = graph.GetFirst(RoomType.Boss);

            var hubList = graph.GetAll(RoomType.Hub);
            var keyList = graph.GetAll(RoomType.KeyRoom);
            var others = graph.GetAllExcept(new[] { RoomType.Start, RoomType.Boss, RoomType.Training, RoomType.Hub, RoomType.KeyRoom });
            var trainingRoom = graph.GetFirst(RoomType.Training);

            // Start -> Training (always available for weapons/gesture practice)
            graph.Connect(start, trainingRoom);

            // Chain start -> hubs
            DungeonRoom prev = start;
            foreach (var hub in hubList)
            {
                graph.Connect(prev, hub);
                prev = hub;
            }

            // Attach keys to random hubs
            foreach (var key in keyList)
            {
                var hub = rng.Choice(hubList.Count > 0 ? hubList : new List<DungeonRoom> { start });
                graph.Connect(hub, key);
            }

            // Sprinkle others between hubs/keys
            foreach (var node in others)
            {
                var a = rng.Choice(graph.Nodes);
                var b = rng.Choice(graph.Nodes);
                if (a != b) graph.Connect(a, b);
            }

            // Ensure at least one full path to boss
            var lastGate = hubList.Count > 0 ? hubList[hubList.Count - 1] : start;
            graph.Connect(lastGate, boss);

            return graph;
        }
    }

    public enum RoomType { Start, Corridor, Hub, KeyRoom, TrapRoom, MiniBoss, Boss, Training }

    [Serializable]
    public class DungeonRoom
    {
        public readonly string Id;
        public readonly RoomType Type;
        public readonly List<string> Links = new List<string>();

        public DungeonRoom(RoomType type)
        {
            Type = type;
            Id = Guid.NewGuid().ToString("N");
        }
    }

    public class DungeonGraph
    {
        public readonly List<DungeonRoom> Nodes = new List<DungeonRoom>();

        public void AddNode(DungeonRoom node) => Nodes.Add(node);

        public void Connect(DungeonRoom a, DungeonRoom b)
        {
            if (!a.Links.Contains(b.Id)) a.Links.Add(b.Id);
            if (!b.Links.Contains(a.Id)) b.Links.Add(a.Id);
        }

        public DungeonRoom GetFirst(RoomType type) => Nodes.Find(n => n.Type == type);

        public List<DungeonRoom> GetAll(RoomType type) => Nodes.FindAll(n => n.Type == type);

        public List<DungeonRoom> GetAllExcept(RoomType[] excluded) => Nodes.FindAll(n => Array.IndexOf(excluded, n.Type) < 0);

        public string ToDebugString()
        {
            var lines = new List<string>();
            lines.Add($"DungeonGraph: {Nodes.Count} rooms");
            foreach (var n in Nodes)
            {
                lines.Add($"- {n.Type} {n.Id.Substring(0, 6)} -> {string.Join(",", n.Links.ConvertAll(x => x.Substring(0, 6)))}");
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// Small deterministic RNG wrapper to avoid System.Random sharing issues across frames.
    /// </summary>
    public sealed class DeterministicRng
    {
        private uint state;
        public DeterministicRng(int seed) { state = (uint)seed * 747796405u + 2891336453u; }
        private uint NextU()
        {
            state += 0x6D2B79F5;
            uint z = state;
            z = (z ^ (z >> 15)) * (z | 1);
            z ^= z + (z ^ (z >> 7)) * (z | 61);
            return z ^ (z >> 14);
        }
        public float Value() => (NextU() & 0xFFFFFF) / (float)0x01000000; // [0,1)
        public int Range(int min, int max) => min + (int)Mathf.Floor(Value() * (max - min));
        public T Choice<T>(IList<T> list) => list.Count == 0 ? default : list[Range(0, list.Count)];
    }
}


