using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshFrustumTests
    {
        [Test]
        public void TestAabb_InsideInwardPlanes()
        {
            var planes = new[]
            {
                new Plane(Vector3.right, 2f),
                new Plane(Vector3.left, 2f),
                new Plane(Vector3.up, 2f),
                new Plane(Vector3.down, 2f),
                new Plane(Vector3.forward, 2f),
                new Plane(Vector3.back, 2f)
            };
            var header = new ClusterHeader
            {
                aabbCenter = Vector3.zero,
                aabbExtents = Vector3.one * 0.1f
            };
            Assert.That(ClusterMeshFrustum.TestAabb(header, planes), Is.True);
        }

        [Test]
        public void TestAabb_OutsideFarPlane()
        {
            var planes = new[]
            {
                new Plane(Vector3.right, 2f),
                new Plane(Vector3.left, 2f),
                new Plane(Vector3.up, 2f),
                new Plane(Vector3.down, 2f),
                new Plane(Vector3.forward, 2f),
                new Plane(Vector3.back, -10f)
            };
            var header = new ClusterHeader
            {
                aabbCenter = Vector3.zero,
                aabbExtents = Vector3.one * 0.1f
            };
            Assert.That(ClusterMeshFrustum.TestAabb(header, planes), Is.False);
        }

        [Test]
        public void TestCone_BackfaceCulls_FrontDoesNot_DisabledNeverCulls()
        {
            var cone = new ClusterHeader
            {
                coneAxisCutoff = new Vector4(0f, 0f, 1f, 0.5f),
                coneApex = Vector3.zero
            };
            Assert.That(ClusterMeshFrustum.TestCone(cone, new Vector3(0f, 0f, -2f)), Is.False);
            Assert.That(ClusterMeshFrustum.TestCone(cone, new Vector3(0f, 0f, 2f)), Is.True);

            cone.coneAxisCutoff = new Vector4(0f, 0f, 1f, -1f);
            Assert.That(ClusterMeshFrustum.TestCone(cone, new Vector3(0f, 0f, -2f)), Is.True);
        }

        [Test]
        public void CameraToLocalPlanes_IdentityCameraContainsOrigin()
        {
            var go = new GameObject("CMFrustumCam");
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            go.transform.position = new Vector3(0f, 0f, -5f);
            go.transform.rotation = Quaternion.identity;

            var planes = new Plane[6];
            ClusterMeshFrustum.CameraToLocalPlanes(camera, Matrix4x4.identity, planes);
            var header = new ClusterHeader
            {
                aabbCenter = Vector3.zero,
                aabbExtents = Vector3.one * 0.2f
            };
            Assert.That(ClusterMeshFrustum.TestAabb(header, planes), Is.True);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TransformAabb_Identity_MatchesLocal()
        {
            var h = new ClusterHeader
            {
                aabbCenter = new Vector4(1f, 2f, 3f, 0f),
                aabbExtents = new Vector4(0.5f, 1f, 1.5f, 0f)
            };
            ClusterMeshFrustum.TransformAabb(h, Matrix4x4.identity, out var c, out var e);
            Assert.That(c, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(e, Is.EqualTo(new Vector3(0.5f, 1f, 1.5f)));
        }

        [Test]
        public void TransformAabb_Translation_MovesCenterKeepsExtents()
        {
            var h = new ClusterHeader
            {
                aabbCenter = new Vector4(0f, 0f, 0f, 0f),
                aabbExtents = new Vector4(1f, 2f, 3f, 0f)
            };
            ClusterMeshFrustum.TransformAabb(h, Matrix4x4.Translate(new Vector3(10f, 0f, 0f)), out var c, out var e);
            Assert.That(c, Is.EqualTo(new Vector3(10f, 0f, 0f)));
            Assert.That(e, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }
    }
}
