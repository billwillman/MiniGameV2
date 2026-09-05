using System.Collections.Generic;
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

        public static float MaxAxisScale(Matrix4x4 localToWorld)
        {
            float sx = new Vector3(localToWorld.m00, localToWorld.m10, localToWorld.m20).magnitude;
            float sy = new Vector3(localToWorld.m01, localToWorld.m11, localToWorld.m21).magnitude;
            float sz = new Vector3(localToWorld.m02, localToWorld.m12, localToWorld.m22).magnitude;
            return Mathf.Max(sx, Mathf.Max(sy, Mathf.Max(sz, 1e-6f)));
        }

        public static bool UsePerClusterCone(int hierarchyVersion, float threshold, bool hasOwningGroup)
        {
            return !(hierarchyVersion >= HierarchyVersionDag && threshold > 0f && hasOwningGroup);
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

        public static bool TryGetOwningGroup(int clusterIndex, IList<ClusterGroup> groups, out int groupIndex)
        {
            groupIndex = NoParent;
            if (groups == null || clusterIndex < 0)
                return false;
            for (int i = 0; i < groups.Count; i++)
            {
                ClusterGroup g = groups[i];
                if (clusterIndex >= g.clusterStart && clusterIndex < g.clusterStart + g.clusterCount)
                {
                    groupIndex = i;
                    return true;
                }
            }

            return false;
        }

        public static bool IsClusterVisible(
            int clusterIndex,
            ClusterHeader[] clusters,
            ClusterGroup[] groups,
            Matrix4x4 localToWorld,
            Vector3 cameraPos,
            float scale,
            float threshold,
            int hierarchyVersion,
            bool perspective)
        {
            if (clusters == null || clusterIndex < 0 || clusterIndex >= clusters.Length)
                return false;
            ClusterHeader self = clusters[clusterIndex];
            float objScale = MaxAxisScale(localToWorld);
            if (hierarchyVersion >= HierarchyVersionDag &&
                TryGetOwningGroup(clusterIndex, groups, out int ownGroup))
            {
                ClusterGroup g = groups[ownGroup];
                Vector3 ownWorld = localToWorld.MultiplyPoint3x4(g.aabbCenter);
                float selfDist = Vector3.Distance(cameraPos, ownWorld);
                bool hasParent = g.parentGroupIndex >= 0 && groups != null && g.parentGroupIndex < groups.Length;
                float parentLodError = 0f;
                float parentDist = 0f;
                if (hasParent)
                {
                    ClusterGroup p = groups[g.parentGroupIndex];
                    parentLodError = p.lodError * objScale;
                    parentDist = Vector3.Distance(cameraPos, localToWorld.MultiplyPoint3x4(p.aabbCenter));
                }

                ClusterHeader own = self;
                own.lodError = g.lodError * objScale;
                return IsVisible(
                    own, parentLodError, hasParent, selfDist, parentDist,
                    scale, threshold, hierarchyVersion, perspective);
            }

            ClusterMeshFrustum.TransformAabb(self, localToWorld, out Vector3 wc, out _);
            float leafDist = Vector3.Distance(cameraPos, wc);
            TryGetParent(
                self, clusters, groups, hierarchyVersion,
                out float parentErr, out Vector3 parentLocal, out bool hasP);
            float pDist = 0f;
            if (hasP)
                pDist = Vector3.Distance(cameraPos, localToWorld.MultiplyPoint3x4(parentLocal));
            ClusterHeader scaled = self;
            scaled.lodError = self.lodError * objScale;
            return IsVisible(
                scaled, parentErr * objScale, hasP, leafDist, pDist,
                scale, threshold, hierarchyVersion, perspective);
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
            int index = -1;
            if (clusters != null)
            {
                for (int i = 0; i < clusters.Length; i++)
                {
                    if (clusters[i].vertexOffset == self.vertexOffset &&
                        clusters[i].indexOffset == self.indexOffset &&
                        clusters[i].flags == self.flags)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index >= 0)
                return IsClusterVisible(
                    index, clusters, groups, localToWorld, cameraPos, scale, threshold, hierarchyVersion, perspective);

            ClusterMeshFrustum.TransformAabb(self, localToWorld, out Vector3 wc, out _);
            float selfDist = Vector3.Distance(cameraPos, wc);
            TryGetParent(
                self, clusters, groups, hierarchyVersion,
                out float parentLodError, out Vector3 parentLocal, out bool hasParent);
            float parentDist = 0f;
            if (hasParent)
                parentDist = Vector3.Distance(cameraPos, localToWorld.MultiplyPoint3x4(parentLocal));
            float objScale = MaxAxisScale(localToWorld);
            ClusterHeader scaled = self;
            scaled.lodError = self.lodError * objScale;
            return IsVisible(
                scaled, parentLodError * objScale, hasParent, selfDist, parentDist,
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
