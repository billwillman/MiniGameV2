using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ClusterMesh
{
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
            if (clip == null || clip.boneCurves == null || clip.boneCurves.Length != boneCount)
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
            s_EvaluationTime = duration > 0f ? Mathf.Clamp(time, 0f, duration) : 0f;
            try
            {
                return EvaluatePaletteInternal(asset, clipIndex, destination);
            }
            finally
            {
                s_EvaluationTime = 0f;
            }
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
