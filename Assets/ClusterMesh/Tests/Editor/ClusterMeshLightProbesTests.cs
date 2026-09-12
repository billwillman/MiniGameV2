using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshLightProbesTests
    {
        [Test]
        public void ObjectSH_Stride_Is112()
        {
            Assert.That(Marshal.SizeOf<ClusterMeshObjectSH>(), Is.EqualTo(112));
            Assert.That(ClusterMeshLimits.ObjectSHStride, Is.EqualTo(112));
        }

        [Test]
        public void Pack_L0Only_PositiveYMatchesHandEval()
        {
            var sh = new SphericalHarmonicsL2();
            sh[0, 0] = 0.4f;
            sh[1, 0] = 0.5f;
            sh[2, 0] = 0.6f;
            ClusterMeshLightProbes.Pack(sh, out ClusterMeshObjectSH packed);
            Vector3 plusY = SampleSH9(packed, Vector3.up);
            Vector3 plusX = SampleSH9(packed, Vector3.right);
            Assert.That(plusY.x, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(plusY.y, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(plusY.z, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(plusX.x, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(plusX.y, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(plusX.z, Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void Evaluate_NoProbeGroup_EqualsAmbientProbe()
        {
            if (LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.count > 0)
                Assert.Ignore("Open scene has light probes; ambient fallback is not observable.");
            SphericalHarmonicsL2 evaluated = ClusterMeshLightProbes.Evaluate(new Vector3(3f, 7f, -2f));
            SphericalHarmonicsL2 ambient = RenderSettings.ambientProbe;
            for (int rgb = 0; rgb < 3; rgb++)
            {
                for (int i = 0; i < 9; i++)
                    Assert.That(evaluated[rgb, i], Is.EqualTo(ambient[rgb, i]).Within(1e-4f));
            }
        }

        [Test]
        public void LitShaders_SampleObjectSH_AndForwardFog()
        {
            string staticHlsl = System.IO.File.ReadAllText("Assets/ClusterMesh/Shaders/ClusterMeshLit.hlsl");
            string skinnedHlsl = System.IO.File.ReadAllText(
                "Assets/ClusterMesh/Shaders/ClusterSkinnedMeshLit.hlsl");
            string staticShader = System.IO.File.ReadAllText("Assets/ClusterMesh/Shaders/ClusterMeshLit.shader");
            string skinnedShader = System.IO.File.ReadAllText(
                "Assets/ClusterMesh/Shaders/ClusterSkinnedMeshLit.shader");
            Assert.That(staticHlsl, Does.Contain("_ObjectSH"));
            Assert.That(staticHlsl, Does.Contain("SampleSH9"));
            Assert.That(staticHlsl, Does.Not.Contain("bakedGI = SampleSH("));
            Assert.That(staticHlsl, Does.Contain("ComputeFogFactor"));
            Assert.That(staticHlsl, Does.Not.Contain("fogCoord = 0"));
            Assert.That(skinnedHlsl, Does.Contain("_ObjectSH"));
            Assert.That(skinnedHlsl, Does.Contain("SampleSH9"));
            Assert.That(skinnedHlsl, Does.Not.Contain("d.bakedGI=SampleSH("));
            Assert.That(skinnedHlsl, Does.Contain("ComputeFogFactor"));
            Assert.That(staticShader, Does.Contain("multi_compile_fog"));
            Assert.That(skinnedShader, Does.Contain("multi_compile_fog"));
            int staticGbuffer = staticShader.IndexOf("Name \"GBuffer\"");
            int skinnedGbuffer = skinnedShader.IndexOf("Name \"GBuffer\"");
            Assert.That(staticShader.IndexOf("multi_compile_fog", staticGbuffer), Is.EqualTo(-1));
            Assert.That(skinnedShader.IndexOf("multi_compile_fog", skinnedGbuffer), Is.EqualTo(-1));
        }

        static Vector3 SampleSH9(ClusterMeshObjectSH sh, Vector3 n)
        {
            var n1 = new Vector4(n.x, n.y, n.z, 1f);
            var x1 = new Vector3(
                Vector4.Dot(sh.shAr, n1), Vector4.Dot(sh.shAg, n1), Vector4.Dot(sh.shAb, n1));
            var vB = new Vector4(n.x * n.y, n.y * n.z, n.z * n.z, n.z * n.x);
            var x2 = new Vector3(
                Vector4.Dot(sh.shBr, vB), Vector4.Dot(sh.shBg, vB), Vector4.Dot(sh.shBb, vB));
            float vC = n.x * n.x - n.y * n.y;
            return x1 + x2 + new Vector3(sh.shC.x, sh.shC.y, sh.shC.z) * vC;
        }
    }
}
