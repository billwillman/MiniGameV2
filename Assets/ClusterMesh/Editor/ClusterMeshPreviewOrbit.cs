using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshPreviewOrbit
    {
        public static readonly Vector2 DefaultAngles = new Vector2(Mathf.Atan2(0.35f, 2.4f) * Mathf.Rad2Deg, 0f);
        public static readonly float DefaultDistance = Mathf.Sqrt(0.35f * 0.35f + 2.4f * 2.4f);

        public static Vector3 Offset(Vector2 orbit, float radius, float distance)
        {
            return Quaternion.Euler(orbit.x, orbit.y, 0f) * new Vector3(0f, 0f, -radius * distance);
        }

        public static Vector2 ApplyDrag(Vector2 orbit, Vector2 delta)
        {
            orbit.y += delta.x;
            orbit.x = Mathf.Clamp(orbit.x + delta.y, -89f, 89f);
            return orbit;
        }
    }
}
