using System.Collections.Generic;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshQem
    {
        public const float QemNormalWeight = 1f;
        public const float QemUvWeight = 1f;

        public static bool TryCollapseQem(
            List<Vector3> pos,
            List<Vector3> nrm,
            List<Vector4> tan,
            List<Vector2> uv,
            List<int> tris,
            List<bool> locked)
        {
            if (pos == null || tris == null || locked == null || pos.Count == 0)
                return false;

            Quadric[] quads = BuildQuadrics(pos, tris);
            List<int>[] adj = BuildAdj(pos.Count, tris);
            int bestA = -1;
            int bestB = -1;
            Vector3 bestDest = default;
            float bestCost = float.MaxValue;
            var seen = new HashSet<long>();

            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Consider(pos, nrm, uv, tris, locked, quads, adj, seen, tris[t], tris[t + 1], ref bestA, ref bestB, ref bestDest, ref bestCost);
                Consider(pos, nrm, uv, tris, locked, quads, adj, seen, tris[t + 1], tris[t + 2], ref bestA, ref bestB, ref bestDest, ref bestCost);
                Consider(pos, nrm, uv, tris, locked, quads, adj, seen, tris[t + 2], tris[t], ref bestA, ref bestB, ref bestDest, ref bestCost);
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
                pos[keep] = bestDest;
                nrm[keep] = (nrm[keep] + nrm[drop]).normalized;
                uv[keep] = (uv[keep] + uv[drop]) * 0.5f;
                tan[keep] = (tan[keep] + tan[drop]) * 0.5f;
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
            List<Vector3> nrm,
            List<Vector2> uv,
            List<int> tris,
            List<bool> locked,
            Quadric[] quads,
            List<int>[] adj,
            HashSet<long> seen,
            int a,
            int b,
            ref int bestA,
            ref int bestB,
            ref Vector3 bestDest,
            ref float bestCost)
        {
            if (a == b)
                return;
            if (locked[a] && locked[b])
                return;
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            if (!seen.Add(((long)lo << 32) | (uint)hi))
                return;

            Vector3 dest;
            if (locked[a] && !locked[b])
                dest = pos[a];
            else if (locked[b] && !locked[a])
                dest = pos[b];
            else
                dest = ClampToEdge(Optimal(quads[a] + quads[b], pos[a], pos[b]), pos[a], pos[b]);

            Quadric q = quads[a] + quads[b];
            float cost = q.Error(dest)
                + QemNormalWeight * (nrm[a] - nrm[b]).sqrMagnitude
                + QemUvWeight * (uv[a] - uv[b]).sqrMagnitude;
            if (cost >= bestCost)
                return;

            int keep = locked[a] && !locked[b] ? a : locked[b] && !locked[a] ? b : Mathf.Min(a, b);
            int drop = keep == a ? b : a;
            if (WouldFlip(pos, tris, adj, keep, drop, dest))
                return;

            bestCost = cost;
            bestA = a;
            bestB = b;
            bestDest = dest;
        }

        static List<int>[] BuildAdj(int vertCount, List<int> tris)
        {
            var adj = new List<int>[vertCount];
            for (int i = 0; i < vertCount; i++)
                adj[i] = new List<int>(6);
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                adj[tris[t]].Add(t);
                adj[tris[t + 1]].Add(t);
                adj[tris[t + 2]].Add(t);
            }

            return adj;
        }

        static bool WouldFlip(List<Vector3> pos, List<int> tris, List<int>[] adj, int keep, int drop, Vector3 newKeepPos)
        {
            return WouldFlipIncident(pos, tris, adj[keep], keep, drop, newKeepPos)
                || WouldFlipIncident(pos, tris, adj[drop], keep, drop, newKeepPos);
        }

        static bool WouldFlipIncident(List<Vector3> pos, List<int> tris, List<int> incident, int keep, int drop, Vector3 newKeepPos)
        {
            for (int n = 0; n < incident.Count; n++)
            {
                int t = incident[n];
                int i0 = tris[t];
                int i1 = tris[t + 1];
                int i2 = tris[t + 2];
                Vector3 oldN = Vector3.Cross(pos[i1] - pos[i0], pos[i2] - pos[i0]);
                if (i0 == drop)
                    i0 = keep;
                if (i1 == drop)
                    i1 = keep;
                if (i2 == drop)
                    i2 = keep;
                if (i0 == i1 || i1 == i2 || i0 == i2)
                    continue;
                Vector3 na = i0 == keep ? newKeepPos : pos[i0];
                Vector3 nb = i1 == keep ? newKeepPos : pos[i1];
                Vector3 nc = i2 == keep ? newKeepPos : pos[i2];
                Vector3 newN = Vector3.Cross(nb - na, nc - na);
                if (oldN.sqrMagnitude < 1e-12f || newN.sqrMagnitude < 1e-12f)
                    continue;
                if (Vector3.Dot(oldN, newN) < 0f)
                    return true;
            }

            return false;
        }

        static Quadric[] BuildQuadrics(List<Vector3> pos, List<int> tris)
        {
            var quads = new Quadric[pos.Count];
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Vector3 a = pos[tris[t]];
                Vector3 b = pos[tris[t + 1]];
                Vector3 c = pos[tris[t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                float area = n.magnitude * 0.5f;
                if (area < 1e-12f)
                    continue;
                n /= n.magnitude;
                float d = -Vector3.Dot(n, a);
                var q = Quadric.FromPlane(n, d, area);
                quads[tris[t]] += q;
                quads[tris[t + 1]] += q;
                quads[tris[t + 2]] += q;
            }

            return quads;
        }

        static Vector3 Optimal(Quadric q, Vector3 fallbackA, Vector3 fallbackB)
        {
            if (q.TrySolve(out Vector3 p))
                return p;
            float ea = q.Error(fallbackA);
            float eb = q.Error(fallbackB);
            Vector3 mid = (fallbackA + fallbackB) * 0.5f;
            float em = q.Error(mid);
            if (ea <= eb && ea <= em)
                return fallbackA;
            if (eb <= em)
                return fallbackB;
            return mid;
        }

        static Vector3 ClampToEdge(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float denom = Vector3.Dot(ab, ab);
            if (denom < 1e-12f)
                return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / denom);
            return a + ab * t;
        }

        struct Quadric
        {
            public float q00, q01, q02, q03;
            public float q11, q12, q13;
            public float q22, q23;
            public float q33;

            public static Quadric FromPlane(Vector3 n, float d, float area)
            {
                float a = n.x;
                float b = n.y;
                float c = n.z;
                return new Quadric
                {
                    q00 = area * a * a,
                    q01 = area * a * b,
                    q02 = area * a * c,
                    q03 = area * a * d,
                    q11 = area * b * b,
                    q12 = area * b * c,
                    q13 = area * b * d,
                    q22 = area * c * c,
                    q23 = area * c * d,
                    q33 = area * d * d
                };
            }

            public static Quadric operator +(Quadric x, Quadric y)
            {
                return new Quadric
                {
                    q00 = x.q00 + y.q00,
                    q01 = x.q01 + y.q01,
                    q02 = x.q02 + y.q02,
                    q03 = x.q03 + y.q03,
                    q11 = x.q11 + y.q11,
                    q12 = x.q12 + y.q12,
                    q13 = x.q13 + y.q13,
                    q22 = x.q22 + y.q22,
                    q23 = x.q23 + y.q23,
                    q33 = x.q33 + y.q33
                };
            }

            public float Error(Vector3 p)
            {
                float x = p.x;
                float y = p.y;
                float z = p.z;
                return q00 * x * x + 2f * q01 * x * y + 2f * q02 * x * z + 2f * q03 * x
                    + q11 * y * y + 2f * q12 * y * z + 2f * q13 * y
                    + q22 * z * z + 2f * q23 * z
                    + q33;
            }

            public bool TrySolve(out Vector3 p)
            {
                float a = q00;
                float b = q01;
                float c = q02;
                float d = q01;
                float e = q11;
                float f = q12;
                float g = q02;
                float h = q12;
                float i = q22;
                float det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
                if (Mathf.Abs(det) < 1e-10f)
                {
                    p = default;
                    return false;
                }

                float x1 = -q03;
                float y1 = -q13;
                float z1 = -q23;
                p = new Vector3(
                    (x1 * (e * i - f * h) - b * (y1 * i - f * z1) + c * (y1 * h - e * z1)) / det,
                    (a * (y1 * i - f * z1) - x1 * (d * i - f * g) + c * (d * z1 - y1 * g)) / det,
                    (a * (e * z1 - y1 * h) - b * (d * z1 - y1 * g) + x1 * (d * h - e * g)) / det);
                return true;
            }
        }
    }
}
