using UnityEngine;
using System.Collections.Generic;

namespace Aetheria.World
{
    /// <summary>
    /// Combines multiple tile meshes into a single mesh.
    /// Reduces draw calls from 256+ per chunk to 1 per material type.
    /// Enable via ChunkManager.combineChunkMeshes for VR performance.
    /// </summary>
    public static class ChunkMeshCombiner
    {
        /// <summary>Combine tiles of the same mesh type into one mesh.</summary>
        public static Mesh Combine(List<CombineInstance> instances)
        {
            if (instances == null || instances.Count == 0) return null;
            var mesh = new Mesh();
            mesh.CombineMeshes(instances.ToArray(), true, true);
            return mesh;
        }
    }
}
