using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshGeometryTests
    {
        [Test]
        public void PackedVertex_StrideIs32()
        {
            Assert.That(Marshal.SizeOf<ClusterPackedVertex>(), Is.EqualTo(32));
            Assert.That(ClusterMeshLimits.ClusterVertexStride, Is.EqualTo(32));
            Assert.That(ClusterMeshLimits.GeometryVersion, Is.EqualTo(1));
        }

        [Test]
        public void PackUnpack_PositionInUnitCube_ErrorBelow1e4()
        {
            var src = new ClusterVertex
            {
                position = new Vector4(0.25f, -0.5f, 0.75f, 0f),
                normal = new Vector4(0f, 0f, 1f, 0f),
                tangent = new Vector4(1f, 0f, 0f, 1f),
                uv = new Vector4(0.1f, 0.2f, 0f, 0f)
            };
            ClusterVertex dst = ClusterMeshGeometry.UnpackVertex(ClusterMeshGeometry.PackVertex(src));
            Assert.That(((Vector3)dst.position - (Vector3)src.position).magnitude, Is.LessThan(1e-4f));
        }

        [Test]
        public void PackUnpack_Normal_DotProductAbove999()
        {
            var n = new Vector3(0.2f, 0.5f, 0.84f).normalized;
            var src = new ClusterVertex
            {
                position = Vector4.zero,
                normal = n,
                tangent = new Vector4(1f, 0f, 0f, 1f),
                uv = Vector4.zero
            };
            ClusterVertex dst = ClusterMeshGeometry.UnpackVertex(ClusterMeshGeometry.PackVertex(src));
            Assert.That(Vector3.Dot(((Vector3)dst.normal).normalized, n), Is.GreaterThan(0.999f));
        }

        [Test]
        public void PackUnpack_TangentZ_WhenNormalAlongX()
        {
            var src = new ClusterVertex
            {
                position = Vector4.zero,
                normal = new Vector4(1f, 0f, 0f, 0f),
                tangent = new Vector4(0f, 0f, 1f, -1f),
                uv = Vector4.zero
            };
            ClusterVertex dst = ClusterMeshGeometry.UnpackVertex(ClusterMeshGeometry.PackVertex(src));
            Assert.That(Vector3.Dot((Vector3)dst.normal, (Vector3)dst.tangent), Is.LessThan(1e-3f));
            Assert.That(dst.tangent.z, Is.GreaterThan(0.99f));
            Assert.That(dst.tangent.w, Is.EqualTo(-1f));
        }

        [Test]
        public void PackIndices_OddCount_PadsAndRoundTrips()
        {
            uint[] src = { 1, 2, 3, 4, 5 };
            uint[] packed = ClusterMeshGeometry.PackIndices(src);
            Assert.That(packed.Length, Is.EqualTo(3));
            Assert.That(packed[2] & 0xFFFFu, Is.EqualTo(5u));
            Assert.That(packed[2] >> 16, Is.EqualTo(0u));
            Assert.That(ClusterMeshGeometry.UnpackIndices(packed, src.Length), Is.EqualTo(src));
        }

        [Test]
        public void Deflate_RandomBytes_RoundTrip()
        {
            var src = new byte[64];
            for (int i = 0; i < src.Length; i++)
                src[i] = (byte)(i * 17 + 3);
            byte[] packed = ClusterMeshGeometry.Deflate(src);
            Assert.That(ClusterMeshGeometry.TryInflate(packed, out byte[] raw), Is.True);
            Assert.That(raw, Is.EqualTo(src));
        }

        [Test]
        public void CopyFrom_WritesGeometryVersionOne()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());

            Assert.That(asset.geometryVersion, Is.EqualTo(ClusterMeshLimits.GeometryVersion));
            Assert.That(asset.vertexCount, Is.EqualTo(bake.vertices.Length));
            Assert.That(asset.indexCount, Is.EqualTo(bake.indices.Length));
            Assert.That(asset.packedVertices, Is.Not.Null);
            Assert.That(asset.packedVertices.Length, Is.GreaterThan(0));
            Assert.That(asset.packedIndices, Is.Not.Null);
            Assert.That(asset.packedIndices.Length, Is.GreaterThan(0));

            Assert.That(
                ClusterMeshGeometry.TryReadWorkingGeometry(asset, out ClusterVertex[] verts, out uint[] inds, out string error),
                Is.True);
            Assert.That(error, Is.Null);
            Assert.That(verts.Length, Is.EqualTo(bake.vertices.Length));
            Assert.That(inds, Is.EqualTo(bake.indices));
            Assert.That(((Vector3)verts[0].position - (Vector3)bake.vertices[0].position).magnitude, Is.LessThan(1e-4f));

            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void TryReadGpuGeometry_VersionZero_Fails()
        {
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.geometryVersion = 0;
            asset.vertexCount = 1;
            asset.indexCount = 3;
            Assert.That(
                ClusterMeshGeometry.TryReadGpuGeometry(asset, out _, out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("rebake"));
            Object.DestroyImmediate(asset);
        }
    }
}
