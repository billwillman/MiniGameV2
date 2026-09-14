using System.Collections.Generic;
using UnityEngine;

namespace ClusterMesh
{
    public sealed class ClusterSkinnedQemContext
    {
        public readonly List<Matrix4x4[]> posePalettes = new List<Matrix4x4[]>();
        public float skinningErrorWeight = 4f;
    }

    public static class ClusterSkinnedMeshQem
    {
        const float NormalWeight = 1f;
        const float UvWeight = 1f;

        public static bool TryCollapse(
            List<Vector3> pos,
            List<Vector3> nrm,
            List<Vector4> tan,
            List<Vector2> uv,
            List<ClusterSkinWeight> skin,
            List<int> tris,
            List<bool> locked,
            ClusterSkinnedQemContext context)
        {
            if (pos == null || skin == null || tris == null || locked == null ||
                pos.Count == 0 || skin.Count != pos.Count)
                return false;

            Quadric[] quadrics = BuildQuadrics(pos, tris);
            List<int>[] adjacency = BuildAdjacency(pos.Count, tris);
            int bestA = -1;
            int bestB = -1;
            Vector3 bestPosition = default;
            ClusterSkinWeight bestSkin = default;
            float bestCost = float.MaxValue;
            var seen = new HashSet<long>();
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Consider(pos, nrm, uv, skin, tris, locked, quadrics, adjacency, seen,
                    tris[t], tris[t + 1], context, ref bestA, ref bestB, ref bestPosition, ref bestSkin, ref bestCost);
                Consider(pos, nrm, uv, skin, tris, locked, quadrics, adjacency, seen,
                    tris[t + 1], tris[t + 2], context, ref bestA, ref bestB, ref bestPosition, ref bestSkin, ref bestCost);
                Consider(pos, nrm, uv, skin, tris, locked, quadrics, adjacency, seen,
                    tris[t + 2], tris[t], context, ref bestA, ref bestB, ref bestPosition, ref bestSkin, ref bestCost);
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
                float edgeT = EdgeParameter(bestPosition, pos[bestA], pos[bestB]);
                pos[keep] = bestPosition;
                nrm[keep] = Vector3.Lerp(nrm[bestA], nrm[bestB], edgeT).normalized;
                tan[keep] = Vector4.Lerp(tan[bestA], tan[bestB], edgeT);
                uv[keep] = Vector2.Lerp(uv[bestA], uv[bestB], edgeT);
                skin[keep] = bestSkin;
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
            skin.RemoveAt(drop);
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

        public static ClusterSkinWeight BlendWeights(in ClusterSkinWeight a, in ClusterSkinWeight b, float t)
        {
            t = Mathf.Clamp01(t);
            var sums = new Dictionary<int, float>(8);
            Accumulate(sums, a, 1f - t);
            Accumulate(sums, b, t);
            var influences = new List<KeyValuePair<int, float>>(sums);
            influences.Sort((x, y) =>
            {
                int weightOrder = y.Value.CompareTo(x.Value);
                return weightOrder != 0 ? weightOrder : x.Key.CompareTo(y.Key);
            });
            while (influences.Count > 4)
                influences.RemoveAt(influences.Count - 1);
            float sum = 0f;
            for (int i = 0; i < influences.Count; i++)
                sum += Mathf.Max(0f, influences[i].Value);
            if (sum <= 1e-8f)
                return new ClusterSkinWeight { boneIndex0 = 0, weight0 = 1f };

            ClusterSkinWeight result = default;
            for (int i = 0; i < influences.Count; i++)
                SetInfluence(ref result, i, influences[i].Key, Mathf.Max(0f, influences[i].Value) / sum);
            return result;
        }

        static void Consider(
            List<Vector3> pos,
            List<Vector3> nrm,
            List<Vector2> uv,
            List<ClusterSkinWeight> skin,
            List<int> tris,
            List<bool> locked,
            Quadric[] quadrics,
            List<int>[] adjacency,
            HashSet<long> seen,
            int a,
            int b,
            ClusterSkinnedQemContext context,
            ref int bestA,
            ref int bestB,
            ref Vector3 bestPosition,
            ref ClusterSkinWeight bestSkin,
            ref float bestCost)
        {
            if (a == b || (locked[a] && locked[b]))
                return;
            int lo = Mathf.Min(a, b);
            int hi = Mathf.Max(a, b);
            if (!seen.Add(((long)lo << 32) | (uint)hi))
                return;

            Vector3 destination;
            if (locked[a])
                destination = pos[a];
            else if (locked[b])
                destination = pos[b];
            else
                destination = ClampToEdge(Optimal(quadrics[a] + quadrics[b], pos[a], pos[b]), pos[a], pos[b]);
            float edgeT = EdgeParameter(destination, pos[a], pos[b]);
            ClusterSkinWeight destinationSkin = locked[a] ? skin[a] : locked[b] ? skin[b] : BlendWeights(skin[a], skin[b], edgeT);
            Quadric quadric = quadrics[a] + quadrics[b];
            float cost = quadric.Error(destination)
                + NormalWeight * (nrm[a] - nrm[b]).sqrMagnitude
                + UvWeight * (uv[a] - uv[b]).sqrMagnitude
                + PoseError(pos[a], pos[b], destination, skin[a], skin[b], destinationSkin, edgeT, context);
            if (cost >= bestCost)
                return;

            int keep = locked[a] ? a : locked[b] ? b : Mathf.Min(a, b);
            int drop = keep == a ? b : a;
            if (WouldFlip(pos, tris, adjacency, keep, drop, destination))
                return;
            bestA = a;
            bestB = b;
            bestPosition = destination;
            bestSkin = destinationSkin;
            bestCost = cost;
        }

        static float PoseError(
            Vector3 a,
            Vector3 b,
            Vector3 destination,
            in ClusterSkinWeight skinA,
            in ClusterSkinWeight skinB,
            in ClusterSkinWeight destinationSkin,
            float t,
            ClusterSkinnedQemContext context)
        {
            if (context == null || context.posePalettes.Count == 0)
                return WeightDistance(skinA, skinB);
            float error = 0f;
            for (int i = 0; i < context.posePalettes.Count; i++)
            {
                Matrix4x4[] palette = context.posePalettes[i];
                Vector3 offsetA = Skin(a, skinA, palette) - a;
                Vector3 offsetB = Skin(b, skinB, palette) - b;
                Vector3 offsetDestination = Skin(destination, destinationSkin, palette) - destination;
                Vector3 expectedOffset = Vector3.Lerp(offsetA, offsetB, t);
                float fittedError = (offsetDestination - expectedOffset).sqrMagnitude;
                float deformationGradient = (offsetA - offsetB).sqrMagnitude;
                error = Mathf.Max(error, fittedError + deformationGradient * 0.25f);
            }
            return (error + WeightDistance(skinA, skinB) * 0.1f) *
                Mathf.Max(0f, context.skinningErrorWeight);
        }

        static Vector3 Skin(Vector3 position, in ClusterSkinWeight weight, Matrix4x4[] palette)
        {
            Vector3 result = Vector3.zero;
            float sum = 0f;
            for (int i = 0; i < 4; i++)
            {
                float w = weight.GetWeight(i);
                int bone = weight.GetBoneIndex(i);
                if (w <= 0f || palette == null || bone < 0 || bone >= palette.Length)
                    continue;
                result += palette[bone].MultiplyPoint3x4(position) * w;
                sum += w;
            }
            return sum > 1e-8f ? result / sum : position;
        }

        static float WeightDistance(in ClusterSkinWeight a, in ClusterSkinWeight b)
        {
            var values = new Dictionary<int, float>(8);
            Accumulate(values, a, 1f);
            Accumulate(values, b, -1f);
            float result = 0f;
            foreach (var pair in values)
                result += pair.Value * pair.Value;
            return result;
        }

        static void Accumulate(Dictionary<int, float> sums, in ClusterSkinWeight weight, float multiplier)
        {
            for (int i = 0; i < 4; i++)
            {
                float value = weight.GetWeight(i) * multiplier;
                if (Mathf.Abs(value) <= 1e-8f)
                    continue;
                int bone = weight.GetBoneIndex(i);
                sums.TryGetValue(bone, out float old);
                sums[bone] = old + value;
            }
        }

        static void SetInfluence(ref ClusterSkinWeight weight, int slot, int bone, float value)
        {
            switch (slot)
            {
                case 0: weight.boneIndex0 = bone; weight.weight0 = value; break;
                case 1: weight.boneIndex1 = bone; weight.weight1 = value; break;
                case 2: weight.boneIndex2 = bone; weight.weight2 = value; break;
                case 3: weight.boneIndex3 = bone; weight.weight3 = value; break;
            }
        }

        static float EdgeParameter(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 edge = b - a;
            float denominator = Vector3.Dot(edge, edge);
            return denominator > 1e-12f ? Mathf.Clamp01(Vector3.Dot(point - a, edge) / denominator) : 0f;
        }

        static Vector3 ClampToEdge(Vector3 point, Vector3 a, Vector3 b)
        {
            return Vector3.Lerp(a, b, EdgeParameter(point, a, b));
        }

        static List<int>[] BuildAdjacency(int vertexCount, List<int> tris)
        {
            var result = new List<int>[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                result[i] = new List<int>(6);
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                result[tris[t]].Add(t);
                result[tris[t + 1]].Add(t);
                result[tris[t + 2]].Add(t);
            }
            return result;
        }

        static bool WouldFlip(List<Vector3> pos, List<int> tris, List<int>[] adjacency, int keep, int drop, Vector3 newPosition)
        {
            return WouldFlipIncident(pos, tris, adjacency[keep], keep, drop, newPosition)
                || WouldFlipIncident(pos, tris, adjacency[drop], keep, drop, newPosition);
        }

        static bool WouldFlipIncident(List<Vector3> pos, List<int> tris, List<int> incident, int keep, int drop, Vector3 newPosition)
        {
            for (int n = 0; n < incident.Count; n++)
            {
                int t = incident[n];
                int i0 = tris[t];
                int i1 = tris[t + 1];
                int i2 = tris[t + 2];
                Vector3 oldNormal = Vector3.Cross(pos[i1] - pos[i0], pos[i2] - pos[i0]);
                if (i0 == drop) i0 = keep;
                if (i1 == drop) i1 = keep;
                if (i2 == drop) i2 = keep;
                if (i0 == i1 || i1 == i2 || i0 == i2)
                    continue;
                Vector3 a = i0 == keep ? newPosition : pos[i0];
                Vector3 b = i1 == keep ? newPosition : pos[i1];
                Vector3 c = i2 == keep ? newPosition : pos[i2];
                Vector3 newNormal = Vector3.Cross(b - a, c - a);
                if (oldNormal.sqrMagnitude >= 1e-12f && newNormal.sqrMagnitude >= 1e-12f && Vector3.Dot(oldNormal, newNormal) < 0f)
                    return true;
            }
            return false;
        }

        static Quadric[] BuildQuadrics(List<Vector3> pos, List<int> tris)
        {
            var quadrics = new Quadric[pos.Count];
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Vector3 a = pos[tris[t]];
                Vector3 b = pos[tris[t + 1]];
                Vector3 c = pos[tris[t + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                float area = normal.magnitude * 0.5f;
                if (area < 1e-12f)
                    continue;
                normal.Normalize();
                Quadric quadric = Quadric.FromPlane(normal, -Vector3.Dot(normal, a), area);
                quadrics[tris[t]] += quadric;
                quadrics[tris[t + 1]] += quadric;
                quadrics[tris[t + 2]] += quadric;
            }
            return quadrics;
        }

        static Vector3 Optimal(Quadric quadric, Vector3 a, Vector3 b)
        {
            if (quadric.TrySolve(out Vector3 result))
                return result;
            Vector3 middle = (a + b) * 0.5f;
            float errorA = quadric.Error(a);
            float errorB = quadric.Error(b);
            float errorMiddle = quadric.Error(middle);
            if (errorA <= errorB && errorA <= errorMiddle) return a;
            return errorB <= errorMiddle ? b : middle;
        }

        struct Quadric
        {
            public float q00, q01, q02, q03;
            public float q11, q12, q13;
            public float q22, q23, q33;

            public static Quadric FromPlane(Vector3 n, float d, float area)
            {
                return new Quadric
                {
                    q00 = area * n.x * n.x, q01 = area * n.x * n.y, q02 = area * n.x * n.z, q03 = area * n.x * d,
                    q11 = area * n.y * n.y, q12 = area * n.y * n.z, q13 = area * n.y * d,
                    q22 = area * n.z * n.z, q23 = area * n.z * d, q33 = area * d * d
                };
            }

            public static Quadric operator +(Quadric a, Quadric b)
            {
                return new Quadric
                {
                    q00 = a.q00 + b.q00, q01 = a.q01 + b.q01, q02 = a.q02 + b.q02, q03 = a.q03 + b.q03,
                    q11 = a.q11 + b.q11, q12 = a.q12 + b.q12, q13 = a.q13 + b.q13,
                    q22 = a.q22 + b.q22, q23 = a.q23 + b.q23, q33 = a.q33 + b.q33
                };
            }

            public float Error(Vector3 p)
            {
                return q00 * p.x * p.x + 2f * q01 * p.x * p.y + 2f * q02 * p.x * p.z + 2f * q03 * p.x
                    + q11 * p.y * p.y + 2f * q12 * p.y * p.z + 2f * q13 * p.y
                    + q22 * p.z * p.z + 2f * q23 * p.z + q33;
            }

            public bool TrySolve(out Vector3 p)
            {
                float determinant = q00 * (q11 * q22 - q12 * q12)
                    - q01 * (q01 * q22 - q12 * q02)
                    + q02 * (q01 * q12 - q11 * q02);
                if (Mathf.Abs(determinant) < 1e-10f)
                {
                    p = default;
                    return false;
                }
                float x = -q03;
                float y = -q13;
                float z = -q23;
                p = new Vector3(
                    (x * (q11 * q22 - q12 * q12) - q01 * (y * q22 - q12 * z) + q02 * (y * q12 - q11 * z)) / determinant,
                    (q00 * (y * q22 - q12 * z) - x * (q01 * q22 - q12 * q02) + q02 * (q01 * z - y * q02)) / determinant,
                    (q00 * (q11 * z - y * q12) - q01 * (q01 * z - y * q02) + x * (q01 * q12 - q11 * q02)) / determinant);
                return true;
            }
        }
    }
}
