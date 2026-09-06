using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshQemTests
    {
        [Test]
        public void Settings_Default_UseQemSimplifyIsTrue()
        {
            Assert.That(new ClusterMeshBakeSettings().useQemSimplify, Is.True);
        }

        [Test]
        public void ShowsQemToggle_OnlyWhenHierarchyOn()
        {
            Assert.That(ClusterMeshBakerWindow.ShowsQemToggle(true), Is.True);
            Assert.That(ClusterMeshBakerWindow.ShowsQemToggle(false), Is.False);
        }

        [Test]
        public void TryCollapseQem_LockLock_DoesNotCollapse()
        {
            var pos = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(0f, 1f, 0f)
            };
            var nrm = Nrm(4);
            var tan = Tan(4);
            var uv = Uv(4);
            var tris = new List<int> { 0, 1, 2, 0, 2, 3 };
            var locked = new List<bool> { true, true, true, true };
            Assert.That(ClusterMeshQem.TryCollapseQem(pos, nrm, tan, uv, tris, locked), Is.False);
            Assert.That(pos.Count, Is.EqualTo(4));
            Assert.That(tris.Count / 3, Is.EqualTo(2));
        }

        [Test]
        public void TryCollapseQem_UnlockToLock_KeepsLockPosition()
        {
            var lockedA = new Vector3(0f, 0f, 0f);
            var lockedB = new Vector3(1f, 0f, 0f);
            var pos = new List<Vector3>
            {
                lockedA,
                lockedB,
                new Vector3(0.5f, 1f, 0f)
            };
            var nrm = Nrm(3);
            var tan = Tan(3);
            var uv = Uv(3);
            var tris = new List<int> { 0, 1, 2 };
            var locked = new List<bool> { true, true, false };
            Assert.That(ClusterMeshQem.TryCollapseQem(pos, nrm, tan, uv, tris, locked), Is.True);
            Assert.That(pos.Count, Is.EqualTo(2));
            Assert.That((pos[0] - lockedA).sqrMagnitude, Is.LessThan(1e-10f));
            Assert.That((pos[1] - lockedB).sqrMagnitude, Is.LessThan(1e-10f));
        }

        [Test]
        public void Weights_AreOne()
        {
            Assert.That(ClusterMeshQem.QemNormalWeight, Is.EqualTo(1f));
            Assert.That(ClusterMeshQem.QemUvWeight, Is.EqualTo(1f));
        }

        [Test]
        public void TryCollapseQemAndShortest_BumpyBorderLockedGrid_BothReachHalf_QemKeepsLocks()
        {
            BuildBumpyGrid(4, out List<Vector3> qPos, out List<Vector3> qNrm, out List<Vector4> qTan, out List<Vector2> qUv, out List<int> qTris, out List<bool> qLocked, out List<Vector3> lockPos);
            var sPos = new List<Vector3>(qPos);
            var sNrm = new List<Vector3>(qNrm);
            var sTan = new List<Vector4>(qTan);
            var sUv = new List<Vector2>(qUv);
            var sTris = new List<int>(qTris);
            var sLocked = new List<bool>(qLocked);

            int srcTris = qTris.Count / 3;
            int target = Mathf.Max(1, srcTris / 2);
            CollapseTo(target, qPos, qNrm, qTan, qUv, qTris, qLocked, useQem: true);
            CollapseTo(target, sPos, sNrm, sTan, sUv, sTris, sLocked, useQem: false);

            Assert.That(qTris.Count / 3, Is.LessThanOrEqualTo(target));
            Assert.That(sTris.Count / 3, Is.LessThanOrEqualTo(target));
            for (int i = 0; i < lockPos.Count; i++)
            {
                bool found = false;
                for (int v = 0; v < qPos.Count && !found; v++)
                    found = (qPos[v] - lockPos[i]).sqrMagnitude <= 1e-8f;
                Assert.That(found, Is.True, "locked vertex moved or deleted");
            }
        }

        [Test]
        public void Bake_UseQem_FourQuads_LockedBorderInParents_LodErrorIsMeters()
        {
            var mesh = ClusterMeshTestMeshes.BumpyFourQuads();
            var settings = new ClusterMeshBakeSettings
            {
                maxVerticesPerCluster = 4,
                maxTrianglesPerCluster = 2,
                useQemSimplify = true
            };
            var result = ClusterMeshBaker.Bake(mesh, new Material[1], settings);
            var children = new List<int>();
            for (int i = 0; i < result.clusters.Length; i++)
            {
                if (result.clusters[i].parentIndex == 0)
                    children.Add(i);
            }

            Assert.That(children.Count, Is.EqualTo(4));
            var locked = new List<Vector3>();
            ClusterMeshLodBaker.GetGroupLockedPositions(
                new List<ClusterHeader>(result.clusters),
                new List<ClusterVertex>(result.vertices),
                new List<uint>(result.indices),
                children,
                locked);
            Assert.That(locked.Count, Is.GreaterThan(0));

            ClusterGroup g0 = result.groups[0];
            float maxErr = 0f;
            for (int c = 0; c < g0.clusterCount; c++)
                maxErr = Mathf.Max(maxErr, result.clusters[g0.clusterStart + c].lodError);
            for (int i = 0; i < locked.Count; i++)
            {
                bool found = false;
                for (int c = 0; c < g0.clusterCount && !found; c++)
                {
                    ClusterHeader p = result.clusters[g0.clusterStart + c];
                    for (int v = 0; v < (int)p.vertexCount; v++)
                    {
                        Vector3 pv = result.vertices[(int)p.vertexOffset + v].position;
                        if ((pv - locked[i]).sqrMagnitude <= 1e-8f)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                Assert.That(found, Is.True, "locked border vertex missing from parent clusters");
            }

            Assert.That(maxErr, Is.GreaterThan(0f));
            Assert.That(maxErr, Is.LessThan(10f));
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        static void CollapseTo(
            int target,
            List<Vector3> pos,
            List<Vector3> nrm,
            List<Vector4> tan,
            List<Vector2> uv,
            List<int> tris,
            List<bool> locked,
            bool useQem)
        {
            int guard = pos.Count * 8 + 8;
            while (guard-- > 0 && tris.Count / 3 > target)
            {
                bool ok = useQem
                    ? ClusterMeshQem.TryCollapseQem(pos, nrm, tan, uv, tris, locked)
                    : ClusterMeshLodBaker.TryCollapseShortest(pos, nrm, tan, uv, tris, locked);
                if (!ok)
                    break;
            }
        }

        static void BuildBumpyGrid(
            int quads,
            out List<Vector3> pos,
            out List<Vector3> nrm,
            out List<Vector4> tan,
            out List<Vector2> uv,
            out List<int> tris,
            out List<bool> locked,
            out List<Vector3> lockPos)
        {
            int verts = quads + 1;
            pos = new List<Vector3>();
            locked = new List<bool>();
            lockPos = new List<Vector3>();
            for (int y = 0; y < verts; y++)
            {
                for (int x = 0; x < verts; x++)
                {
                    bool border = x == 0 || y == 0 || x == quads || y == quads;
                    float h = border ? 0f : 0.35f * ((x + y) % 2);
                    var p = new Vector3(x, h, y);
                    pos.Add(p);
                    locked.Add(border);
                    if (border)
                        lockPos.Add(p);
                }
            }

            nrm = Nrm(pos.Count);
            tan = Tan(pos.Count);
            uv = Uv(pos.Count);
            tris = new List<int>();
            for (int y = 0; y < quads; y++)
            {
                for (int x = 0; x < quads; x++)
                {
                    int i = y * verts + x;
                    tris.Add(i);
                    tris.Add(i + 1);
                    tris.Add(i + verts + 1);
                    tris.Add(i);
                    tris.Add(i + verts + 1);
                    tris.Add(i + verts);
                }
            }
        }

        static List<Vector3> Nrm(int n)
        {
            var list = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
                list.Add(Vector3.up);
            return list;
        }

        static List<Vector4> Tan(int n)
        {
            var list = new List<Vector4>(n);
            for (int i = 0; i < n; i++)
                list.Add(new Vector4(1f, 0f, 0f, 1f));
            return list;
        }

        static List<Vector2> Uv(int n)
        {
            var list = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
                list.Add(new Vector2(i, 0f));
            return list;
        }
    }
}
