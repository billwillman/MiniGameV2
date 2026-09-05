using System.Collections.Generic;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshLodBaker
    {
        public static void BuildParents(
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<uint> indices,
            int leafStart,
            int leafEnd,
            ClusterMeshBakeSettings settings)
        {
            if (clusters == null || settings == null || leafEnd - leafStart < 4)
                return;

            var used = new bool[leafEnd];
            while (true)
            {
                int first = -1;
                int remaining = 0;
                for (int i = leafStart; i < leafEnd; i++)
                {
                    if (used[i])
                        continue;
                    remaining++;
                    if (first < 0)
                        first = i;
                }

                if (remaining < 4)
                    break;

                var group = new List<int>(4) { first };
                used[first] = true;
                while (group.Count < 4)
                {
                    Vector3 pivot = AverageCenter(clusters, group);
                    int best = -1;
                    float bestD = float.MaxValue;
                    for (int i = leafStart; i < leafEnd; i++)
                    {
                        if (used[i])
                            continue;
                        float d = ((Vector3)clusters[i].aabbCenter - pivot).sqrMagnitude;
                        if (d >= bestD)
                            continue;
                        bestD = d;
                        best = i;
                    }

                    used[best] = true;
                    group.Add(best);
                }

                int parentIndex = EmitParent(clusters, vertices, indices, group, settings);
                if (parentIndex < 0)
                    continue;
                for (int g = 0; g < group.Count; g++)
                {
                    ClusterHeader leaf = clusters[group[g]];
                    leaf.parentIndex = parentIndex;
                    clusters[group[g]] = leaf;
                }
            }
        }

        static Vector3 AverageCenter(List<ClusterHeader> clusters, List<int> group)
        {
            Vector3 s = Vector3.zero;
            for (int i = 0; i < group.Count; i++)
                s += (Vector3)clusters[group[i]].aabbCenter;
            return s / group.Count;
        }

        static int EmitParent(
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<uint> indices,
            List<int> group,
            ClusterMeshBakeSettings settings)
        {
            var pos = new List<Vector3>();
            var nrm = new List<Vector3>();
            var tan = new List<Vector4>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            ExpandLeaves(clusters, vertices, indices, group, pos, nrm, tan, uv, tris);
            if (tris.Count < 3)
                return -1;

            var srcPos = new List<Vector3>(pos);
            var srcTris = new List<int>(tris);
            Collapse(pos, nrm, tan, uv, tris, settings.maxVerticesPerCluster, settings.maxTrianglesPerCluster);
            if (tris.Count < 3 || pos.Count < 3)
                return -1;

            uint materialIndex = clusters[group[0]].materialIndex;
            uint vertexOffset = (uint)vertices.Count;
            uint indexOffset = (uint)indices.Count;
            for (int i = 0; i < pos.Count; i++)
            {
                vertices.Add(new ClusterVertex
                {
                    position = pos[i],
                    normal = nrm[i].sqrMagnitude > 1e-12f ? nrm[i].normalized : Vector3.up,
                    tangent = tan[i],
                    uv = new Vector4(uv[i].x, uv[i].y, 0f, 0f)
                });
            }

            for (int i = 0; i < tris.Count; i++)
                indices.Add((uint)tris[i]);

            Vector3 min = pos[0];
            Vector3 max = pos[0];
            for (int i = 1; i < pos.Count; i++)
            {
                min = Vector3.Min(min, pos[i]);
                max = Vector3.Max(max, pos[i]);
            }

            Vector3 center = (min + max) * 0.5f;
            BuildCone(pos, tris, out Vector3 axis, out float cutoff);
            float lodError = Mathf.Max(1e-6f, MaxDeviation(pos, srcPos, srcTris));

            clusters.Add(new ClusterHeader
            {
                vertexOffset = vertexOffset,
                vertexCount = (uint)pos.Count,
                indexOffset = indexOffset,
                triangleCount = (uint)(tris.Count / 3),
                materialIndex = materialIndex,
                parentIndex = ClusterMeshLod.NoParent,
                lodError = lodError,
                flags = ClusterMeshLod.FlagParent,
                aabbCenter = center,
                aabbExtents = (max - min) * 0.5f,
                coneAxisCutoff = new Vector4(axis.x, axis.y, axis.z, cutoff),
                coneApex = center
            });
            return clusters.Count - 1;
        }

        static void ExpandLeaves(
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<uint> indices,
            List<int> group,
            List<Vector3> pos,
            List<Vector3> nrm,
            List<Vector4> tan,
            List<Vector2> uv,
            List<int> tris)
        {
            for (int g = 0; g < group.Count; g++)
            {
                ClusterHeader h = clusters[group[g]];
                int baseV = pos.Count;
                for (int v = 0; v < (int)h.vertexCount; v++)
                {
                    ClusterVertex cv = vertices[(int)h.vertexOffset + v];
                    pos.Add(cv.position);
                    nrm.Add(cv.normal);
                    tan.Add(cv.tangent);
                    uv.Add(new Vector2(cv.uv.x, cv.uv.y));
                }

                int triN = (int)h.triangleCount;
                for (int t = 0; t < triN; t++)
                {
                    int i0 = (int)indices[(int)h.indexOffset + t * 3];
                    int i1 = (int)indices[(int)h.indexOffset + t * 3 + 1];
                    int i2 = (int)indices[(int)h.indexOffset + t * 3 + 2];
                    tris.Add(baseV + i0);
                    tris.Add(baseV + i1);
                    tris.Add(baseV + i2);
                }
            }
        }

        static void Collapse(
            List<Vector3> pos,
            List<Vector3> nrm,
            List<Vector4> tan,
            List<Vector2> uv,
            List<int> tris,
            int maxVerts,
            int maxTris)
        {
            int guard = pos.Count * 8 + 8;
            while (guard-- > 0 && (pos.Count > maxVerts || tris.Count / 3 > maxTris))
            {
                if (!TryCollapseShortest(pos, nrm, tan, uv, tris))
                    break;
            }
        }

        static bool TryCollapseShortest(
            List<Vector3> pos,
            List<Vector3> nrm,
            List<Vector4> tan,
            List<Vector2> uv,
            List<int> tris)
        {
            int bestA = -1;
            int bestB = -1;
            float bestLen = float.MaxValue;
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Consider(pos, tris[t], tris[t + 1], ref bestA, ref bestB, ref bestLen);
                Consider(pos, tris[t + 1], tris[t + 2], ref bestA, ref bestB, ref bestLen);
                Consider(pos, tris[t + 2], tris[t], ref bestA, ref bestB, ref bestLen);
            }

            if (bestA < 0)
                return false;
            int keep = Mathf.Min(bestA, bestB);
            int drop = Mathf.Max(bestA, bestB);
            nrm[keep] = (nrm[keep] + nrm[drop]).normalized;
            tan[keep] = tan[keep];
            uv[keep] = (uv[keep] + uv[drop]) * 0.5f;
            for (int i = 0; i < tris.Count; i++)
            {
                if (tris[i] == drop)
                    tris[i] = keep;
                else if (tris[i] > drop)
                    tris[i]--;
            }

            pos.RemoveAt(drop);
            nrm.RemoveAt(drop);
            tan.RemoveAt(drop);
            uv.RemoveAt(drop);

            for (int i = tris.Count - 3; i >= 0; i -= 3)
            {
                int a = tris[i];
                int b = tris[i + 1];
                int c = tris[i + 2];
                if (a == b || b == c || a == c)
                    tris.RemoveRange(i, 3);
            }

            return true;
        }

        static void Consider(List<Vector3> pos, int a, int b, ref int bestA, ref int bestB, ref float bestLen)
        {
            if (a == b)
                return;
            float len = (pos[a] - pos[b]).sqrMagnitude;
            if (len >= bestLen)
                return;
            bestLen = len;
            bestA = a;
            bestB = b;
        }

        static void BuildCone(List<Vector3> pos, List<int> tris, out Vector3 axis, out float cutoff)
        {
            Vector3 weighted = Vector3.zero;
            var normals = new List<Vector3>();
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                Vector3 a = pos[tris[i]];
                Vector3 b = pos[tris[i + 1]];
                Vector3 c = pos[tris[i + 2]];
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

        static float MaxDeviation(List<Vector3> parentVerts, List<Vector3> srcPos, List<int> srcTris)
        {
            float max = 0f;
            for (int i = 0; i < parentVerts.Count; i++)
            {
                float best = float.MaxValue;
                for (int t = 0; t + 2 < srcTris.Count; t += 3)
                {
                    float d = PointTriangleDistance(
                        parentVerts[i],
                        srcPos[srcTris[t]],
                        srcPos[srcTris[t + 1]],
                        srcPos[srcTris[t + 2]]);
                    if (d < best)
                        best = d;
                }

                if (best < float.MaxValue)
                    max = Mathf.Max(max, best);
            }

            return max;
        }

        static float PointTriangleDistance(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = p - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
                return (p - a).magnitude;

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
                return (p - b).magnitude;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                return (p - (a + v * ab)).magnitude;
            }

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
                return (p - c).magnitude;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                return (p - (a + w * ac)).magnitude;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return (p - (b + w * (c - b))).magnitude;
            }

            float denom = 1f / (va + vb + vc);
            Vector3 closest = a + ab * (vb * denom) + ac * (vc * denom);
            return (p - closest).magnitude;
        }
    }
}
