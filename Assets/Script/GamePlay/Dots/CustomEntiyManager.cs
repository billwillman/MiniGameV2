using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;

namespace SOC.GamePlay
{
    [BurstCompile]
    public struct TestMyJob : IJobParallelFor
    {
        public NativeArray<float3> transformPosArray;
        [ReadOnly] public float deltaTime;
        [ReadOnly] static readonly float3 forward = new float3(0, 0, 1);

        [BurstCompile]
        public void Execute(int index) {
            //  for (int i = 0; i < 1000000; ++i) {
            float3 transformPos = transformPosArray[index] + forward * deltaTime;
            transformPosArray[index] = transformPos;
              //}
        }
    }
    [BurstCompile]
    public struct TestRotJob : IJobParallelForTransform
    {
        [ReadOnly] public float deltaTime;

        [BurstCompile]
        public void Execute(int index, TransformAccess transform) {
            for (int i = 0; i < 1000000; ++i) {
                transform.localPosition += Vector3.forward * deltaTime;
            }
            
        }
    }

    public class CustomEntiyManager : ILuaBinder
    {
        public Transform[] m_InstanceObject = null;
        private TransformAccessArray m_TransAccesArray;
        public override void OnUpdate() {
            /*
            if (m_TransAccesArray.isCreated && m_TransAccesArray.length > 0) {
                if (m_JobHandle.IsCompleted) {
                    Debug.Log("Start Job");
                    var job = new TestRotJob();
                    job.deltaTime = Time.deltaTime;
                    m_JobHandle = job.Schedule(m_TransAccesArray); 
                } else {
                    Debug.LogWarning("Job Running"); // 必然不会执行，因为transform是在主线程，需要同步到主线程
                }
            }
            */
            if (m_InstanceObject != null && m_InstanceObject.Length > 0) {
                NativeArray<float3> transPosArray = new NativeArray<float3>(m_InstanceObject.Length, Allocator.TempJob);
                for (int i = 0; i < transPosArray.Length; ++i) {
                    transPosArray[i] = m_InstanceObject[i].position;
                }

                var job = new TestMyJob() {
                    deltaTime = Time.deltaTime,
                    transformPosArray = transPosArray,
                };
                var handle = job.Schedule(transPosArray.Length, 1);
                handle.Complete();
                for (int i = 0; i < transPosArray.Length; ++i) {
                    m_InstanceObject[i].position = transPosArray[i];
                }
                transPosArray.Dispose();
            }
        }

        public override void OnAwake() {
            base.OnAwake();
            if (m_InstanceObject != null) {
                Transform[] transs = new Transform[m_InstanceObject.Length];
                for (int i = 0; i < m_InstanceObject.Length; ++i) {
                    var trans = m_InstanceObject[i];
                    transs[i] = trans;
                }
                m_TransAccesArray = new TransformAccessArray(transs);
            }
        }

        public override void OnDestroyed() {
            base.OnDestroyed();
            if (m_TransAccesArray.isCreated) {
                m_TransAccesArray.Dispose();
            }
        }
    }

}
