using System.Collections.Generic;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshLodBaker
    {
        const float QuantizeScale = 100000f;

        public static void BuildHierarchy(
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<uint> indices,
            List<ClusterGroup> groups,
            int leafStart,
            int leafEnd,
            ClusterMeshBakeSettings settings)
        {
            if (clusters == null || groups == null || settings == null || leafEnd - leafStart < 2)
                return;

            var pending = new List<int>(leafEnd - leafStart);
            for (int i = leafStart; i < leafEnd; i++)
                pending.Add(i);

            int level = 0;
            while (pending.Count >= 2 && level < ClusterMeshLod.MaxLodLevels)
            {
                level++;
                var remaining = new List<int>(pending);
                var next = new List<int>();
                bool emitted = false;

                while (remaining.Count >= 2)
                {
                    int size = remaining.Count >= 4 ? 4 : remaining.Count;
                    List<int> group = PickGroup(clusters, remaining, size);
                    RemoveAll(remaining, group);

                    int startClusters = clusters.Count;
                    if (!TryEmitGroup(clusters, vertices, indices, groups, group, settings, level))
                        continue;

                    emitted = true;
                    for (int i = startClusters; i < clusters.Count; i++)
                        next.Add(i);
                }

                if (remaining.Count == 1)
                    next.Add(remaining[0]);

                if (!emitted)
                    break;
                pending = next;
            }
        }

        public static void GetGroupLockedPositions(
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<uint> indices,
            List<int> group,
            List<Vector3> dest)
        {
            dest.Clear();
            var pos = new List<Vector3>();
            var nrm = new List<Vector3>();
            var tan = new List<Vector4>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            ExpandLeaves(clusters, vertices, indices, group, pos, nrm, tan, uv, tris);
            var locked = new List<bool>();
            MarkLocked(pos, tris, locked);
            var seen = new HashSet<(int, int, int)>();
            for (int i = 0; i < pos.Count; i++)
            {
                if (!locked[i])
                    continue;
                var key = Quantize(pos[i]);
                if (!seen.Add(key))
                    continue;
                dest.Add(pos[i]);
            }
        }

        static List<int> PickGroup(List<ClusterHeader> clusters, List<int> remaining, int size)
        {
            int first = remaining[0];
            for (int i = 1; i < remaining.Count; i++)
            {
                if (remaining[i] < first)
                    first = remaining[i];
            }

            var group = new List<int>(size) { first };
            while (group.Count < size)
            {
                Vector3 pivot = AverageCenter(clusters, group);
                int best = -1;
                float bestD = float.MaxValue;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int idx = remaining[i];
                    if (Contains(group, idx))
                        continue;
                    float d = ((Vector3)clusters[idx].aabbCenter - pivot).sqrMagnitude;
                    if (d >= bestD)
                        continue;
                    bestD = d;
                    best = idx;
                }

                group.Add(best);
            }

            return group;
        }

        static void RemoveAll(List<int> remaining, List<int> group)
        {
            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                if (Contains(group, remaining[i]))
                    remaining.RemoveAt(i);
            }
        }

        static bool Contains(List<int> list, int value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                    return true;
            }

            return false;
        }

        static Vector3 AverageCenter(List<ClusterHeader> clusters, List<int> group)
        {
            Vector3 s = Vector3.zero;
            for (int i = 0; i < group.Count; i++)
                s += (Vector3)clusters[group[i]].aabbCenter;
            return s / group.Count;
        }

        static bool TryEmitGroup(
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<uint> indices,
            List<ClusterGroup> groups,
            List<int> group,
            ClusterMeshBakeSettings settings,
            int level)
        {
            var pos = new List<Vector3>();
            var nrm = new List<Vector3>();
            var tan = new List<Vector4>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            ExpandLeaves(clusters, vertices, indices, group, pos, nrm, tan, uv, tris);
            if (tris.Count < 3)
                return false;

            var srcPos = new List<Vector3>(pos);
            var srcTris = new List<int>(tris);
            int srcTriCount = tris.Count / 3;
            var locked = new List<bool>();
            MarkLocked(pos, tris, locked);
            CollapseHalf(pos, nrm, tan, uv, tris, locked, srcTriCount);
            if (tris.Count < 3 || tris.Count / 3 >= srcTriCount)
                return false;

            float childError = 0f;
            Vector3 gmin = (Vector3)clusters[group[0]].aabbCenter - (Vector3)clusters[group[0]].aabbExtents;
            Vector3 gmax = (Vector3)clusters[group[0]].aabbCenter + (Vector3)clusters[group[0]].aabbExtents;
            for (int i = 0; i < group.Count; i++)
            {
                ClusterHeader ch = clusters[group[i]];
                childError = Mathf.Max(childError, ch.lodError);
                Vector3 cmin = (Vector3)ch.aabbCenter - (Vector3)ch.aabbExtents;
                Vector3 cmax = (Vector3)ch.aabbCenter + (Vector3)ch.aabbExtents;
                gmin = Vector3.Min(gmin, cmin);
                gmax = Vector3.Max(gmax, cmax);
            }

            float lodError = Mathf.Max(1e-6f, Mathf.Max(MaxDeviation(pos, srcPos, srcTris), childError));
            int startClusters = clusters.Count;
            ClusterMeshBaker.ClusterTriangles(
                clusters[group[0]].materialIndex,
                new List<int>(tris),
                pos.ToArray(),
                nrm.ToArray(),
                tan.ToArray(),
                uv.ToArray(),
                settings,
                clusters,
                vertices,
                indices,
                lodError,
                ClusterMeshLod.PackFlags(level));

            int newCount = clusters.Count - startClusters;
            if (newCount <= 0)
                return false;

            int newTris = 0;
            for (int i = startClusters; i < clusters.Count; i++)
                newTris += (int)clusters[i].triangleCount;
            if (newCount >= group.Count && newTris >= srcTriCount)
            {
                clusters.RemoveRange(startClusters, newCount);
                return false;
            }

            int groupIndex = groups.Count;
            groups.Add(new ClusterGroup
            {
                clusterStart = startClusters,
                clusterCount = newCount,
                parentGroupIndex = ClusterMeshLod.NoParent,
                lodError = lodError,
                aabbCenter = (gmin + gmax) * 0.5f,
                aabbExtents = (gmax - gmin) * 0.5f
            });

            for (int i = 0; i < group.Count; i++)
            {
                ClusterHeader leaf = clusters[group[i]];
                leaf.parentIndex = groupIndex;
                clusters[group[i]] = leaf;
                LinkSourceGroup(groups, group[i], groupIndex);
            }

            return true;
        }

        static void LinkSourceGroup(List<ClusterGroup> groups, int clusterIndex, int parentGroupIndex)
        {
            for (int i = 0; i < groups.Count - 1; i++)
            {
                ClusterGroup g = groups[i];
                if (clusterIndex < g.clusterStart || clusterIndex >= g.clusterStart + g.clusterCount)
                    continue;
                g.parentGroupIndex = parentGroupIndex;
                groups[i] = g;
            }
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

        static void MarkLocked(List<Vector3> pos, List<int> tris, List<bool> locked)
        {
            locked.Clear();
            var weld = new int[pos.Count];
            var map = new Dictionary<(int, int, int), int>();
            for (int i = 0; i < pos.Count; i++)
            {
                var key = Quantize(pos[i]);
                if (!map.TryGetValue(key, out int canon))
                {
                    canon = i;
                    map[key] = i;
                }

                weld[i] = canon;
                locked.Add(false);
            }

            var edges = new Dictionary<(int, int), int>();
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                CountEdge(edges, weld[tris[t]], weld[tris[t + 1]]);
                CountEdge(edges, weld[tris[t + 1]], weld[tris[t + 2]]);
                CountEdge(edges, weld[tris[t + 2]], weld[tris[t]]);
            }

            var lockedCanon = new HashSet<int>();
            foreach (var kv in edges)
            {
                if (kv.Value != 1)
                    continue;
                lockedCanon.Add(kv.Key.Item1);
                lockedCanon.Add(kv.Key.Item2);
            }

            for (int i = 0; i < pos.Count; i++)
                locked[i] = lockedCanon.Contains(weld[i]);
        }

        static void CountEdge(Dictionary<(int, int), int> edges, int a, int b)
        {
            if (a == b)
                return;
            if (a > b)
            {
                int tmp = a;
                a = b;
                b = tmp;
            }

            edges.TryGetValue((a, b), out int count);
            edges[(a, b)] = count + 1;
        }

        static (int, int, int) Quantize(Vector3 p)
        {
            return (
                Mathf.RoundToInt(p.x * QuantizeScale),
                Mathf.RoundToInt(p.y * QuantizeScale),
                Mathf.RoundToInt(p.z * QuantizeScale));
        }

        static void CollapseHalf(
            List<Vector3> pos,
            List<Vector3> nrm,
            List<Vector4> tan,
            List<Vector2> uv,
            List<int> tris,
            List<bool> locked,
            int srcTriCount)
        {
            int target = Mathf.Max(1, srcTriCount / 2);
            int guard = pos.Count * 8 + 8;
            while (guard-- > 0 && tris.Count / 3 > target)
            {
                if (!TryCollapseShortest(pos, nrm, tan, uv, tris, locked))
                    break;
            }
        }

        static bool TryCollapseShortest(
            List<Vector3> pos,
            List<Vector3> nrm,
            List<Vector4> tan,
            List<Vector2> uv,
            List<int> tris,
            List<bool> locked)
        {
            int bestA = -1;
            int bestB = -1;
            float bestLen = float.MaxValue;
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Consider(pos, locked, tris[t], tris[t + 1], ref bestA, ref bestB, ref bestLen);
                Consider(pos, locked, tris[t + 1], tris[t + 2], ref bestA, ref bestB, ref bestLen);
                Consider(pos, locked, tris[t + 2], tris[t], ref bestA, ref bestB, ref bestLen);
            }

            if (bestA < 0)
                return false;

            int keep;
            int drop;
            if (locked[bestA] && !locked[bestB])
            {
                keep = bestA;
                drop = bestB;
            }
            else if (locked[bestB] && !locked[bestA])
            {
                keep = bestB;
                drop = bestA;
            }
            else
            {
                keep = Mathf.Min(bestA, bestB);
                drop = Mathf.Max(bestA, bestB);
            }

            if (!locked[keep])
            {
                nrm[keep] = (nrm[keep] + nrm[drop]).normalized;
                uv[keep] = (uv[keep] + uv[drop]) * 0.5f;
            }

            int keepMapped = keep > drop ? keep - 1 : keep;
            for (int i = 0; i < tris.Count; i++)
            {
                if (tris[i] == drop)
                    tris[i] = keepMapped;
                else if (tris[i] > drop)
                    tris[i]--;
            }

            pos.RemoveAt(drop);
            nrm.RemoveAt(drop);
            tan.RemoveAt(drop);
            uv.RemoveAt(drop);
            locked.RemoveAt(drop);

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

        static void Consider(
            List<Vector3> pos,
            List<bool> locked,
            int a,
            int b,
            ref int bestA,
            ref int bestB,
            ref float bestLen)
        {
            if (a == b)
                return;
            if (locked[a] && locked[b])
                return;
            float len = (pos[a] - pos[b]).sqrMagnitude;
            if (len >= bestLen)
                return;
            bestLen = len;
            bestA = a;
            bestB = b;
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
