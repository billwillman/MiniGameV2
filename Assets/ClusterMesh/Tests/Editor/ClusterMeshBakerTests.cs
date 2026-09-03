using System;
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
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_GridLargerThanBudget_SplitsAndCoversEveryTriangleOnce()
        {
            var mesh = ClusterMeshTestMeshes.Grid(16, 16);
            Assert.That(mesh.vertexCount, Is.GreaterThan(ClusterMeshLimits.MaxVerticesPerCluster));

            var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            Assert.That(result.clusters.Length, Is.GreaterThan(1));

            int sourceTris = mesh.triangles.Length / 3;
            int bakedTris = 0;
            foreach (var cluster in result.clusters)
            {
                Assert.That(cluster.vertexCount, Is.LessThanOrEqualTo((uint)ClusterMeshLimits.MaxVerticesPerCluster));
                Assert.That(cluster.triangleCount, Is.LessThanOrEqualTo((uint)ClusterMeshLimits.MaxTrianglesPerCluster));
                bakedTris += (int)cluster.triangleCount;
            }

            Assert.That(bakedTris, Is.EqualTo(sourceTris));
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_TwoSubmeshes_SetsDistinctMaterialIndices()
        {
            var mesh = ClusterMeshTestMeshes.TwoSubmeshTriangles();
            var result = ClusterMeshBaker.Bake(mesh, new Material[2], new ClusterMeshBakeSettings());

            Assert.That(result.clusters.Length, Is.EqualTo(2));
            Assert.That(result.clusters[0].materialIndex, Is.EqualTo(0u));
            Assert.That(result.clusters[1].materialIndex, Is.EqualTo(1u));
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_SkipsDegenerateTriangles()
        {
            var mesh = ClusterMeshTestMeshes.DegeneratePlusReal();
            var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());

            Assert.That(result.clusters.Length, Is.EqualTo(1));
            Assert.That(result.clusters[0].triangleCount, Is.EqualTo(1u));
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Bake_NullMesh_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ClusterMeshBaker.Bake(null, Array.Empty<Material>(), new ClusterMeshBakeSettings()));
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
