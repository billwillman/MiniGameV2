using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshViewerTests
    {
        [Test]
        public void CreatePreviewContext_UsesSameTypeAsRuntime()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings());
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings());

            using (var ctx = ClusterMeshViewerWindow.CreatePreviewContext(asset))
            {
                Assert.That(ctx, Is.Not.Null);
                Assert.That(ctx.IsReady, Is.EqualTo(ClusterMeshCapability.IsSupported()));
            }

            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }
    }
}
