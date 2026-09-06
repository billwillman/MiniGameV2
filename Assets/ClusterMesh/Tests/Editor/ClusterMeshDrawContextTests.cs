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
    }
}
