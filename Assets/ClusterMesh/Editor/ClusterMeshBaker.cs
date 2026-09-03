using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshBaker
    {
        public static ClusterMeshBakeResult Bake(Mesh mesh, Material[] materials, ClusterMeshBakeSettings settings)
        {
            if (mesh == null)
                throw new InvalidOperationException("ClusterMesh baker requires a Mesh.");
            if (settings == null)
                settings = new ClusterMeshBakeSettings();
            if (settings.maxVerticesPerCluster < 3 || settings.maxTrianglesPerCluster < 1)
                throw new InvalidOperationException("ClusterMesh baker budgets must allow at least one triangle.");

            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            var sourcePositions = mesh.vertices;
            var sourceNormals = mesh.normals;
            var sourceTangents = mesh.tangents;
            var sourceUvs = mesh.uv;
            if (sourcePositions == null || sourcePositions.Length == 0)
                throw new InvalidOperationException("ClusterMesh baker requires a mesh with vertices.");

            var clusters = new List<ClusterHeader>();
            var vertices = new List<ClusterVertex>();
            var indices = new List<uint>();

            for (int sub = 0; sub < subMeshCount; sub++)
            {
                int[] tris = mesh.GetTriangles(sub);
                BakeSubmesh(
                    (uint)sub,
                    tris,
                    sourcePositions,
                    sourceNormals,
                    sourceTangents,
                    sourceUvs,
                    settings,
                    clusters,
                    vertices,
                    indices);
            }

            if (clusters.Count == 0)
                throw new InvalidOperationException("ClusterMesh baker found no valid triangles.");

            var materialSlots = new Material[subMeshCount];
            if (materials != null)
            {
                for (int i = 0; i < subMeshCount && i < materials.Length; i++)
                    materialSlots[i] = materials[i];
            }

            return new ClusterMeshBakeResult
            {
                clusters = clusters.ToArray(),
                vertices = vertices.ToArray(),
                indices = indices.ToArray(),
                materials = materialSlots
            };
        }

        static void BakeSubmesh(
            uint materialIndex,
            int[] tris,
            Vector3[] positions,
            Vector3[] normals,
            Vector4[] tangents,
            Vector2[] uvs,
            ClusterMeshBakeSettings settings,
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<uint> indices)
        {
            var triangleList = new List<int>();
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int a = tris[i];
                int b = tris[i + 1];
                int c = tris[i + 2];
                if (a == b || b == c || a == c)
                    continue;
                triangleList.Add(a);
                triangleList.Add(b);
                triangleList.Add(c);
            }

            int triCount = triangleList.Count / 3;
            var unused = new bool[triCount];
            for (int i = 0; i < triCount; i++)
                unused[i] = true;

            var vertexToTris = new Dictionary<int, List<int>>();
            for (int t = 0; t < triCount; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int v = triangleList[t * 3 + k];
                    if (!vertexToTris.TryGetValue(v, out var list))
                    {
                        list = new List<int>();
                        vertexToTris[v] = list;
                    }

                    list.Add(t);
                }
            }

            int remaining = triCount;
            while (remaining > 0)
            {
                int seed = -1;
                for (int t = 0; t < triCount; t++)
                {
                    if (unused[t])
                    {
                        seed = t;
                        break;
                    }
                }

                var clusterTris = new List<int>();
                var usedVerts = new HashSet<int>();
                AddTriangle(seed, triangleList, unused, clusterTris, usedVerts);
                remaining--;

                bool grew = true;
                while (grew)
                {
                    grew = false;
                    int candidate = FindGrowCandidate(clusterTris, triangleList, unused, vertexToTris, usedVerts, settings);
                    if (candidate < 0)
                        candidate = FindAnyFit(unused, triangleList, usedVerts, clusterTris.Count, settings);
                    if (candidate < 0)
                        break;
                    AddTriangle(candidate, triangleList, unused, clusterTris, usedVerts);
                    remaining--;
                    grew = true;
                }

                EmitCluster(materialIndex, clusterTris, triangleList, positions, normals, tangents, uvs, clusters, vertices, indices);
            }
        }

        static int FindGrowCandidate(
            List<int> clusterTris,
            List<int> triangleList,
            bool[] unused,
            Dictionary<int, List<int>> vertexToTris,
            HashSet<int> usedVerts,
            ClusterMeshBakeSettings settings)
        {
            foreach (int t in clusterTris)
            {
                for (int k = 0; k < 3; k++)
                {
                    int v = triangleList[t * 3 + k];
                    if (!vertexToTris.TryGetValue(v, out var neighbors))
                        continue;
                    foreach (int n in neighbors)
                    {
                        if (!unused[n])
                            continue;
                        if (Fits(n, triangleList, usedVerts, clusterTris.Count, settings))
                            return n;
                    }
                }
            }

            return -1;
        }

        static int FindAnyFit(bool[] unused, List<int> triangleList, HashSet<int> usedVerts, int clusterTriangleCount, ClusterMeshBakeSettings settings)
        {
            for (int t = 0; t < unused.Length; t++)
            {
                if (unused[t] && Fits(t, triangleList, usedVerts, clusterTriangleCount, settings))
                    return t;
            }

            return -1;
        }

        static bool Fits(int tri, List<int> triangleList, HashSet<int> usedVerts, int clusterTriangleCount, ClusterMeshBakeSettings settings)
        {
            if (clusterTriangleCount + 1 > settings.maxTrianglesPerCluster)
                return false;
            int added = 0;
            for (int k = 0; k < 3; k++)
            {
                if (!usedVerts.Contains(triangleList[tri * 3 + k]))
                    added++;
            }

            return usedVerts.Count + added <= settings.maxVerticesPerCluster;
        }

        static void AddTriangle(int tri, List<int> triangleList, bool[] unused, List<int> clusterTris, HashSet<int> usedVerts)
        {
            unused[tri] = false;
            clusterTris.Add(tri);
            for (int k = 0; k < 3; k++)
                usedVerts.Add(triangleList[tri * 3 + k]);
        }

        static void EmitCluster(
            uint materialIndex,
            List<int> clusterTris,
            List<int> triangleList,
            Vector3[] positions,
            Vector3[] normals,
            Vector4[] tangents,
            Vector2[] uvs,
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<uint> destIndices)
        {
            var remap = new Dictionary<int, uint>();
            uint vertexOffset = (uint)vertices.Count;
            uint indexOffset = (uint)destIndices.Count;
            var packed = new List<Vector3>();

            foreach (int tri in clusterTris)
            {
                for (int k = 0; k < 3; k++)
                {
                    int src = triangleList[tri * 3 + k];
                    if (!remap.TryGetValue(src, out uint local))
                    {
                        local = (uint)remap.Count;
                        remap[src] = local;
                        var n = (normals != null && src < normals.Length) ? normals[src] : Vector3.up;
                        var tan = (tangents != null && src < tangents.Length) ? tangents[src] : new Vector4(1f, 0f, 0f, 1f);
                        var uv = (uvs != null && src < uvs.Length) ? uvs[src] : Vector2.zero;
                        vertices.Add(new ClusterVertex
                        {
                            position = positions[src],
                            normal = n,
                            tangent = tan,
                            uv = new Vector4(uv.x, uv.y, 0f, 0f)
                        });
                        packed.Add(positions[src]);
                    }

                    destIndices.Add(local);
                }
            }

            var min = packed[0];
            var max = packed[0];
            for (int i = 1; i < packed.Count; i++)
            {
                min = Vector3.Min(min, packed[i]);
                max = Vector3.Max(max, packed[i]);
            }

            Vector3 center = (min + max) * 0.5f;
            Vector3 extents = (max - min) * 0.5f;
            BuildCone(clusterTris, triangleList, positions, center, out Vector3 axis, out float cutoff);

            clusters.Add(new ClusterHeader
            {
                vertexOffset = vertexOffset,
                vertexCount = (uint)remap.Count,
                indexOffset = indexOffset,
                triangleCount = (uint)clusterTris.Count,
                materialIndex = materialIndex,
                aabbCenter = center,
                aabbExtents = extents,
                coneAxisCutoff = new Vector4(axis.x, axis.y, axis.z, cutoff),
                coneApex = center
            });
        }

        static void BuildCone(List<int> clusterTris, List<int> triangleList, Vector3[] positions, Vector3 apex, out Vector3 axis, out float cutoff)
        {
            Vector3 weighted = Vector3.zero;
            var normals = new List<Vector3>(clusterTris.Count);
            foreach (int tri in clusterTris)
            {
                Vector3 a = positions[triangleList[tri * 3]];
                Vector3 b = positions[triangleList[tri * 3 + 1]];
                Vector3 c = positions[triangleList[tri * 3 + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                float area2 = n.magnitude;
                if (area2 < 1e-12f)
                    continue;
                n /= area2;
                normals.Add(n);
                weighted += n * area2;
            }

            if (weighted.sqrMagnitude < 1e-12f)
            {
                axis = Vector3.up;
                cutoff = -1f;
                return;
            }

            axis = weighted.normalized;
            float minDot = 1f;
            foreach (var n in normals)
                minDot = Mathf.Min(minDot, Vector3.Dot(axis, n));
            cutoff = minDot < 0f ? -1f : minDot;
        }
    }
}
