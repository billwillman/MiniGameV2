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
            Assert.That(asset.groups == null || asset.groups.Length == 0, Is.True);
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }
    }
}
