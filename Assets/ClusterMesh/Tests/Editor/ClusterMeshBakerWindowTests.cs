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
    }
}
