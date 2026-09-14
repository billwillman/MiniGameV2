using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshCapabilityTests
    {
        [Test]
        public void IsSupported_MatchesReasonRule()
        {
            bool supported = ClusterMeshCapability.IsSupported();
            string reason = ClusterMeshCapability.GetUnsupportedReason();
            if (supported)
                Assert.That(reason, Is.Null);
            else
                Assert.That(reason, Is.Not.Null.And.Not.Empty);
        }
    }
}
