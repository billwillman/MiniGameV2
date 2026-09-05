using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshRendererLodGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Active)]
        static void DrawLodLabels(ClusterMeshRenderer renderer, GizmoType type)
        {
            if (renderer == null || !renderer.showLodLevels || renderer.asset == null || renderer.asset.clusters == null)
                return;

            Camera cam = renderer.targetCamera;
            if (cam == null && SceneView.lastActiveSceneView != null)
                cam = SceneView.lastActiveSceneView.camera;
            if (cam == null)
                cam = Camera.main;
            if (cam == null)
                return;

            float scale = ClusterMeshLod.ProjectionScale(cam);
            bool perspective = !cam.orthographic;
            Matrix4x4 m = renderer.transform.localToWorldMatrix;
            Handles.matrix = m;
            ClusterHeader[] clusters = renderer.asset.clusters;
            ClusterGroup[] groups = renderer.asset.groups;
            for (int i = 0; i < clusters.Length; i++)
            {
                ClusterHeader h = clusters[i];
                if (!ClusterMeshLod.IsClusterVisible(
                        h, clusters, groups, m, cam.transform.position, scale,
                        renderer.lodErrorThreshold, renderer.asset.hierarchyVersion, perspective))
                    continue;

                int lod = ClusterMeshLod.Level(h.flags);
                Handles.color = ClusterMeshLod.LevelColor(lod);
                Handles.Label((Vector3)h.aabbCenter, "L" + lod);
            }

            Handles.matrix = Matrix4x4.identity;
        }
    }
}
