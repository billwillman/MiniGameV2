using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ClusterMesh
{
    public enum ClusterSkinnedAnimationEvaluation
    {
        GpuTexture = 0,
        CpuCurves = 1
    }

    public enum ClusterSkinnedAnimationDataMode
    {
        GpuOnly = 0,
        CpuOnly = 1,
        GpuAndCpu = 2
    }

    [Serializable]
    public sealed class ClusterSkinnedMeshBakeOptions
    {
        public ClusterSkinnedAnimationDataMode animationDataMode = ClusterSkinnedAnimationDataMode.GpuOnly;
        public bool retainAnimationCurves;
        [Range(1f, 60f)] public float gpuFramesPerSecond = 30f;
        [Range(0.000001f, 0.01f)] public float cpuCurveTolerance = 0.0001f;

        public bool IncludesGpu => animationDataMode != ClusterSkinnedAnimationDataMode.CpuOnly;
        public bool IncludesCpu => animationDataMode != ClusterSkinnedAnimationDataMode.GpuOnly;
    }

    [Serializable]
    public struct ClusterSkinWeight
    {
        public int boneIndex0;
        public int boneIndex1;
        public int boneIndex2;
        public int boneIndex3;
        public float weight0;
        public float weight1;
        public float weight2;
        public float weight3;

        public int GetBoneIndex(int index)
        {
            switch (index)
            {
                case 0: return boneIndex0;
                case 1: return boneIndex1;
                case 2: return boneIndex2;
                default: return boneIndex3;
            }
        }

        public float GetWeight(int index)
        {
            switch (index)
            {
                case 0: return weight0;
                case 1: return weight1;
                case 2: return weight2;
                default: return weight3;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterPackedSkinWeight
    {
        public uint boneIndices01;
        public uint boneIndices23;
        public uint boneWeights01;
        public uint boneWeights23;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterSkinnedCurveHeader
    {
        public int segmentOffset;
        public int segmentCount;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterSkinnedCurveSegment
    {
        public Vector4 coefficients;
        public float startTime;
        public float inverseDuration;
    }

    [Serializable]
    public sealed class ClusterSkinnedBoneCurves
    {
        public AnimationCurve positionX = new AnimationCurve();
        public AnimationCurve positionY = new AnimationCurve();
        public AnimationCurve positionZ = new AnimationCurve();
        public AnimationCurve rotationX = new AnimationCurve();
        public AnimationCurve rotationY = new AnimationCurve();
        public AnimationCurve rotationZ = new AnimationCurve();
        public AnimationCurve rotationW = new AnimationCurve();
        public AnimationCurve scaleX = new AnimationCurve();
        public AnimationCurve scaleY = new AnimationCurve();
        public AnimationCurve scaleZ = new AnimationCurve();

        public Matrix4x4 Evaluate(float time)
        {
            Vector3 position = new Vector3(
                positionX.Evaluate(time),
                positionY.Evaluate(time),
                positionZ.Evaluate(time));
            Quaternion rotation = new Quaternion(
                rotationX.Evaluate(time),
                rotationY.Evaluate(time),
                rotationZ.Evaluate(time),
                rotationW.Evaluate(time));
            if (rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w < 1e-12f)
                rotation = Quaternion.identity;
            else
                rotation.Normalize();
            Vector3 scale = new Vector3(
                scaleX.Evaluate(time),
                scaleY.Evaluate(time),
                scaleZ.Evaluate(time));
            return Matrix4x4.TRS(position, rotation, scale);
        }
    }

    [Serializable]
    public sealed class ClusterSkinnedClip
    {
        public string name;
        public float duration;
        public float frameRate;
        public int segmentCount;
        public int cullFrameOffset;
        public int cpuCurveHeaderOffset;
        public ClusterSkinnedBoneCurves[] boneCurves;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterSkinnedCullFrame
    {
        public Vector4 aabbCenter;
        public Vector4 aabbExtents;
        public Vector4 coneAxisCutoff;
        public Vector4 coneApex;
    }

    public static class ClusterSkinnedAnimation
    {
        public static bool EvaluatePalette(
            ClusterSkinnedMeshAsset asset,
            int clipIndex,
            float normalizedTime,
            Matrix4x4[] destination)
        {
            if (asset == null || asset.clips == null || clipIndex < 0 || clipIndex >= asset.clips.Length)
                return false;
            int boneCount = asset.bindPoses != null ? asset.bindPoses.Length : 0;
            if (boneCount == 0 || destination == null || destination.Length < boneCount ||
                asset.boneParentIndices == null || asset.boneParentIndices.Length != boneCount)
                return false;

            ClusterSkinnedClip clip = asset.clips[clipIndex];
            if (clip == null)
                return false;
            float duration = Mathf.Max(0f, clip.duration);
            return EvaluatePaletteAtTime(
                asset, clipIndex, Mathf.Clamp01(normalizedTime) * duration, destination);
        }

        static bool EvaluateGlobal(
            int bone,
            ClusterSkinnedClip clip,
            int[] parents,
            Matrix4x4[] globals,
            byte[] states)
        {
            if (states[bone] == 2)
                return true;
            if (states[bone] == 1)
                return false;
            states[bone] = 1;

            ClusterSkinnedBoneCurves curves = clip.boneCurves[bone];
            if (curves == null)
                return false;
            float time = s_EvaluationTime;
            Matrix4x4 local = curves.Evaluate(time);
            int parent = parents[bone];
            if (parent >= 0)
            {
                if (parent >= parents.Length || !EvaluateGlobal(parent, clip, parents, globals, states))
                    return false;
                globals[bone] = globals[parent] * local;
            }
            else
            {
                globals[bone] = local;
            }

            states[bone] = 2;
            return true;
        }

        [ThreadStatic] static float s_EvaluationTime;

        public static bool EvaluatePaletteAtTime(
            ClusterSkinnedMeshAsset asset,
            int clipIndex,
            float time,
            Matrix4x4[] destination)
        {
            if (asset == null || asset.clips == null || clipIndex < 0 || clipIndex >= asset.clips.Length)
                return false;
            ClusterSkinnedClip clip = asset.clips[clipIndex];
            float duration = clip != null ? Mathf.Max(0f, clip.duration) : 0f;
            float clampedTime = duration > 0f ? Mathf.Clamp(time, 0f, duration) : 0f;
            if (!asset.HasManagedCurves(clipIndex))
                return EvaluatePackedPalette(asset, clipIndex, clampedTime, destination);
            s_EvaluationTime = clampedTime;
            try
            {
                return EvaluatePaletteInternal(asset, clipIndex, destination);
            }
            finally
            {
                s_EvaluationTime = 0f;
            }
        }

        static bool EvaluatePackedPalette(ClusterSkinnedMeshAsset asset, int clipIndex, float time,
            Matrix4x4[] destination)
        {
            if (!asset.HasCpuBurstCurves(clipIndex) || destination == null ||
                destination.Length < asset.bindPoses.Length)
                return false;
            int boneCount = asset.bindPoses.Length;
            ClusterSkinnedClip clip = asset.clips[clipIndex];
            var globals = new Matrix4x4[boneCount];
            for (int orderIndex = 0; orderIndex < boneCount; orderIndex++)
            {
                int bone = asset.boneEvaluationOrder[orderIndex];
                int curve = clip.cpuCurveHeaderOffset + bone * 10;
                Vector3 position = new Vector3(
                    EvaluatePackedCurve(asset, curve, time),
                    EvaluatePackedCurve(asset, curve + 1, time),
                    EvaluatePackedCurve(asset, curve + 2, time));
                Quaternion rotation = new Quaternion(
                    EvaluatePackedCurve(asset, curve + 3, time),
                    EvaluatePackedCurve(asset, curve + 4, time),
                    EvaluatePackedCurve(asset, curve + 5, time),
                    EvaluatePackedCurve(asset, curve + 6, time));
                if (rotation.x * rotation.x + rotation.y * rotation.y +
                    rotation.z * rotation.z + rotation.w * rotation.w < 1e-12f)
                    rotation = Quaternion.identity;
                else
                    rotation.Normalize();
                Vector3 scale = new Vector3(
                    EvaluatePackedCurve(asset, curve + 7, time),
                    EvaluatePackedCurve(asset, curve + 8, time),
                    EvaluatePackedCurve(asset, curve + 9, time));
                Matrix4x4 local = Matrix4x4.TRS(position, rotation, scale);
                int parent = asset.boneParentIndices[bone];
                globals[bone] = parent >= 0 ? globals[parent] * local : local;
                destination[bone] = globals[bone] * asset.bindPoses[bone];
            }
            return true;
        }

        static float EvaluatePackedCurve(ClusterSkinnedMeshAsset asset, int headerIndex, float time)
        {
            ClusterSkinnedCurveHeader header = asset.cpuCurveHeaders[headerIndex];
            int first = header.segmentOffset;
            int count = Mathf.Max(1, header.segmentCount);
            int low = 0;
            int high = count - 1;
            while (low < high)
            {
                int middle = (low + high + 1) >> 1;
                if (asset.cpuCurveSegments[first + middle].startTime <= time)
                    low = middle;
                else
                    high = middle - 1;
            }
            ClusterSkinnedCurveSegment segment = asset.cpuCurveSegments[first + low];
            float u = segment.inverseDuration > 0f
                ? Mathf.Clamp01((time - segment.startTime) * segment.inverseDuration)
                : 0f;
            Vector4 c = segment.coefficients;
            return ((c.w * u + c.z) * u + c.y) * u + c.x;
        }

        static bool EvaluatePaletteInternal(ClusterSkinnedMeshAsset asset, int clipIndex, Matrix4x4[] destination)
        {
            int boneCount = asset.bindPoses != null ? asset.bindPoses.Length : 0;
            if (boneCount == 0 || destination == null || destination.Length < boneCount ||
                asset.boneParentIndices == null || asset.boneParentIndices.Length != boneCount)
                return false;
            ClusterSkinnedClip clip = asset.clips[clipIndex];
            if (clip == null || clip.boneCurves == null || clip.boneCurves.Length != boneCount)
                return false;
            var globals = new Matrix4x4[boneCount];
            var states = new byte[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                if (!EvaluateGlobal(i, clip, asset.boneParentIndices, globals, states))
                    return false;
            }
            for (int i = 0; i < boneCount; i++)
                destination[i] = globals[i] * asset.bindPoses[i];
            return true;
        }
    }
}
