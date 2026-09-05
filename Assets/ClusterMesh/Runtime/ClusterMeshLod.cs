using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshLod
    {
        public const int NoParent = -1;
        public const uint FlagParent = 1u;

        public static bool IsParent(uint flags)
        {
            return (flags & FlagParent) != 0;
        }

        public static int Level(uint flags)
        {
            return IsParent(flags) ? 1 : 0;
        }

        public static float ProjectionScale(Camera camera)
        {
            if (camera == null)
                return 0f;
            float h = camera.pixelHeight;
            if (camera.orthographic)
                return h / Mathf.Max(2f * camera.orthographicSize, 1e-4f);
            float tanHalf = Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad);
            return (0.5f * h) / Mathf.Max(tanHalf, 1e-6f);
        }

        public static float ProjectError(float lodError, float distance, float scale, bool perspective)
        {
            float e = lodError * scale;
            if (perspective)
                e /= Mathf.Max(distance, 1e-4f);
            return e;
        }

        public static bool IsVisible(
            in ClusterHeader self,
            in ClusterHeader parent,
            bool hasParent,
            float selfDistance,
            float parentDistance,
            float scale,
            float threshold,
            int hierarchyVersion,
            bool perspective)
        {
            if (hierarchyVersion < 1)
                return true;
            if (threshold <= 0f)
                return !IsParent(self.flags);
            float selfP = ProjectError(self.lodError, selfDistance, scale, perspective);
            if (selfP >= threshold)
                return false;
            if (!hasParent || self.parentIndex < 0)
                return true;
            float parentP = ProjectError(parent.lodError, parentDistance, scale, perspective);
            return parentP >= threshold;
        }
    }
}
