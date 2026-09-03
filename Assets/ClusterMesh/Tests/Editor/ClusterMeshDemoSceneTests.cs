using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshDemoSceneTests
    {
        [Test]
        public void CreateDemoObjects_AddsRendererWithBakedAsset()
        {
            var root = ClusterMeshDemoSceneMenu.CreateDemoObjects();
            var renderer = root.GetComponentInChildren<ClusterMeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.asset, Is.Not.Null);
            Assert.That(renderer.asset.clusters.Length, Is.GreaterThan(0));
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(renderer.asset);
        }
    }
}
