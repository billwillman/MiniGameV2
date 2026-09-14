using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshPlaceMenuTests
    {
        static void ExpectCapabilityErrorIfUnsupported()
        {
            string reason = ClusterMeshCapability.GetUnsupportedReason();
            if (reason != null)
                LogAssert.Expect(LogType.Error, "ClusterMesh: " + reason);
        }

        [Test]
        public void CreateInScene_AssignsRendererAssetAndShaders()
        {
            ExpectCapabilityErrorIfUnsupported();
            var mesh = ClusterMeshTestMeshes.Triangle();
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.name = "PlaceMenuTri";
            ClusterMeshBakerWindow.WriteAsset(asset, mesh, new Material[1], new ClusterMeshBakeSettings());
            GameObject go = null;
            try
            {
                go = ClusterMeshPlaceMenu.CreateInScene(asset);
                var renderer = go.GetComponent<ClusterMeshRenderer>();
                Assert.That(go.name, Is.EqualTo("PlaceMenuTri"));
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.asset, Is.SameAs(asset));
                Assert.That(renderer.cullShader, Is.Not.Null);
                Assert.That(renderer.litShader, Is.Not.Null);
                Assert.That(renderer.litShader.name, Is.EqualTo("ClusterMesh/Lit"));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(mesh);
                ClusterMeshSceneBatcher.ResetForTests();
            }
        }

        [Test]
        public void CreateInScene_NullAsset_CreatesEmptyNamedNode()
        {
            GameObject go = null;
            try
            {
                go = ClusterMeshPlaceMenu.CreateInScene((ClusterMeshAsset)null);
                var renderer = go.GetComponent<ClusterMeshRenderer>();
                Assert.That(go.name, Is.EqualTo(ClusterMeshPlaceMenu.DefaultObjectName));
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.asset, Is.Null);
                Assert.That(renderer.cullShader, Is.Not.Null);
                Assert.That(renderer.litShader, Is.Not.Null);
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                ClusterMeshSceneBatcher.ResetForTests();
            }
        }

        [Test]
        public void CreateInScene_MultipleAssets_OffsetsOnX()
        {
            var a = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            a.name = "A";
            var b = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            b.name = "B";
            GameObject[] created = null;
            try
            {
                created = ClusterMeshPlaceMenu.CreateInScene(new[] { a, b });
                Assert.That(created.Length, Is.EqualTo(2));
                Assert.That(created[0].name, Is.EqualTo("A"));
                Assert.That(created[1].name, Is.EqualTo("B"));
                Assert.That(created[1].transform.position.x, Is.Not.EqualTo(created[0].transform.position.x));
            }
            finally
            {
                if (created != null)
                {
                    for (int i = 0; i < created.Length; i++)
                    {
                        if (created[i] != null)
                            Object.DestroyImmediate(created[i]);
                    }
                }

                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                ClusterMeshSceneBatcher.ResetForTests();
            }
        }

        [Test]
        public void CreateInScene_Undo_DestroysObject()
        {
            var go = ClusterMeshPlaceMenu.CreateInScene((ClusterMeshAsset)null);
            Assert.That(go, Is.Not.Null);
            Undo.PerformUndo();
            Assert.That(go == null, Is.True);
            ClusterMeshSceneBatcher.ResetForTests();
        }
    }
}
