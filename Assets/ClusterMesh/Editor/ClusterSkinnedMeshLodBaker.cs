using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterSkinnedMeshLodBaker
    {
        const float QuantizeScale = 100000f;

        public static void BuildHierarchy(
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<ClusterSkinWeight> skinWeights,
            List<uint> indices,
            List<ClusterGroup> groups,
            int leafStart,
            int leafEnd,
            ClusterMeshBakeSettings settings,
            ClusterSkinnedQemContext qemContext)
        {
            if (clusters == null || vertices == null || skinWeights == null || indices == null || groups == null ||
                settings == null || leafEnd - leafStart < 2 || vertices.Count != skinWeights.Count)
                return;
            var pending = new List<int>();
            for (int i = leafStart; i < leafEnd; i++) pending.Add(i);
            int level = 0;
            while (pending.Count >= 2 && level < ClusterMeshLod.MaxLodLevels)
            {
                level++;
                var remaining = new List<int>(pending);
                var next = new List<int>();
                bool emitted = false;
                var edgeCache = new Dictionary<int, HashSet<EdgeKey>>();
                while (remaining.Count >= 2)
                {
                    List<int> group = PickGroup(clusters, vertices, skinWeights, indices, groups, remaining, edgeCache);
                    RemoveAll(remaining, group);
                    if (group.Count < 2 || !TryEmitGroup(clusters, vertices, skinWeights, indices, groups,
                        group, settings, qemContext, level))
                    {
                        next.AddRange(group);
                        continue;
                    }
                    emitted = true;
                    ClusterGroup newest = groups[groups.Count - 1];
                    for (int i = 0; i < newest.clusterCount; i++)
                        next.Add(newest.clusterStart + i);
                }
                if (remaining.Count == 1) next.Add(remaining[0]);
                if (!emitted) break;
                pending = next;
            }
        }

        static List<int> PickGroup(
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<ClusterSkinWeight> skin,
            List<uint> indices,
            List<ClusterGroup> groups,
            List<int> remaining,
            Dictionary<int, HashSet<EdgeKey>> edgeCache)
        {
            int seed = remaining[0];
            List<int> group = OwningMembersInRemaining(seed, remaining, groups);
            var edges = new HashSet<EdgeKey>();
            for (int i = 0; i < group.Count; i++)
                edges.UnionWith(BoundaryEdges(clusters, vertices, skin, indices, group[i], edgeCache));
            var skipped = new HashSet<int>();
            while (group.Count < 4)
            {
                int best = -1;
                float bestDistance = float.MaxValue;
                Vector3 center = AverageCenter(clusters, group);
                for (int i = 0; i < remaining.Count; i++)
                {
                    int candidate = remaining[i];
                    if (group.Contains(candidate) || skipped.Contains(candidate) ||
                        !Shares(edges, BoundaryEdges(clusters, vertices, skin, indices, candidate, edgeCache)))
                        continue;
                    float distance = ((Vector3)clusters[candidate].aabbCenter - center).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = candidate;
                    }
                }
                if (best < 0) break;
                List<int> members = OwningMembersInRemaining(best, remaining, groups);
                int addCount = 0;
                for (int i = 0; i < members.Count; i++)
                    if (!group.Contains(members[i])) addCount++;
                if (group.Count + addCount > 4)
                {
                    skipped.Add(best);
                    continue;
                }
                for (int i = 0; i < members.Count; i++)
                {
                    int member = members[i];
                    if (group.Contains(member)) continue;
                    group.Add(member);
                    edges.UnionWith(BoundaryEdges(clusters, vertices, skin, indices, member, edgeCache));
                }
            }
            return group;
        }

        static List<int> OwningMembersInRemaining(int seed, List<int> remaining, List<ClusterGroup> groups)
        {
            var result = new List<int>();
            if (groups != null && ClusterMeshLod.TryGetOwningGroup(seed, groups, out int owning))
            {
                ClusterGroup group = groups[owning];
                for (int i = 0; i < remaining.Count; i++)
                {
                    int cluster = remaining[i];
                    if (cluster >= group.clusterStart && cluster < group.clusterStart + group.clusterCount)
                        result.Add(cluster);
                }
            }
            if (result.Count == 0) result.Add(seed);
            return result;
        }

        static bool TryEmitGroup(
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<ClusterSkinWeight> outputSkin,
            List<uint> indices,
            List<ClusterGroup> groups,
            List<int> group,
            ClusterMeshBakeSettings settings,
            ClusterSkinnedQemContext context,
            int level)
        {
            var pos = new List<Vector3>();
            var nrm = new List<Vector3>();
            var tan = new List<Vector4>();
            var uv = new List<Vector2>();
            var skin = new List<ClusterSkinWeight>();
            var tris = new List<int>();
            BuildWeldedGroupSoup(clusters, vertices, outputSkin, indices, group, pos, nrm, tan, uv, skin, tris);
            if (tris.Count < 3) return false;

            var sourcePositions = new List<Vector3>(pos);
            var sourceNormals = new List<Vector3>(nrm);
            var sourceTangents = new List<Vector4>(tan);
            var sourceUvs = new List<Vector2>(uv);
            var sourceTriangles = new List<int>(tris);
            int sourceTriangleCount = tris.Count / 3;
            var locked = new List<bool>();
            MarkLocked(pos, nrm, tan, uv, skin, tris, locked);
            int target = Mathf.Max(1, sourceTriangleCount / 2);
            int guard = pos.Count * 8 + 8;
            while (guard-- > 0 && tris.Count / 3 > target)
            {
                if (!ClusterSkinnedMeshQem.TryCollapse(pos, nrm, tan, uv, skin, tris, locked, context))
                    break;
            }
            if (tris.Count < 3 || tris.Count / 3 >= sourceTriangleCount)
                return false;
            if (CountBoundaryEdges(pos, nrm, tan, uv, skin, tris) >
                CountBoundaryEdges(sourcePositions, sourceNormals, sourceTangents, sourceUvs,
                    ExtractSourceSkin(clusters, vertices, outputSkin, indices, group), sourceTriangles))
                return false;

            float childError = 0f;
            Vector3 groupMin = (Vector3)clusters[group[0]].aabbCenter - (Vector3)clusters[group[0]].aabbExtents;
            Vector3 groupMax = (Vector3)clusters[group[0]].aabbCenter + (Vector3)clusters[group[0]].aabbExtents;
            for (int i = 0; i < group.Count; i++)
            {
                ClusterHeader child = clusters[group[i]];
                childError = Mathf.Max(childError, child.lodError);
                groupMin = Vector3.Min(groupMin, (Vector3)child.aabbCenter - (Vector3)child.aabbExtents);
                groupMax = Vector3.Max(groupMax, (Vector3)child.aabbCenter + (Vector3)child.aabbExtents);
            }
            float lodError = Mathf.Max(1e-6f, childError,
                ClusterMeshLodBaker.SurfaceDeviation(pos, sourcePositions, sourceTriangles),
                ClusterMeshLodBaker.SurfaceDeviation(sourcePositions, pos, tris));

            int startClusters = clusters.Count;
            int startVertices = vertices.Count;
            int startSkin = outputSkin.Count;
            int startIndices = indices.Count;
            ClusterSkinnedMeshBaker.ClusterTriangles(
                clusters[group[0]].materialIndex, new List<int>(tris), pos.ToArray(), nrm.ToArray(), tan.ToArray(), uv.ToArray(),
                skin.ToArray(), settings, clusters, vertices, outputSkin, indices, lodError, ClusterMeshLod.PackFlags(level));
            int newCount = clusters.Count - startClusters;
            int newTriangles = 0;
            for (int i = startClusters; i < clusters.Count; i++) newTriangles += (int)clusters[i].triangleCount;
            if (newCount <= 0 || (newCount >= group.Count && newTriangles >= sourceTriangleCount))
            {
                if (clusters.Count > startClusters) clusters.RemoveRange(startClusters, clusters.Count - startClusters);
                if (vertices.Count > startVertices) vertices.RemoveRange(startVertices, vertices.Count - startVertices);
                if (outputSkin.Count > startSkin) outputSkin.RemoveRange(startSkin, outputSkin.Count - startSkin);
                if (indices.Count > startIndices) indices.RemoveRange(startIndices, indices.Count - startIndices);
                return false;
            }

            int groupIndex = groups.Count;
            groups.Add(new ClusterGroup
            {
                clusterStart = startClusters,
                clusterCount = newCount,
                parentGroupIndex = ClusterMeshLod.NoParent,
                lodError = lodError,
                aabbCenter = (groupMin + groupMax) * 0.5f,
                aabbExtents = (groupMax - groupMin) * 0.5f
            });
            for (int i = 0; i < group.Count; i++)
            {
                ClusterHeader child = clusters[group[i]];
                child.parentIndex = groupIndex;
                clusters[group[i]] = child;
                LinkSourceGroup(groups, group, group[i], groupIndex);
            }
            return true;
        }

        public static void BuildWeldedGroupSoup(
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<ClusterSkinWeight> sourceSkin,
            List<uint> indices,
            List<int> group,
            List<Vector3> pos,
            List<Vector3> nrm,
            List<Vector4> tan,
            List<Vector2> uv,
            List<ClusterSkinWeight> skin,
            List<int> tris)
        {
            pos.Clear(); nrm.Clear(); tan.Clear(); uv.Clear(); skin.Clear(); tris.Clear();
            for (int g = 0; g < group.Count; g++)
            {
                ClusterHeader header = clusters[group[g]];
                int baseVertex = pos.Count;
                for (int v = 0; v < header.vertexCount; v++)
                {
                    int source = (int)header.vertexOffset + v;
                    ClusterVertex vertex = vertices[source];
                    pos.Add(vertex.position); nrm.Add(vertex.normal); tan.Add(vertex.tangent);
                    uv.Add(new Vector2(vertex.uv.x, vertex.uv.y)); skin.Add(sourceSkin[source]);
                }
                for (int t = 0; t < header.triangleCount; t++)
                {
                    int offset = (int)header.indexOffset + t * 3;
                    tris.Add(baseVertex + (int)indices[offset]);
                    tris.Add(baseVertex + (int)indices[offset + 1]);
                    tris.Add(baseVertex + (int)indices[offset + 2]);
                }
            }
            WeldSoup(pos, nrm, tan, uv, skin, tris);
        }

        static List<ClusterSkinWeight> ExtractSourceSkin(
            List<ClusterHeader> clusters, List<ClusterVertex> vertices, List<ClusterSkinWeight> sourceSkin,
            List<uint> indices, List<int> group)
        {
            var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var tan = new List<Vector4>();
            var uv = new List<Vector2>(); var skin = new List<ClusterSkinWeight>(); var tris = new List<int>();
            BuildWeldedGroupSoup(clusters, vertices, sourceSkin, indices, group, pos, nrm, tan, uv, skin, tris);
            return skin;
        }

        static void WeldSoup(List<Vector3> pos, List<Vector3> nrm, List<Vector4> tan, List<Vector2> uv,
            List<ClusterSkinWeight> skin, List<int> tris)
        {
            var map = new Dictionary<VertexKey, int>();
            var remap = new int[pos.Count];
            var newPos = new List<Vector3>(); var newNrm = new List<Vector3>(); var newTan = new List<Vector4>();
            var newUv = new List<Vector2>(); var newSkin = new List<ClusterSkinWeight>();
            for (int i = 0; i < pos.Count; i++)
            {
                VertexKey key = MakeKey(pos[i], nrm[i], tan[i], uv[i], skin[i]);
                if (!map.TryGetValue(key, out int canonical))
                {
                    canonical = newPos.Count;
                    map.Add(key, canonical);
                    newPos.Add(pos[i]); newNrm.Add(nrm[i]); newTan.Add(tan[i]); newUv.Add(uv[i]); newSkin.Add(skin[i]);
                }
                remap[i] = canonical;
            }
            var newTris = new List<int>();
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                int a = remap[tris[i]]; int b = remap[tris[i + 1]]; int c = remap[tris[i + 2]];
                if (a == b || b == c || a == c) continue;
                newTris.Add(a); newTris.Add(b); newTris.Add(c);
            }
            pos.Clear(); pos.AddRange(newPos); nrm.Clear(); nrm.AddRange(newNrm); tan.Clear(); tan.AddRange(newTan);
            uv.Clear(); uv.AddRange(newUv); skin.Clear(); skin.AddRange(newSkin); tris.Clear(); tris.AddRange(newTris);
        }

        static void MarkLocked(List<Vector3> pos, List<Vector3> nrm, List<Vector4> tan, List<Vector2> uv,
            List<ClusterSkinWeight> skin, List<int> tris, List<bool> locked)
        {
            locked.Clear();
            for (int i = 0; i < pos.Count; i++) locked.Add(false);
            var counts = CountEdges(pos, nrm, tan, uv, skin, tris);
            var boundary = new HashSet<VertexKey>();
            foreach (KeyValuePair<EdgeKey, int> pair in counts)
            {
                if (pair.Value != 1) continue;
                boundary.Add(pair.Key.a); boundary.Add(pair.Key.b);
            }
            for (int i = 0; i < pos.Count; i++)
                locked[i] = boundary.Contains(MakeKey(pos[i], nrm[i], tan[i], uv[i], skin[i]));
        }

        public static int CountBoundaryEdges(List<Vector3> pos, List<ClusterSkinWeight> skin, List<int> tris)
        {
            var nrm = new List<Vector3>(pos.Count);
            var tan = new List<Vector4>(pos.Count);
            var uv = new List<Vector2>(pos.Count);
            for (int i = 0; i < pos.Count; i++)
            {
                nrm.Add(Vector3.up);
                tan.Add(new Vector4(1f, 0f, 0f, 1f));
                uv.Add(Vector2.zero);
            }
            return CountBoundaryEdges(pos, nrm, tan, uv, skin, tris);
        }

        static int CountBoundaryEdges(List<Vector3> pos, List<Vector3> nrm, List<Vector4> tan, List<Vector2> uv,
            List<ClusterSkinWeight> skin, List<int> tris)
        {
            int result = 0;
            foreach (KeyValuePair<EdgeKey, int> pair in CountEdges(pos, nrm, tan, uv, skin, tris))
            {
                if (pair.Value == 1) result++;
            }
            return result;
        }

        static Dictionary<EdgeKey, int> CountEdges(List<Vector3> pos, List<Vector3> nrm, List<Vector4> tan,
            List<Vector2> uv, List<ClusterSkinWeight> skin, List<int> tris)
        {
            var result = new Dictionary<EdgeKey, int>();
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                int a=tris[i], b=tris[i+1], c=tris[i+2];
                VertexKey ka=MakeKey(pos[a],nrm[a],tan[a],uv[a],skin[a]);
                VertexKey kb=MakeKey(pos[b],nrm[b],tan[b],uv[b],skin[b]);
                VertexKey kc=MakeKey(pos[c],nrm[c],tan[c],uv[c],skin[c]);
                CountEdge(result, MakeEdge(ka, kb));
                CountEdge(result, MakeEdge(kb, kc));
                CountEdge(result, MakeEdge(kc, ka));
            }
            return result;
        }

        static HashSet<EdgeKey> BoundaryEdges(List<ClusterHeader> clusters, List<ClusterVertex> vertices,
            List<ClusterSkinWeight> skin, List<uint> indices, int cluster, Dictionary<int, HashSet<EdgeKey>> cache)
        {
            if (cache.TryGetValue(cluster, out HashSet<EdgeKey> result)) return result;
            ClusterHeader header = clusters[cluster];
            var counts = new Dictionary<EdgeKey, int>();
            for (int t = 0; t < header.triangleCount; t++)
            {
                int offset = (int)header.indexOffset + t * 3;
                int i0 = (int)header.vertexOffset + (int)indices[offset];
                int i1 = (int)header.vertexOffset + (int)indices[offset + 1];
                int i2 = (int)header.vertexOffset + (int)indices[offset + 2];
                ClusterVertex v0=vertices[i0], v1=vertices[i1], v2=vertices[i2];
                VertexKey k0=MakeKey(v0.position,v0.normal,v0.tangent,new Vector2(v0.uv.x,v0.uv.y),skin[i0]);
                VertexKey k1=MakeKey(v1.position,v1.normal,v1.tangent,new Vector2(v1.uv.x,v1.uv.y),skin[i1]);
                VertexKey k2=MakeKey(v2.position,v2.normal,v2.tangent,new Vector2(v2.uv.x,v2.uv.y),skin[i2]);
                CountEdge(counts, MakeEdge(k0,k1)); CountEdge(counts, MakeEdge(k1,k2)); CountEdge(counts, MakeEdge(k2,k0));
            }
            result = new HashSet<EdgeKey>();
            foreach (KeyValuePair<EdgeKey, int> pair in counts) if (pair.Value == 1) result.Add(pair.Key);
            cache.Add(cluster, result);
            return result;
        }

        static void CountEdge(Dictionary<EdgeKey, int> counts, EdgeKey edge)
        {
            counts.TryGetValue(edge, out int count); counts[edge] = count + 1;
        }

        static bool Shares(HashSet<EdgeKey> a, HashSet<EdgeKey> b)
        {
            foreach (EdgeKey edge in b) if (a.Contains(edge)) return true;
            return false;
        }

        static VertexKey MakeKey(Vector3 position, Vector3 normal, Vector4 tangent, Vector2 uv,
            in ClusterSkinWeight skin)
        {
            ClusterPackedSkinWeight packed = ClusterSkinnedMeshBaker.PackSkinWeight(skin);
            return new VertexKey
            {
                x = Mathf.RoundToInt(position.x * QuantizeScale),
                y = Mathf.RoundToInt(position.y * QuantizeScale),
                z = Mathf.RoundToInt(position.z * QuantizeScale),
                nx = Mathf.RoundToInt(normal.x * QuantizeScale),
                ny = Mathf.RoundToInt(normal.y * QuantizeScale),
                nz = Mathf.RoundToInt(normal.z * QuantizeScale),
                tx = Mathf.RoundToInt(tangent.x * QuantizeScale),
                ty = Mathf.RoundToInt(tangent.y * QuantizeScale),
                tz = Mathf.RoundToInt(tangent.z * QuantizeScale),
                tw = tangent.w >= 0f ? 1 : -1,
                u = Mathf.RoundToInt(uv.x * QuantizeScale),
                v = Mathf.RoundToInt(uv.y * QuantizeScale),
                indices01 = packed.boneIndices01, indices23 = packed.boneIndices23,
                weights01 = packed.boneWeights01, weights23 = packed.boneWeights23
            };
        }

        static EdgeKey MakeEdge(VertexKey a, VertexKey b)
        {
            return a.CompareTo(b) <= 0 ? new EdgeKey { a = a, b = b } : new EdgeKey { a = b, b = a };
        }

        static Vector3 AverageCenter(List<ClusterHeader> clusters, List<int> group)
        {
            Vector3 result = Vector3.zero;
            for (int i = 0; i < group.Count; i++) result += (Vector3)clusters[group[i]].aabbCenter;
            return result / group.Count;
        }

        static void RemoveAll(List<int> source, List<int> values)
        {
            for (int i = source.Count - 1; i >= 0; i--) if (values.Contains(source[i])) source.RemoveAt(i);
        }

        static void LinkSourceGroup(List<ClusterGroup> groups, List<int> members, int cluster, int parentGroup)
        {
            for (int i = 0; i < groups.Count - 1; i++)
            {
                ClusterGroup source = groups[i];
                if (cluster < source.clusterStart || cluster >= source.clusterStart + source.clusterCount) continue;
                bool all = true;
                for (int c = source.clusterStart; c < source.clusterStart + source.clusterCount; c++)
                    all &= members.Contains(c);
                if (!all) continue;
                source.parentGroupIndex = parentGroup;
                groups[i] = source;
            }
        }

        struct EdgeKey : IEquatable<EdgeKey>
        {
            public VertexKey a;
            public VertexKey b;
            public bool Equals(EdgeKey other) { return a.Equals(other.a) && b.Equals(other.b); }
            public override bool Equals(object obj) { return obj is EdgeKey other && Equals(other); }
            public override int GetHashCode() { unchecked { return a.GetHashCode() * 397 ^ b.GetHashCode(); } }
        }

        struct VertexKey : IEquatable<VertexKey>, IComparable<VertexKey>
        {
            public int x, y, z, nx, ny, nz, tx, ty, tz, tw, u, v;
            public uint indices01, indices23, weights01, weights23;
            public bool Equals(VertexKey other)
            {
                return x == other.x && y == other.y && z == other.z && nx == other.nx && ny == other.ny &&
                    nz == other.nz && tx == other.tx && ty == other.ty && tz == other.tz && tw == other.tw &&
                    u == other.u && v == other.v && indices01 == other.indices01 &&
                    indices23 == other.indices23 && weights01 == other.weights01 && weights23 == other.weights23;
            }
            public override bool Equals(object obj) { return obj is VertexKey other && Equals(other); }
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = x; hash = hash * 397 ^ y; hash = hash * 397 ^ z;
                    hash = hash * 397 ^ nx; hash = hash * 397 ^ ny; hash = hash * 397 ^ nz;
                    hash = hash * 397 ^ tx; hash = hash * 397 ^ ty; hash = hash * 397 ^ tz;
                    hash = hash * 397 ^ tw; hash = hash * 397 ^ u; hash = hash * 397 ^ v;
                    hash = hash * 397 ^ (int)indices01; hash = hash * 397 ^ (int)indices23;
                    hash = hash * 397 ^ (int)weights01; return hash * 397 ^ (int)weights23;
                }
            }
            public int CompareTo(VertexKey other)
            {
                int value = x.CompareTo(other.x); if (value != 0) return value;
                value = y.CompareTo(other.y); if (value != 0) return value;
                value = z.CompareTo(other.z); if (value != 0) return value;
                value = nx.CompareTo(other.nx); if (value != 0) return value;
                value = ny.CompareTo(other.ny); if (value != 0) return value;
                value = nz.CompareTo(other.nz); if (value != 0) return value;
                value = tx.CompareTo(other.tx); if (value != 0) return value;
                value = ty.CompareTo(other.ty); if (value != 0) return value;
                value = tz.CompareTo(other.tz); if (value != 0) return value;
                value = tw.CompareTo(other.tw); if (value != 0) return value;
                value = u.CompareTo(other.u); if (value != 0) return value;
                value = v.CompareTo(other.v); if (value != 0) return value;
                value = indices01.CompareTo(other.indices01); if (value != 0) return value;
                value = indices23.CompareTo(other.indices23); if (value != 0) return value;
                value = weights01.CompareTo(other.weights01); return value != 0 ? value : weights23.CompareTo(other.weights23);
            }
        }
    }
}
