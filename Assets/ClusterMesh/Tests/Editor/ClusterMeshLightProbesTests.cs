using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEditor;
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
            if (ClusterMeshLightProbes.HasTetrahedralProbes())
                Assert.Ignore("Open scene has light probes; ambient fallback is not observable.");
            SphericalHarmonicsL2 evaluated = ClusterMeshLightProbes.Evaluate(new Vector3(3f, 7f, -2f));
            SphericalHarmonicsL2 ambient = RenderSettings.ambientProbe;
            AssertShEqual(evaluated, ambient);
        }

        [Test]
        public void Evaluate_SkyOnly_EqualsAmbientProbe()
        {
            SphericalHarmonicsL2 evaluated = ClusterMeshLightProbes.Evaluate(
                Vector3.zero, false, true);
            AssertShEqual(evaluated, RenderSettings.ambientProbe);
        }

        [Test]
        public void Evaluate_BothOff_IsZero()
        {
            SphericalHarmonicsL2 evaluated = ClusterMeshLightProbes.Evaluate(
                Vector3.zero, false, false);
            AssertShEqual(evaluated, default);
        }

        [Test]
        public void Evaluate_ProbesOnSkyOff_WithoutGroup_IsZero()
        {
            if (ClusterMeshLightProbes.HasTetrahedralProbes())
                Assert.Ignore("Open scene has light probes; sky-off without group is not observable.");
            SphericalHarmonicsL2 evaluated = ClusterMeshLightProbes.Evaluate(
                Vector3.zero, true, false);
            AssertShEqual(evaluated, default);
        }

        [Test]
        public void Pack_FogFlag_WritesShC_W()
        {
            var sh = new SphericalHarmonicsL2();
            ClusterMeshLightProbes.Pack(sh, true, out ClusterMeshObjectSH on);
            ClusterMeshLightProbes.Pack(sh, false, out ClusterMeshObjectSH off);
            Assert.That(on.shC.w, Is.EqualTo(1f));
            Assert.That(off.shC.w, Is.EqualTo(0f));
        }

        [Test]
        public void PackForObject_UsesPerObjectFlags()
        {
            ClusterMeshLightProbes.PackForObject(
                Vector3.zero,
                new[] { false },
                new[] { false },
                new[] { false },
                0,
                out ClusterMeshObjectSH packed);
            Assert.That(packed.shC.w, Is.EqualTo(0f));
            Assert.That(packed.shAr, Is.EqualTo(Vector4.zero));
            Assert.That(packed.shAg, Is.EqualTo(Vector4.zero));
            Assert.That(packed.shAb, Is.EqualTo(Vector4.zero));
        }

        [Test]
        public void ExistingRenderer_MissingLightingVersion_DefaultsOn()
        {
            var mesh = new GameObject("CMLitUpgrade");
            var skinned = new GameObject("CMSkinnedLitUpgrade");
            var renderer = mesh.AddComponent<ClusterMeshRenderer>();
            var skinnedRenderer = skinned.AddComponent<ClusterSkinnedMeshRenderer>();
            ResetLightingVersion(renderer);
            ResetLightingVersion(skinnedRenderer);
            renderer.OnAfterDeserialize();
            skinnedRenderer.OnAfterDeserialize();
            Assert.That(renderer.enableLightProbes, Is.True);
            Assert.That(renderer.enableAmbientSky, Is.True);
            Assert.That(renderer.enableFog, Is.True);
            Assert.That(skinnedRenderer.enableLightProbes, Is.True);
            Assert.That(skinnedRenderer.enableAmbientSky, Is.True);
            Assert.That(skinnedRenderer.enableFog, Is.True);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(skinned);
        }

        [Test]
        public void Renderer_LightingToggles_DefaultOn()
        {
            var mesh = new GameObject("CMLitFlags");
            var skinned = new GameObject("CMSkinnedLitFlags");
            var renderer = mesh.AddComponent<ClusterMeshRenderer>();
            var skinnedRenderer = skinned.AddComponent<ClusterSkinnedMeshRenderer>();
            Assert.That(renderer.enableLightProbes, Is.True);
            Assert.That(renderer.enableAmbientSky, Is.True);
            Assert.That(renderer.enableFog, Is.True);
            Assert.That(skinnedRenderer.enableLightProbes, Is.True);
            Assert.That(skinnedRenderer.enableAmbientSky, Is.True);
            Assert.That(skinnedRenderer.enableFog, Is.True);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(skinned);
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
            Assert.That(staticHlsl, Does.Contain("shC.w"));
            Assert.That(staticHlsl, Does.Not.Contain("fogCoord = 0"));
            Assert.That(skinnedHlsl, Does.Contain("_ObjectSH"));
            Assert.That(skinnedHlsl, Does.Contain("SampleSH9"));
            Assert.That(skinnedHlsl, Does.Not.Contain("d.bakedGI=SampleSH("));
            Assert.That(skinnedHlsl, Does.Contain("ComputeFogFactor"));
            Assert.That(skinnedHlsl, Does.Contain("shC.w"));
            Assert.That(staticShader, Does.Contain("multi_compile_fog"));
            Assert.That(skinnedShader, Does.Contain("multi_compile_fog"));
            int staticGbuffer = staticShader.IndexOf("Name \"GBuffer\"");
            int skinnedGbuffer = skinnedShader.IndexOf("Name \"GBuffer\"");
            Assert.That(staticShader.IndexOf("multi_compile_fog", staticGbuffer), Is.EqualTo(-1));
            Assert.That(skinnedShader.IndexOf("multi_compile_fog", skinnedGbuffer), Is.EqualTo(-1));
        }

        static void ResetLightingVersion(Object target)
        {
            var so = new SerializedObject(target);
            so.FindProperty("lightingToggleVersion").intValue = 0;
            so.FindProperty("enableLightProbes").boolValue = false;
            so.FindProperty("enableAmbientSky").boolValue = false;
            so.FindProperty("enableFog").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssertShEqual(SphericalHarmonicsL2 a, SphericalHarmonicsL2 b)
        {
            for (int rgb = 0; rgb < 3; rgb++)
            {
                for (int i = 0; i < 9; i++)
                    Assert.That(a[rgb, i], Is.EqualTo(b[rgb, i]).Within(1e-4f));
            }
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
