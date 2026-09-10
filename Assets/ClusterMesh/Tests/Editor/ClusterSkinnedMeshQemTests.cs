using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterSkinnedMeshQemTests
    {
        [Test]
        public void BakerWindow_SkinnedMode_HidesQemToggle()
        {
            Assert.That(ClusterMeshBakerWindow.ShowsQemToggle(true, false), Is.True);
            Assert.That(ClusterMeshBakerWindow.ShowsQemToggle(true, true), Is.False);
            Assert.That(ClusterMeshBakerWindow.ShowsQemToggle(false, true), Is.False);
        }

        [Test]
        public void BlendWeights_Midpoint_MergesAndNormalizesInfluences()
        {
            var a = new ClusterSkinWeight { boneIndex0 = 2, weight0 = 1f };
            var b = new ClusterSkinWeight { boneIndex0 = 7, weight0 = 1f };
            ClusterSkinWeight result = ClusterSkinnedMeshQem.BlendWeights(a, b, 0.5f);
            Assert.That(result.boneIndex0, Is.EqualTo(2));
            Assert.That(result.boneIndex1, Is.EqualTo(7));
            Assert.That(result.weight0, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(result.weight1, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(result.weight0 + result.weight1 + result.weight2 + result.weight3, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void TryCollapse_FreeToLocked_PreservesLockedPositionAndSkin()
        {
            var lockedPosition = Vector3.zero;
            var lockedSkin = new ClusterSkinWeight { boneIndex0 = 3, weight0 = 1f };
            var positions = new List<Vector3> { lockedPosition, new Vector3(0.01f, 0f, 0f), new Vector3(10f, 1f, 0f) };
            var normals = new List<Vector3> { Vector3.forward, Vector3.forward, Vector3.forward };
            var tangent = new Vector4(1f, 0f, 0f, 1f);
            var tangents = new List<Vector4> { tangent, tangent, tangent };
            var uvs = new List<Vector2> { Vector2.zero, Vector2.right, Vector2.up };
            var skin = new List<ClusterSkinWeight>
            {
                lockedSkin,
                new ClusterSkinWeight { boneIndex0 = 5, weight0 = 1f },
                new ClusterSkinWeight { boneIndex0 = 5, weight0 = 1f }
            };
            var triangles = new List<int> { 0, 1, 2 };
            var locked = new List<bool> { true, false, true };

            Assert.That(ClusterSkinnedMeshQem.TryCollapse(
                positions, normals, tangents, uvs, skin, triangles, locked, new ClusterSkinnedQemContext()), Is.True);
            Assert.That(positions[0], Is.EqualTo(lockedPosition));
            Assert.That(skin[0].boneIndex0, Is.EqualTo(lockedSkin.boneIndex0));
            Assert.That(skin[0].weight0, Is.EqualTo(lockedSkin.weight0));
        }

        [Test]
        public void PackedWeight_Is16Bytes_AndWeightsSumTo65535()
        {
            var source = new ClusterSkinWeight
            {
                boneIndex0 = 1, boneIndex1 = 2, boneIndex2 = 3,
                weight0 = 0.5f, weight1 = 0.3f, weight2 = 0.2f
            };
            ClusterPackedSkinWeight packed = ClusterSkinnedMeshBaker.PackSkinWeight(source);
            int sum = (int)(packed.boneWeights01 & 0xFFFFu)
                + (int)(packed.boneWeights01 >> 16)
                + (int)(packed.boneWeights23 & 0xFFFFu)
                + (int)(packed.boneWeights23 >> 16);
            Assert.That(Marshal.SizeOf<ClusterPackedSkinWeight>(), Is.EqualTo(16));
            Assert.That(Marshal.SizeOf<ClusterSkinnedCullFrame>(), Is.EqualTo(64));
            Assert.That(sum, Is.EqualTo(65535));
        }

        [Test]
        public void EvaluatePalette_UsesNormalizedClipTime()
        {
            var asset = ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>();
            try
            {
                var curves = new ClusterSkinnedBoneCurves
                {
                    positionX = AnimationCurve.Linear(0f, 0f, 2f, 2f),
                    positionY = AnimationCurve.Constant(0f, 2f, 0f),
                    positionZ = AnimationCurve.Constant(0f, 2f, 0f),
                    rotationX = AnimationCurve.Constant(0f, 2f, 0f),
                    rotationY = AnimationCurve.Constant(0f, 2f, 0f),
                    rotationZ = AnimationCurve.Constant(0f, 2f, 0f),
                    rotationW = AnimationCurve.Constant(0f, 2f, 1f),
                    scaleX = AnimationCurve.Constant(0f, 2f, 1f),
                    scaleY = AnimationCurve.Constant(0f, 2f, 1f),
                    scaleZ = AnimationCurve.Constant(0f, 2f, 1f)
                };
                asset.bindPoses = new[] { Matrix4x4.identity };
                asset.boneParentIndices = new[] { -1 };
                asset.clips = new[]
                {
                    new ClusterSkinnedClip { duration = 2f, boneCurves = new[] { curves } }
                };
                var destination = new Matrix4x4[1];
                Assert.That(ClusterSkinnedAnimation.EvaluatePalette(asset, 0, 0.5f, destination), Is.True);
                Assert.That(destination[0].m03, Is.EqualTo(1f).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
