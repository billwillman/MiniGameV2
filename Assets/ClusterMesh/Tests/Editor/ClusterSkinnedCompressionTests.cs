using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterSkinnedCompressionTests
    {
        [Test]
        public void BakeOptions_AdvancedCompression_DefaultsOff()
        {
            var options = new ClusterSkinnedMeshBakeOptions();
            Assert.That(options.packSkinWeights8, Is.False);
            Assert.That(options.gpuCompactPalette, Is.False);
            Assert.That(options.compressCullFrames, Is.False);
            Assert.That(options.packTightRestVertices, Is.False);
            Assert.That(options.animationDataMode, Is.EqualTo(ClusterSkinnedAnimationDataMode.GpuOnly));
        }

        [Test]
        public void PackSkinWeight8_NormalizesTo255_AndIs8Bytes()
        {
            var source = new ClusterSkinWeight
            {
                boneIndex0 = 1, boneIndex1 = 2, boneIndex2 = 3,
                weight0 = 0.5f, weight1 = 0.3f, weight2 = 0.2f
            };
            ClusterPackedSkinWeight8 packed = ClusterSkinnedMeshBaker.PackSkinWeight8(source);
            int sum = (int)(packed.boneWeights & 0xFFu)
                + (int)((packed.boneWeights >> 8) & 0xFFu)
                + (int)((packed.boneWeights >> 16) & 0xFFu)
                + (int)(packed.boneWeights >> 24);
            Assert.That(Marshal.SizeOf<ClusterPackedSkinWeight8>(), Is.EqualTo(8));
            Assert.That(sum, Is.EqualTo(255));
            Assert.That(packed.boneIndices & 0xFFu, Is.EqualTo(1u));
            Assert.That((packed.boneIndices >> 8) & 0xFFu, Is.EqualTo(2u));
        }

        [Test]
        public void CanPackSkinWeights8_RejectsBoneIndexAbove255()
        {
            var ok = new[] { new ClusterSkinWeight { boneIndex0 = 255, weight0 = 1f } };
            var tooBig = new[] { new ClusterSkinWeight { boneIndex0 = 256, weight0 = 1f } };
            Assert.That(ClusterSkinnedMeshBaker.CanPackSkinWeights8(ok), Is.True);
            Assert.That(ClusterSkinnedMeshBaker.CanPackSkinWeights8(tooBig), Is.False);
        }

        [Test]
        public void PackSkinWeights_DefaultStride_Stays16AndDeflates()
        {
            var weights = new[] { new ClusterSkinWeight { boneIndex0 = 4, weight0 = 1f } };
            byte[] packed = ClusterSkinnedMeshBaker.PackSkinWeights(weights);
            Assert.That(ClusterMeshGeometry.TryInflate(packed, out byte[] raw), Is.True);
            Assert.That(raw.Length, Is.EqualTo(16));
        }

        [Test]
        public void PackSkinWeights_Stride8_Writes8Bytes()
        {
            var weights = new[] { new ClusterSkinWeight { boneIndex0 = 4, weight0 = 1f } };
            byte[] packed = ClusterSkinnedMeshBaker.PackSkinWeights(weights, 8);
            Assert.That(ClusterMeshGeometry.TryInflate(packed, out byte[] raw), Is.True);
            Assert.That(raw.Length, Is.EqualTo(8));
        }

        [Test]
        public void Asset_TryReadSkinWeights_OldStrideZero_Reads16()
        {
            var weights = new[] { new ClusterSkinWeight { boneIndex0 = 1, weight0 = 1f } };
            var asset = ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>();
            var geometry = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            try
            {
                geometry.vertexCount = 1;
                asset.geometry = geometry;
                asset.skinningVersion = ClusterSkinnedMeshAsset.CurrentSkinningVersion;
                asset.skinVertexCount = 1;
                asset.skinWeightStride = 0;
                asset.packedSkinWeights = ClusterSkinnedMeshBaker.PackSkinWeights(weights);
                Assert.That(asset.ResolvedSkinWeightStride, Is.EqualTo(16));
                Assert.That(asset.TryReadSkinWeights(out ClusterPackedSkinWeight[] packed, out string error), Is.True);
                Assert.That(error, Is.Null);
                Assert.That(packed.Length, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(geometry);
            }
        }

        [Test]
        public void CullSegmentCount_CompressHalvesRate()
        {
            Assert.That(ClusterSkinnedMeshBaker.CullSegmentCount(2f, 30f, false), Is.EqualTo(8));
            Assert.That(ClusterSkinnedMeshBaker.CullSegmentCount(2f, 30f, true), Is.EqualTo(4));
        }

        [Test]
        public void TryGetCullFrames_Packed_RoundTrips()
        {
            var frames = new[]
            {
                new ClusterSkinnedCullFrame
                {
                    aabbCenter = new Vector4(1f, 2f, 3f, 0f),
                    aabbExtents = new Vector4(0.5f, 0.5f, 0.5f, 0f),
                    coneAxisCutoff = new Vector4(0f, 1f, 0f, -1f),
                    coneApex = new Vector4(1f, 2f, 3f, 0f)
                }
            };
            var asset = ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>();
            try
            {
                asset.cullFrames = null;
                asset.packedCullFrames = ClusterSkinnedMeshBaker.PackCullFrames(frames);
                Assert.That(asset.TryGetCullFrames(out ClusterSkinnedCullFrame[] read, out string error), Is.True);
                Assert.That(error, Is.Null);
                Assert.That(read.Length, Is.EqualTo(1));
                Assert.That((Vector3)read[0].aabbCenter, Is.EqualTo((Vector3)frames[0].aabbCenter));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TryGetCullFrames_UnpackedArray_StillWorks()
        {
            var frames = new[]
            {
                new ClusterSkinnedCullFrame { aabbCenter = Vector4.one, aabbExtents = Vector4.one }
            };
            var asset = ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>();
            try
            {
                asset.cullFrames = frames;
                Assert.That(asset.TryGetCullFrames(out ClusterSkinnedCullFrame[] read, out _), Is.True);
                Assert.That(read, Is.SameAs(frames));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void HasGpuPalette_CompactWidthUsesTwoPixelsPerBone()
        {
            var asset = ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>();
            var texture = new Texture2D(2, 2, TextureFormat.RGBAHalf, false, true);
            try
            {
                asset.bindPoses = new[] { Matrix4x4.identity };
                asset.clips = new[] { new ClusterSkinnedClip() };
                asset.gpuPaletteTextures = new[] { texture };
                asset.gpuAnimationVersion = ClusterSkinnedMeshAsset.CurrentGpuAnimationVersion;
                asset.gpuCompactPalette = true;
                Assert.That(asset.GpuPalettePixelsPerBone, Is.EqualTo(2));
                Assert.That(asset.HasGpuPalette(0), Is.True);

                asset.gpuCompactPalette = false;
                Assert.That(asset.GpuPalettePixelsPerBone, Is.EqualTo(3));
                Assert.That(asset.HasGpuPalette(0), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CompactPalette_UniformScale_RoundTripsTranslationAndRotation()
        {
            Quaternion rotation = Quaternion.Euler(20f, -35f, 10f);
            Vector3 translation = new Vector3(1.5f, -0.25f, 4f);
            Matrix4x4 source = Matrix4x4.TRS(translation, rotation, Vector3.one * 2f);
            ClusterSkinnedMeshBaker.MatrixToCompact(source, out Quaternion q, out Vector3 t, out float s);
            Matrix4x4 back = ClusterSkinnedMeshBaker.CompactToMatrix(q, t, s);
            Assert.That((back.GetColumn(3) - source.GetColumn(3)).magnitude, Is.LessThan(1e-4f));
            Assert.That(s, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(Quaternion.Angle(q, rotation), Is.LessThan(0.05f));
        }

        [Test]
        public void TightVertex_StrideIs24_AndNormalRoundTrips()
        {
            Assert.That(Marshal.SizeOf<ClusterPackedVertexTight>(), Is.EqualTo(24));
            var n = new Vector3(0.2f, 0.5f, 0.84f).normalized;
            var src = new ClusterVertex
            {
                position = new Vector4(1.25f, -2f, 0.5f, 0f),
                normal = n,
                tangent = new Vector4(1f, 0f, 0f, -1f),
                uv = new Vector4(0.25f, 0.75f, 0f, 0f)
            };
            ClusterVertex dst = ClusterMeshGeometry.UnpackVertexTight(ClusterMeshGeometry.PackVertexTight(src));
            Assert.That(((Vector3)dst.position - (Vector3)src.position).magnitude, Is.LessThan(1e-5f));
            Assert.That(Vector3.Dot(((Vector3)dst.normal).normalized, n), Is.GreaterThan(0.995f));
            Assert.That(dst.tangent.w, Is.EqualTo(-1f));
            Assert.That(Mathf.Abs(dst.uv.x - 0.25f), Is.LessThan(1e-3f));
        }

        [Test]
        public void CopyFrom_Default_Keeps32ByteStride()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            try
            {
                asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());
                Assert.That(asset.ResolvedVertexStride, Is.EqualTo(32));
                Assert.That(
                    ClusterMeshGeometry.TryReadGpuGeometry(asset, out _, out _, out string error),
                    Is.True);
                Assert.That(error, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void CopyFrom_TightRest_Writes24AndStaticReaderRejects()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            try
            {
                asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings(), true);
                Assert.That(asset.ResolvedVertexStride, Is.EqualTo(24));
                Assert.That(
                    ClusterMeshGeometry.TryReadGpuGeometry(asset, out _, out _, out string gpuError),
                    Is.False);
                Assert.That(gpuError, Does.Contain("tight"));
                Assert.That(
                    ClusterMeshGeometry.TryReadTightVertices(asset, out ClusterPackedVertexTight[] tight, out string tightError),
                    Is.True);
                Assert.That(tightError, Is.Null);
                Assert.That(tight.Length, Is.EqualTo(bake.vertices.Length));
                Assert.That(
                    ClusterMeshGeometry.TryReadWorkingGeometry(asset, out ClusterVertex[] verts, out _, out string workError),
                    Is.True);
                Assert.That(workError, Is.Null);
                Assert.That(verts.Length, Is.EqualTo(bake.vertices.Length));
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
