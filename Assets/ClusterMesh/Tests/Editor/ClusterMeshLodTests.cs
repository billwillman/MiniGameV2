using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshLodTests
    {
        [Test]
        public void Header_StrideIs96()
        {
            Assert.That(Marshal.SizeOf<ClusterHeader>(), Is.EqualTo(96));
            Assert.That(ClusterMeshLimits.ClusterHeaderStride, Is.EqualTo(96));
        }

        [Test]
        public void Group_StrideIs48()
        {
            Assert.That(Marshal.SizeOf<ClusterGroup>(), Is.EqualTo(48));
            Assert.That(ClusterMeshLimits.ClusterGroupStride, Is.EqualTo(48));
        }

        [Test]
        public void ThresholdZero_HidesParents_ShowsLeaves()
        {
            var leaf = new ClusterHeader { parentIndex = 0, lodError = 0f, flags = 0 };
            var parent = new ClusterHeader
            {
                parentIndex = ClusterMeshLod.NoParent,
                lodError = 0.5f,
                flags = ClusterMeshLod.FlagParent
            };
            Assert.That(ClusterMeshLod.IsVisible(leaf, parent, true, 1f, 2f, 100f, 0f, 1, true), Is.True);
            Assert.That(ClusterMeshLod.IsVisible(parent, parent, false, 2f, 0f, 100f, 0f, 1, true), Is.False);
        }

        [Test]
        public void LargeThreshold_ShowsParent_HidesChild()
        {
            var parent = new ClusterHeader
            {
                parentIndex = ClusterMeshLod.NoParent,
                lodError = 0.5f,
                flags = ClusterMeshLod.FlagParent
            };
            var leaf = new ClusterHeader { parentIndex = 0, lodError = 0f, flags = 0 };
            const float scale = 100f;
            const float t = 51f;
            Assert.That(ClusterMeshLod.IsVisible(leaf, parent, true, 1f, 1f, scale, t, 1, true), Is.False);
            Assert.That(ClusterMeshLod.IsVisible(parent, parent, false, 1f, 0f, scale, t, 1, true), Is.True);
        }

        [Test]
        public void VersionZero_AlwaysVisibleRegardlessOfFlags()
        {
            var parent = new ClusterHeader
            {
                flags = ClusterMeshLod.FlagParent,
                lodError = 0.5f,
                parentIndex = ClusterMeshLod.NoParent
            };
            Assert.That(ClusterMeshLod.IsVisible(parent, parent, false, 1f, 0f, 100f, 50f, 0, true), Is.True);
        }

        [Test]
        public void Level_ParentIsOne_LeafIsZero()
        {
            Assert.That(ClusterMeshLod.Level(0), Is.EqualTo(0));
            Assert.That(ClusterMeshLod.Level(ClusterMeshLod.FlagParent), Is.EqualTo(1));
            Assert.That(ClusterMeshLod.Level(ClusterMeshLod.PackFlags(0)), Is.EqualTo(0));
            Assert.That(ClusterMeshLod.Level(ClusterMeshLod.PackFlags(1)), Is.EqualTo(1));
            Assert.That(ClusterMeshLod.Level(ClusterMeshLod.PackFlags(2)), Is.EqualTo(2));
            Assert.That(ClusterMeshLod.IsParent(ClusterMeshLod.PackFlags(2)), Is.True);
        }

        [Test]
        public void DagVersion_ThresholdZero_HidesParents()
        {
            var leaf = new ClusterHeader { parentIndex = 0, lodError = 0f, flags = ClusterMeshLod.PackFlags(0) };
            var parent = new ClusterHeader
            {
                parentIndex = ClusterMeshLod.NoParent,
                lodError = 0.5f,
                flags = ClusterMeshLod.PackFlags(1)
            };
            var group = new ClusterGroup { lodError = 0.5f };
            Assert.That(
                ClusterMeshLod.IsVisible(leaf, group, true, 1f, 2f, 100f, 0f, 2, true),
                Is.True);
            Assert.That(
                ClusterMeshLod.IsVisible(parent, group, false, 2f, 0f, 100f, 0f, 2, true),
                Is.False);
        }

        [Test]
        public void DagVersion_LargeThreshold_ShowsParent_HidesChild()
        {
            var group = new ClusterGroup { lodError = 0.4f };
            var parent = new ClusterHeader
            {
                parentIndex = ClusterMeshLod.NoParent,
                lodError = 0.4f,
                flags = ClusterMeshLod.PackFlags(1)
            };
            var leaf = new ClusterHeader { parentIndex = 0, lodError = 0f, flags = ClusterMeshLod.PackFlags(0) };
            const float scale = 100f;
            const float t = 51f;
            Assert.That(
                ClusterMeshLod.IsVisible(leaf, group, true, 1f, 1f, scale, t, 2, true),
                Is.False);
            Assert.That(
                ClusterMeshLod.IsVisible(parent, group, false, 1f, 0f, scale, t, 2, true),
                Is.True);
        }
    }
}
