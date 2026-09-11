using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshMaterialUtilTests
    {
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
        public void CanSubmitShaderPass_InternalLoading_RejectsDepthAndGBuffer()
        {
            Shader shader = Shader.Find("Hidden/Internal-Loading");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            Assert.That(material.passCount, Is.LessThanOrEqualTo(1));
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(null, 0), Is.False);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, -1), Is.False);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 2), Is.False);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 3), Is.False);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void CanSubmitShaderPass_ClusterMeshLit_AllowsForwardDepthAndGBuffer()
        {
            Shader shader = Shader.Find("ClusterMesh/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 0), Is.True);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 2), Is.True);
            Assert.That(ClusterMeshMaterialUtil.CanSubmitShaderPass(material, 3), Is.True);
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
