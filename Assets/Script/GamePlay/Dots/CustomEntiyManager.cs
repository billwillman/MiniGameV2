using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;

namespace SOC.GamePlay
{

    [BurstCompile]
    public struct TestRotJob : IJobParallelForTransform
    {
        [ReadOnly] public float deltaTime;

        [BurstCompile]
        public void Execute(int index, TransformAccess transform) {
            transform.localPosition += Vector3.forward * deltaTime;
        }
    }

    public class CustomEntiyManager : ILuaBinder
    {
        public GameObject m_InstanceObject = null;
        public override void OnUpdate() {
            
        }
    }

}
