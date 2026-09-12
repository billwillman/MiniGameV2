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
            ClusterMeshSettings.OverrideForTests = null;
            ClusterMeshSettings.ClearCacheForTests();
            ClusterMeshUrpFeatureMenu.RadiantInstalledOverrideForTests = null;
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
            string staticHlsl = System.IO.File.ReadAllText("Assets/ClusterMesh/Shaders/ClusterMeshLit.hlsl");
            string skinnedHlsl = System.IO.File.ReadAllText("Assets/ClusterMesh/Shaders/ClusterSkinnedMeshLit.hlsl");
            Assert.That(staticHlsl, Does.Not.Contain("MotionVectorsCommon.hlsl"));
            Assert.That(skinnedHlsl, Does.Not.Contain("MotionVectorsCommon.hlsl"));
            Assert.That(staticHlsl, Does.Contain("ClusterMeshMotionVectors.hlsl"));
            Assert.That(skinnedHlsl, Does.Contain("ClusterMeshMotionVectors.hlsl"));
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
        public void AddRenderPasses_Forward_DoesNotEnqueueDepthPrepass()
        {
            var feature = Track(ScriptableObject.CreateInstance<ClusterMeshUrpFeature>());
            feature.Create();
            Assert.That(
                System.IO.File.ReadAllText("Assets/ClusterMesh/Runtime/ClusterMeshUrpFeature.cs"),
                Does.Not.Contain("renderer.EnqueuePass(_depthPass)"));
        }

        [Test]
        public void MotionVectorPassEvent_IsAfterSkyboxBeforeRadiant()
        {
            Assert.That(
                ClusterMeshUrpBridge.MotionVectorPassEvent,
                Is.EqualTo((RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox + 1)));
            Assert.That(
                ClusterMeshUrpBridge.ResolveMotionVectorPassEvent(default),
                Is.EqualTo(ClusterMeshUrpBridge.MotionVectorPassEvent));
            Assert.That(
                ClusterMeshUrpBridge.ResolveMotionVectorPassEvent(
                    ClusterMeshMotionVectorSlot.AfterSkyboxPlus1),
                Is.EqualTo(ClusterMeshUrpBridge.MotionVectorPassEvent));
            Assert.That(
                ClusterMeshUrpBridge.ResolveMotionVectorPassEvent(
                    ClusterMeshMotionVectorSlot.AfterOpaques),
                Is.EqualTo(RenderPassEvent.AfterRenderingOpaques));
            Assert.That(
                ClusterMeshUrpBridge.ResolveMotionVectorPassEvent(
                    ClusterMeshMotionVectorSlot.AfterSkybox),
                Is.EqualTo(RenderPassEvent.AfterRenderingSkybox));
            Assert.That(
                ClusterMeshUrpBridge.ResolveMotionVectorPassEvent(
                    ClusterMeshMotionVectorSlot.BeforePostProcessingMinus1),
                Is.EqualTo((RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPostProcessing - 1)));
            Assert.That(
                ClusterMeshUrpBridge.MotionVectorSlotDescription(
                    ClusterMeshMotionVectorSlot.AfterSkyboxPlus1),
                Does.Contain("官方物体 MV 同级，早于 Radiant + 2")
                    .And.Contain("Radiant GI 的 Temporal 和 URP TAA"));
            Assert.That(
                ClusterMeshUrpBridge.MotionVectorSlotLabel(
                    ClusterMeshMotionVectorSlot.AfterSkyboxPlus1),
                Does.Contain("推荐"));
            Assert.That(
                ClusterMeshUrpBridge.MotionVectorSlotDescription(
                    ClusterMeshMotionVectorSlot.AfterOpaques),
                Does.Contain("比天空盒和官方物体 MV 更早")
                    .And.Contain("不推荐日常"));
            Assert.That(
                ClusterMeshUrpBridge.MotionVectorSlotDescription(
                    ClusterMeshMotionVectorSlot.AfterSkybox),
                Does.Contain("早于 Radiant + 2")
                    .And.Contain("通常仍有效"));
            Assert.That(
                ClusterMeshUrpBridge.MotionVectorSlotDescription(
                    ClusterMeshMotionVectorSlot.BeforePostProcessingMinus1),
                Does.Contain("晚于 Radiant + 2")
                    .And.Contain("选这项无效"));
            var defaultSettings = Track(ScriptableObject.CreateInstance<ClusterMeshSettings>());
            Assert.That(
                defaultSettings.MotionVectorSlot,
                Is.EqualTo(ClusterMeshMotionVectorSlot.AfterSkyboxPlus1));
            var overrideSettings = Track(ScriptableObject.CreateInstance<ClusterMeshSettings>());
            overrideSettings.MotionVectorSlot = ClusterMeshMotionVectorSlot.AfterOpaques;
            ClusterMeshSettings.OverrideForTests = overrideSettings;
            Assert.That(
                ClusterMeshSettings.CurrentMotionVectorSlot,
                Is.EqualTo(ClusterMeshMotionVectorSlot.AfterOpaques));
            Assert.That(
                ClusterMeshUrpBridge.CurrentMotionVectorPassEvent,
                Is.EqualTo(RenderPassEvent.AfterRenderingOpaques));
            string feature = System.IO.File.ReadAllText(
                "Assets/ClusterMesh/Runtime/ClusterMeshUrpFeature.cs");
            Assert.That(feature, Does.Contain("CurrentMotionVectorPassEvent"));
            Assert.That(feature, Does.Not.Contain("motionVectorSlot"));
            string window = System.IO.File.ReadAllText(
                "Assets/ClusterMesh/Editor/ClusterMeshSettingsWindow.cs");
            Assert.That(window, Does.Contain("Tools/ClusterMesh/通用设置"));
            string menu = System.IO.File.ReadAllText(
                "Assets/ClusterMesh/Editor/ClusterMeshUrpFeatureMenu.cs");
            Assert.That(menu, Does.Contain("Tools/ClusterMesh/Setup Radiant URP 延迟渲染"));
            Assert.That(menu, Does.Contain("没有安装 Radiant"));
        }

        [Test]
        public void RadiantInstalled_CanBeOverriddenForMissingPackage()
        {
            ClusterMeshUrpFeatureMenu.RadiantInstalledOverrideForTests = false;
            Assert.That(ClusterMeshUrpFeatureMenu.IsRadiantInstalled(), Is.False);
            ClusterMeshUrpFeatureMenu.RadiantInstalledOverrideForTests = true;
            Assert.That(ClusterMeshUrpFeatureMenu.IsRadiantInstalled(), Is.True);
        }

        [Test]
        public void EnableRadiantOn_AddsOnce_WithoutProjectRendererNames()
        {
            if (!ClusterMeshUrpFeatureMenu.IsRadiantInstalled())
                Assert.Ignore("没有安装 Radiant");

            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            data.name = "AnyUniversalRenderer";
            Assert.That(ClusterMeshUrpFeatureMenu.HasRadiantFeature(data), Is.False);
            ClusterMeshUrpFeatureMenu.EnableRadiantOn(data);
            Assert.That(ClusterMeshUrpFeatureMenu.HasRadiantFeature(data), Is.True);
            int count = FeatureCount(data);
            ClusterMeshUrpFeatureMenu.EnableRadiantOn(data);
            Assert.That(FeatureCount(data), Is.EqualTo(count));
        }

        [Test]
        public void SetupFolders_ResolveFromClusterMeshModule_NotProjectSettings()
        {
            string module = ClusterMeshUrpFeatureMenu.ResolveModuleFolder();
            string settings = ClusterMeshUrpFeatureMenu.ResolveSettingsFolder();
            Assert.That(module, Is.Not.Null.And.EndWith("ClusterMesh"));
            Assert.That(settings, Is.EqualTo(module + "/Settings"));
            Assert.That(settings, Does.Not.Contain("Assets/Settings"));
            Assert.That(settings, Does.Not.Contain("FogOfWar"));
            string menu = System.IO.File.ReadAllText(
                "Assets/ClusterMesh/Editor/ClusterMeshUrpFeatureMenu.cs");
            Assert.That(menu, Does.Not.Contain("return \"Assets/ClusterMesh\""));
            Assert.That(menu, Does.Contain("ForEachDefaultRenderer"));
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
        public void PrepareAndSubmitUrpShadows_SameGameCameraTwice_SubmitsOnce()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            var cam = Track(new GameObject("CMUrpShadowOnceCam")).AddComponent<Camera>();
            cam.cameraType = CameraType.Game;
            var mesh = ClusterMeshTestMeshes.Triangle();
            _trash.Add(mesh);
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var asset = Track(ScriptableObject.CreateInstance<ClusterMeshAsset>());
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var host = Track(new GameObject("CMUrpShadowOnceHost"));
            var renderer = host.AddComponent<ClusterMeshRenderer>();
            renderer.asset = asset;
            renderer.targetCamera = cam;
            renderer.cullShader = AssetDatabaseCull();
            string reason = ClusterMeshCapability.GetUnsupportedReason();
            if (reason != null)
                LogAssert.Expect(LogType.Error, "ClusterMesh: " + reason);
            ClusterMeshSceneBatcher.Register(renderer);

            ClusterMeshUrpBridge.SubmitUrpShadowsBeforeCull(cam);
            int first = ClusterMeshSceneBatcher.UrpShadowSubmitCountForTests;
            Assert.That(first, Is.EqualTo(1));
            ClusterMeshUrpBridge.SubmitUrpShadowsBeforeCull(cam);
            Assert.That(ClusterMeshSceneBatcher.UrpShadowSubmitCountForTests, Is.EqualTo(1));
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
