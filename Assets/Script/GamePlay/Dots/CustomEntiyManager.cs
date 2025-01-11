using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;

namespace SOC.GamePlay
{

    public class TestRotJob : IJobParallelForTransform
    {
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
