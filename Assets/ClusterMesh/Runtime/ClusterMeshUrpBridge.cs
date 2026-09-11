using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ClusterMesh
{
    public static class ClusterMeshUrpBridge
    {
        static readonly FieldInfo RendererIndexField = typeof(UniversalAdditionalCameraData)
            .GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo RendererDataListField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo DefaultRendererIndexField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);

        public static ScriptableRendererData RendererDataOverrideForTests;

        public static bool HasActiveFeature(ScriptableRendererData data)
        {
            if (data == null || data.rendererFeatures == null)
                return false;
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                ScriptableRendererFeature feature = data.rendererFeatures[i];
                if (feature is ClusterMeshUrpFeature && feature.isActive)
                    return true;
            }

            return false;
        }

        public static bool ShouldSkipLegacyFlush(Camera camera)
        {
            return ShouldSubmitUrp(camera);
        }

        public static bool ShouldSubmitUrp(Camera camera)
        {
            // Game cameras (Play, Edit Mode Game View, and Player) use the Feature
            // when Setup URP attached an active ClusterMeshUrpFeature. Scene / Preview /
            // Reflection stay on Lifetime Update + Graphics.Draw.
            if (camera == null || camera.cameraType != CameraType.Game)
                return false;
            return TryGetRendererData(camera, out ScriptableRendererData data) && HasActiveFeature(data);
        }

        public static bool TryGetDefaultRendererData(out ScriptableRendererData data)
        {
            data = null;
            UniversalRenderPipelineAsset urp = UniversalRenderPipeline.asset;
            if (urp == null)
                return false;
            ScriptableRendererData[] list = RendererDataList(urp);
            if (list == null || list.Length == 0)
                return false;
            int index = DefaultRendererIndex(urp);
            if (index < 0 || index >= list.Length)
                index = 0;
            data = list[index];
            return data != null;
        }

        public static bool TryGetRendererData(Camera camera, out ScriptableRendererData data)
        {
            if (RendererDataOverrideForTests != null)
            {
                data = RendererDataOverrideForTests;
                return true;
            }

            data = null;
            if (camera == null)
                return false;
            UniversalRenderPipelineAsset urp = UniversalRenderPipeline.asset;
            if (urp == null)
                return false;
            ScriptableRendererData[] list = RendererDataList(urp);
            if (list == null || list.Length == 0)
                return false;
            int index = DefaultRendererIndex(urp);
            var extra = camera.GetUniversalAdditionalCameraData();
            if (extra != null && RendererIndexField != null)
            {
                int cameraIndex = (int)RendererIndexField.GetValue(extra);
                if (cameraIndex >= 0)
                    index = cameraIndex;
            }

            if (index < 0 || index >= list.Length)
                return false;
            data = list[index];
            return data != null;
        }

        static ScriptableRendererData[] RendererDataList(UniversalRenderPipelineAsset urp)
        {
            return RendererDataListField != null
                ? RendererDataListField.GetValue(urp) as ScriptableRendererData[]
                : null;
        }

        static int DefaultRendererIndex(UniversalRenderPipelineAsset urp)
        {
            if (DefaultRendererIndexField == null)
                return 0;
            return (int)DefaultRendererIndexField.GetValue(urp);
        }
    }
}
