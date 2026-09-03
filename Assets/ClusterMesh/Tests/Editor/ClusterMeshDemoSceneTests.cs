using NUnit.Framework;
using UnityEditor;
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

        [Test]
        public void PersistDemoAsset_CalledTwice_DoesNotThrow()
        {
            ClusterMeshDemoSceneMenu.EnsureSamplesFolder();
            var root = ClusterMeshDemoSceneMenu.CreateDemoObjects();
            var renderer = root.GetComponentInChildren<ClusterMeshRenderer>();
            ClusterMeshAsset stored1 = null;
            ClusterMeshAsset stored2 = null;
            try
            {
                var bake = new ClusterMeshBakeResult
                {
                    clusters = renderer.asset.clusters,
                    vertices = renderer.asset.vertices,
                    indices = renderer.asset.indices,
                    materials = renderer.asset.materials
                };
                stored1 = ScriptableObject.CreateInstance<ClusterMeshAsset>();
                stored1.CopyFrom(bake, renderer.asset.sourceMesh, new ClusterMeshBakeSettings());
                stored2 = ScriptableObject.CreateInstance<ClusterMeshAsset>();
                stored2.CopyFrom(bake, renderer.asset.sourceMesh, new ClusterMeshBakeSettings());

                Assert.DoesNotThrow(() =>
                {
                    ClusterMeshDemoSceneMenu.PersistDemoAsset(stored1);
                    ClusterMeshDemoSceneMenu.PersistDemoAsset(stored2);
                });

                Assert.That(
                    AssetDatabase.LoadAssetAtPath<ClusterMeshAsset>(ClusterMeshDemoSceneMenu.AssetPath),
                    Is.Not.Null);
            }
            finally
            {
                ClusterMeshDemoSceneMenu.DeleteAssetIfExists(ClusterMeshDemoSceneMenu.AssetPath);
                if (stored1 != null)
                    Object.DestroyImmediate(stored1);
                if (stored2 != null)
                    Object.DestroyImmediate(stored2);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(renderer.asset);
            }
        }
    }
}
