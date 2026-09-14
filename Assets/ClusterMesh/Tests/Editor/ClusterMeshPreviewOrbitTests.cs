using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshPreviewOrbitTests
    {
        [Test]
        public void DefaultOffset_MatchesFixedInspectorCamera()
        {
            Vector3 offset = ClusterMeshPreviewOrbit.Offset(
                ClusterMeshPreviewOrbit.DefaultAngles,
                1f,
                ClusterMeshPreviewOrbit.DefaultDistance);
            Assert.That(offset.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(offset.y, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(offset.z, Is.EqualTo(-2.4f).Within(1e-4f));
        }

        [Test]
        public void ApplyDrag_AddsYawAndClampsPitch()
        {
            Vector2 orbit = ClusterMeshPreviewOrbit.ApplyDrag(Vector2.zero, new Vector2(12f, 5f));
            Assert.That(orbit.y, Is.EqualTo(12f).Within(1e-4f));
            Assert.That(orbit.x, Is.EqualTo(5f).Within(1e-4f));

            orbit = ClusterMeshPreviewOrbit.ApplyDrag(new Vector2(80f, 0f), new Vector2(0f, 20f));
            Assert.That(orbit.x, Is.EqualTo(89f));
            orbit = ClusterMeshPreviewOrbit.ApplyDrag(new Vector2(-80f, 0f), new Vector2(0f, -20f));
            Assert.That(orbit.x, Is.EqualTo(-89f));
        }
    }
}
