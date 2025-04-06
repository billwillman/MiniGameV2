using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Unity.Netcode;
using KinematicCharacterController;
using Animancer;
using System.Collections.Generic;
using System;
using XLua;

namespace SOC.GamePlay
{
    // 加上这个可以在LUA覆写方法
    [XLua.LuaCallCSharp]
    public class MoeCharacterController : MonoBehaviour, /*NetworkBehaviour,*/ ICharacterController
    {
        private KinematicCharacterMotor m_CharacterMotor = null;
        public AnimancerComponent[] m_Animancer = null; // 从Body上获取

        public SkinnedMeshRenderer m_Head = null;
        public SkinnedMeshRenderer m_Body = null;
        public SkinnedMeshRenderer m_Weapon = null;
        public SkinnedMeshRenderer m_Hair = null;

        public Vector3 CurrentVelocity
        {
            get
            {
                return m_CharacterMotor.BaseVelocity;
            }

            set
            {
                m_CharacterMotor.BaseVelocity = value;
            }
        }

        public List<SkinnedMeshRenderer> m_OtherSkinedMeshList = null;

        void Awake() {
            InitSkinMesh();
            m_CharacterMotor = GetComponent<KinematicCharacterMotor>();
            if (m_CharacterMotor != null)
                m_CharacterMotor.CharacterController = this;
        }

        public KinematicCharacterMotor Motor
        {
            get
            {
                return m_CharacterMotor;
            }
        }

        void InitSkinMesh() {
           
        }

        [CSharpCallLua]
        public Func<Quaternion, float, Quaternion> onUpdateRotation
        {
            get;
            set;
        }

        [CSharpCallLua]
        public Func<Vector3, float, Vector3> onUpdateVelocity
        {
            get;
            set;
        }

        [CSharpCallLua]
        public Action<float> onBeforeCharacterUpdate
        {
            get;
            set;
        }

        [CSharpCallLua]
        public Action<float> onPostGroundingUpdate
        {
            get;
            set;
        }

        [CSharpCallLua]
        public Action<float> onAfterCharacterUpdate
        {
            get;
            set;
        }

        [CSharpCallLua]
        public Func<Collider, bool> onIsColliderValidForCollisions
        {
            get;
            set;
        }

        // ------------------------------------------- Collision and Slide 移动控制相关 ---------------------------------------- //
        /// <summary>
        /// This is called when the motor wants to know what its rotation should be right now
        /// </summary>
        virtual public void UpdateRotation(ref Quaternion currentRotation, float deltaTime) {
            if (onUpdateRotation != null)
                currentRotation = onUpdateRotation(currentRotation, deltaTime);
        }
        /// <summary>
        /// This is called when the motor wants to know what its velocity should be right now
        /// </summary>
        virtual public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime) {
            if (onUpdateVelocity != null)
                currentVelocity = onUpdateVelocity(currentVelocity, deltaTime);
        }
        /// <summary>
        /// This is called before the motor does anything
        /// </summary>
        virtual public void BeforeCharacterUpdate(float deltaTime) {
            if (onBeforeCharacterUpdate != null)
                onBeforeCharacterUpdate(deltaTime);
        }
        /// <summary>
        /// This is called after the motor has finished its ground probing, but before PhysicsMover/Velocity/etc.... handling
        /// </summary>
        virtual public void PostGroundingUpdate(float deltaTime) {
            if (onPostGroundingUpdate != null)
                onPostGroundingUpdate(deltaTime);
        }
        /// <summary>
        /// This is called after the motor has finished everything in its update
        /// </summary>
        virtual public void AfterCharacterUpdate(float deltaTime) {
            if (onAfterCharacterUpdate != null)
                onAfterCharacterUpdate(deltaTime);
        }
        /// <summary>
        /// This is called after when the motor wants to know if the collider can be collided with (or if we just go through it)
        /// </summary>
        virtual public bool IsColliderValidForCollisions(Collider coll) {
            if (onIsColliderValidForCollisions != null)
                return onIsColliderValidForCollisions(coll);
            return true;
        }
        /// <summary>
        /// This is called when the motor's ground probing detects a ground hit
        /// </summary>
        virtual public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) {
            if (OnGroundHitEvent != null)
                hitStabilityReport = OnGroundHitEvent(hitCollider, hitNormal, hitPoint, hitStabilityReport);
        }
        /// <summary>
        /// This is called when the motor's movement logic detects a hit
        /// </summary>
        virtual public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) {
            if (OnMovementHitEvent != null)
                hitStabilityReport = OnMovementHitEvent(hitCollider, hitNormal, hitPoint, hitStabilityReport);
        }
        /// <summary>
        /// This is called after every move hit, to give you an opportunity to modify the HitStabilityReport to your liking
        /// </summary>
        virtual public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) {
            if (OnProcessHitStabilityReport != null)
                hitStabilityReport = OnProcessHitStabilityReport(hitCollider, hitNormal, hitPoint, atCharacterPosition, atCharacterRotation, hitStabilityReport);
        }
        /// <summary>
        /// This is called when the character detects discrete collisions (collisions that don't result from the motor's capsuleCasts when moving)
        /// </summary>
        virtual public void OnDiscreteCollisionDetected(Collider hitCollider) {
            if (OnDiscreteCollisionDetectedEvent != null)
                OnDiscreteCollisionDetectedEvent(hitCollider);
        }
        // ----------------------------------------------------------------------------------------------------------------------------- //

        public Func<Collider, Vector3, Vector3, HitStabilityReport, HitStabilityReport> OnGroundHitEvent
        {
            get;
            set;
        }

        public Func<Collider, Vector3, Vector3, HitStabilityReport, HitStabilityReport> OnMovementHitEvent
        {
            get;
            set;
        }

        public Action<Collider> OnDiscreteCollisionDetectedEvent
        {
            get;
            set;
        }

        public Func<Collider, Vector3, Vector3, Vector3, Quaternion, HitStabilityReport, HitStabilityReport> OnProcessHitStabilityReport
        {
            get;
            set;
        }
    }
}
