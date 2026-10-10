using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshMaterialUtilTests
    {
        [Test]
        public void SharedMaterial_BorrowsCompatibleSourceAndReleasesReferences()
        {
            int initial=ClusterMeshSharedMaterials.EntryCount;
            var source=new Material(Shader.Find("ClusterMesh/Lit")){enableInstancing=true};
            var a=ClusterMeshSharedMaterials.Acquire(source,source.shader,true);
            var b=ClusterMeshSharedMaterials.Acquire(source,source.shader,true);
            try
            {
                Assert.That(a.Material,Is.SameAs(source));Assert.That(b.Material,Is.SameAs(source));
                Assert.That(ClusterMeshSharedMaterials.EntryCount,Is.EqualTo(initial+1));
                a.Dispose();Assert.That(b.Material!=null,Is.True);
                b.Dispose();Assert.That(source!=null,Is.True);Assert.That(ClusterMeshSharedMaterials.EntryCount,Is.EqualTo(initial));
            }
            finally{a.Dispose();b.Dispose();Object.DestroyImmediate(source);}
        }
        [Test]
        public void SharedMaterial_KeywordVariantsAreSharedAndDoNotMutateSource()
        {
            int initial=ClusterMeshSharedMaterials.EntryCount;
            var source=new Material(Shader.Find("ClusterMesh/Lit")){enableInstancing=true};
            using(var on=ClusterMeshSharedMaterials.Acquire(source,source.shader,true))
            using(var off=ClusterMeshSharedMaterials.Acquire(source,source.shader,false))
            using(var anotherOff=ClusterMeshSharedMaterials.Acquire(source,source.shader,false))
            {
                Assert.That(on.Material,Is.SameAs(source));Assert.That(off.Material,Is.Not.SameAs(source));
                Assert.That(off.Material,Is.SameAs(anotherOff.Material));
                Assert.That(source.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF"),Is.False);
                Assert.That(off.Material.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF"),Is.True);
            }
            Assert.That(ClusterMeshSharedMaterials.EntryCount,Is.EqualTo(initial));Object.DestroyImmediate(source);
        }
        [Test]
        public void SharedMaterial_AdaptationIsSharedAndDestroyedAfterLastLease()
        {
            var source=new Material(Shader.Find("Unlit/Color"));
            var a=ClusterMeshSharedMaterials.Acquire(source,Shader.Find("ClusterMesh/Lit"),true);
            var b=ClusterMeshSharedMaterials.Acquire(source,Shader.Find("ClusterMesh/Lit"),true);
            var shared=a.Material;
            Assert.That(shared,Is.SameAs(b.Material));Assert.That(shared,Is.Not.SameAs(source));
            a.Dispose();Assert.That(shared!=null,Is.True);b.Dispose();Assert.That(shared==null,Is.True);
            Assert.That(source!=null,Is.True);Object.DestroyImmediate(source);
        }
        [Test]
        public void CreateRuntimeMaterial_CopiesBaseMapAndColorAndEnablesInstancing()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var source = new Material(shader);
            source.color = Color.red;

            var runtime = ClusterMeshMaterialUtil.CreateRuntimeMaterial(source, shader);

            Assert.That(runtime.enableInstancing, Is.True);
            Assert.That(runtime.color, Is.EqualTo(Color.red));
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(runtime);
        }

        [Test]
        public void CreateRuntimeMaterial_NullSource_StillUsesLitShader()
        {
            var shader = Shader.Find("Unlit/Color");
            var runtime = ClusterMeshMaterialUtil.CreateRuntimeMaterial(null, shader);
            Assert.That(runtime.shader, Is.EqualTo(shader));
            Assert.That(runtime.enableInstancing, Is.True);
            Object.DestroyImmediate(runtime);
        }

        [Test]
        public void CreateRuntimeMaterial_MarksHideAndDontSave()
        {
            var shader = Shader.Find("Unlit/Color");
            var runtime = ClusterMeshMaterialUtil.CreateRuntimeMaterial(null, shader);
            Assert.That(runtime.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            Object.DestroyImmediate(runtime);
        }

        [Test]
        public void CanSubmitShaderPass_InternalLoading_RejectsDepthGBufferAndMotionVectors()
        {
            Shader shader = Shader.Find("Hidden/Internal-Loading");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            Assert.That(material.passCount, Is.LessThanOrEqualTo(1));
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(null, 0), Is.False);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, -1), Is.False);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 2), Is.False);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 3), Is.False);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 4), Is.False);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 5), Is.False);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void CanSubmitShaderPass_ClusterMeshLit_AllowsForwardDepthGBufferAndMotionVectors()
        {
            Shader shader = Shader.Find("ClusterMesh/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 0), Is.True);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 2), Is.True);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 3), Is.True);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 4), Is.True);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 5), Is.True);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void EditorSyncCompilation_NullOrEmptyCommandBuffer_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ClusterMeshMaterialUtil.BeginEditorSyncCompilation(null));
            Assert.DoesNotThrow(() => ClusterMeshMaterialUtil.EndEditorSyncCompilation(null));
            var cmd = new CommandBuffer { name = "CMSyncCompile" };
            try
            {
                Assert.DoesNotThrow(() => ClusterMeshMaterialUtil.BeginEditorSyncCompilation(cmd));
                Assert.DoesNotThrow(() => ClusterMeshMaterialUtil.EndEditorSyncCompilation(cmd));
            }
            finally
            {
                cmd.Release();
            }
        }
    }
}
