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
        public void ClusterTriangles_PrefersBoneAffineCandidateOverTriangleOrder()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f),
                new Vector3(-1f, 0f, 0f), new Vector3(0f, -1f, 0f),
                new Vector3(2f, 0f, 0f), new Vector3(1f, 1f, 0f)
            };
            var normals = new Vector3[positions.Length];
            var tangents = new Vector4[positions.Length];
            var uvs = new Vector2[positions.Length];
            var skin = new ClusterSkinWeight[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                normals[i] = Vector3.forward;
                tangents[i] = new Vector4(1f, 0f, 0f, 1f);
                skin[i] = new ClusterSkinWeight { boneIndex0 = 0, weight0 = 1f };
            }
            skin[3] = new ClusterSkinWeight { boneIndex0 = 1, weight0 = 1f };
            skin[4] = new ClusterSkinWeight { boneIndex0 = 1, weight0 = 1f };

            // t1 is encountered first through vertex 0 but crosses into bone 1.
            // t2 is encountered later through vertex 1 and remains entirely on bone 0.
            var triangles = new List<int> { 0, 1, 2, 0, 3, 4, 1, 5, 6 };
            var settings = new ClusterMeshBakeSettings
            {
                maxVerticesPerCluster = 5,
                maxTrianglesPerCluster = 2,
                buildLodHierarchy = false
            };
            var clusters = new List<ClusterHeader>();
            var vertices = new List<ClusterVertex>();
            var outputSkin = new List<ClusterSkinWeight>();
            var indices = new List<uint>();
            var method = typeof(ClusterSkinnedMeshBaker).GetMethod(
                "ClusterTriangles",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[]
            {
                0u, triangles, positions, normals, tangents, uvs, skin, settings,
                clusters, vertices, outputSkin, indices, 0f, 0u
            });

            Assert.That(clusters.Count, Is.EqualTo(2));
            Assert.That(clusters[0].triangleCount, Is.EqualTo(2u));
            for (int i = 0; i < clusters[0].vertexCount; i++)
            {
                ClusterSkinWeight weight = outputSkin[(int)clusters[0].vertexOffset + i];
                Assert.That(weight.boneIndex0, Is.EqualTo(0));
                Assert.That(weight.weight0, Is.EqualTo(1f));
            }
            int bakedTriangleCount = 0;
            for (int i = 0; i < clusters.Count; i++)
                bakedTriangleCount += (int)clusters[i].triangleCount;
            Assert.That(bakedTriangleCount, Is.EqualTo(3));

            var crossOnlyClusters = new List<ClusterHeader>();
            var crossOnlyVertices = new List<ClusterVertex>();
            var crossOnlySkin = new List<ClusterSkinWeight>();
            var crossOnlyIndices = new List<uint>();
            method.Invoke(null, new object[]
            {
                0u, new List<int> { 0, 1, 2, 0, 3, 4 }, positions, normals, tangents, uvs, skin, settings,
                crossOnlyClusters, crossOnlyVertices, crossOnlySkin, crossOnlyIndices, 0f, 0u
            });
            Assert.That(crossOnlyClusters.Count, Is.EqualTo(1), "bone affinity must remain a soft preference");
            Assert.That(crossOnlyClusters[0].triangleCount, Is.EqualTo(2u));

            var repeatedClusters = new List<ClusterHeader>();
            var repeatedVertices = new List<ClusterVertex>();
            var repeatedSkin = new List<ClusterSkinWeight>();
            var repeatedIndices = new List<uint>();
            method.Invoke(null, new object[]
            {
                0u, triangles, positions, normals, tangents, uvs, skin, settings,
                repeatedClusters, repeatedVertices, repeatedSkin, repeatedIndices, 0f, 0u
            });
            Assert.That(repeatedIndices, Is.EqualTo(indices), "cluster output must be deterministic");
            Assert.That(repeatedClusters.Count, Is.EqualTo(clusters.Count));
            for (int i = 0; i < clusters.Count; i++)
            {
                Assert.That(repeatedClusters[i].vertexCount, Is.EqualTo(clusters[i].vertexCount));
                Assert.That(repeatedClusters[i].triangleCount, Is.EqualTo(clusters[i].triangleCount));
            }
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

        [Test]
        public void DrawContext_NullAsset_CannotDraw()
        {
            var cull = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/ClusterMesh/Shaders/ClusterSkinnedMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/SkinnedLit");
            using (var ctx = new ClusterSkinnedMeshDrawContext(null, cull, lit))
            {
                Assert.That(ctx.IsReady, Is.False);
                Assert.That(ctx.CanDraw, Is.False);
                Assert.DoesNotThrow(() => ctx.Draw(
                    new[] { Matrix4x4.identity }, null, null, new[] { 0f },
                    0, ClusterSkinnedAnimationEvaluation.GpuTexture,
                    false, 0f, Camera.main, Camera.main, true, true, 0));
            }
        }

        [Test]
        public void DisposeCachedContexts_Twice_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                ClusterSkinnedMeshSceneBatcher.DisposeCachedContexts();
                ClusterSkinnedMeshSceneBatcher.DisposeCachedContexts();
            });
            Assert.That(ClusterSkinnedMeshSceneBatcher.CachedContextCount, Is.EqualTo(0));
        }

        [Test]
        public void GpuPalette_DefaultModeAndVersionedTexture_AreRecognized()
        {
            var go = new GameObject("GpuPaletteDefault");
            var asset = ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>();
            var texture = new Texture2D(3, 2, TextureFormat.RGBAHalf, false, true);
            try
            {
                var renderer = go.AddComponent<ClusterSkinnedMeshRenderer>();
                Assert.That(renderer.animationEvaluation,
                    Is.EqualTo(ClusterSkinnedAnimationEvaluation.GpuTexture));

                asset.bindPoses = new[] { Matrix4x4.identity };
                asset.clips = new[] { new ClusterSkinnedClip() };
                asset.gpuPaletteTextures = new[] { texture };
                asset.gpuAnimationVersion = ClusterSkinnedMeshAsset.CurrentGpuAnimationVersion;
                Assert.That(asset.HasGpuPalette(0), Is.True);

                asset.gpuAnimationVersion = 0;
                Assert.That(asset.HasGpuPalette(0), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CpuBurstCurve_HermiteCoefficients_MatchAnimationCurve()
        {
            var a = new Keyframe(0f, 1.25f, 0f, 0.75f);
            var b = new Keyframe(2f, 4.5f, -0.25f, 0f);
            var curve = new AnimationCurve(a, b);
            var headers = new List<ClusterSkinnedCurveHeader>();
            var segments = new List<ClusterSkinnedCurveSegment>();
            var method = typeof(ClusterSkinnedMeshBaker).GetMethod(
                "AddCpuCurve",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { curve, headers, segments });
            Assert.That(headers.Count, Is.EqualTo(1));
            Assert.That(segments.Count, Is.EqualTo(1));
            ClusterSkinnedCurveSegment segment = segments[0];
            for (int i = 0; i <= 8; i++)
            {
                float time = i * 0.25f;
                float u = Mathf.Clamp01((time - segment.startTime) * segment.inverseDuration);
                Vector4 c = segment.coefficients;
                float packedValue = ((c.w * u + c.z) * u + c.y) * u + c.x;
                Assert.That(packedValue, Is.EqualTo(curve.Evaluate(time)).Within(1e-5f));
            }
        }

        [Test]
        public void CpuBurstCurve_VersionedData_IsRecognized()
        {
            var asset = ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>();
            try
            {
                asset.bindPoses = new[] { Matrix4x4.identity };
                asset.boneParentIndices = new[] { -1 };
                asset.boneEvaluationOrder = new[] { 0 };
                asset.clips = new[] { new ClusterSkinnedClip { cpuCurveHeaderOffset = 0 } };
                asset.cpuCurveHeaders = new ClusterSkinnedCurveHeader[10];
                asset.cpuCurveSegments = new ClusterSkinnedCurveSegment[10];
                for (int i = 0; i < 10; i++)
                {
                    asset.cpuCurveHeaders[i] = new ClusterSkinnedCurveHeader
                    {
                        segmentOffset = i,
                        segmentCount = 1
                    };
                }
                asset.cpuBurstAnimationVersion = ClusterSkinnedMeshAsset.CurrentCpuBurstAnimationVersion;
                Assert.That(asset.HasCpuBurstCurves(0), Is.True);

                asset.cpuBurstAnimationVersion = 0;
                Assert.That(asset.HasCpuBurstCurves(0), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
