using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshFrustum
    {
        public static void CameraToLocalPlanes(Camera camera, Matrix4x4 localToWorld, Plane[] dest)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, dest);
            Matrix4x4 worldToLocal = localToWorld.inverse;
            for (int i = 0; i < 6; i++)
            {
                Vector3 n = dest[i].normal;
                float d = dest[i].distance;
                Vector3 pointWorld = n * -d;
                Vector3 pointLocal = worldToLocal.MultiplyPoint3x4(pointWorld);
                Vector3 nLocal = localToWorld.transpose.MultiplyVector(n);
                float len = nLocal.magnitude;
                dest[i] = len > 1e-8f
                    ? new Plane(nLocal / len, pointLocal)
                    : dest[i];
            }
        }

        public static bool TestAabb(in ClusterHeader header, Plane[] planes)
        {
            Vector3 c = header.aabbCenter;
            Vector3 e = header.aabbExtents;
            for (int i = 0; i < 6; i++)
            {
                Vector3 n = planes[i].normal;
                float r = e.x * Mathf.Abs(n.x) + e.y * Mathf.Abs(n.y) + e.z * Mathf.Abs(n.z);
                if (Vector3.Dot(n, c) + planes[i].distance + r < 0f)
                    return false;
            }

            return true;
        }

        public static bool TestCone(in ClusterHeader header, Vector3 localCameraPos)
        {
            float cutoff = header.coneAxisCutoff.w;
            if (cutoff < 0f)
                return true;

            Vector3 axis = header.coneAxisCutoff;
            float axisLenSq = axis.sqrMagnitude;
            if (axisLenSq < 1e-16f)
                return true;
            axis /= Mathf.Sqrt(axisLenSq);

            Vector3 view = localCameraPos - (Vector3)header.coneApex;
            float dist = view.magnitude;
            if (dist < 1e-6f)
                return true;

            Vector3 extents = header.aabbExtents;
            float radius = extents.magnitude;
            if (dist <= radius)
                return true;

            view /= dist;
            float cosA = Mathf.Clamp01(cutoff);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            float sinB = radius / dist;
            float cosB = Mathf.Sqrt(Mathf.Max(0f, 1f - sinB * sinB));
            float threshold = -(sinA * cosB + cosA * sinB);
            return Vector3.Dot(view, axis) >= threshold;
        }

        public static void WorldPlanes(Camera camera, Plane[] dest)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, dest);
        }

        public static void TransformAabb(in ClusterHeader header, Matrix4x4 localToWorld, out Vector3 worldCenter, out Vector3 worldExtents)
        {
            Vector3 c = header.aabbCenter;
            Vector3 e = header.aabbExtents;
            worldCenter = localToWorld.MultiplyPoint3x4(c);
            Vector3 axisX = localToWorld.MultiplyVector(new Vector3(e.x, 0f, 0f));
            Vector3 axisY = localToWorld.MultiplyVector(new Vector3(0f, e.y, 0f));
            Vector3 axisZ = localToWorld.MultiplyVector(new Vector3(0f, 0f, e.z));
            worldExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        }

        public static Bounds AssetLocalBounds(ClusterMeshAsset asset)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (asset == null || asset.clusters == null || asset.clusters.Length == 0)
                return bounds;

            bool init = false;
            for (int i = 0; i < asset.clusters.Length; i++)
            {
                Vector3 c = asset.clusters[i].aabbCenter;
                Vector3 e = asset.clusters[i].aabbExtents;
                if (!init)
                {
                    bounds = new Bounds(c, e * 2f);
                    init = true;
                }
                else
                {
                    bounds.Encapsulate(c + e);
                    bounds.Encapsulate(c - e);
                }
            }

            return bounds;
        }

        public static bool TestAabbWorld(Vector3 worldCenter, Vector3 worldExtents, Plane[] planes)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 n = planes[i].normal;
                float r = worldExtents.x * Mathf.Abs(n.x) + worldExtents.y * Mathf.Abs(n.y) + worldExtents.z * Mathf.Abs(n.z);
                if (Vector3.Dot(n, worldCenter) + planes[i].distance + r < 0f)
                    return false;
            }

            return true;
        }
    }
}
