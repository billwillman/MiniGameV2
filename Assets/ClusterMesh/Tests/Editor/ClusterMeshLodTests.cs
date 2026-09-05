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

        [Test]
        public void TryGetOwningGroup_FindsRange_LeavesMiss()
        {
            var groups = new[]
            {
                new ClusterGroup { clusterStart = 2, clusterCount = 2 }
            };
            Assert.That(ClusterMeshLod.TryGetOwningGroup(2, groups, out int g0), Is.True);
            Assert.That(g0, Is.EqualTo(0));
            Assert.That(ClusterMeshLod.TryGetOwningGroup(3, groups, out int g1), Is.True);
            Assert.That(g1, Is.EqualTo(0));
            Assert.That(ClusterMeshLod.TryGetOwningGroup(0, groups, out _), Is.False);
            Assert.That(ClusterMeshLod.TryGetOwningGroup(1, groups, out _), Is.False);
        }

        [Test]
        public void DagVersion_SiblingParents_ShareGroupVisibility()
        {
            var groups = new[]
            {
                new ClusterGroup
                {
                    clusterStart = 2,
                    clusterCount = 2,
                    parentGroupIndex = ClusterMeshLod.NoParent,
                    lodError = 1f,
                    aabbCenter = Vector3.zero,
                    aabbExtents = new Vector4(5f, 5f, 5f, 0f)
                }
            };
            var clusters = new[]
            {
                new ClusterHeader
                {
                    parentIndex = 0,
                    lodError = 0f,
                    flags = ClusterMeshLod.PackFlags(0),
                    aabbCenter = new Vector3(-1f, 0f, 0f)
                },
                new ClusterHeader
                {
                    parentIndex = 0,
                    lodError = 0f,
                    flags = ClusterMeshLod.PackFlags(0),
                    aabbCenter = new Vector3(1f, 0f, 0f)
                },
                new ClusterHeader
                {
                    parentIndex = ClusterMeshLod.NoParent,
                    lodError = 1f,
                    flags = ClusterMeshLod.PackFlags(1),
                    aabbCenter = new Vector3(0f, 0f, 0.5f)
                },
                new ClusterHeader
                {
                    parentIndex = ClusterMeshLod.NoParent,
                    lodError = 1f,
                    flags = ClusterMeshLod.PackFlags(1),
                    aabbCenter = new Vector3(0f, 0f, -4f)
                }
            };

            Vector3 cam = new Vector3(0f, 0f, 1f);
            const float scale = 100f;
            const float t = 150f;
            Matrix4x4 m = Matrix4x4.identity;
            int v = ClusterMeshLod.HierarchyVersionDag;

            Assert.That(ClusterMeshLod.IsClusterVisible(0, clusters, groups, m, cam, scale, t, v, true), Is.False);
            Assert.That(ClusterMeshLod.IsClusterVisible(1, clusters, groups, m, cam, scale, t, v, true), Is.False);
            Assert.That(ClusterMeshLod.IsClusterVisible(2, clusters, groups, m, cam, scale, t, v, true), Is.True);
            Assert.That(ClusterMeshLod.IsClusterVisible(3, clusters, groups, m, cam, scale, t, v, true), Is.True);
        }

        [Test]
        public void LodQuality_Presets_AreUltraFine05_Fine1_Medium2_Coarse4()
        {
            Assert.That(ClusterMeshLodQuality.Value(ClusterMeshLodQuality.Preset.UltraFine), Is.EqualTo(0.5f));
            Assert.That(ClusterMeshLodQuality.Value(ClusterMeshLodQuality.Preset.Fine), Is.EqualTo(1f));
            Assert.That(ClusterMeshLodQuality.Value(ClusterMeshLodQuality.Preset.Medium), Is.EqualTo(2f));
            Assert.That(ClusterMeshLodQuality.Value(ClusterMeshLodQuality.Preset.Coarse), Is.EqualTo(4f));
        }

        [Test]
        public void LodQuality_FromValue_MatchesPresetOrCustom()
        {
            Assert.That(ClusterMeshLodQuality.FromValue(0.5f), Is.EqualTo(ClusterMeshLodQuality.Preset.UltraFine));
            Assert.That(ClusterMeshLodQuality.FromValue(1f), Is.EqualTo(ClusterMeshLodQuality.Preset.Fine));
            Assert.That(ClusterMeshLodQuality.FromValue(2f), Is.EqualTo(ClusterMeshLodQuality.Preset.Medium));
            Assert.That(ClusterMeshLodQuality.FromValue(4f), Is.EqualTo(ClusterMeshLodQuality.Preset.Coarse));
            Assert.That(ClusterMeshLodQuality.FromValue(0f), Is.EqualTo(ClusterMeshLodQuality.Preset.Custom));
            Assert.That(ClusterMeshLodQuality.FromValue(10f), Is.EqualTo(ClusterMeshLodQuality.Preset.Custom));
        }
    }
}
