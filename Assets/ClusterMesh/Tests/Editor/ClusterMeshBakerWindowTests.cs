using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshBakerWindowTests
    {
        [Test]
        public void WriteAsset_PopulatesClusters()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshBakerWindow.WriteAsset(asset, mesh, new Material[1], new ClusterMeshBakeSettings());
            Assert.That(asset.clusters, Is.Not.Null);
            Assert.That(asset.clusters.Length, Is.EqualTo(1));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void WriteAsset_LodOff_WritesVersionZero()
        {
            var mesh = ClusterMeshTestMeshes.Grid(16, 16);
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshBakerWindow.WriteAsset(
                asset,
                mesh,
                new Material[1],
                new ClusterMeshBakeSettings { buildLodHierarchy = false });
            Assert.That(asset.hierarchyVersion, Is.EqualTo(0));
            Assert.That(asset.geometryVersion, Is.EqualTo(ClusterMeshLimits.GeometryVersion));
            Assert.That(asset.groups == null || asset.groups.Length == 0, Is.True);
            Assert.That(asset.packedVertices, Is.Not.Null);
            Assert.That(asset.packedVertices.Length, Is.GreaterThan(0));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void WriteAsset_Default_Keeps32ByteStride()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshBakerWindow.WriteAsset(asset, mesh, new Material[1], new ClusterMeshBakeSettings());
            Assert.That(asset.ResolvedVertexStride, Is.EqualTo(ClusterMeshLimits.ClusterVertexStride));
            Assert.That(ClusterMeshGeometry.TryReadGpuGeometry(asset, out _, out _, out string error), Is.True);
            Assert.That(error, Is.Null);
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void WriteAsset_TightRestOn_Writes24ByteStride()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshBakerWindow.WriteAsset(
                asset,
                mesh,
                new Material[1],
                new ClusterMeshBakeSettings { packTightRestVertices = true });
            Assert.That(asset.ResolvedVertexStride, Is.EqualTo(ClusterMeshLimits.TightVertexStride));
            Assert.That(ClusterMeshGeometry.TryReadGpuGeometry(asset, out _, out _, out string gpuError), Is.False);
            Assert.That(gpuError, Does.Contain("tight"));
            Assert.That(
                ClusterMeshGeometry.TryReadTightVertices(asset, out ClusterPackedVertexTight[] tight, out string tightError),
                Is.True);
            Assert.That(tightError, Is.Null);
            Assert.That(tight.Length, Is.EqualTo(asset.vertexCount));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }
    }
}
