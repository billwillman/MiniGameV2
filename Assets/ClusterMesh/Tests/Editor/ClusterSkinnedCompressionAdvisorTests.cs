using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterSkinnedCompressionAdvisorTests
    {
        [Test]
        public void Analyze_NoMesh_AllUnknown()
        {
            ClusterSkinnedCompressionAdviceSet set = ClusterSkinnedCompressionAdvisor.Analyze(
                (Mesh)null, null, null, new ClusterMeshBakeSettings());
            Assert.That(set.packSkinWeights8.kind, Is.EqualTo(ClusterCompressionAdviceKind.Unknown));
            Assert.That(set.gpuCompactPalette.kind, Is.EqualTo(ClusterCompressionAdviceKind.Unknown));
            Assert.That(set.packTightRestVertices.kind, Is.EqualTo(ClusterCompressionAdviceKind.Unknown));
            Assert.That(ClusterSkinnedCompressionAdvisor.Label(set.packSkinWeights8), Does.Contain("待分析"));
        }

        [Test]
        public void AdviseSkinWeights8_SmallSkeleton_Recommends()
        {
            Mesh mesh = SkinnedGrid(3, 2, Matrix4x4.identity);
            try
            {
                ClusterSkinnedCompressionAdviceSet set = ClusterSkinnedCompressionAdvisor.Analyze(
                    mesh, null, null, new ClusterMeshBakeSettings());
                Assert.That(set.packSkinWeights8.kind, Is.EqualTo(ClusterCompressionAdviceKind.Recommend));
                Assert.That(set.packSkinWeights8.reason, Does.Contain("≤255"));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void AdviseSkinWeights8_BoneIndex300_NotRecommended()
        {
            Mesh mesh = SkinnedGrid(3, 80, Matrix4x4.identity);
            var weights = mesh.boneWeights;
            weights[0] = new BoneWeight { boneIndex0 = 300, weight0 = 1f };
            mesh.boneWeights = weights;
            try
            {
                ClusterSkinnedCompressionAdviceSet set = ClusterSkinnedCompressionAdvisor.Analyze(
                    mesh, null, null, new ClusterMeshBakeSettings());
                Assert.That(set.packSkinWeights8.kind, Is.EqualTo(ClusterCompressionAdviceKind.NotRecommend));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void AdviseCompactPalette_UniformBindPose_Recommends()
        {
            Mesh mesh = SkinnedGrid(3, 2, Matrix4x4.identity);
            try
            {
                ClusterSkinnedCompressionAdviceSet set = ClusterSkinnedCompressionAdvisor.Analyze(
                    mesh, null, new AnimationClip[0], new ClusterMeshBakeSettings());
                Assert.That(set.gpuCompactPalette.kind, Is.EqualTo(ClusterCompressionAdviceKind.Recommend));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void AdviseCompactPalette_NonUniformBindPose_NotRecommended()
        {
            Mesh mesh = SkinnedGrid(3, 2, Matrix4x4.Scale(new Vector3(1f, 2f, 1f)));
            try
            {
                ClusterSkinnedCompressionAdviceSet set = ClusterSkinnedCompressionAdvisor.Analyze(
                    mesh, null, null, new ClusterMeshBakeSettings());
                Assert.That(set.gpuCompactPalette.kind, Is.EqualTo(ClusterCompressionAdviceKind.NotRecommend));
                Assert.That(set.gpuCompactPalette.reason, Does.Contain("非均匀"));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void AdviseTightRest_VertexCountGatesRecommendation()
        {
            Mesh small = SkinnedGrid(3, 2, Matrix4x4.identity);
            Mesh large = SkinnedGrid(ClusterSkinnedCompressionAdvisor.TightRestRecommendVertices, 2, Matrix4x4.identity);
            try
            {
                Assert.That(
                    ClusterSkinnedCompressionAdvisor.Analyze(small, null, null, new ClusterMeshBakeSettings())
                        .packTightRestVertices.kind,
                    Is.EqualTo(ClusterCompressionAdviceKind.NotRecommend));
                Assert.That(
                    ClusterSkinnedCompressionAdvisor.Analyze(large, null, null, new ClusterMeshBakeSettings())
                        .packTightRestVertices.kind,
                    Is.EqualTo(ClusterCompressionAdviceKind.Recommend));
            }
            finally
            {
                Object.DestroyImmediate(small);
                Object.DestroyImmediate(large);
            }
        }

        [Test]
        public void AdviseCull_LongClip_Recommends()
        {
            Mesh mesh = SkinnedGrid(64, 2, Matrix4x4.identity);
            var clip = new AnimationClip { name = "Long", frameRate = 30f };
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 4f, 1f));
            try
            {
                ClusterSkinnedCompressionAdviceSet set = ClusterSkinnedCompressionAdvisor.Analyze(
                    mesh, null, new[] { clip }, new ClusterMeshBakeSettings());
                Assert.That(clip.length, Is.GreaterThanOrEqualTo(2f));
                Assert.That(set.compressCullFrames.kind, Is.EqualTo(ClusterCompressionAdviceKind.Recommend));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void AdviseCull_ShortTinyClip_NotRecommended()
        {
            Mesh mesh = SkinnedGrid(3, 2, Matrix4x4.identity);
            var clip = new AnimationClip { name = "Short", frameRate = 30f };
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 0.2f, 1f));
            try
            {
                ClusterSkinnedCompressionAdviceSet set = ClusterSkinnedCompressionAdvisor.Analyze(
                    mesh, null, new[] { clip }, new ClusterMeshBakeSettings { buildLodHierarchy = false });
                Assert.That(set.compressCullFrames.kind, Is.EqualTo(ClusterCompressionAdviceKind.NotRecommend));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Label_Recommend_StartsWithRecommended()
        {
            string label = ClusterSkinnedCompressionAdvisor.Label(
                ClusterCompressionAdvice.Recommend("骨 80"));
            Assert.That(label, Is.EqualTo("推荐开启 · 骨 80"));
        }

        static Mesh SkinnedGrid(int vertexCount, int boneCount, Matrix4x4 bindPose)
        {
            vertexCount = Mathf.Max(3, vertexCount);
            boneCount = Mathf.Max(1, boneCount);
            var mesh = new Mesh { name = "CMAdviceSkinned" };
            var positions = new Vector3[vertexCount];
            var weights = new BoneWeight[vertexCount];
            var bindPoses = new Matrix4x4[boneCount];
            for (int i = 0; i < boneCount; i++)
                bindPoses[i] = bindPose;
            for (int i = 0; i < vertexCount; i++)
            {
                positions[i] = new Vector3(i % 16, i / 16, 0f);
                weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
            mesh.vertices = positions;
            var tris = new int[(vertexCount - 2) * 3];
            for (int i = 0; i < vertexCount - 2; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            mesh.triangles = tris;
            mesh.bindposes = bindPoses;
            mesh.boneWeights = weights;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
