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
        public void CenteredIn_PlacesWindowInHostCenter()
        {
            var host = new Rect(100f, 40f, 1920f, 1080f);
            Rect placed = ClusterMeshStartupSplashWindow.CenteredIn(host, 536f, 572f);
            Assert.That(placed.x, Is.EqualTo(100f + (1920f - 536f) * 0.5f).Within(0.01f));
            Assert.That(placed.y, Is.EqualTo(40f + (1080f - 572f) * 0.5f).Within(0.01f));
            Assert.That(placed.width, Is.EqualTo(536f));
            Assert.That(placed.height, Is.EqualTo(572f));
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
