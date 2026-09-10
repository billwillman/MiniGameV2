using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace ClusterMesh
{
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    internal struct ClusterSkinnedPaletteJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ClusterSkinnedCurveHeader> headers;
        [ReadOnly] public NativeArray<ClusterSkinnedCurveSegment> segments;
        [ReadOnly] public NativeArray<int> parents;
        [ReadOnly] public NativeArray<int> evaluationOrder;
        [ReadOnly] public NativeArray<float4x4> bindPoses;
        [ReadOnly] public NativeArray<float> normalizedTimes;
        // Each parallel index owns [objectIndex * stride, (objectIndex + 1) * stride).
        // The ranges never overlap, but IJobParallelFor cannot infer this ownership.
        [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> globalScratch;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4> palettePixels;
        public int curveHeaderOffset;
        public int boneCount;
        public int paletteWidth;
        public float duration;

        public void Execute(int objectIndex)
        {
            float time = math.saturate(normalizedTimes[objectIndex]) * math.max(0f, duration);
            int matrixBase = objectIndex * boneCount;
            int pixelBase = objectIndex * paletteWidth;
            for (int orderIndex = 0; orderIndex < boneCount; orderIndex++)
            {
                int bone = evaluationOrder[orderIndex];
                int curve = curveHeaderOffset + bone * 10;
                float3 position = new float3(
                    Evaluate(curve, time), Evaluate(curve + 1, time), Evaluate(curve + 2, time));
                float4 rotationValue = new float4(
                    Evaluate(curve + 3, time), Evaluate(curve + 4, time),
                    Evaluate(curve + 5, time), Evaluate(curve + 6, time));
                float rotationLength = math.lengthsq(rotationValue);
                rotationValue = rotationLength > 1e-12f
                    ? rotationValue * math.rsqrt(rotationLength)
                    : new float4(0f, 0f, 0f, 1f);
                float3 scale = new float3(
                    Evaluate(curve + 7, time), Evaluate(curve + 8, time), Evaluate(curve + 9, time));

                float4x4 local = float4x4.TRS(position, new quaternion(rotationValue), scale);
                int parent = parents[bone];
                float4x4 global = parent >= 0
                    ? math.mul(globalScratch[matrixBase + parent], local)
                    : local;
                globalScratch[matrixBase + bone] = global;
                float4x4 palette = math.mul(global, bindPoses[bone]);
                int pixel = pixelBase + bone * 3;
                palettePixels[pixel] = new float4(palette.c0.x, palette.c1.x, palette.c2.x, palette.c3.x);
                palettePixels[pixel + 1] = new float4(palette.c0.y, palette.c1.y, palette.c2.y, palette.c3.y);
                palettePixels[pixel + 2] = new float4(palette.c0.z, palette.c1.z, palette.c2.z, palette.c3.z);
            }
        }

        float Evaluate(int headerIndex, float time)
        {
            ClusterSkinnedCurveHeader header = headers[headerIndex];
            int first = header.segmentOffset;
            int count = math.max(1, header.segmentCount);
            int low = 0;
            int high = count - 1;
            while (low < high)
            {
                int middle = (low + high + 1) >> 1;
                if (segments[first + middle].startTime <= time)
                    low = middle;
                else
                    high = middle - 1;
            }
            ClusterSkinnedCurveSegment segment = segments[first + low];
            float u = segment.inverseDuration > 0f
                ? math.saturate((time - segment.startTime) * segment.inverseDuration)
                : 0f;
            float c0 = segment.coefficients.x;
            float c1 = segment.coefficients.y;
            float c2 = segment.coefficients.z;
            float c3 = segment.coefficients.w;
            return ((c3 * u + c2) * u + c1) * u + c0;
        }
    }
}
