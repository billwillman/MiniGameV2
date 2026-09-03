using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshViewerTests
    {
        static ClusterMeshAsset CreateTestAsset()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());
            Object.DestroyImmediate(mesh);
            return asset;
        }

        [Test]
        public void CreatePreviewContext_UsesSameTypeAsRuntime()
        {
            var asset = CreateTestAsset();

            using (var ctx = ClusterMeshViewerWindow.CreatePreviewContext(asset))
            {
                Assert.That(ctx, Is.Not.Null);
                Assert.That(ctx.IsReady, Is.EqualTo(ClusterMeshCapability.IsSupported()));
            }

            Object.DestroyImmediate(asset);
        }

        [Test]
        public void OnEnable_WithSerializedAsset_RebuildsContext()
        {
            var asset = CreateTestAsset();
            var window = ScriptableObject.CreateInstance<ClusterMeshViewerWindow>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(ClusterMeshViewerWindow).GetField("_asset", flags).SetValue(window, asset);
            Assert.That(typeof(ClusterMeshViewerWindow).GetField("_context", flags).GetValue(window), Is.Null);

            typeof(ClusterMeshViewerWindow).GetMethod("OnEnable", flags).Invoke(window, null);

            var context = typeof(ClusterMeshViewerWindow).GetField("_context", flags).GetValue(window);
            Assert.That(context, Is.Not.Null);
            Assert.That(((ClusterMeshDrawContext)context).IsReady, Is.EqualTo(ClusterMeshCapability.IsSupported()));

            window.Close();
            Object.DestroyImmediate(window);
            Object.DestroyImmediate(asset);
        }
    }
}
