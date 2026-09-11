using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterSkinnedMeshRendererGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
        static void DrawAnimatedClusterDebug(ClusterSkinnedMeshRenderer renderer, GizmoType type)
        {
            if (renderer == null || renderer.asset == null || renderer.asset.geometry == null)
                return;
            bool drawAabb = renderer.showClusterAabb && (type & GizmoType.Selected) != 0;
            bool drawLod = renderer.showLodLevels;
            if (!drawAabb && !drawLod)
                return;

            ClusterMeshAsset geometry = renderer.asset.geometry;
            ClusterHeader[] clusters = geometry.clusters;
            if (clusters == null || clusters.Length == 0 || !TryGetFrameStart(renderer, clusters.Length, out int frameStart))
                return;

            Camera camera = renderer.targetCamera != null ? renderer.targetCamera : Camera.main;
            float projectionScale = camera != null ? ClusterMeshLod.ProjectionScale(camera) : 0f;
            bool perspective = camera != null && !camera.orthographic;
            Matrix4x4 localToWorld = renderer.transform.localToWorldMatrix;
            Handles.matrix = localToWorld;

            if (!renderer.asset.TryGetCullFrames(out ClusterSkinnedCullFrame[] cullFrames, out _) ||
                cullFrames == null)
                return;
            for (int i = 0; i < clusters.Length; i++)
            {
                int frameIndex = frameStart + i;
                if (frameIndex < 0 || frameIndex >= cullFrames.Length)
                    break;
                ClusterSkinnedCullFrame frame = cullFrames[frameIndex];
                Vector3 center = frame.aabbCenter;
                Vector3 size = (Vector3)frame.aabbExtents * 2f;

                if (drawAabb)
                {
                    Handles.color = renderer.showClusterColors
                        ? ClusterMeshDebugColors.Rgb((uint)i)
                        : new Color(0.2f, 1f, 0.35f, 1f);
                    Handles.DrawWireCube(center, size);
                }

                if (!drawLod || camera == null || !IsLodVisible(
                        i, center, clusters, geometry.groups, geometry.hierarchyVersion,
                        localToWorld, camera.transform.position, projectionScale,
                        renderer.lodErrorThreshold, perspective))
                    continue;

                int level = ClusterMeshLod.Level(clusters[i].flags);
                Handles.color = ClusterMeshLod.LevelColor(level);
                Handles.DrawWireCube(center, size);
                Handles.Label(center, "L" + level);
            }

            Handles.matrix = Matrix4x4.identity;
        }

        static bool TryGetFrameStart(ClusterSkinnedMeshRenderer renderer, int clusterCount, out int frameStart)
        {
            frameStart = 0;
            ClusterSkinnedMeshAsset asset = renderer.asset;
            if (asset.clips == null || asset.clips.Length == 0 ||
                !asset.TryGetCullFrames(out ClusterSkinnedCullFrame[] cullFrames, out _) ||
                cullFrames == null)
                return false;
            int clipIndex = Mathf.Clamp(renderer.clipIndex, 0, asset.clips.Length - 1);
            ClusterSkinnedClip clip = asset.clips[clipIndex];
            if (clip == null)
                return false;
            float clock = Application.isPlaying ? Time.time : (float)EditorApplication.timeSinceStartup;
            float normalized = renderer.CurrentNormalizedTime(clock);
            int segmentCount = Mathf.Max(1, clip.segmentCount);
            int segment = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(normalized, 1f) * segmentCount), 0, segmentCount - 1);
            frameStart = clip.cullFrameOffset + segment * clusterCount;
            return frameStart >= 0 && frameStart + clusterCount <= cullFrames.Length;
        }

        static bool IsLodVisible(
            int clusterIndex,
            Vector3 localCenter,
            ClusterHeader[] clusters,
            ClusterGroup[] groups,
            int hierarchyVersion,
            Matrix4x4 localToWorld,
            Vector3 cameraPosition,
            float projectionScale,
            float threshold,
            bool perspective)
        {
            ClusterHeader cluster = clusters[clusterIndex];
            if (hierarchyVersion < 1)
                return true;
            if (threshold <= 0f)
                return ClusterMeshLod.Level(cluster.flags) == 0;
            if (hierarchyVersion < ClusterMeshLod.HierarchyVersionDag)
                return ClusterMeshLod.Level(cluster.flags) == 0;
            if (!ClusterMeshLod.TryGetOwningGroup(clusterIndex, groups, out int ownGroup))
                return true;

            Vector3 worldCenter = localToWorld.MultiplyPoint3x4(localCenter);
            float distance = Vector3.Distance(cameraPosition, worldCenter);
            float objectScale = ClusterMeshLod.MaxAxisScale(localToWorld);
            ClusterGroup group = groups[ownGroup];
            float ownError = ClusterMeshLod.ProjectError(
                group.lodError * objectScale, distance, projectionScale, perspective);
            if (ownError >= threshold)
                return false;
            if (group.parentGroupIndex < 0 || group.parentGroupIndex >= groups.Length)
                return true;
            float parentError = ClusterMeshLod.ProjectError(
                groups[group.parentGroupIndex].lodError * objectScale, distance, projectionScale, perspective);
            return parentError >= threshold;
        }
    }
}
