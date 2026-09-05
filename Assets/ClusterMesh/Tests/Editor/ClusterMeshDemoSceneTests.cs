using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshDemoSceneTests
    {
        static void ExpectCapabilityErrorIfUnsupported()
        {
            string reason = ClusterMeshCapability.GetUnsupportedReason();
            if (reason != null)
                LogAssert.Expect(LogType.Error, "ClusterMesh: " + reason);
        }

        [Test]
        public void CreateDemoObjects_AddsRendererWithBakedAsset()
        {
            ExpectCapabilityErrorIfUnsupported();
            var root = ClusterMeshDemoSceneMenu.CreateDemoObjects();
            var renderers = root.GetComponentsInChildren<ClusterMeshRenderer>();
            Assert.That(renderers.Length, Is.EqualTo(ClusterMeshDemoSceneMenu.InstanceCount));
            Assert.That(renderers[0].asset, Is.Not.Null);
            Assert.That(renderers[0].asset.clusters.Length, Is.GreaterThan(0));
            for (int i = 1; i < renderers.Length; i++)
                Assert.That(renderers[i].asset, Is.SameAs(renderers[0].asset));
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(renderers[0].asset);
            ClusterMeshSceneBatcher.ResetForTests();
        }

        [Test]
        public void PersistDemoAsset_CalledTwice_DoesNotThrow()
        {
            ClusterMeshDemoSceneMenu.EnsureSamplesFolder();
            ExpectCapabilityErrorIfUnsupported();
            var root = ClusterMeshDemoSceneMenu.CreateDemoObjects();
            var renderer = root.GetComponentInChildren<ClusterMeshRenderer>();
            ClusterMeshAsset stored1 = null;
            ClusterMeshAsset stored2 = null;
            try
            {
                stored1 = ScriptableObject.CreateInstance<ClusterMeshAsset>();
                stored1.CopyPackedFrom(renderer.asset);
                stored2 = ScriptableObject.CreateInstance<ClusterMeshAsset>();
                stored2.CopyPackedFrom(renderer.asset);

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
                ClusterMeshSceneBatcher.ResetForTests();
            }
        }
    }
}
