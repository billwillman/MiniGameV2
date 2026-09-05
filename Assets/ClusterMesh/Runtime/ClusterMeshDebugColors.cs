using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshDebugColors
    {
        public const float Saturation = 0.72f;
        public const float Value = 0.95f;
        public const float GoldenRatio = 0.6180339887f;

        public static Color Rgb(uint clusterId)
        {
            float hue = Mathf.Repeat((clusterId + 1f) * GoldenRatio, 1f);
            return Color.HSVToRGB(hue, Saturation, Value);
        }
    }
}
