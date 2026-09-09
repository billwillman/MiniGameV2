using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ClusterMesh
{
    public static class ClusterMeshUrpFeatureMenu
    {
        const string FeatureName = "ClusterMesh URP";

        [MenuItem("Tools/ClusterMesh/Enable URP Feature")]
        public static void Enable()
        {
            if (!TryGetDefaultRenderer(out UniversalRendererData data))
            {
                Debug.LogError("ClusterMesh: 当前没有 URP Asset 或 Default Renderer。");
                return;
            }

            EnableOn(data);
            EditorUtility.SetDirty(data);
            Debug.Log("ClusterMesh: 已在 Default Renderer 上启用 URP Feature。");
        }

        [MenuItem("Tools/ClusterMesh/Enable URP Feature", true)]
        public static bool EnableValidate()
        {
            return ClusterMeshUrpBridge.TryGetDefaultRendererData(out _);
        }

        [MenuItem("Tools/ClusterMesh/Disable URP Feature")]
        public static void Disable()
        {
            if (!TryGetDefaultRenderer(out UniversalRendererData data))
            {
                Debug.LogError("ClusterMesh: 当前没有 URP Asset 或 Default Renderer。");
                return;
            }

            DisableOn(data);
            EditorUtility.SetDirty(data);
            Debug.Log("ClusterMesh: 已从 Default Renderer 卸下 URP Feature。");
        }

        [MenuItem("Tools/ClusterMesh/Disable URP Feature", true)]
        public static bool DisableValidate()
        {
            return TryGetDefaultRenderer(out UniversalRendererData data) && HasFeature(data);
        }

        public static bool TryGetDefaultRenderer(out UniversalRendererData data)
        {
            data = null;
            if (!ClusterMeshUrpBridge.TryGetDefaultRendererData(out ScriptableRendererData raw))
                return false;
            data = raw as UniversalRendererData;
            return data != null;
        }

        public static bool HasFeature(ScriptableRendererData data)
        {
            return FindFeature(data) != null;
        }

        public static void EnableOn(ScriptableRendererData data)
        {
            if (data == null)
                return;
            ClusterMeshUrpFeature existing = FindFeature(data);
            if (existing != null)
            {
                existing.SetActive(true);
                data.SetDirty();
                return;
            }

            var feature = ScriptableObject.CreateInstance<ClusterMeshUrpFeature>();
            feature.name = FeatureName;
            feature.SetActive(true);
            string path = AssetDatabase.GetAssetPath(data);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
            data.SetDirty();
        }

        public static void DisableOn(ScriptableRendererData data)
        {
            if (data == null || data.rendererFeatures == null)
                return;
            for (int i = data.rendererFeatures.Count - 1; i >= 0; i--)
            {
                if (!(data.rendererFeatures[i] is ClusterMeshUrpFeature feature))
                    continue;
                data.rendererFeatures.RemoveAt(i);
                string path = AssetDatabase.GetAssetPath(feature);
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.RemoveObjectFromAsset(feature);
                Object.DestroyImmediate(feature, true);
            }

            data.SetDirty();
        }

        static ClusterMeshUrpFeature FindFeature(ScriptableRendererData data)
        {
            if (data == null || data.rendererFeatures == null)
                return null;
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                if (data.rendererFeatures[i] is ClusterMeshUrpFeature feature)
                    return feature;
            }

            return null;
        }
    }
}
