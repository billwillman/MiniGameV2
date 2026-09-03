using NUnit.Framework;
using UnityEngine;

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
    }
}
