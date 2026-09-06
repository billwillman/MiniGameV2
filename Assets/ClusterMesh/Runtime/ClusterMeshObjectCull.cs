using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ClusterMesh
{
    public static class ClusterMeshObjectCull
    {
        public const float NeverCullDistance = 1e9f;

        public static Plane NeverCullPlane()
        {
            return new Plane(Vector3.up, NeverCullDistance);
        }

        public static float ShadowDistance(Camera camera)
        {
            if (camera == null)
                return 0f;
            float far = camera.farClipPlane;
            UniversalRenderPipelineAsset urp = UniversalRenderPipeline.asset;
            if (urp != null)
                far = Mathf.Min(far, urp.shadowDistance);
            return Mathf.Max(far, camera.nearClipPlane + 1e-3f);
        }

        public static bool TryGetMainDirectionalShadowLight(out Light light)
        {
            light = RenderSettings.sun;
            if (light == null || !light.isActiveAndEnabled)
                return false;
            if (light.type != LightType.Directional)
                return false;
            return light.shadows != LightShadows.None;
        }

        public static void BuildReceiverFrustumPlanes(Camera camera, float shadowDistance, Plane[] dest)
        {
            if (camera == null || dest == null || dest.Length < 6)
                return;

            float far = Mathf.Min(camera.farClipPlane, shadowDistance);
            far = Mathf.Max(far, camera.nearClipPlane + 1e-3f);
            Matrix4x4 proj = camera.orthographic
                ? Matrix4x4.Ortho(
                    -camera.orthographicSize * camera.aspect,
                    camera.orthographicSize * camera.aspect,
                    -camera.orthographicSize,
                    camera.orthographicSize,
                    camera.nearClipPlane,
                    far)
                : Matrix4x4.Perspective(camera.fieldOfView, camera.aspect, camera.nearClipPlane, far);
            GeometryUtility.CalculateFrustumPlanes(proj * camera.worldToCameraMatrix, dest);
        }

        public static void ExtrudePlanesToward(Plane[] src, Vector3 direction, Plane[] dest)
        {
            if (dest == null || dest.Length < 6)
                return;

            Plane dummy = NeverCullPlane();
            if (src == null || src.Length < 6 || direction.sqrMagnitude < 1e-16f)
            {
                for (int i = 0; i < 6; i++)
                    dest[i] = src != null && i < src.Length ? src[i] : dummy;
                return;
            }

            int w = 0;
            for (int i = 0; i < 6; i++)
            {
                if (Vector3.Dot(src[i].normal, direction) >= 0f)
                    dest[w++] = src[i];
            }

            while (w < 6)
                dest[w++] = dummy;
        }

        public static bool KeepObject(
            Bounds world,
            Plane[] cameraPlanes,
            bool enableCpuCull,
            bool testShadowVolume,
            Plane[] shadowPlanes)
        {
            if (!enableCpuCull)
                return true;
            if (ClusterMeshFrustum.TestAabbWorld(world.center, world.extents, cameraPlanes))
                return true;
            return testShadowVolume
                && ClusterMeshFrustum.TestAabbWorld(world.center, world.extents, shadowPlanes);
        }
    }
}
