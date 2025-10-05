using NUnit.Framework;
using Aetheria.Dungeons;

namespace Aetheria.Tests
{
    /// <summary>
    /// Deterministic dungeon tests: same seed/floor yields identical graph topology.
    /// </summary>
    public class DungeonTests
    {
        [Test]
        public void SameSeedSameFloor_IdenticalCounts()
        {
            var genA = new UnityEngine.GameObject("A").AddComponent<DungeonGenerator>();
            genA.SetSeedAndFloor(12345, 2);
            var graphA = genA.CurrentGraph;

            var genB = new UnityEngine.GameObject("B").AddComponent<DungeonGenerator>();
            genB.SetSeedAndFloor(12345, 2);
            var graphB = genB.CurrentGraph;

            Assert.AreEqual(graphA.Nodes.Count, graphB.Nodes.Count);
        }
    }
}


