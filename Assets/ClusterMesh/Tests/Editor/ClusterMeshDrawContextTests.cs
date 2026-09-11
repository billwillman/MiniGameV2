using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshDrawContextTests
    {
        [Test]
        public void Constructor_NullAsset_IsNotReady()
        {
            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            using (var ctx = new ClusterMeshDrawContext(null, cull, lit))
            {
                Assert.That(ctx.IsReady, Is.False);
                Assert.That(ctx.Error, Is.Not.Null);
            }
        }

        [Test]
        public void Constructor_OldGeometryVersion_IsNotReady()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());
            asset.geometryVersion = 0;

            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            using (var ctx = new ClusterMeshDrawContext(asset, cull, lit))
            {
                Assert.That(ctx.IsReady, Is.False);
                Assert.That(ctx.Error, Does.Contain("rebake"));
            }

            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Constructor_ValidBake_ShadowMaterialIsDistinctInstance()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());

            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            using (var ctx = new ClusterMeshDrawContext(asset, cull, lit))
            {
                if (!ctx.IsReady)
                {
                    Object.DestroyImmediate(asset);
                    Object.DestroyImmediate(mesh);
                    return;
                }

                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var color = (Material[])typeof(ClusterMeshDrawContext).GetField("_materials", flags).GetValue(ctx);
                var shadow = (Material[])typeof(ClusterMeshDrawContext).GetField("_shadowMaterials", flags).GetValue(ctx);
                Assert.That(shadow, Is.Not.Null);
                Assert.That(shadow.Length, Is.EqualTo(color.Length));
                Assert.That(shadow[0], Is.Not.SameAs(color[0]));
            }

            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Constructor_ValidBake_ReadyIffCapabilityAllows()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());

            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            using (var ctx = new ClusterMeshDrawContext(asset, cull, lit))
            {
                Assert.That(ctx.IsReady, Is.EqualTo(ClusterMeshCapability.IsSupported()));
                if (!ctx.IsReady)
                    Assert.That(ctx.Error, Is.Not.Null);
            }

            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Constructor_TightRest_ReadyIffCapabilityAllows()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var settings = new ClusterMeshBakeSettings { packTightRestVertices = true };
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], settings);
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, settings);

            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            using (var ctx = new ClusterMeshDrawContext(asset, cull, lit))
            {
                Assert.That(asset.ResolvedVertexStride, Is.EqualTo(ClusterMeshLimits.TightVertexStride));
                Assert.That(ctx.IsReady, Is.EqualTo(ClusterMeshCapability.IsSupported()));
                if (!ctx.IsReady)
                    Assert.That(ctx.Error, Is.Not.Null);
            }

            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Draw_AfterRuntimeMaterialsDestroyed_DoesNotThrow()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            var camGo = new GameObject("CMDestroyedMatCam");
            var cam = camGo.AddComponent<Camera>();
            using (var ctx = new ClusterMeshDrawContext(asset, cull, lit))
            {
                if (!ctx.IsReady)
                {
                    Object.DestroyImmediate(camGo);
                    Object.DestroyImmediate(asset);
                    Object.DestroyImmediate(mesh);
                    Assert.Ignore(ctx.Error);
                    return;
                }

                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var color = (Material[])typeof(ClusterMeshDrawContext).GetField("_materials", flags).GetValue(ctx);
                Object.DestroyImmediate(color[0]);
                Assert.That(ctx.CanDraw, Is.False);
                Assert.DoesNotThrow(() => ctx.Draw(Matrix4x4.identity, cam));
                Assert.DoesNotThrow(() => ctx.DrawEditorPreview(
                    new[] { Matrix4x4.identity }, new[] { true }, new[] { true }, cam, cam, true, true));
            }

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Dispose_Twice_DoesNotThrow()
        {
            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            var ctx = new ClusterMeshDrawContext(null, cull, lit);
            Assert.DoesNotThrow(() => { ctx.Dispose(); ctx.Dispose(); });
        }

        [Test]
        public void Dispose_SetsIsReadyFalse()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());

            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            var ctx = new ClusterMeshDrawContext(asset, cull, lit);
            ctx.Dispose();
            Assert.That(ctx.IsReady, Is.False);

            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void PrepareUrp_CameraCullFlagControlsGpuFrustum()
        {
            string unsupported = ClusterMeshCapability.GetUnsupportedReason();
            if (unsupported != null)
                Assert.Ignore(unsupported);

            var mesh = ClusterMeshTestMeshes.Triangle();
            var settings = new ClusterMeshBakeSettings { buildLodHierarchy = false };
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], settings);
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, settings);
            var cameraObject = new GameObject("CMCameraCullGpuTest");
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.aspect = 1f;
            Matrix4x4 behindCamera = Matrix4x4.Translate(new Vector3(0f, 0f, -5f));
            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");

            using (var ctx = new ClusterMeshDrawContext(asset, cull, lit))
            {
                Assert.That(ctx.IsReady, Is.True, ctx.Error);
                ctx.EnableConeCull = false;
                Assert.That(ctx.PrepareUrp(
                    new[] { behindCamera }, new[] { false }, new[] { true },
                    camera, false, false), Is.True);
                Assert.That(VisibleInstanceCount(ctx), Is.Zero);

                Assert.That(ctx.PrepareUrp(
                    new[] { behindCamera }, new[] { false }, new[] { false },
                    camera, false, false), Is.True);
                Assert.That(VisibleInstanceCount(ctx), Is.EqualTo(1u));
            }

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Renderer_OnEnableWithEmptyAsset_DoesNotThrow()
        {
            var go = new GameObject("CMRenderer");
            var renderer = go.AddComponent<ClusterMeshRenderer>();
            Assert.DoesNotThrow(() => renderer.EnsureInitialized());
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Renderer_ShadowFlags_DefaultOn()
        {
            var go = new GameObject("CMRendererShadows");
            var renderer = go.AddComponent<ClusterMeshRenderer>();
            Assert.That(renderer.castShadows, Is.True);
            Assert.That(renderer.receiveShadows, Is.True);
            Object.DestroyImmediate(go);
        }

        static uint VisibleInstanceCount(ClusterMeshDrawContext context)
        {
            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var buffers = (GraphicsBuffer[])typeof(ClusterMeshDrawContext)
                .GetField("_argsBuffers", Flags).GetValue(context);
            var args = new uint[5];
            buffers[0].GetData(args);
            return args[1];
        }

    }
}
