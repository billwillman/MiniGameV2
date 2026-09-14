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
        public void TestCone_GrazingAngle_DoesNotCull()
        {
            var cone = new ClusterHeader
            {
                coneAxisCutoff = new Vector4(0f, 0f, 1f, 0.5f),
                coneApex = Vector3.zero
            };
            Assert.That(ClusterMeshFrustum.TestCone(cone, new Vector3(2f, 0f, 0f)), Is.True);
        }

        [Test]
        public void TestCone_CameraInsideClusterBounds_DoesNotCull()
        {
            var cone = new ClusterHeader
            {
                coneAxisCutoff = new Vector4(0f, 0f, 1f, 1f),
                coneApex = Vector3.zero,
                aabbExtents = new Vector4(1f, 1f, 1f, 0f)
            };
            Assert.That(ClusterMeshFrustum.TestCone(cone, new Vector3(0f, 0f, -0.2f)), Is.True);
        }

        [Test]
        public void TestCone_FarBehind_StillCullsWithExtents()
        {
            var cone = new ClusterHeader
            {
                coneAxisCutoff = new Vector4(0f, 0f, 1f, 1f),
                coneApex = Vector3.zero,
                aabbExtents = new Vector4(0.1f, 0.1f, 0.1f, 0f)
            };
            Assert.That(ClusterMeshFrustum.TestCone(cone, new Vector3(0f, 0f, -2f)), Is.False);
        }

        [Test]
        public void TestCone_BakedFlatTriangle_SideViewVisible()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var result = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            ClusterHeader header = result.clusters[0];
            Vector3 side = (Vector3)header.coneApex + new Vector3(2f, 0f, 0.1f);
            Assert.That(ClusterMeshFrustum.TestCone(header, side), Is.True);
            Object.DestroyImmediate(mesh);
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
        public void AssetLocalBounds_EncapsulatesClusters()
        {
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.clusters = new[]
            {
                new ClusterHeader { aabbCenter = new Vector4(0f, 0f, 0f, 0f), aabbExtents = new Vector4(1f, 1f, 1f, 0f) },
                new ClusterHeader { aabbCenter = new Vector4(10f, 0f, 0f, 0f), aabbExtents = new Vector4(1f, 1f, 1f, 0f) }
            };
            Bounds b = ClusterMeshFrustum.AssetLocalBounds(asset);
            Assert.That(b.min.x, Is.LessThanOrEqualTo(-1f));
            Assert.That(b.max.x, Is.GreaterThanOrEqualTo(11f));
            Object.DestroyImmediate(asset);
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

        [Test]
        public void TransformLocalBounds_Identity_MatchesLocal()
        {
            var local = new Bounds(new Vector3(1f, 2f, 3f), new Vector3(2f, 4f, 6f));
            Bounds world = ClusterMeshFrustum.TransformLocalBounds(local, Matrix4x4.identity);
            Assert.That(world.center, Is.EqualTo(local.center));
            Assert.That(world.extents, Is.EqualTo(local.extents));
        }

        [Test]
        public void TransformLocalBounds_Translation_MovesCenterKeepsSize()
        {
            var local = new Bounds(Vector3.zero, new Vector3(2f, 4f, 6f));
            Bounds world = ClusterMeshFrustum.TransformLocalBounds(local, Matrix4x4.Translate(new Vector3(10f, 0f, 0f)));
            Assert.That(world.center, Is.EqualTo(new Vector3(10f, 0f, 0f)));
            Assert.That(world.extents, Is.EqualTo(local.extents));
        }

        [Test]
        public void TransformLocalBounds_AssetBoxContainsClusterWorldCorners()
        {
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.clusters = new[]
            {
                new ClusterHeader { aabbCenter = new Vector4(0f, 0f, 0f, 0f), aabbExtents = new Vector4(1f, 0.5f, 0.25f, 0f) },
                new ClusterHeader { aabbCenter = new Vector4(10f, 1f, -2f, 0f), aabbExtents = new Vector4(0.5f, 1f, 1.5f, 0f) }
            };
            Bounds local = ClusterMeshFrustum.AssetLocalBounds(asset);
            Matrix4x4 l2w = Matrix4x4.TRS(new Vector3(3f, -1f, 4f), Quaternion.Euler(20f, 40f, -15f), new Vector3(2f, 0.5f, 1.25f));
            Bounds world = ClusterMeshFrustum.TransformLocalBounds(local, l2w);
            for (int i = 0; i < asset.clusters.Length; i++)
            {
                Vector3 c = asset.clusters[i].aabbCenter;
                Vector3 e = asset.clusters[i].aabbExtents;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 w = l2w.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3(x, y, z)));
                    Assert.That(world.Contains(w) || PointOnBounds(world, w), Is.True);
                }
            }

            Object.DestroyImmediate(asset);
        }

        static bool PointOnBounds(Bounds bounds, Vector3 p)
        {
            const float eps = 1e-4f;
            return p.x >= bounds.min.x - eps && p.x <= bounds.max.x + eps
                && p.y >= bounds.min.y - eps && p.y <= bounds.max.y + eps
                && p.z >= bounds.min.z - eps && p.z <= bounds.max.z + eps;
        }
    }
}
