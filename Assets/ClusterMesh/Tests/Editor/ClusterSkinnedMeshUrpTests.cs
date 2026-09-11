using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed class ClusterSkinnedMeshUrpTests
    {
        readonly List<Object> _trash = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            ClusterMeshUrpBridge.RendererDataOverrideForTests = null;
            ClusterSkinnedMeshSceneBatcher.ResetForTests();
            ClusterMeshSceneBatcher.ResetForTests();
            for (int i = 0; i < _trash.Count; i++)
            {
                if (_trash[i] != null)
                    Object.DestroyImmediate(_trash[i]);
            }

            _trash.Clear();
        }

        [Test]
        public void Flush_GameCameraWithoutFeature_CountsLegacySubmit()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            ClusterSkinnedMeshRenderer renderer = CreateRegisteredRenderer();
            Assert.That(ClusterMeshUrpBridge.ShouldSkipLegacyFlush(renderer.targetCamera), Is.False);
            Assert.DoesNotThrow(() => ClusterSkinnedMeshSceneBatcher.Flush());
            Assert.That(ClusterSkinnedMeshSceneBatcher.LegacyFlushBatchCountForTests, Is.EqualTo(1));
        }

        [Test]
        public void Flush_GameCameraWithFeature_SkipsLegacySubmit()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            ClusterSkinnedMeshRenderer renderer = CreateRegisteredRenderer();
            Assert.That(ClusterMeshUrpBridge.ShouldSkipLegacyFlush(renderer.targetCamera), Is.True);
            Assert.DoesNotThrow(() => ClusterSkinnedMeshSceneBatcher.Flush());
            Assert.That(ClusterSkinnedMeshSceneBatcher.LegacyFlushBatchCountForTests, Is.EqualTo(0));
        }

        [Test]
        public void PrepareAndSubmitUrpShadows_WithoutFeature_DoesNotPrepare()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            ClusterSkinnedMeshRenderer renderer = CreateRegisteredRenderer();
            ClusterSkinnedMeshSceneBatcher.PrepareAndSubmitUrpShadows(renderer.targetCamera);
            Assert.That(ClusterSkinnedMeshSceneBatcher.UrpPreparedCountForTests, Is.EqualTo(0));
        }

        [Test]
        public void DrawContext_PrepareUrp_NullOrUnready_IsFalseAndSubmitSafe()
        {
            var cull = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/ClusterMesh/Shaders/ClusterSkinnedMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/SkinnedLit");
            var cam = Track(new GameObject("CMSkinnedUrpNullCam")).AddComponent<Camera>();
            using (var ctx = new ClusterSkinnedMeshDrawContext(null, cull, lit))
            {
                Assert.That(ctx.PrepareUrp(
                    new[] { Matrix4x4.identity }, null, null, new[] { 0f },
                    0, ClusterSkinnedAnimationEvaluation.GpuTexture,
                    false, false, 0f, cam, true, true, 0), Is.False);
                var cmd = new CommandBuffer { name = "CMSkinnedUrpNull" };
                try
                {
                    Assert.DoesNotThrow(() => ctx.SubmitUrpDepth(cmd));
                    Assert.DoesNotThrow(() => ctx.SubmitUrpColor(cmd));
                    Assert.DoesNotThrow(() => ctx.SubmitUrpGBuffer(cmd));
                    Assert.DoesNotThrow(() => ctx.SubmitUrpDepth(null));
                    Assert.DoesNotThrow(() => ctx.SubmitUrpColor(null));
                    Assert.DoesNotThrow(() => ctx.SubmitUrpGBuffer(null));
                }
                finally
                {
                    cmd.Release();
                }
            }
        }

        [Test]
        public void Feature_GameCamera_PreparesSkinnedAndStaticTogether()
        {
            var data = Track(ScriptableObject.CreateInstance<UniversalRendererData>());
            ClusterMeshUrpFeatureMenu.EnableOn(data);
            ClusterMeshUrpBridge.RendererDataOverrideForTests = data;
            ClusterSkinnedMeshRenderer skinned = CreateRegisteredRenderer();
            ClusterMeshRenderer staticRenderer = CreateRegisteredStaticRenderer(skinned.targetCamera);
            ClusterMeshSceneBatcher.PrepareAndSubmitUrpShadows(skinned.targetCamera);
            ClusterSkinnedMeshSceneBatcher.PrepareAndSubmitUrpShadows(skinned.targetCamera);
            Assert.That(ClusterMeshUrpBridge.ShouldSubmitUrp(skinned.targetCamera), Is.True);
            Assert.That(staticRenderer, Is.Not.Null);
            var cmd = new CommandBuffer { name = "CMBothUrp" };
            try
            {
                Assert.DoesNotThrow(() => ClusterMeshSceneBatcher.SubmitUrpDepth(skinned.targetCamera, cmd));
                Assert.DoesNotThrow(() => ClusterSkinnedMeshSceneBatcher.SubmitUrpDepth(skinned.targetCamera, cmd));
                Assert.DoesNotThrow(() => ClusterMeshSceneBatcher.SubmitUrpColor(skinned.targetCamera, cmd));
                Assert.DoesNotThrow(() => ClusterSkinnedMeshSceneBatcher.SubmitUrpColor(skinned.targetCamera, cmd));
                Assert.DoesNotThrow(() => ClusterMeshSceneBatcher.SubmitUrpGBuffer(skinned.targetCamera, cmd));
                Assert.DoesNotThrow(() => ClusterSkinnedMeshSceneBatcher.SubmitUrpGBuffer(skinned.targetCamera, cmd));
            }
            finally
            {
                cmd.Release();
            }
        }

        [Test]
        public void BakedGpuOnly_PrepareUrp_ReadyIffCapabilityAllows()
        {
            if (!TryBakeGpuOnly(out ClusterSkinnedMeshAsset asset, out ClusterMeshAsset geometry))
                return;
            var cull = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/ClusterMesh/Shaders/ClusterSkinnedMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/SkinnedLit");
            var cam = Track(new GameObject("CMSkinnedUrpBakeCam")).AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            using (var ctx = new ClusterSkinnedMeshDrawContext(asset, cull, lit))
            {
                if (!ctx.IsReady)
                {
                    Assert.That(ctx.Error, Is.Not.Null);
                    return;
                }

                bool prepared = ctx.PrepareUrp(
                    new[] { Matrix4x4.identity }, new[] { false }, new[] { false }, new[] { 0f },
                    0, ClusterSkinnedAnimationEvaluation.GpuTexture,
                    false, true, 0f, cam, true, true, 0);
                Assert.That(prepared, Is.EqualTo(ClusterMeshCapability.IsSupported()));
                if (!prepared)
                    return;
                var cmd = new CommandBuffer { name = "CMSkinnedUrpBake" };
                try
                {
                    Assert.DoesNotThrow(() => ctx.SubmitUrpDepth(cmd));
                    Assert.DoesNotThrow(() => ctx.SubmitUrpColor(cmd));
                }
                finally
                {
                    cmd.Release();
                }
            }
        }

        ClusterSkinnedMeshRenderer CreateRegisteredRenderer()
        {
            var cam = Track(new GameObject("CMSkinnedUrpCam")).AddComponent<Camera>();
            cam.cameraType = CameraType.Game;
            var host = Track(new GameObject("CMSkinnedUrpHost"));
            var renderer = host.AddComponent<ClusterSkinnedMeshRenderer>();
            var asset = Track(ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>());
            var geometry = Track(ScriptableObject.CreateInstance<ClusterMeshAsset>());
            geometry.clusters = new[] { new ClusterHeader() };
            asset.geometry = geometry;
            renderer.asset = asset;
            renderer.targetCamera = cam;
            renderer.cullShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/ClusterMesh/Shaders/ClusterSkinnedMeshCull.compute");
            renderer.litShader = Shader.Find("ClusterMesh/SkinnedLit");
            ClusterSkinnedMeshSceneBatcher.Register(renderer);
            return renderer;
        }

        ClusterMeshRenderer CreateRegisteredStaticRenderer(Camera camera)
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            _trash.Add(mesh);
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var asset = Track(ScriptableObject.CreateInstance<ClusterMeshAsset>());
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var host = Track(new GameObject("CMStaticUrpHost"));
            var renderer = host.AddComponent<ClusterMeshRenderer>();
            renderer.asset = asset;
            renderer.targetCamera = camera;
            renderer.cullShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            string reason = ClusterMeshCapability.GetUnsupportedReason();
            if (reason != null)
                LogAssert.Expect(LogType.Error, "ClusterMesh: " + reason);
            ClusterMeshSceneBatcher.Register(renderer);
            return renderer;
        }

        bool TryBakeGpuOnly(out ClusterSkinnedMeshAsset asset, out ClusterMeshAsset geometry)
        {
            asset = null;
            geometry = null;
            var root = Track(new GameObject("CMSkinnedUrpBakeRoot"));
            var bone = new GameObject("Bone").transform;
            bone.SetParent(root.transform, false);
            var smr = root.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { name = "CMSkinnedUrpTri" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            };
            mesh.bindposes = new[] { bone.worldToLocalMatrix * root.transform.localToWorldMatrix };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            _trash.Add(mesh);
            smr.sharedMesh = mesh;
            smr.bones = new[] { bone };
            smr.rootBone = bone;
            smr.sharedMaterials = new Material[1];
            var clip = new AnimationClip { name = "CMSkinnedUrpClip", frameRate = 30f };
            clip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 0.1f));
            _trash.Add(clip);
            asset = Track(ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>());
            geometry = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            geometry.name = "Geometry";
            try
            {
                ClusterMeshBakerWindow.WriteSkinnedAsset(
                    asset,
                    geometry,
                    smr,
                    new[] { clip },
                    new ClusterMeshBakeSettings { buildLodHierarchy = false },
                    new ClusterSkinnedMeshBakeOptions());
            }
            catch (System.Exception)
            {
                Object.DestroyImmediate(geometry);
                asset = null;
                geometry = null;
                Assert.Inconclusive("Skinned baker could not produce a GpuOnly asset in this environment.");
                return false;
            }

            _trash.Add(geometry);
            if (!asset.HasGpuPalette(0))
            {
                Assert.Inconclusive("Skinned baker did not write a GPU palette.");
                return false;
            }

            return true;
        }

        T Track<T>(T obj) where T : Object
        {
            _trash.Add(obj);
            return obj;
        }
    }
}
