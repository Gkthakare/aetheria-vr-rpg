using UnityEngine;
using System.Collections.Generic;

namespace Aetheria.AI
{
    /// <summary>
    /// Simple OBJ parser for runtime mesh loading.
    /// Supports v (vertex) and f (face) lines. Used by Model3DRequest.
    /// </summary>
    public static class OBJLoader
    {
        public static Mesh ParseOBJ(string objText)
        {
            var vertices = new List<Vector3>();
            var faces = new List<int>();

            var lines = objText.Split('\n');
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.StartsWith("v "))
                {
                    var parts = t.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        float x = float.Parse(parts[1]);
                        float y = float.Parse(parts[2]);
                        float z = float.Parse(parts[3]);
                        vertices.Add(new Vector3(x, y, z));
                    }
                }
                else if (t.StartsWith("f "))
                {
                    var parts = t.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    var faceVerts = new List<int>();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var idxStr = parts[i].Split('/')[0];
                        int idx = int.Parse(idxStr);
                        faceVerts.Add(idx - 1); // OBJ is 1-indexed
                    }
                    faces.AddRange(faceVerts);
                    faces.Add(-1); // sentinel between faces
                }
            }

            var mesh = new Mesh();
            mesh.name = "GeneratedMesh";
            mesh.SetVertices(vertices);

            // Convert faces to triangles (tri or quad -> tris)
            var triangles = new List<int>();
            int j = 0;
            while (j < faces.Count)
            {
                var face = new List<int>();
                while (j < faces.Count && faces[j] >= 0)
                {
                    face.Add(faces[j]);
                    j++;
                }
                j++; // skip sentinel
                if (face.Count == 3)
                {
                    triangles.AddRange(face);
                }
                else if (face.Count == 4)
                {
                    triangles.Add(face[0]);
                    triangles.Add(face[1]);
                    triangles.Add(face[2]);
                    triangles.Add(face[0]);
                    triangles.Add(face[2]);
                    triangles.Add(face[3]);
                }
            }

            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
