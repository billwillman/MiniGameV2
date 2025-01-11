using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Burst;

namespace SOC.GamePlay
{

    [BurstCompile]
    public class TestRotJob : IJobParallelForTransform
    {
        [BurstCompile]
        public void Execute(int index, TransformAccess transform) {
          
        }
    }

    public class CustomEntiyManager : ILuaBinder
    {
        public GameObject m_InstanceObject = null;
        public override void OnUpdate() {
            
        }
    }

}
