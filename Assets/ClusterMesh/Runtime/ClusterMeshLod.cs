using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshLod
    {
        public const int NoParent = -1;
        public const uint FlagParent = 1u;
        public const int LodLevelShift = 8;
        public const uint LodLevelMask = 0xFFu;
        public const int HierarchyVersionTwoLevel = 1;
        public const int HierarchyVersionDag = 2;
        public const int MaxLodLevels = 16;

        public static bool IsParent(uint flags)
        {
            return (flags & FlagParent) != 0;
        }

        public static uint PackFlags(int level)
        {
            uint packed = ((uint)Mathf.Max(0, level) & LodLevelMask) << LodLevelShift;
            if (level > 0)
                packed |= FlagParent;
            return packed;
        }

        public static int Level(uint flags)
        {
            int packed = (int)((flags >> LodLevelShift) & LodLevelMask);
            if (packed > 0)
                return packed;
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

        public static bool TryGetParent(
            in ClusterHeader self,
            ClusterHeader[] clusters,
            ClusterGroup[] groups,
            int hierarchyVersion,
            out float parentLodError,
            out Vector3 parentLocalCenter,
            out bool hasParent)
        {
            parentLodError = 0f;
            parentLocalCenter = Vector3.zero;
            hasParent = false;
            if (self.parentIndex < 0)
                return false;

            if (hierarchyVersion >= HierarchyVersionDag)
            {
                if (groups == null || self.parentIndex >= groups.Length)
                    return false;
                ClusterGroup g = groups[self.parentIndex];
                parentLodError = g.lodError;
                parentLocalCenter = g.aabbCenter;
                hasParent = true;
                return true;
            }

            if (clusters == null || self.parentIndex >= clusters.Length)
                return false;
            ClusterHeader p = clusters[self.parentIndex];
            parentLodError = p.lodError;
            parentLocalCenter = p.aabbCenter;
            hasParent = true;
            return true;
        }

        public static bool IsVisible(
            in ClusterHeader self,
            float parentLodError,
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
                return Level(self.flags) == 0;
            float selfP = ProjectError(self.lodError, selfDistance, scale, perspective);
            if (selfP >= threshold)
                return false;
            if (!hasParent)
                return true;
            return ProjectError(parentLodError, parentDistance, scale, perspective) >= threshold;
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
            return IsVisible(
                self,
                parent.lodError,
                hasParent,
                selfDistance,
                parentDistance,
                scale,
                threshold,
                hierarchyVersion,
                perspective);
        }

        public static Color LevelColor(int level)
        {
            switch (((level % 6) + 6) % 6)
            {
                case 0: return new Color(0.2f, 0.85f, 1f, 1f);
                case 1: return new Color(1f, 0.55f, 0.1f, 1f);
                case 2: return new Color(0.95f, 0.3f, 0.75f, 1f);
                case 3: return new Color(0.95f, 0.85f, 0.2f, 1f);
                case 4: return new Color(0.3f, 0.85f, 0.35f, 1f);
                default: return new Color(0.85f, 0.85f, 0.9f, 1f);
            }
        }

        public static bool IsClusterVisible(
            in ClusterHeader self,
            ClusterHeader[] clusters,
            ClusterGroup[] groups,
            Matrix4x4 localToWorld,
            Vector3 cameraPos,
            float scale,
            float threshold,
            int hierarchyVersion,
            bool perspective)
        {
            ClusterMeshFrustum.TransformAabb(self, localToWorld, out Vector3 wc, out _);
            float selfDist = Vector3.Distance(cameraPos, wc);
            TryGetParent(
                self, clusters, groups, hierarchyVersion,
                out float parentLodError, out Vector3 parentLocal, out bool hasParent);
            float parentDist = 0f;
            if (hasParent)
                parentDist = Vector3.Distance(cameraPos, localToWorld.MultiplyPoint3x4(parentLocal));
            return IsVisible(
                self, parentLodError, hasParent, selfDist, parentDist,
                scale, threshold, hierarchyVersion, perspective);
        }

        public static bool IsVisible(
            in ClusterHeader self,
            in ClusterGroup parentGroup,
            bool hasParent,
            float selfDistance,
            float parentDistance,
            float scale,
            float threshold,
            int hierarchyVersion,
            bool perspective)
        {
            return IsVisible(
                self,
                parentGroup.lodError,
                hasParent,
                selfDistance,
                parentDistance,
                scale,
                threshold,
                hierarchyVersion,
                perspective);
        }
    }
}
