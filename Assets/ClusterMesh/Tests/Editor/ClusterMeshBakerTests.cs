using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshBakerTests
    {
        [Test]
        public void Bake_SingleTriangle_OneClusterWithinBudget()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());

            Assert.That(result.clusters.Length, Is.EqualTo(1));
            Assert.That(result.clusters[0].vertexCount, Is.EqualTo(3u));
            Assert.That(result.clusters[0].triangleCount, Is.EqualTo(1u));
            Assert.That(result.clusters[0].materialIndex, Is.EqualTo(0u));
            Assert.That(result.vertices.Length, Is.EqualTo(3));
            Assert.That(result.indices.Length, Is.EqualTo(3));
            Assert.That(ContainsPoint(result.clusters[0], result.vertices[0].position));
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_GridLargerThanBudget_SplitsAndCoversEveryTriangleOnce()
        {
            var mesh = ClusterMeshTestMeshes.Grid(16, 16);
            Assert.That(mesh.vertexCount, Is.GreaterThan(ClusterMeshLimits.MaxVerticesPerCluster));

            var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            Assert.That(result.clusters.Length, Is.GreaterThan(1));

            int sourceTris = mesh.triangles.Length / 3;
            int leafTris = 0;
            foreach (var cluster in result.clusters)
            {
                Assert.That(cluster.vertexCount, Is.LessThanOrEqualTo((uint)ClusterMeshLimits.MaxVerticesPerCluster));
                Assert.That(cluster.triangleCount, Is.LessThanOrEqualTo((uint)ClusterMeshLimits.MaxTrianglesPerCluster));
                if (!ClusterMeshLod.IsParent(cluster.flags))
                    leafTris += (int)cluster.triangleCount;
            }

            Assert.That(leafTris, Is.EqualTo(sourceTris));
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_TwoSubmeshes_SetsDistinctMaterialIndices()
        {
            var mesh = ClusterMeshTestMeshes.TwoSubmeshTriangles();
            var result = ClusterMeshBaker.Bake(mesh, new Material[2], new ClusterMeshBakeSettings());

            Assert.That(result.clusters.Length, Is.EqualTo(2));
            Assert.That(result.clusters[0].materialIndex, Is.EqualTo(0u));
            Assert.That(result.clusters[1].materialIndex, Is.EqualTo(1u));
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_SkipsDegenerateTriangles()
        {
            var mesh = ClusterMeshTestMeshes.DegeneratePlusReal();
            var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());

            Assert.That(result.clusters.Length, Is.EqualTo(1));
            Assert.That(result.clusters[0].triangleCount, Is.EqualTo(1u));
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_LodOff_Grid_TriangleCountEqualsSource()
        {
            var mesh = ClusterMeshTestMeshes.Grid(16, 16);
            var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings { buildLodHierarchy = false });
            int baked = 0;
            foreach (var c in result.clusters)
                baked += (int)c.triangleCount;
            Assert.That(baked, Is.EqualTo(mesh.triangles.Length / 3));
            Assert.That(result.hierarchyVersion, Is.EqualTo(0));
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_LodOn_Grid_HasParent_LeafTrisEqualSource()
        {
            var mesh = ClusterMeshTestMeshes.Grid(16, 16);
            var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            int leafTris = 0;
            int parents = 0;
            foreach (var c in result.clusters)
            {
                if (ClusterMeshLod.IsParent(c.flags))
                    parents++;
                else
                    leafTris += (int)c.triangleCount;
            }

            Assert.That(parents, Is.GreaterThan(0));
            Assert.That(leafTris, Is.EqualTo(mesh.triangles.Length / 3));
            Assert.That(result.hierarchyVersion, Is.EqualTo(ClusterMeshLod.HierarchyVersionDag));
            Assert.That(result.groups, Is.Not.Null);
            Assert.That(result.groups.Length, Is.GreaterThan(0));
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_LodOn_FourQuads_OneGroup_SharedParentIndex()
        {
            var mesh = ClusterMeshTestMeshes.Grid(2, 2);
            var settings = new ClusterMeshBakeSettings
            {
                maxVerticesPerCluster = 4,
                maxTrianglesPerCluster = 2
            };
            var result = ClusterMeshBaker.Bake(mesh, new Material[1], settings);
            Assert.That(result.hierarchyVersion, Is.EqualTo(ClusterMeshLod.HierarchyVersionDag));
            Assert.That(result.groups.Length, Is.GreaterThan(0));

            int leaves = 0;
            foreach (var c in result.clusters)
            {
                if (ClusterMeshLod.Level(c.flags) == 0)
                    leaves++;
            }

            Assert.That(leaves, Is.EqualTo(4));
            ClusterGroup g0 = result.groups[0];
            Assert.That(g0.clusterCount, Is.GreaterThan(0));
            int children = 0;
            for (int i = 0; i < result.clusters.Length; i++)
            {
                if (result.clusters[i].parentIndex == 0)
                    children++;
            }

            Assert.That(children, Is.EqualTo(4));
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_LodOn_FourQuads_LockedBorderInParents()
        {
            var mesh = ClusterMeshTestMeshes.Grid(2, 2);
            var settings = new ClusterMeshBakeSettings
            {
                maxVerticesPerCluster = 4,
                maxTrianglesPerCluster = 2
            };
            var result = ClusterMeshBaker.Bake(mesh, new Material[1], settings);
            var clusters = new List<ClusterHeader>(result.clusters);
            var vertices = new List<ClusterVertex>(result.vertices);
            var indices = new List<uint>(result.indices);
            var children = new List<int>();
            for (int i = 0; i < result.clusters.Length; i++)
            {
                if (result.clusters[i].parentIndex == 0)
                    children.Add(i);
            }

            Assert.That(children.Count, Is.EqualTo(4));
            var locked = new List<Vector3>();
            ClusterMeshLodBaker.GetGroupLockedPositions(clusters, vertices, indices, children, locked);
            Assert.That(locked.Count, Is.GreaterThan(0));

            ClusterGroup g0 = result.groups[0];
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

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_NullMesh_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ClusterMeshBaker.Bake(null, Array.Empty<Material>(), new ClusterMeshBakeSettings()));
        }

        [Test]
        public void Bake_EmptyTangents_RebuildsFromUvAndPosition()
        {
            var mesh = new Mesh { name = "CMTestNoTangents" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 1f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Assert.That(mesh.tangents == null || mesh.tangents.Length == 0, Is.True);

            var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var dummy = new Vector4(1f, 0f, 0f, 1f);
            Assert.That(result.vertices.Length, Is.EqualTo(3));
            for (int i = 0; i < result.vertices.Length; i++)
            {
                Vector4 tan = result.vertices[i].tangent;
                Assert.That(new Vector3(tan.x, tan.y, tan.z).sqrMagnitude, Is.GreaterThan(0.5f));
                Assert.That(tan, Is.Not.EqualTo(dummy));
            }

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        static bool ContainsPoint(ClusterHeader cluster, Vector4 position)
        {
            var c = (Vector3)cluster.aabbCenter;
            var e = (Vector3)cluster.aabbExtents;
            var p = (Vector3)position;
            return Mathf.Abs(p.x - c.x) <= e.x + 1e-4f
                && Mathf.Abs(p.y - c.y) <= e.y + 1e-4f
                && Mathf.Abs(p.z - c.z) <= e.z + 1e-4f;
        }
    }
}
