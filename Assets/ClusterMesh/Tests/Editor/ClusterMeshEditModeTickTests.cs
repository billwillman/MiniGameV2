using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshEditModeTickTests
    {
        readonly List<Object> _trash = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            ClusterMeshSceneBatcher.ResetForTests();
            ClusterMeshSceneViewRenderer.DisposeCachedContexts();
            ClusterMeshLifetime.SyncSceneViewTick(true);
            ClusterMeshLifetime.SyncEditModeTick();
            for (int i = 0; i < _trash.Count; i++)
            {
                if (_trash[i] != null)
                    Object.DestroyImmediate(_trash[i]);
            }

            _trash.Clear();
        }

        [Test]
        public void ShouldQueuePlayerLoop_Zero_IsFalse()
        {
            Assert.That(ClusterMeshLifetime.ShouldQueuePlayerLoop(0), Is.False);
        }

        [Test]
        public void ShouldQueuePlayerLoop_One_IsTrue()
        {
            Assert.That(ClusterMeshLifetime.ShouldQueuePlayerLoop(1), Is.True);
        }

        [Test]
        public void RegisteredCount_Empty_IsZero()
        {
            ClusterMeshSceneBatcher.ResetForTests();
            Assert.That(ClusterMeshSceneBatcher.RegisteredCount, Is.EqualTo(0));
        }

        [Test]
        public void RegisteredCount_OneRegistered_IsOne()
        {
            ExpectCapabilityErrorIfUnsupported();
            var renderer = CreateRenderer();
            ClusterMeshSceneBatcher.Register(renderer);
            Assert.That(ClusterMeshSceneBatcher.RegisteredCount, Is.EqualTo(1));
        }

        [Test]
        public void RegisteredCount_DestroyedWithoutUnregister_IsZero()
        {
            ExpectCapabilityErrorIfUnsupported();
            var renderer = CreateRenderer();
            ClusterMeshSceneBatcher.Register(renderer);
            Object.DestroyImmediate(renderer.gameObject);
            Assert.That(ClusterMeshSceneBatcher.RegisteredCount, Is.EqualTo(0));
        }

        [Test]
        public void SceneViewRenderer_DisposeEmptyCache_DoesNotThrow()
        {
            Assert.DoesNotThrow(ClusterMeshSceneViewRenderer.DisposeCachedContexts);
            Assert.That(ClusterMeshSceneViewRenderer.CachedContextCount, Is.EqualTo(0));
        }

        [Test]
        public void IsLayerVisible_RequiresEditorAndCameraMasks()
        {
            int layer = 7;
            int mask = 1 << layer;
            Assert.That(ClusterMeshSceneViewRenderer.IsLayerVisible(layer, mask, mask), Is.True);
            Assert.That(ClusterMeshSceneViewRenderer.IsLayerVisible(layer, 0, mask), Is.False);
            Assert.That(ClusterMeshSceneViewRenderer.IsLayerVisible(layer, mask, 0), Is.False);
        }

        [Test]
        public void SyncEditModeTick_False_SetsActive()
        {
            ClusterMeshLifetime.SyncEditModeTick(true);
            ClusterMeshLifetime.SyncEditModeTick(false);
            Assert.That(ClusterMeshLifetime.EditModeTickActive, Is.True);
        }

        [Test]
        public void SyncEditModeTick_True_ClearsActive()
        {
            ClusterMeshLifetime.SyncSceneViewTick(true);
            ClusterMeshLifetime.SyncEditModeTick(false);
            ClusterMeshLifetime.SyncEditModeTick(true);
            Assert.That(ClusterMeshLifetime.EditModeTickActive, Is.False);
            Assert.That(ClusterMeshLifetime.SceneViewTickActive, Is.True);
        }

        [Test]
        public void SyncSceneViewTick_TracksIndependentSubscription()
        {
            ClusterMeshLifetime.SyncSceneViewTick(false);
            Assert.That(ClusterMeshLifetime.SceneViewTickActive, Is.False);
            ClusterMeshLifetime.SyncSceneViewTick(true);
            Assert.That(ClusterMeshLifetime.SceneViewTickActive, Is.True);
        }

        [Test]
        public void SyncEditModeTick_FalseTwice_StaysActive()
        {
            ClusterMeshLifetime.SyncEditModeTick(false);
            ClusterMeshLifetime.SyncEditModeTick(false);
            Assert.That(ClusterMeshLifetime.EditModeTickActive, Is.True);
        }

        static void ExpectCapabilityErrorIfUnsupported()
        {
            string reason = ClusterMeshCapability.GetUnsupportedReason();
            if (reason != null)
                LogAssert.Expect(LogType.Error, "ClusterMesh: " + reason);
        }

        ClusterMeshRenderer CreateRenderer()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            _trash.Add(mesh);
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());
            _trash.Add(asset);
            var go = new GameObject("CMTickRenderer");
            go.SetActive(false);
            _trash.Add(go);
            var renderer = go.AddComponent<ClusterMeshRenderer>();
            renderer.asset = asset;
            renderer.cullShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            renderer.litShader = Shader.Find("ClusterMesh/Lit");
            return renderer;
        }
    }
}
