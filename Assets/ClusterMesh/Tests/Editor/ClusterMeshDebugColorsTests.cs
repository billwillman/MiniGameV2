using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshDebugColorsTests
    {
        [Test]
        public void Rgb_DifferentIds_AreDifferent()
        {
            Color a = ClusterMeshDebugColors.Rgb(0);
            Color b = ClusterMeshDebugColors.Rgb(1);
            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void Rgb_SameId_IsStable()
        {
            Assert.That(ClusterMeshDebugColors.Rgb(7), Is.EqualTo(ClusterMeshDebugColors.Rgb(7)));
        }

        [Test]
        public void Rgb_SequentialIds_HaveUniqueHues()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (uint i = 0; i < 32; i++)
            {
                Color c = ClusterMeshDebugColors.Rgb(i);
                string key = c.r.ToString("F3") + "," + c.g.ToString("F3") + "," + c.b.ToString("F3");
                Assert.That(seen.Add(key), Is.True, "duplicate color for cluster " + i);
            }
        }

        [Test]
        public void DrawContext_ClusterColorDefaultsOff()
        {
            var ctx = new ClusterMeshDrawContext(null, null, null);
            Assert.That(ctx.EnableClusterColor, Is.False);
            ctx.Dispose();
        }
    }
}
