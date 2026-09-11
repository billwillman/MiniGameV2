using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
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
        public void ConfigureForwardOn_SwitchesFromDeferredAndKeepsFeature()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpFeatureMenu.ConfigureDeferredOn(data);
            ClusterMeshUrpFeatureMenu.ConfigureForwardOn(data);
            Assert.That(data.renderingMode, Is.EqualTo(RenderingMode.Forward));
            Assert.That(ClusterMeshUrpBridge.HasActiveFeature(data), Is.True);
        }

        [Test]
        public void AreCompatibleDeferredTargets_MismatchedSizes_IsFalse()
        {
            RTHandle color = AllocHandle(987, 354, false);
            RTHandle depth = AllocHandle(1288, 478, true);
            try
            {
                Assert.That(ClusterMeshUrpBridge.AreCompatibleDeferredTargets(null, depth), Is.False);
                Assert.That(ClusterMeshUrpBridge.AreCompatibleDeferredTargets(new[] { color }, null), Is.False);
                Assert.That(ClusterMeshUrpBridge.AreCompatibleDeferredTargets(new[] { color }, depth), Is.False);
            }
            finally
            {
                ReleaseHandle(color);
                ReleaseHandle(depth);
            }
        }

        [Test]
        public void AreCompatibleDeferredTargets_MatchingSizes_IsTrue()
        {
            RTHandle color = AllocHandle(64, 32, false);
            RTHandle depth = AllocHandle(64, 32, true);
            try
            {
                Assert.That(ClusterMeshUrpBridge.AreCompatibleDeferredTargets(new[] { color }, depth), Is.True);
            }
            finally
            {
                ReleaseHandle(color);
                ReleaseHandle(depth);
            }
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
            Assert.That(staticMaterial.FindPass("MotionVectors"), Is.EqualTo(4));
            Assert.That(skinnedMaterial.FindPass("ForwardLit"), Is.EqualTo(0));
            Assert.That(skinnedMaterial.FindPass("ShadowCaster"), Is.EqualTo(1));
            Assert.That(skinnedMaterial.FindPass("DepthOnly"), Is.EqualTo(2));
            Assert.That(skinnedMaterial.FindPass("GBuffer"), Is.EqualTo(3));
            Assert.That(skinnedMaterial.FindPass("MotionVectors"), Is.EqualTo(4));
            Assert.That(
                System.IO.File.ReadAllText("Assets/ClusterMesh/Shaders/ClusterMeshLit.hlsl"),
                Does.Contain("#pragma editor_sync_compilation"));
            Assert.That(
                System.IO.File.ReadAllText("Assets/ClusterMesh/Shaders/ClusterSkinnedMeshLit.hlsl"),
                Does.Contain("#pragma editor_sync_compilation"));
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

        static RTHandle AllocHandle(int width, int height, bool depth)
        {
            var rt = new RenderTexture(width, height, depth ? 24 : 0)
            {
                name = depth ? "CMTestDepth" : "CMTestColor"
            };
            rt.Create();
            return RTHandles.Alloc(rt);
        }

        static void ReleaseHandle(RTHandle handle)
        {
            if (handle == null)
                return;
            RenderTexture rt = handle.rt;
            RTHandles.Release(handle);
            if (rt != null)
                Object.DestroyImmediate(rt);
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
