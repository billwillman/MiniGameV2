using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshLodQuality
    {
        public enum Preset
        {
            Custom = 0,
            UltraFine = 1,
            Fine = 2,
            Medium = 3,
            Coarse = 4
        }

        public const float UltraFine = 0.5f;
        public const float Fine = 1f;
        public const float Medium = 2f;
        public const float Coarse = 4f;

        public static readonly string[] Labels = { "极度精细", "精细", "一般", "粗糙" };

        public static float Value(Preset preset)
        {
            switch (preset)
            {
                case Preset.UltraFine:
                    return UltraFine;
                case Preset.Fine:
                    return Fine;
                case Preset.Medium:
                    return Medium;
                case Preset.Coarse:
                    return Coarse;
                default:
                    return 0f;
            }
        }

        public static Preset FromValue(float threshold)
        {
            if (Mathf.Approximately(threshold, UltraFine))
                return Preset.UltraFine;
            if (Mathf.Approximately(threshold, Fine))
                return Preset.Fine;
            if (Mathf.Approximately(threshold, Medium))
                return Preset.Medium;
            if (Mathf.Approximately(threshold, Coarse))
                return Preset.Coarse;
            return Preset.Custom;
        }

        public static int PopupIndex(float threshold)
        {
            Preset preset = FromValue(threshold);
            return preset == Preset.Custom ? -1 : (int)preset - 1;
        }

        public static float ValueFromPopupIndex(int index)
        {
            if (index == 0)
                return UltraFine;
            if (index == 1)
                return Fine;
            if (index == 2)
                return Medium;
            if (index == 3)
                return Coarse;
            return 0f;
        }
    }
}
