using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshCpuCullTests
    {
        [Test]
        public void EnableCpuObjectCull_DefaultsTrue()
        {
            var go = new GameObject("CMCpuCullFlag");
            var renderer = go.AddComponent<ClusterMeshRenderer>();
            Assert.That(renderer.enableCpuObjectCull, Is.True);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void EnableCameraCull_DefaultsTrue()
        {
            var go = new GameObject("CMCameraCullFlag");
            var renderer = go.AddComponent<ClusterMeshRenderer>();
            Assert.That(renderer.enableCameraCull, Is.True);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void MotionVectors_DefaultOff_AndStaticHistoryAdvancesOncePerFrame()
        {
            var go = new GameObject("CMMotionHistory");
            var renderer = go.AddComponent<ClusterMeshRenderer>();
            Assert.That(renderer.enableMotionVectors, Is.False);
            renderer.enableMotionVectors = true;
            Matrix4x4 a = Matrix4x4.identity;
            Matrix4x4 b = Matrix4x4.Translate(Vector3.right);
            Matrix4x4 c = Matrix4x4.Translate(Vector3.up);
            Assert.That(renderer.CapturePreviousMotionMatrix(a, 10), Is.EqualTo(a));
            Assert.That(renderer.CapturePreviousMotionMatrix(b, 11), Is.EqualTo(a));
            Assert.That(renderer.CapturePreviousMotionMatrix(c, 11), Is.EqualTo(a));
            Assert.That(renderer.CapturePreviousMotionMatrix(c, 12), Is.EqualTo(c));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SkinnedMotionVectors_DefaultOff_AndHistoryTracksPoseTime()
        {
            var go = new GameObject("CMSkinnedMotionHistory");
            var renderer = go.AddComponent<ClusterSkinnedMeshRenderer>();
            Assert.That(renderer.enableMotionVectors, Is.False);
            renderer.enableMotionVectors = true;
            renderer.CapturePreviousMotion(Matrix4x4.identity, 0.1f, 0, 20,
                out Matrix4x4 firstMatrix, out float firstTime);
            renderer.CapturePreviousMotion(Matrix4x4.Translate(Vector3.right), 0.2f, 0, 21,
                out Matrix4x4 previousMatrix, out float previousTime);
            Assert.That(firstMatrix, Is.EqualTo(Matrix4x4.identity));
            Assert.That(firstTime, Is.EqualTo(0.1f));
            Assert.That(previousMatrix, Is.EqualTo(Matrix4x4.identity));
            Assert.That(previousTime, Is.EqualTo(0.1f));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CountIndirectDraws_SplitsWhenRequested()
        {
            Assert.That(ClusterMeshSceneBatcher.CountDrawCalls(10, 1), Is.EqualTo(1));
            Assert.That(ClusterMeshSceneBatcher.CountIndirectDraws(10, 1, false), Is.EqualTo(1));
            Assert.That(ClusterMeshSceneBatcher.CountIndirectDraws(10, 1, true), Is.EqualTo(2));
            Assert.That(ClusterMeshSceneBatcher.CountIndirectDraws(10, 2, true), Is.EqualTo(4));
            Assert.That(ClusterMeshSceneBatcher.CountIndirectDraws(0, 1, true), Is.EqualTo(0));
        }

        [Test]
        public void KeepObject_InsideCamera_AlwaysTrue()
        {
            Plane[] cam = InwardBoxPlanes(5f);
            var world = new Bounds(Vector3.zero, Vector3.one);
            Assert.That(ClusterMeshObjectCull.KeepObject(world, cam, true, false, cam), Is.True);
        }

        [Test]
        public void KeepObject_Behind_DisableCpuCull_True()
        {
            Plane[] cam = LookPlusZPlanes();
            var behind = new Bounds(new Vector3(0f, 0f, -4f), Vector3.one);
            Assert.That(ClusterMeshObjectCull.KeepObject(behind, cam, false, true, cam), Is.True);
        }

        [Test]
        public void KeepObject_Behind_NoShadowVolume_False()
        {
            Plane[] cam = LookPlusZPlanes();
            var behind = new Bounds(new Vector3(0f, 0f, -4f), Vector3.one);
            Assert.That(ClusterMeshObjectCull.KeepObject(behind, cam, true, false, cam), Is.False);
        }

        [Test]
        public void KeepObject_Behind_LightFromBehind_Kept()
        {
            Plane[] cam = LookPlusZPlanes();
            var shadow = new Plane[6];
            ClusterMeshObjectCull.ExtrudePlanesToward(cam, Vector3.back, shadow);
            var behind = new Bounds(new Vector3(0f, 0f, -4f), Vector3.one);
            Assert.That(ClusterMeshObjectCull.KeepObject(behind, cam, true, true, shadow), Is.True);
        }

        [Test]
        public void KeepObject_Behind_LightFromFront_Dropped()
        {
            Plane[] cam = LookPlusZPlanes();
            var shadow = new Plane[6];
            ClusterMeshObjectCull.ExtrudePlanesToward(cam, Vector3.forward, shadow);
            var behind = new Bounds(new Vector3(0f, 0f, -4f), Vector3.one);
            Assert.That(ClusterMeshObjectCull.KeepObject(behind, cam, true, true, shadow), Is.False);
        }

        [Test]
        public void ExtrudeTowardBack_DropsNear_KeepsBehindPoint()
        {
            Plane[] cam = LookPlusZPlanes();
            var shadow = new Plane[6];
            ClusterMeshObjectCull.ExtrudePlanesToward(cam, Vector3.back, shadow);
            var behind = new Bounds(new Vector3(0f, 0f, -4f), Vector3.one);
            Assert.That(ClusterMeshFrustum.TestAabbWorld(behind.center, behind.extents, cam), Is.False);
            Assert.That(ClusterMeshFrustum.TestAabbWorld(behind.center, behind.extents, shadow), Is.True);
        }

        [Test]
        public void CompactCount_OneInView_ThreeBehindLightFromFront_KeepsOne()
        {
            Plane[] cam = LookPlusZPlanes();
            var shadow = new Plane[6];
            ClusterMeshObjectCull.ExtrudePlanesToward(cam, Vector3.forward, shadow);
            var boxes = new[]
            {
                new Bounds(new Vector3(0f, 0f, 3f), Vector3.one),
                new Bounds(new Vector3(0f, 0f, -4f), Vector3.one),
                new Bounds(new Vector3(1f, 0f, -5f), Vector3.one),
                new Bounds(new Vector3(-1f, 0f, -6f), Vector3.one)
            };
            int kept = 0;
            for (int i = 0; i < boxes.Length; i++)
            {
                if (ClusterMeshObjectCull.KeepObject(boxes[i], cam, true, true, shadow))
                    kept++;
            }

            Assert.That(kept, Is.EqualTo(1));
        }

        [Test]
        public void ShadowDistance_AtMostCameraFarAndUrp()
        {
            var go = new GameObject("CMShadowDistCam");
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            float sd = ClusterMeshObjectCull.ShadowDistance(camera);
            Assert.That(sd, Is.LessThanOrEqualTo(camera.farClipPlane));
            UniversalRenderPipelineAsset urp = UniversalRenderPipeline.asset;
            if (urp != null)
                Assert.That(sd, Is.LessThanOrEqualTo(urp.shadowDistance));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TryGetMainDirectionalShadowLight_OnlyUsesSun()
        {
            Light previous = RenderSettings.sun;
            var go = new GameObject("CMSun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
            Assert.That(ClusterMeshObjectCull.TryGetMainDirectionalShadowLight(out Light found), Is.True);
            Assert.That(found, Is.EqualTo(light));

            light.shadows = LightShadows.None;
            Assert.That(ClusterMeshObjectCull.TryGetMainDirectionalShadowLight(out _), Is.False);

            RenderSettings.sun = previous;
            Object.DestroyImmediate(go);
        }

        static Plane[] InwardBoxPlanes(float extent)
        {
            return new[]
            {
                new Plane(Vector3.right, extent),
                new Plane(Vector3.left, extent),
                new Plane(Vector3.up, extent),
                new Plane(Vector3.down, extent),
                new Plane(Vector3.forward, extent),
                new Plane(Vector3.back, extent)
            };
        }

        static Plane[] LookPlusZPlanes()
        {
            return new[]
            {
                new Plane(Vector3.forward, 0f),
                new Plane(Vector3.back, 20f),
                new Plane(Vector3.right, 10f),
                new Plane(Vector3.left, 10f),
                new Plane(Vector3.up, 10f),
                new Plane(Vector3.down, 10f)
            };
        }
    }
}
