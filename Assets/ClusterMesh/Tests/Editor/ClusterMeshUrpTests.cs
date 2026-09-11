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
            ClusterSkinnedMeshSceneBatcher.ResetForTests();
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
        public void ConfigureDeferredOn_EnablesDeferredAndFeature()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            data.renderingMode = RenderingMode.Forward;
            ClusterMeshUrpFeatureMenu.ConfigureDeferredOn(data);
            Assert.That(data.renderingMode, Is.EqualTo(RenderingMode.Deferred));
            Assert.That(ClusterMeshUrpBridge.HasActiveFeature(data), Is.True);
        }

        [Test]
        public void ShouldSkipLegacyFlush_NullCamera_IsFalse()
        {
            Assert.That(ClusterMeshUrpBridge.ShouldSkipLegacyFlush(null), Is.False);
        }

        [Test]
        public void UrpShaders_AppendGBufferWithoutChangingExistingPassIndices()
        {
            Shader staticShader = Shader.Find("ClusterMesh/Lit");
            Shader skinnedShader = Shader.Find("ClusterMesh/SkinnedLit");
            Assert.That(staticShader, Is.Not.Null);
            Assert.That(skinnedShader, Is.Not.Null);
            Material staticMaterial = Track(new Material(staticShader));
            Material skinnedMaterial = Track(new Material(skinnedShader));
            Assert.That(staticMaterial.FindPass("ForwardLit"), Is.EqualTo(0));
            Assert.That(staticMaterial.FindPass("ShadowCaster"), Is.EqualTo(1));
            Assert.That(staticMaterial.FindPass("DepthOnly"), Is.EqualTo(2));
            Assert.That(staticMaterial.FindPass("GBuffer"), Is.EqualTo(3));
            Assert.That(skinnedMaterial.FindPass("ForwardLit"), Is.EqualTo(0));
            Assert.That(skinnedMaterial.FindPass("ShadowCaster"), Is.EqualTo(1));
            Assert.That(skinnedMaterial.FindPass("DepthOnly"), Is.EqualTo(2));
            Assert.That(skinnedMaterial.FindPass("GBuffer"), Is.EqualTo(3));
        }

        [Test]
        public void DeferredSupport_IsExplicitlyUrpOnly()
        {
            StringAssert.Contains("only by URP", ClusterMeshUrpBridge.DeferredSupportDescription);
            StringAssert.Contains("Built-in", ClusterMeshUrpBridge.DeferredSupportDescription);
            StringAssert.Contains("HDRP", ClusterMeshUrpBridge.DeferredSupportDescription);
        }

        [Test]
        public void EditorGameCamera_WithActiveFeature_UsesUrp()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            var go = Track(new GameObject("CMUrpCam")).AddComponent<Camera>();
            go.cameraType = CameraType.Game;
            Assert.That(ClusterMeshUrpBridge.ShouldSubmitUrp(go), Is.True);
            Assert.That(ClusterMeshUrpBridge.ShouldSkipLegacyFlush(go), Is.True);
        }

        [Test]
        public void EditorGameCamera_WithoutFeature_KeepsLegacyFlush()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            var go = Track(new GameObject("CMUrpLegacyCam")).AddComponent<Camera>();
            go.cameraType = CameraType.Game;
            Assert.That(ClusterMeshUrpBridge.HasActiveFeature(data), Is.False);
            Assert.That(ClusterMeshUrpBridge.ShouldSubmitUrp(go), Is.False);
            Assert.That(ClusterMeshUrpBridge.ShouldSkipLegacyFlush(go), Is.False);
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
        public void EditorLegacyFallback_WithActiveFeature_DoesNotThrow()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            var cam = Track(new GameObject("CMUrpFlushCam")).AddComponent<Camera>();
            cam.cameraType = CameraType.Game;
            Assert.That(ClusterMeshUrpBridge.HasActiveFeature(data), Is.True);
            Assert.That(ClusterMeshUrpBridge.ShouldSubmitUrp(cam), Is.True);
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
