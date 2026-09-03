using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshMaterialUtil
    {
        public static Material CreateRuntimeMaterial(Material source, Shader lit)
        {
            if (lit == null)
                throw new System.InvalidOperationException("ClusterMesh Lit shader is missing.");

            var material = new Material(lit) { name = source != null ? source.name + " (ClusterMesh)" : "ClusterMeshLit" };
            material.enableInstancing = true;
            if (source == null)
                return material;

            CopyTex(source, material, "_BaseMap", "_MainTex");
            CopyColor(source, material, "_BaseColor", "_Color");
            CopyTex(source, material, "_BumpMap");
            CopyFloat(source, material, "_BumpScale");
            CopyFloat(source, material, "_Metallic");
            CopyFloat(source, material, "_Smoothness");
            CopyFloat(source, material, "_Cutoff");
            if (source.HasProperty("_Color") && material.HasProperty("_Color"))
                material.color = source.color;
            return material;
        }

        static void CopyTex(Material source, Material dest, string name, string fallback = null)
        {
            if (dest.HasProperty(name) && source.HasProperty(name))
                dest.SetTexture(name, source.GetTexture(name));
            else if (fallback != null && dest.HasProperty(name) && source.HasProperty(fallback))
                dest.SetTexture(name, source.GetTexture(fallback));
        }

        static void CopyColor(Material source, Material dest, string name, string fallback)
        {
            if (dest.HasProperty(name) && source.HasProperty(name))
                dest.SetColor(name, source.GetColor(name));
            else if (dest.HasProperty(name) && source.HasProperty(fallback))
                dest.SetColor(name, source.GetColor(fallback));
        }

        static void CopyFloat(Material source, Material dest, string name)
        {
            if (dest.HasProperty(name) && source.HasProperty(name))
                dest.SetFloat(name, source.GetFloat(name));
        }
    }
}
