using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    public static class ClusterMeshLightProbes
    {
        public static bool HasTetrahedralProbes()
        {
            return LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.count > 0;
        }

        public static bool FlagOrDefault(IList<bool> flags, int index, bool fallback = true)
        {
            return flags == null || index >= flags.Count || flags[index];
        }

        public static SphericalHarmonicsL2 Evaluate(Vector3 worldPosition)
        {
            return Evaluate(worldPosition, true, true);
        }

        public static SphericalHarmonicsL2 Evaluate(
            Vector3 worldPosition,
            bool enableLightProbes,
            bool enableAmbientSky)
        {
            if (enableLightProbes && HasTetrahedralProbes())
            {
                LightProbes.GetInterpolatedProbe(worldPosition, null, out SphericalHarmonicsL2 probes);
                return probes;
            }

            if (enableAmbientSky)
                return RenderSettings.ambientProbe;
            return default;
        }

        public static void Pack(SphericalHarmonicsL2 sh, out ClusterMeshObjectSH packed)
        {
            Pack(sh, true, out packed);
        }

        public static void Pack(SphericalHarmonicsL2 sh, bool enableFog, out ClusterMeshObjectSH packed)
        {
            packed = new ClusterMeshObjectSH
            {
                shAr = new Vector4(sh[0, 3], sh[0, 1], sh[0, 2], sh[0, 0] - sh[0, 6]),
                shAg = new Vector4(sh[1, 3], sh[1, 1], sh[1, 2], sh[1, 0] - sh[1, 6]),
                shAb = new Vector4(sh[2, 3], sh[2, 1], sh[2, 2], sh[2, 0] - sh[2, 6]),
                shBr = new Vector4(sh[0, 4], sh[0, 5], sh[0, 6] * 3f, sh[0, 7]),
                shBg = new Vector4(sh[1, 4], sh[1, 5], sh[1, 6] * 3f, sh[1, 7]),
                shBb = new Vector4(sh[2, 4], sh[2, 5], sh[2, 6] * 3f, sh[2, 7]),
                shC = new Vector4(sh[0, 8], sh[1, 8], sh[2, 8], enableFog ? 1f : 0f)
            };
        }

        public static void PackForObject(
            Vector3 worldPosition,
            IList<bool> enableLightProbes,
            IList<bool> enableAmbientSky,
            IList<bool> enableFog,
            int index,
            out ClusterMeshObjectSH packed)
        {
            Pack(
                Evaluate(
                    worldPosition,
                    FlagOrDefault(enableLightProbes, index),
                    FlagOrDefault(enableAmbientSky, index)),
                FlagOrDefault(enableFog, index),
                out packed);
        }
    }
}
