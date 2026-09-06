using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshEditModeTickTests
    {
        readonly List<Object> _trash = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            ClusterMeshSceneBatcher.ResetForTests();
            ClusterMeshLifetime.SyncEditModeTick();
            for (int i = 0; i < _trash.Count; i++)
            {
                if (_trash[i] != null)
                    Object.DestroyImmediate(_trash[i]);
            }

            _trash.Clear();
        }

        [Test]
        public void ShouldQueuePlayerLoop_Zero_IsFalse()
        {
            Assert.That(ClusterMeshLifetime.ShouldQueuePlayerLoop(0), Is.False);
        }

        [Test]
        public void ShouldQueuePlayerLoop_One_IsTrue()
        {
            Assert.That(ClusterMeshLifetime.ShouldQueuePlayerLoop(1), Is.True);
        }

        [Test]
        public void RegisteredCount_Empty_IsZero()
        {
            ClusterMeshSceneBatcher.ResetForTests();
            Assert.That(ClusterMeshSceneBatcher.RegisteredCount, Is.EqualTo(0));
        }

        [Test]
        public void RegisteredCount_OneRegistered_IsOne()
        {
            ExpectCapabilityErrorIfUnsupported();
            var renderer = CreateRenderer();
            ClusterMeshSceneBatcher.Register(renderer);
            Assert.That(ClusterMeshSceneBatcher.RegisteredCount, Is.EqualTo(1));
        }

        [Test]
        public void RegisteredCount_DestroyedWithoutUnregister_IsZero()
        {
            ExpectCapabilityErrorIfUnsupported();
            var renderer = CreateRenderer();
            ClusterMeshSceneBatcher.Register(renderer);
            Object.DestroyImmediate(renderer.gameObject);
            Assert.That(ClusterMeshSceneBatcher.RegisteredCount, Is.EqualTo(0));
        }

        [Test]
        public void SyncEditModeTick_False_SetsActive()
        {
            ClusterMeshLifetime.SyncEditModeTick(true);
            ClusterMeshLifetime.SyncEditModeTick(false);
            Assert.That(ClusterMeshLifetime.EditModeTickActive, Is.True);
        }

        [Test]
        public void SyncEditModeTick_True_ClearsActive()
        {
            ClusterMeshLifetime.SyncEditModeTick(false);
            ClusterMeshLifetime.SyncEditModeTick(true);
            Assert.That(ClusterMeshLifetime.EditModeTickActive, Is.False);
        }

        [Test]
        public void SyncEditModeTick_FalseTwice_StaysActive()
        {
            ClusterMeshLifetime.SyncEditModeTick(false);
            ClusterMeshLifetime.SyncEditModeTick(false);
            Assert.That(ClusterMeshLifetime.EditModeTickActive, Is.True);
        }

        static void ExpectCapabilityErrorIfUnsupported()
        {
            string reason = ClusterMeshCapability.GetUnsupportedReason();
            if (reason != null)
                LogAssert.Expect(LogType.Error, "ClusterMesh: " + reason);
        }

        ClusterMeshRenderer CreateRenderer()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            _trash.Add(mesh);
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());
            _trash.Add(asset);
            var go = new GameObject("CMTickRenderer");
            go.SetActive(false);
            _trash.Add(go);
            var renderer = go.AddComponent<ClusterMeshRenderer>();
            renderer.asset = asset;
            renderer.cullShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            renderer.litShader = Shader.Find("ClusterMesh/Lit");
            return renderer;
        }
    }
}
