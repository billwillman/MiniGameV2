using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshUrpTests
    {
        readonly List<Object> _trash = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            ClusterMeshUrpBridge.RendererDataOverrideForTests = null;
            ClusterMeshSceneBatcher.ResetForTests();
            for (int i = 0; i < _trash.Count; i++)
            {
                if (_trash[i] != null)
                    Object.DestroyImmediate(_trash[i]);
            }

            _trash.Clear();
        }

        [Test]
        public void HasActiveFeature_NullOrEmpty_IsFalse()
        {
            Assert.That(ClusterMeshUrpBridge.HasActiveFeature(null), Is.False);
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            Assert.That(ClusterMeshUrpBridge.HasActiveFeature(data), Is.False);
        }

        [Test]
        public void EnableThenDisable_DefaultRendererData_TogglesFeature()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            Assert.That(ClusterMeshUrpFeatureMenu.HasFeature(data), Is.False);
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            Assert.That(ClusterMeshUrpFeatureMenu.HasFeature(data), Is.True);
            Assert.That(ClusterMeshUrpBridge.HasActiveFeature(data), Is.True);
            int count = FeatureCount(data);
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            Assert.That(FeatureCount(data), Is.EqualTo(count));
            ClusterMeshUrpFeatureMenu.DisableOn(data);
            Assert.That(ClusterMeshUrpFeatureMenu.HasFeature(data), Is.False);
            Assert.That(ClusterMeshUrpBridge.HasActiveFeature(data), Is.False);
        }

        [Test]
        public void ShouldSkipLegacyFlush_NullCamera_IsFalse()
        {
            Assert.That(ClusterMeshUrpBridge.ShouldSkipLegacyFlush(null), Is.False);
        }

        [Test]
        public void ShouldSkipLegacyFlush_OverrideWithFeature_IsTrue()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            var go = Track(new GameObject("CMUrpCam")).AddComponent<Camera>();
            Assert.That(ClusterMeshUrpBridge.ShouldSkipLegacyFlush(go), Is.True);
            Assert.That(ClusterMeshUrpBridge.ShouldSubmitUrp(go), Is.True);
        }

        [Test]
        public void ShouldSubmitUrp_SceneCamera_IsFalse()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            var go = Track(new GameObject("CMUrpSceneCam")).AddComponent<Camera>();
            go.cameraType = CameraType.SceneView;
            Assert.That(ClusterMeshUrpBridge.ShouldSubmitUrp(go), Is.False);
        }

        [Test]
        public void ShouldSubmitUrp_PreviewAndReflection_AreFalse()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            var preview = Track(new GameObject("CMUrpPreview")).AddComponent<Camera>();
            preview.cameraType = CameraType.Preview;
            var reflection = Track(new GameObject("CMUrpRefl")).AddComponent<Camera>();
            reflection.cameraType = CameraType.Reflection;
            Assert.That(ClusterMeshUrpBridge.ShouldSubmitUrp(preview), Is.False);
            Assert.That(ClusterMeshUrpBridge.ShouldSubmitUrp(reflection), Is.False);
        }

        [Test]
        public void Flush_WhenFeatureOwnsCamera_DoesNotThrow()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            var cam = Track(new GameObject("CMUrpFlushCam")).AddComponent<Camera>();
            var mesh = ClusterMeshTestMeshes.Triangle();
            _trash.Add(mesh);
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var asset = Track(ScriptableObject.CreateInstance<ClusterMeshAsset>());
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var host = Track(new GameObject("CMUrpFlushHost"));
            var renderer = host.AddComponent<ClusterMeshRenderer>();
            renderer.asset = asset;
            renderer.targetCamera = cam;
            renderer.cullShader = AssetDatabaseCull();
            string reason = ClusterMeshCapability.GetUnsupportedReason();
            if (reason != null)
                LogAssert.Expect(LogType.Error, "ClusterMesh: " + reason);
            ClusterMeshSceneBatcher.Register(renderer);
            Assert.DoesNotThrow(() => ClusterMeshSceneBatcher.Flush());
        }

        static ComputeShader AssetDatabaseCull()
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
        }

        static int FeatureCount(ScriptableRendererData data)
        {
            int n = 0;
            if (data == null || data.rendererFeatures == null)
                return 0;
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                if (data.rendererFeatures[i] is ClusterMeshUrpFeature)
                    n++;
            }

            return n;
        }

        T Track<T>(T obj) where T : Object
        {
            _trash.Add(obj);
            return obj;
        }
    }
}
