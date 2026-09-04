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
            Vector3 view = localCameraPos - (Vector3)header.coneApex;
            if (view.sqrMagnitude < 1e-12f)
                return true;
            return Vector3.Dot(view.normalized, axis) >= cutoff;
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
