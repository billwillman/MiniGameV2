using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public sealed class ClusterSkinnedMeshBakeResult
    {
        public ClusterMeshBakeResult geometry;
        public ClusterSkinWeight[] skinWeights;
        public Matrix4x4[] bindPoses;
        public string[] bonePaths;
        public int[] boneParentIndices;
        public ClusterSkinnedClip[] clips;
        public ClusterSkinnedCullFrame[] cullFrames;
    }

    public static class ClusterSkinnedMeshBaker
    {
        sealed class SampledClip
        {
            public ClusterSkinnedClip clip;
            public float[] times;
            public Matrix4x4[][] palettes;
        }

        public static ClusterSkinnedMeshBakeResult Bake(
            SkinnedMeshRenderer renderer,
            AnimationClip clip,
            ClusterMeshBakeSettings settings)
        {
            if (clip == null)
                throw new InvalidOperationException("Skinned ClusterMesh baker requires an AnimationClip.");
            return Bake(renderer, new[] { clip }, settings);
        }

        public static ClusterSkinnedMeshBakeResult Bake(
            SkinnedMeshRenderer renderer,
            AnimationClip[] animationClips,
            ClusterMeshBakeSettings settings)
        {
            if (renderer == null || renderer.sharedMesh == null)
                throw new InvalidOperationException("Skinned ClusterMesh baker requires a SkinnedMeshRenderer with a shared Mesh.");
            if (animationClips == null || animationClips.Length == 0)
                throw new InvalidOperationException("Skinned ClusterMesh baker requires at least one AnimationClip.");
            settings = settings ?? new ClusterMeshBakeSettings();
            if (settings.maxVerticesPerCluster < 3 || settings.maxTrianglesPerCluster < 1)
                throw new InvalidOperationException("ClusterMesh baker budgets must allow at least one triangle.");

            Mesh mesh = renderer.sharedMesh;
            Transform[] bones = renderer.bones;
            Matrix4x4[] bindPoses = mesh.bindposes;
            if (bones == null || bones.Length == 0 || bindPoses == null || bindPoses.Length != bones.Length)
                throw new InvalidOperationException("SkinnedMesh bones and bind poses must exist and have the same length.");
            if (bones.Length > ushort.MaxValue)
                throw new InvalidOperationException("Skinned ClusterMesh supports at most 65535 bones.");

            BuildSkeleton(renderer, bones, out string[] bonePaths, out int[] parentIndices);
            var sampledClips = new SampledClip[animationClips.Length];
            for (int i = 0; i < animationClips.Length; i++)
            {
                if (animationClips[i] == null)
                    throw new InvalidOperationException("AnimationClip list contains a missing clip.");
                sampledClips[i] = SampleClip(renderer, animationClips[i], bones, parentIndices, bindPoses);
            }

            ClusterSkinWeight[] sourceSkin = ReadSourceWeights(mesh, bones.Length);
            var qemContext = new ClusterSkinnedQemContext();
            AddRepresentativePalettes(sampledClips, qemContext);
            ClusterMeshBakeResult geometry = BakeGeometry(mesh, renderer.sharedMaterials, sourceSkin, settings, qemContext, out List<ClusterSkinWeight> outputSkin);

            var allCullFrames = new List<ClusterSkinnedCullFrame>();
            var clips = new ClusterSkinnedClip[sampledClips.Length];
            for (int i = 0; i < sampledClips.Length; i++)
            {
                SampledClip sampled = sampledClips[i];
                sampled.clip.cullFrameOffset = allCullFrames.Count;
                BuildCullFrames(geometry, outputSkin, sampled, allCullFrames);
                clips[i] = sampled.clip;
            }

            return new ClusterSkinnedMeshBakeResult
            {
                geometry = geometry,
                skinWeights = outputSkin.ToArray(),
                bindPoses = bindPoses,
                bonePaths = bonePaths,
                boneParentIndices = parentIndices,
                clips = clips,
                cullFrames = allCullFrames.ToArray()
            };
        }

        public static byte[] PackSkinWeights(ClusterSkinWeight[] weights)
        {
            if (weights == null || weights.Length == 0)
                return Array.Empty<byte>();
            var packed = new ClusterPackedSkinWeight[weights.Length];
            for (int i = 0; i < weights.Length; i++)
                packed[i] = PackSkinWeight(weights[i]);
            int size = Marshal.SizeOf<ClusterPackedSkinWeight>();
            var bytes = new byte[packed.Length * size];
            GCHandle handle = GCHandle.Alloc(packed, GCHandleType.Pinned);
            try
            {
                Marshal.Copy(handle.AddrOfPinnedObject(), bytes, 0, bytes.Length);
            }
            finally
            {
                handle.Free();
            }
            return ClusterMeshGeometry.Deflate(bytes);
        }

        public static ClusterPackedSkinWeight PackSkinWeight(in ClusterSkinWeight weight)
        {
            ushort i0 = CheckedBoneIndex(weight.boneIndex0, weight.weight0);
            ushort i1 = CheckedBoneIndex(weight.boneIndex1, weight.weight1);
            ushort i2 = CheckedBoneIndex(weight.boneIndex2, weight.weight2);
            ushort i3 = CheckedBoneIndex(weight.boneIndex3, weight.weight3);
            float sum = Mathf.Max(0f, weight.weight0) + Mathf.Max(0f, weight.weight1)
                + Mathf.Max(0f, weight.weight2) + Mathf.Max(0f, weight.weight3);
            float inv = sum > 1e-8f ? 1f / sum : 0f;
            ushort[] quantized =
            {
                (ushort)Mathf.RoundToInt(Mathf.Max(0f, weight.weight0) * inv * 65535f),
                (ushort)Mathf.RoundToInt(Mathf.Max(0f, weight.weight1) * inv * 65535f),
                (ushort)Mathf.RoundToInt(Mathf.Max(0f, weight.weight2) * inv * 65535f),
                (ushort)Mathf.RoundToInt(Mathf.Max(0f, weight.weight3) * inv * 65535f)
            };
            if (sum <= 1e-8f)
            {
                i0 = 0;
                quantized[0] = ushort.MaxValue;
            }
            else
            {
                int total = quantized[0] + quantized[1] + quantized[2] + quantized[3];
                int largest = 0;
                for (int i = 1; i < 4; i++)
                {
                    if (quantized[i] > quantized[largest])
                        largest = i;
                }
                quantized[largest] = (ushort)Mathf.Clamp(quantized[largest] + (65535 - total), 0, 65535);
            }
            return new ClusterPackedSkinWeight
            {
                boneIndices01 = i0 | ((uint)i1 << 16),
                boneIndices23 = i2 | ((uint)i3 << 16),
                boneWeights01 = quantized[0] | ((uint)quantized[1] << 16),
                boneWeights23 = quantized[2] | ((uint)quantized[3] << 16)
            };
        }

        static ushort CheckedBoneIndex(int index, float weight)
        {
            if (weight <= 0f)
                return 0;
            if (index < 0 || index > ushort.MaxValue)
                throw new InvalidOperationException("A skin weight contains a bone index outside the supported ushort range.");
            return (ushort)index;
        }

        static ClusterMeshBakeResult BakeGeometry(
            Mesh mesh,
            Material[] materials,
            ClusterSkinWeight[] sourceSkin,
            ClusterMeshBakeSettings settings,
            ClusterSkinnedQemContext qemContext,
            out List<ClusterSkinWeight> outputSkin)
        {
            Vector3[] positions = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            Vector2[] uvs = mesh.uv;
            if (positions == null || positions.Length == 0)
                throw new InvalidOperationException("Skinned ClusterMesh baker requires mesh vertices.");
            if (tangents == null || tangents.Length != positions.Length)
                tangents = RebuildTangents(positions, normals, uvs, mesh);

            var clusters = new List<ClusterHeader>();
            var vertices = new List<ClusterVertex>();
            var indices = new List<uint>();
            var groups = new List<ClusterGroup>();
            outputSkin = new List<ClusterSkinWeight>();
            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int sub = 0; sub < subMeshCount; sub++)
            {
                int leafStart = clusters.Count;
                var triangleList = new List<int>();
                int[] sourceTriangles = mesh.GetTriangles(sub);
                for (int i = 0; i + 2 < sourceTriangles.Length; i += 3)
                {
                    int a = sourceTriangles[i];
                    int b = sourceTriangles[i + 1];
                    int c = sourceTriangles[i + 2];
                    if (a == b || b == c || a == c)
                        continue;
                    triangleList.Add(a);
                    triangleList.Add(b);
                    triangleList.Add(c);
                }
                ClusterTriangles((uint)sub, triangleList, positions, normals, tangents, uvs, sourceSkin,
                    settings, clusters, vertices, outputSkin, indices, 0f, ClusterMeshLod.PackFlags(0));
                if (settings.buildLodHierarchy)
                    ClusterSkinnedMeshLodBaker.BuildHierarchy(clusters, vertices, outputSkin, indices, groups,
                        leafStart, clusters.Count, settings, qemContext);
            }
            if (clusters.Count == 0)
                throw new InvalidOperationException("Skinned ClusterMesh baker found no valid triangles.");

            var materialSlots = new Material[subMeshCount];
            for (int i = 0; materials != null && i < subMeshCount && i < materials.Length; i++)
                materialSlots[i] = materials[i];
            return new ClusterMeshBakeResult
            {
                clusters = clusters.ToArray(),
                vertices = vertices.ToArray(),
                indices = indices.ToArray(),
                groups = groups.ToArray(),
                materials = materialSlots,
                hierarchyVersion = settings.buildLodHierarchy ? ClusterMeshLod.HierarchyVersionDag : 0
            };
        }

        internal static void ClusterTriangles(
            uint materialIndex,
            List<int> triangleList,
            Vector3[] positions,
            Vector3[] normals,
            Vector4[] tangents,
            Vector2[] uvs,
            ClusterSkinWeight[] sourceSkin,
            ClusterMeshBakeSettings settings,
            List<ClusterHeader> clusters,
            List<ClusterVertex> vertices,
            List<ClusterSkinWeight> destinationSkin,
            List<uint> indices,
            float lodError,
            uint flags)
        {
            int triangleCount = triangleList.Count / 3;
            if (triangleCount == 0)
                return;
            var unused = new bool[triangleCount];
            var vertexToTriangles = new Dictionary<int, List<int>>();
            for (int t = 0; t < triangleCount; t++)
            {
                unused[t] = true;
                for (int k = 0; k < 3; k++)
                {
                    int vertex = triangleList[t * 3 + k];
                    if (!vertexToTriangles.TryGetValue(vertex, out List<int> list))
                    {
                        list = new List<int>();
                        vertexToTriangles.Add(vertex, list);
                    }
                    list.Add(t);
                }
            }

            int remaining = triangleCount;
            while (remaining > 0)
            {
                int seed = Array.FindIndex(unused, value => value);
                var clusterTriangles = new List<int>();
                var usedVertices = new HashSet<int>();
                AddTriangle(seed, triangleList, unused, clusterTriangles, usedVertices);
                remaining--;
                while (true)
                {
                    int candidate = FindCandidate(clusterTriangles, triangleList, unused, vertexToTriangles, usedVertices, settings);
                    if (candidate < 0)
                        break;
                    AddTriangle(candidate, triangleList, unused, clusterTriangles, usedVertices);
                    remaining--;
                }
                EmitCluster(materialIndex, clusterTriangles, triangleList, positions, normals, tangents, uvs, sourceSkin,
                    clusters, vertices, destinationSkin, indices, lodError, flags);
            }
        }

        static int FindCandidate(List<int> clusterTriangles, List<int> triangles, bool[] unused,
            Dictionary<int, List<int>> adjacency, HashSet<int> usedVertices, ClusterMeshBakeSettings settings)
        {
            foreach (int triangle in clusterTriangles)
            {
                for (int k = 0; k < 3; k++)
                {
                    int vertex = triangles[triangle * 3 + k];
                    foreach (int candidate in adjacency[vertex])
                    {
                        if (!unused[candidate])
                            continue;
                        int added = 0;
                        for (int n = 0; n < 3; n++)
                        {
                            if (!usedVertices.Contains(triangles[candidate * 3 + n]))
                                added++;
                        }
                        if (clusterTriangles.Count + 1 <= settings.maxTrianglesPerCluster &&
                            usedVertices.Count + added <= settings.maxVerticesPerCluster)
                            return candidate;
                    }
                }
            }
            return -1;
        }

        static void AddTriangle(int triangle, List<int> triangles, bool[] unused, List<int> clusterTriangles, HashSet<int> usedVertices)
        {
            unused[triangle] = false;
            clusterTriangles.Add(triangle);
            for (int i = 0; i < 3; i++)
                usedVertices.Add(triangles[triangle * 3 + i]);
        }

        static void EmitCluster(
            uint materialIndex, List<int> clusterTriangles, List<int> triangles,
            Vector3[] positions, Vector3[] normals, Vector4[] tangents, Vector2[] uvs,
            ClusterSkinWeight[] sourceSkin, List<ClusterHeader> clusters, List<ClusterVertex> vertices,
            List<ClusterSkinWeight> destinationSkin, List<uint> destinationIndices, float lodError, uint flags)
        {
            var remap = new Dictionary<int, uint>();
            uint vertexOffset = (uint)vertices.Count;
            uint indexOffset = (uint)destinationIndices.Count;
            var clusterPositions = new List<Vector3>();
            foreach (int triangle in clusterTriangles)
            {
                for (int k = 0; k < 3; k++)
                {
                    int source = triangles[triangle * 3 + k];
                    if (!remap.TryGetValue(source, out uint local))
                    {
                        local = (uint)remap.Count;
                        remap.Add(source, local);
                        Vector3 normal = normals != null && source < normals.Length ? normals[source] : Vector3.up;
                        Vector4 tangent = tangents != null && source < tangents.Length ? tangents[source] : new Vector4(1f, 0f, 0f, 1f);
                        Vector2 uv = uvs != null && source < uvs.Length ? uvs[source] : Vector2.zero;
                        vertices.Add(new ClusterVertex
                        {
                            position = positions[source], normal = normal, tangent = tangent,
                            uv = new Vector4(uv.x, uv.y, 0f, 0f)
                        });
                        destinationSkin.Add(sourceSkin[source]);
                        clusterPositions.Add(positions[source]);
                    }
                    destinationIndices.Add(local);
                }
            }
            Vector3 min = clusterPositions[0];
            Vector3 max = min;
            for (int i = 1; i < clusterPositions.Count; i++)
            {
                min = Vector3.Min(min, clusterPositions[i]);
                max = Vector3.Max(max, clusterPositions[i]);
            }
            Vector3 center = (min + max) * 0.5f;
            BuildCone(clusterTriangles, triangles, positions, out Vector3 axis, out float cutoff);
            clusters.Add(new ClusterHeader
            {
                vertexOffset = vertexOffset, vertexCount = (uint)remap.Count,
                indexOffset = indexOffset, triangleCount = (uint)clusterTriangles.Count,
                materialIndex = materialIndex, parentIndex = ClusterMeshLod.NoParent,
                lodError = lodError, flags = flags, aabbCenter = center,
                aabbExtents = (max - min) * 0.5f,
                coneAxisCutoff = new Vector4(axis.x, axis.y, axis.z, cutoff), coneApex = center
            });
        }

        static void BuildCone(List<int> clusterTriangles, List<int> triangles, Vector3[] positions, out Vector3 axis, out float cutoff)
        {
            Vector3 weighted = Vector3.zero;
            var normals = new List<Vector3>();
            foreach (int triangle in clusterTriangles)
            {
                Vector3 a = positions[triangles[triangle * 3]];
                Vector3 b = positions[triangles[triangle * 3 + 1]];
                Vector3 c = positions[triangles[triangle * 3 + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                float area = normal.magnitude;
                if (area < 1e-12f) continue;
                normal /= area;
                normals.Add(normal);
                weighted += normal * area;
            }
            if (weighted.sqrMagnitude < 1e-12f)
            {
                axis = Vector3.up;
                cutoff = -1f;
                return;
            }
            axis = weighted.normalized;
            float minDot = 1f;
            for (int i = 0; i < normals.Count; i++) minDot = Mathf.Min(minDot, Vector3.Dot(axis, normals[i]));
            cutoff = minDot < 0f ? -1f : minDot;
        }

        static ClusterSkinWeight[] ReadSourceWeights(Mesh mesh, int boneCount)
        {
            BoneWeight[] source = mesh.boneWeights;
            if (source == null || source.Length != mesh.vertexCount)
                throw new InvalidOperationException("SkinnedMesh must have four-influence bone weights for every vertex.");
            var result = new ClusterSkinWeight[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                BoneWeight value = source[i];
                ValidateBone(value.boneIndex0, value.weight0, boneCount);
                ValidateBone(value.boneIndex1, value.weight1, boneCount);
                ValidateBone(value.boneIndex2, value.weight2, boneCount);
                ValidateBone(value.boneIndex3, value.weight3, boneCount);
                result[i] = ClusterSkinnedMeshQem.BlendWeights(new ClusterSkinWeight
                {
                    boneIndex0 = value.boneIndex0, boneIndex1 = value.boneIndex1,
                    boneIndex2 = value.boneIndex2, boneIndex3 = value.boneIndex3,
                    weight0 = value.weight0, weight1 = value.weight1,
                    weight2 = value.weight2, weight3 = value.weight3
                }, default, 0f);
            }
            return result;
        }

        static void ValidateBone(int index, float weight, int boneCount)
        {
            if (weight > 0f && (index < 0 || index >= boneCount))
                throw new InvalidOperationException("SkinnedMesh contains a bone weight outside the renderer bone array.");
        }

        static void BuildSkeleton(SkinnedMeshRenderer renderer, Transform[] bones, out string[] paths, out int[] parents)
        {
            Transform animationRoot = renderer.transform.root;
            paths = new string[bones.Length];
            parents = new int[bones.Length];
            var lookup = new Dictionary<Transform, int>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                    throw new InvalidOperationException("SkinnedMesh bone array contains a missing transform.");
                lookup[bones[i]] = i;
                paths[i] = AnimationUtility.CalculateTransformPath(bones[i], animationRoot);
            }
            for (int i = 0; i < bones.Length; i++)
                parents[i] = bones[i].parent != null && lookup.TryGetValue(bones[i].parent, out int parent) ? parent : -1;
        }

        static SampledClip SampleClip(SkinnedMeshRenderer sourceRenderer, AnimationClip sourceClip, Transform[] sourceBones,
            int[] parentIndices, Matrix4x4[] bindPoses)
        {
            GameObject sourceRoot = sourceRenderer.transform.root.gameObject;
            GameObject clone = UnityEngine.Object.Instantiate(sourceRoot);
            clone.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                string rendererPath = AnimationUtility.CalculateTransformPath(sourceRenderer.transform, sourceRoot.transform);
                Transform rendererTransform = string.IsNullOrEmpty(rendererPath) ? clone.transform : clone.transform.Find(rendererPath);
                SkinnedMeshRenderer renderer = rendererTransform != null ? rendererTransform.GetComponent<SkinnedMeshRenderer>() : null;
                if (renderer == null)
                    throw new InvalidOperationException("Could not locate the SkinnedMeshRenderer in the animation sampling clone.");
                Transform[] bones = new Transform[sourceBones.Length];
                for (int i = 0; i < sourceBones.Length; i++)
                {
                    string path = AnimationUtility.CalculateTransformPath(sourceBones[i], sourceRoot.transform);
                    bones[i] = string.IsNullOrEmpty(path) ? clone.transform : clone.transform.Find(path);
                    if (bones[i] == null)
                        throw new InvalidOperationException("Could not locate bone '" + path + "' in the animation sampling clone.");
                }

                float frameRate = Mathf.Clamp(sourceClip.frameRate > 0f ? sourceClip.frameRate : 30f, 1f, 60f);
                float duration = Mathf.Max(0f, sourceClip.length);
                int frameCount = Mathf.Max(2, Mathf.CeilToInt(duration * frameRate) + 1);
                var times = new float[frameCount];
                var palettes = new Matrix4x4[frameCount][];
                var positions = new Vector3[sourceBones.Length][];
                var rotations = new Quaternion[sourceBones.Length][];
                var scales = new Vector3[sourceBones.Length][];
                for (int b = 0; b < sourceBones.Length; b++)
                {
                    positions[b] = new Vector3[frameCount];
                    rotations[b] = new Quaternion[frameCount];
                    scales[b] = new Vector3[frameCount];
                }
                for (int frame = 0; frame < frameCount; frame++)
                {
                    float time = frameCount > 1 ? duration * frame / (frameCount - 1f) : 0f;
                    times[frame] = time;
                    sourceClip.SampleAnimation(clone, time);
                    palettes[frame] = new Matrix4x4[sourceBones.Length];
                    for (int bone = 0; bone < sourceBones.Length; bone++)
                    {
                        Transform parent = parentIndices[bone] >= 0 ? bones[parentIndices[bone]] : null;
                        Matrix4x4 local = parent != null
                            ? parent.worldToLocalMatrix * bones[bone].localToWorldMatrix
                            : renderer.transform.worldToLocalMatrix * bones[bone].localToWorldMatrix;
                        Decompose(local, out positions[bone][frame], out rotations[bone][frame], out scales[bone][frame]);
                        if (frame > 0 && Quaternion.Dot(rotations[bone][frame - 1], rotations[bone][frame]) < 0f)
                        {
                            Quaternion q = rotations[bone][frame];
                            rotations[bone][frame] = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        }
                        palettes[frame][bone] = renderer.transform.worldToLocalMatrix * bones[bone].localToWorldMatrix * bindPoses[bone];
                    }
                }
                var curves = new ClusterSkinnedBoneCurves[sourceBones.Length];
                for (int bone = 0; bone < sourceBones.Length; bone++)
                    curves[bone] = FitBoneCurves(times, positions[bone], rotations[bone], scales[bone]);
                int segmentCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(duration, 1f / frameRate) * 4f));
                return new SampledClip
                {
                    clip = new ClusterSkinnedClip
                    {
                        name = sourceClip.name, duration = duration, frameRate = frameRate,
                        segmentCount = segmentCount, boneCurves = curves
                    },
                    times = times,
                    palettes = palettes
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        static void AddRepresentativePalettes(SampledClip[] clips, ClusterSkinnedQemContext context)
        {
            for (int c = 0; c < clips.Length; c++)
            {
                int step = Mathf.Max(1, clips[c].palettes.Length / 8);
                for (int i = 0; i < clips[c].palettes.Length; i += step)
                    context.posePalettes.Add(clips[c].palettes[i]);
                if ((clips[c].palettes.Length - 1) % step != 0)
                    context.posePalettes.Add(clips[c].palettes[clips[c].palettes.Length - 1]);
            }
        }

        static ClusterSkinnedBoneCurves FitBoneCurves(float[] times, Vector3[] positions, Quaternion[] rotations, Vector3[] scales)
        {
            return new ClusterSkinnedBoneCurves
            {
                positionX = FitCurve(times, i => positions[i].x, 1e-4f),
                positionY = FitCurve(times, i => positions[i].y, 1e-4f),
                positionZ = FitCurve(times, i => positions[i].z, 1e-4f),
                rotationX = FitCurve(times, i => rotations[i].x, 1e-4f),
                rotationY = FitCurve(times, i => rotations[i].y, 1e-4f),
                rotationZ = FitCurve(times, i => rotations[i].z, 1e-4f),
                rotationW = FitCurve(times, i => rotations[i].w, 1e-4f),
                scaleX = FitCurve(times, i => scales[i].x, 1e-4f),
                scaleY = FitCurve(times, i => scales[i].y, 1e-4f),
                scaleZ = FitCurve(times, i => scales[i].z, 1e-4f)
            };
        }

        static AnimationCurve FitCurve(float[] times, Func<int, float> sample, float tolerance)
        {
            int count = times.Length;
            var keep = new bool[count];
            keep[0] = true;
            keep[count - 1] = true;
            ReduceCurve(times, sample, 0, count - 1, tolerance, keep);
            var keys = new List<Keyframe>();
            for (int i = 0; i < count; i++)
            {
                if (keep[i]) keys.Add(new Keyframe(times[i], sample(i)));
            }
            var curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++)
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            for (int i = 0; i < curve.length; i++)
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            return curve;
        }

        static void ReduceCurve(float[] times, Func<int, float> sample, int first, int last, float tolerance, bool[] keep)
        {
            if (last <= first + 1) return;
            float dt = times[last] - times[first];
            float a = sample(first);
            float b = sample(last);
            int worst = -1;
            float worstError = tolerance;
            for (int i = first + 1; i < last; i++)
            {
                float t = dt > 1e-8f ? (times[i] - times[first]) / dt : 0f;
                float error = Mathf.Abs(sample(i) - Mathf.Lerp(a, b, t));
                if (error > worstError)
                {
                    worstError = error;
                    worst = i;
                }
            }
            if (worst < 0) return;
            keep[worst] = true;
            ReduceCurve(times, sample, first, worst, tolerance, keep);
            ReduceCurve(times, sample, worst, last, tolerance, keep);
        }

        static void BuildCullFrames(ClusterMeshBakeResult geometry, List<ClusterSkinWeight> skin, SampledClip clip,
            List<ClusterSkinnedCullFrame> destination)
        {
            int clusterCount = geometry.clusters.Length;
            for (int segment = 0; segment < clip.clip.segmentCount; segment++)
            {
                float start = clip.clip.duration * segment / clip.clip.segmentCount;
                float end = clip.clip.duration * (segment + 1) / clip.clip.segmentCount;
                for (int cluster = 0; cluster < clusterCount; cluster++)
                    destination.Add(BuildCullFrame(geometry, skin, cluster, clip, start, end));
            }
        }

        static ClusterSkinnedCullFrame BuildCullFrame(ClusterMeshBakeResult geometry, List<ClusterSkinWeight> skin,
            int clusterIndex, SampledClip clip, float start, float end)
        {
            ClusterHeader header = geometry.clusters[clusterIndex];
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 normalSum = Vector3.zero;
            var normals = new List<Vector3>();
            bool sampledAny = false;
            for (int frame = 0; frame < clip.times.Length; frame++)
            {
                if (clip.times[frame] + 1e-6f < start || clip.times[frame] - 1e-6f > end)
                    continue;
                sampledAny = true;
                AccumulateCullSample(geometry, skin, header, clip.palettes[frame], ref min, ref max, normalSum, normals);
            }
            if (!sampledAny)
            {
                int frame = NearestFrame(clip.times, (start + end) * 0.5f);
                AccumulateCullSample(geometry, skin, header, clip.palettes[frame], ref min, ref max, normalSum, normals);
            }
            for (int i = 0; i < normals.Count; i++) normalSum += normals[i];
            Vector3 center = (min + max) * 0.5f;
            Vector3 axis = normalSum.sqrMagnitude > 1e-12f ? normalSum.normalized : Vector3.up;
            float cutoff = 1f;
            for (int i = 0; i < normals.Count; i++) cutoff = Mathf.Min(cutoff, Vector3.Dot(axis, normals[i]));
            if (normalSum.sqrMagnitude <= 1e-12f || cutoff < 0f) cutoff = -1f;
            return new ClusterSkinnedCullFrame
            {
                aabbCenter = center, aabbExtents = (max - min) * 0.5f,
                coneAxisCutoff = new Vector4(axis.x, axis.y, axis.z, cutoff), coneApex = center
            };
        }

        static void AccumulateCullSample(ClusterMeshBakeResult geometry, List<ClusterSkinWeight> skin,
            ClusterHeader header, Matrix4x4[] palette, ref Vector3 min, ref Vector3 max,
            Vector3 unusedNormalSum, List<Vector3> normals)
        {
            int count = (int)header.vertexCount;
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int vertex = (int)header.vertexOffset + i;
                positions[i] = SkinPosition(geometry.vertices[vertex].position, skin[vertex], palette);
                min = Vector3.Min(min, positions[i]);
                max = Vector3.Max(max, positions[i]);
            }
            for (int triangle = 0; triangle < header.triangleCount; triangle++)
            {
                int offset = (int)header.indexOffset + triangle * 3;
                Vector3 normal = Vector3.Cross(
                    positions[(int)geometry.indices[offset + 1]] - positions[(int)geometry.indices[offset]],
                    positions[(int)geometry.indices[offset + 2]] - positions[(int)geometry.indices[offset]]);
                if (normal.sqrMagnitude > 1e-12f) normals.Add(normal.normalized);
            }
        }

        static int NearestFrame(float[] times, float target)
        {
            int best = 0;
            float distance = float.MaxValue;
            for (int i = 0; i < times.Length; i++)
            {
                float d = Mathf.Abs(times[i] - target);
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }

        static Vector3 SkinPosition(Vector3 position, in ClusterSkinWeight skin, Matrix4x4[] palette)
        {
            Vector3 result = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                float weight = skin.GetWeight(i);
                if (weight > 0f) result += palette[skin.GetBoneIndex(i)].MultiplyPoint3x4(position) * weight;
            }
            return result;
        }

        static void Decompose(Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = matrix.GetColumn(3);
            Vector3 right = matrix.GetColumn(0);
            Vector3 up = matrix.GetColumn(1);
            Vector3 forward = matrix.GetColumn(2);
            scale = new Vector3(right.magnitude, up.magnitude, forward.magnitude);
            if (Vector3.Dot(Vector3.Cross(right, up), forward) < 0f) scale.x = -scale.x;
            if (Mathf.Abs(scale.x) > 1e-8f) right /= scale.x;
            if (Mathf.Abs(scale.y) > 1e-8f) up /= scale.y;
            if (Mathf.Abs(scale.z) > 1e-8f) forward /= scale.z;
            rotation = forward.sqrMagnitude > 1e-12f && up.sqrMagnitude > 1e-12f
                ? Quaternion.LookRotation(forward, up) : Quaternion.identity;
        }

        static Vector4[] RebuildTangents(Vector3[] positions, Vector3[] normals, Vector2[] uvs, Mesh mesh)
        {
            var result = new Vector4[positions.Length];
            for (int i = 0; i < result.Length; i++)
            {
                Vector3 normal = normals != null && i < normals.Length ? normals[i] : Vector3.up;
                Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
                result[i] = new Vector4(tangent.x, tangent.y, tangent.z, 1f);
            }
            return result;
        }
    }
}
