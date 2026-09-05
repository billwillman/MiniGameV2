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
            for (int i = 0; i < clusters.Length; i++)
            {
                ClusterHeader h = clusters[i];
                ClusterMeshFrustum.TransformAabb(h, m, out Vector3 wc, out _);
                float selfDist = Vector3.Distance(cam.transform.position, wc);
                bool hasParent = h.parentIndex >= 0 && h.parentIndex < clusters.Length;
                ClusterHeader parent = hasParent ? clusters[h.parentIndex] : default;
                float parentDist = 0f;
                if (hasParent)
                {
                    ClusterMeshFrustum.TransformAabb(parent, m, out Vector3 pwc, out _);
                    parentDist = Vector3.Distance(cam.transform.position, pwc);
                }

                if (!ClusterMeshLod.IsVisible(
                        h, parent, hasParent, selfDist, parentDist, scale,
                        renderer.lodErrorThreshold, renderer.asset.hierarchyVersion, perspective))
                    continue;

                int lod = ClusterMeshLod.Level(h.flags);
                Handles.color = lod == 0
                    ? new Color(0.2f, 0.85f, 1f, 1f)
                    : new Color(1f, 0.55f, 0.1f, 1f);
                Handles.Label((Vector3)h.aabbCenter, lod == 0 ? "L0" : "L1");
            }

            Handles.matrix = Matrix4x4.identity;
        }
    }
}
