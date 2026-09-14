using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshBatcherTests
    {
        readonly List<Object> _trash = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            ClusterMeshSceneBatcher.ResetForTests();
            for (int i = 0; i < _trash.Count; i++)
            {
                if (_trash[i] != null)
                    Object.DestroyImmediate(_trash[i]);
            }

            _trash.Clear();
        }

        [Test]
        public void PackVisibleId_RoundTripsObjectAndCluster()
        {
            uint packed = ClusterMeshLimits.PackVisibleId(10, 3);
            ClusterMeshLimits.UnpackVisibleId(packed, out int objectIndex, out int clusterIndex);
            Assert.That(objectIndex, Is.EqualTo(10));
            Assert.That(clusterIndex, Is.EqualTo(3));
            Assert.That(packed, Is.EqualTo((10u << 16) | 3u));
        }

        [Test]
        public void DisposeCachedContexts_Twice_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                ClusterMeshSceneBatcher.DisposeCachedContexts();
                ClusterMeshSceneBatcher.DisposeCachedContexts();
            });
        }

        [Test]
        public void CountDrawCalls_MatchesMaterialAndChunkRules()
        {
            Assert.That(ClusterMeshSceneBatcher.CountDrawCalls(10, 1), Is.EqualTo(1));
            Assert.That(ClusterMeshSceneBatcher.CountDrawCalls(10, 2), Is.EqualTo(2));
            Assert.That(ClusterMeshSceneBatcher.CountDrawCalls(256, 1), Is.EqualTo(1));
            Assert.That(ClusterMeshSceneBatcher.CountDrawCalls(257, 1), Is.EqualTo(2));
            Assert.That(ClusterMeshSceneBatcher.CountDrawCalls(0, 1), Is.EqualTo(0));
        }

        [Test]
        public void CollectBatches_TenSameAssetSameCamera_OneDraw()
        {
            var asset = CreateAsset();
            var camera = CreateCamera("CMBatchCam");
            var list = new List<ClusterMeshRenderer>();
            for (int i = 0; i < 10; i++)
                list.Add(CreateRenderer(asset, camera));

            var dest = new List<ClusterMeshBatchDesc>();
            ClusterMeshSceneBatcher.CollectBatches(list, dest);
            Assert.That(dest.Count, Is.EqualTo(1));
            Assert.That(dest[0].objectCount, Is.EqualTo(10));
            Assert.That(dest[0].drawCallCount, Is.EqualTo(1));
            Assert.That(dest[0].asset, Is.SameAs(asset));
            Assert.That(dest[0].camera, Is.SameAs(camera));
        }

        [Test]
        public void CollectBatches_TwoAssets_TwoBatches()
        {
            var camera = CreateCamera("CMBatchCam2");
            var list = new List<ClusterMeshRenderer>
            {
                CreateRenderer(CreateAsset(), camera),
                CreateRenderer(CreateAsset(), camera)
            };

            var dest = new List<ClusterMeshBatchDesc>();
            ClusterMeshSceneBatcher.CollectBatches(list, dest);
            Assert.That(dest.Count, Is.EqualTo(2));
            Assert.That(dest[0].objectCount, Is.EqualTo(1));
            Assert.That(dest[1].objectCount, Is.EqualTo(1));
        }

        [Test]
        public void CollectBatches_TwoCameras_TwoBatches()
        {
            var asset = CreateAsset();
            var list = new List<ClusterMeshRenderer>
            {
                CreateRenderer(asset, CreateCamera("CMCamA")),
                CreateRenderer(asset, CreateCamera("CMCamB"))
            };

            var dest = new List<ClusterMeshBatchDesc>();
            ClusterMeshSceneBatcher.CollectBatches(list, dest);
            Assert.That(dest.Count, Is.EqualTo(2));
        }

        [Test]
        public void Unregister_RemovesRendererFromRegisteredBatches()
        {
            string reason = ClusterMeshCapability.GetUnsupportedReason();
            if (reason != null)
                LogAssert.Expect(LogType.Error, "ClusterMesh: " + reason);

            var asset = CreateAsset();
            var camera = CreateCamera("CMBatchCam3");
            var a = CreateRenderer(asset, camera);
            var b = CreateRenderer(asset, camera);
            ClusterMeshSceneBatcher.Register(a);
            ClusterMeshSceneBatcher.Register(b);
            ClusterMeshSceneBatcher.Unregister(a);

            var dest = new List<ClusterMeshBatchDesc>();
            ClusterMeshSceneBatcher.CollectRegisteredBatches(dest);
            Assert.That(dest.Count, Is.EqualTo(1));
            Assert.That(dest[0].objectCount, Is.EqualTo(1));
        }

        ClusterMeshAsset CreateAsset()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            _trash.Add(mesh);
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());
            _trash.Add(asset);
            return asset;
        }

        Camera CreateCamera(string name)
        {
            var go = new GameObject(name);
            _trash.Add(go);
            return go.AddComponent<Camera>();
        }

        ClusterMeshRenderer CreateRenderer(ClusterMeshAsset asset, Camera camera)
        {
            var go = new GameObject("CMBatchRenderer");
            go.SetActive(false);
            _trash.Add(go);
            var renderer = go.AddComponent<ClusterMeshRenderer>();
            renderer.asset = asset;
            renderer.targetCamera = camera;
            renderer.cullShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            renderer.litShader = Shader.Find("ClusterMesh/Lit");
            return renderer;
        }
    }
}
