#ifndef CLUSTERMESH_MOTION_VECTORS_INCLUDED
#define CLUSTERMESH_MOTION_VECTORS_INCLUDED

// Tuanjie / URP 14 does not ship MotionVectorsCommon.hlsl (Unity 6+).
// Match Hidden/Universal Render Pipeline/ObjectMotionVectors.
float2 CalcNdcMotionVectorFromCsPositions(float4 positionCSNoJitter, float4 previousPositionCSNoJitter)
{
    float2 posNDC = positionCSNoJitter.xy * rcp(positionCSNoJitter.w);
    float2 prevPosNDC = previousPositionCSNoJitter.xy * rcp(previousPositionCSNoJitter.w);
    float2 velocity = posNDC - prevPosNDC;
#if UNITY_UV_STARTS_AT_TOP
    velocity.y = -velocity.y;
#endif
    return velocity * 0.5;
}

#endif
