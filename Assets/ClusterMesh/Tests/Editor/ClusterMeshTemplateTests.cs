using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshTemplateTests
    {
        [Test]
        public void Create_Has372SequentialTriangles()
        {
            var mesh = ClusterMeshTemplate.Create();
            Assert.That(mesh.vertexCount, Is.EqualTo(ClusterMeshLimits.TemplateVertexCount));
            Assert.That(mesh.triangles.Length, Is.EqualTo(ClusterMeshLimits.TemplateVertexCount));
            Assert.That(mesh.triangles[0], Is.EqualTo(0));
            Assert.That(mesh.triangles[371], Is.EqualTo(371));
            Object.DestroyImmediate(mesh);
        }
    }
}
