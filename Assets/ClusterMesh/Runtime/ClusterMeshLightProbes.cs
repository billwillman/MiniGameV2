using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    public static class ClusterMeshLightProbes
    {
        public static SphericalHarmonicsL2 Evaluate(Vector3 worldPosition)
        {
            LightProbes.GetInterpolatedProbe(worldPosition, null, out SphericalHarmonicsL2 sh);
            return sh;
        }

        public static void Pack(SphericalHarmonicsL2 sh, out ClusterMeshObjectSH packed)
        {
            packed = new ClusterMeshObjectSH
            {
                shAr = new Vector4(sh[0, 3], sh[0, 1], sh[0, 2], sh[0, 0] - sh[0, 6]),
                shAg = new Vector4(sh[1, 3], sh[1, 1], sh[1, 2], sh[1, 0] - sh[1, 6]),
                shAb = new Vector4(sh[2, 3], sh[2, 1], sh[2, 2], sh[2, 0] - sh[2, 6]),
                shBr = new Vector4(sh[0, 4], sh[0, 5], sh[0, 6] * 3f, sh[0, 7]),
                shBg = new Vector4(sh[1, 4], sh[1, 5], sh[1, 6] * 3f, sh[1, 7]),
                shBb = new Vector4(sh[2, 4], sh[2, 5], sh[2, 6] * 3f, sh[2, 7]),
                shC = new Vector4(sh[0, 8], sh[1, 8], sh[2, 8], 1f)
            };
        }
    }
}
