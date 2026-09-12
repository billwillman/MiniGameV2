using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshStartupSplashTests
    {
        [Test]
        public void PromoImage_ExistsAtSplashPath()
        {
            Assert.That(File.Exists(ClusterMeshStartupSplashWindow.PromoImagePath), Is.True);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                ClusterMeshStartupSplashWindow.PromoImagePath);
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(ClusterMeshStartupSplashWindow.ImageSize));
            Assert.That(texture.height, Is.EqualTo(ClusterMeshStartupSplashWindow.ImageSize));
        }

        [Test]
        public void Window_ShowsPromoAndSkipsBatchmode()
        {
            string source = File.ReadAllText("Assets/ClusterMesh/Editor/ClusterMeshStartupSplashWindow.cs");
            Assert.That(source, Does.Contain("ClusterMesh-promo-512.png"));
            Assert.That(source, Does.Contain("Tools/ClusterMesh/关于 ClusterMesh"));
            Assert.That(source, Does.Contain("InitializeOnLoadMethod"));
            Assert.That(source, Does.Contain("isBatchMode"));
            Assert.That(source, Does.Contain("ShowUtility"));
        }

        [Test]
        public void ShouldShowOnLaunch_FalseAfterShownThisSession()
        {
            bool previous = SessionState.GetBool(ClusterMeshStartupSplashWindow.SessionShownKey, false);
            try
            {
                SessionState.SetBool(ClusterMeshStartupSplashWindow.SessionShownKey, true);
                Assert.That(ClusterMeshStartupSplashWindow.ShouldShowOnLaunch(), Is.False);
            }
            finally
            {
                SessionState.SetBool(ClusterMeshStartupSplashWindow.SessionShownKey, previous);
            }
        }
    }
}
