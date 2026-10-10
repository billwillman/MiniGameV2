using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

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
        public void Constructor_ValidBake_ContextsShareImmutableMaterial()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());

            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            using (var ctx = new ClusterMeshDrawContext(asset, cull, lit))
            using (var other = new ClusterMeshDrawContext(asset, cull, lit))
            {
                if (!ctx.IsReady)
                {
                    Object.DestroyImmediate(asset);
                    Object.DestroyImmediate(mesh);
                    return;
                }

                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var color = (Material[])typeof(ClusterMeshDrawContext).GetField("_materials", flags).GetValue(ctx);
                var shared = (Material[])typeof(ClusterMeshDrawContext).GetField("_materials", flags).GetValue(other);
                Assert.That(shared[0], Is.SameAs(color[0]));
                Assert.That(typeof(ClusterMeshDrawContext).GetField("_shadowMaterials",flags),Is.Null);
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
        public void SubmitUrpDepth_WhenShaderIsInternalLoading_DoesNotLogInvalidPass()
        {
            string unsupported = ClusterMeshCapability.GetUnsupportedReason();
            if (unsupported != null)
                Assert.Ignore(unsupported);

            Shader loading = Shader.Find("Hidden/Internal-Loading");
            Assert.That(loading, Is.Not.Null);
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            var camGo = new GameObject("CMLoadingShaderCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
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
                color[0].shader = loading;
                Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(color[0], 2), Is.False);
                Assert.That(ctx.PrepareUrp(
                    new[] { Matrix4x4.identity }, new[] { false }, new[] { false },
                    cam, false, false), Is.True);

                var cmd = new CommandBuffer { name = "CMLoadingShaderDepth" };
                try
                {
                    Assert.DoesNotThrow(() => ctx.SubmitUrpDepth(cmd));
                    Assert.DoesNotThrow(() => ctx.SubmitUrpGBuffer(cmd));
                    Graphics.ExecuteCommandBuffer(cmd);
                }
                finally
                {
                    cmd.Release();
                }
            }

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
            LogAssert.NoUnexpectedReceived();
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
        public void SharedMaterial_QueuedDrawsKeepIndependentTransformsAndBuffers()
        {
            if(!ClusterMeshCapability.IsSupported())Assert.Ignore(ClusterMeshCapability.GetUnsupportedReason());
            var mesh=ClusterMeshTestMeshes.Triangle();mesh.triangles=new[]{0,2,1,0,1,2};
            var source=new Material(Shader.Find("ClusterMesh/Lit")){enableInstancing=true};
            var settings=new ClusterMeshBakeSettings{buildLodHierarchy=false};
            var a=ScriptableObject.CreateInstance<ClusterMeshAsset>();var b=ScriptableObject.CreateInstance<ClusterMeshAsset>();
            a.CopyFrom(ClusterMeshBaker.Bake(mesh,new[]{source},settings),mesh,settings);
            b.CopyFrom(ClusterMeshBaker.Bake(mesh,new[]{source},settings),mesh,settings);
            var go=new GameObject("MPB isolation camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(0,0,-5);camera.orthographic=true;camera.orthographicSize=2;camera.aspect=1;camera.nearClipPlane=.1f;camera.farClipPlane=20;
            var target=new RenderTexture(128,128,24);target.Create();var pixels=new Texture2D(128,128,TextureFormat.RGBA32,false);
            var cmd=new CommandBuffer();var previous=RenderTexture.active;
            var cull=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            try
            {
                using(var left=new ClusterMeshDrawContext(a,cull,source.shader))
                using(var right=new ClusterMeshDrawContext(b,cull,source.shader))
                {
                    Assert.That(left.IsReady&&right.IsReady,Is.True);
                    left.EnableConeCull=right.EnableConeCull=false;left.EnableClusterColor=right.EnableClusterColor=true;
                    Assert.That(left.PrepareUrp(new[]{Matrix4x4.Translate(new Vector3(-1.5f,-.5f,0))},new[]{false},new[]{false},camera,false,true),Is.True);
                    Assert.That(right.PrepareUrp(new[]{Matrix4x4.Translate(new Vector3(.5f,-.5f,0))},new[]{false},new[]{false},camera,false,true),Is.True);
                    cmd.SetRenderTarget(target);cmd.ClearRenderTarget(true,true,Color.black);
                    cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(camera.projectionMatrix,true));
                    left.SubmitUrpColor(cmd);right.SubmitUrpColor(cmd);
                    // Deliberately overwrite the reusable MPB after submission.
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    ((MaterialPropertyBlock)typeof(ClusterMeshDrawContext).GetField("_drawProperties",flags).GetValue(left)).Clear();
                    Graphics.ExecuteCommandBuffer(cmd);RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();
                    int leftPixels=0,rightPixels=0;
                    for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                    {var p=pixels.GetPixel(x,y);if(Mathf.Max(p.r,Mathf.Max(p.g,p.b))>.1f){if(x<64)leftPixels++;else rightPixels++;}}
                    Assert.That(leftPixels,Is.GreaterThan(100),"left draw was overwritten by right draw");
                    Assert.That(rightPixels,Is.GreaterThan(100));
                    var materials=(Material[])typeof(ClusterMeshDrawContext).GetField("_materials",flags).GetValue(left);
                    Assert.That(materials[0],Is.SameAs(source));
                    Assert.That(source.GetFloat("_EnableClusterColor"),Is.Zero,"per-draw data must not touch source");
                }
                Assert.That(source!=null,Is.True,"borrowed source must survive disposal");
            }
            finally
            {
                RenderTexture.active=previous;cmd.Release();target.Release();
                Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);Object.DestroyImmediate(go);
                Object.DestroyImmediate(a);Object.DestroyImmediate(b);Object.DestroyImmediate(source);Object.DestroyImmediate(mesh);
            }
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
