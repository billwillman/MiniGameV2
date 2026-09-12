using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshBakerWindowTests
    {
        [Test]
        public void BakeSettings_DefaultsToLegacyEmbeddedStorage()
        {
            var settings = new ClusterMeshBakeSettings();
            Assert.That(settings.enableStreaming, Is.False);
            Assert.That(settings.streamingPagePoolCapacity, Is.EqualTo(64));
        }

        [Test]
        public void ProjectStreamingSettings_DefaultToSharedBoundedBudgets()
        {
            var settings = ScriptableObject.CreateInstance<ClusterMeshSettings>();
            try
            {
                Assert.That(settings.EnableGlobalSharedGpuPool, Is.True);
                Assert.That(settings.SharedGpuPoolPageCapacity, Is.EqualTo(256));
                Assert.That(settings.MaxConcurrentPageReads, Is.EqualTo(8));
                Assert.That(settings.MaxPageUploadsPerUpdate, Is.EqualTo(4));
                Assert.That(settings.EnableStreamingPrefetch, Is.True);
                Assert.That(settings.StreamingResidentGraceUpdates, Is.EqualTo(12));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void StreamingSharedPool_CanBeDisabledWithoutChangingAssetFormat()
        {
            const string assetPath = "Assets/ClusterMesh/Tests/__SharedPoolRegression.asset";
            string sidecarPath = null;
            var mesh = ClusterMeshTestMeshes.Grid(16, 16);
            var first = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshAsset second = null;
            var projectSettings = ScriptableObject.CreateInstance<ClusterMeshSettings>();
            try
            {
                projectSettings.SharedGpuPoolPageCapacity = 64;
                var bakeSettings = new ClusterMeshBakeSettings
                {
                    enableStreaming = true,
                    buildLodHierarchy = true,
                    streamingPagePoolCapacity = 64
                };
                ClusterMeshBakerWindow.WriteAsset(first, mesh, new Material[1], bakeSettings);
                AssetDatabase.CreateAsset(first, assetPath);
                ClusterMeshStreamBaker.FinalizeStatic(first, assetPath, bakeSettings);
                sidecarPath = first.streamDescriptor.editorFilePath;
                second = Object.Instantiate(first);
                ClusterMeshSettings.OverrideForTests = projectSettings;

                ClusterMeshPageRuntime sharedFirst = ClusterMeshStreaming.Request(first);
                ClusterMeshPageRuntime sharedSecond = ClusterMeshStreaming.Request(second);
                Assert.That(sharedFirst.UsesGlobalSharedGpuPool, Is.True);
                Assert.That(sharedSecond.UsesGlobalSharedGpuPool, Is.True);
                Assert.That(ClusterMeshStreaming.SharedGpuPoolCount, Is.EqualTo(1));

                ClusterMeshStreaming.DisposeAll();
                projectSettings.EnableGlobalSharedGpuPool = false;
                ClusterMeshPageRuntime localFirst = ClusterMeshStreaming.Request(first);
                ClusterMeshPageRuntime localSecond = ClusterMeshStreaming.Request(second);
                Assert.That(localFirst.UsesGlobalSharedGpuPool, Is.False);
                Assert.That(localSecond.UsesGlobalSharedGpuPool, Is.False);
                Assert.That(ClusterMeshStreaming.SharedGpuPoolCount, Is.Zero);
            }
            finally
            {
                ClusterMeshStreaming.DisposeAll();
                ClusterMeshSettings.OverrideForTests = null;
                if (!string.IsNullOrEmpty(sidecarPath))
                    AssetDatabase.DeleteAsset(sidecarPath);
                AssetDatabase.DeleteAsset(assetPath);
                if (first != null && !AssetDatabase.Contains(first)) Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
                Object.DestroyImmediate(projectSettings);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void WriteAsset_PopulatesClusters()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshBakerWindow.WriteAsset(asset, mesh, new Material[1], new ClusterMeshBakeSettings());
            Assert.That(asset.clusters, Is.Not.Null);
            Assert.That(asset.clusters.Length, Is.EqualTo(1));
            Assert.That(asset.UsesStreaming, Is.False);
            Assert.That(asset.streamDescriptor, Is.Null);
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void WriteAsset_LodOff_WritesVersionZero()
        {
            var mesh = ClusterMeshTestMeshes.Grid(16, 16);
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshBakerWindow.WriteAsset(
                asset,
                mesh,
                new Material[1],
                new ClusterMeshBakeSettings { buildLodHierarchy = false });
            Assert.That(asset.hierarchyVersion, Is.EqualTo(0));
            Assert.That(asset.geometryVersion, Is.EqualTo(ClusterMeshLimits.GeometryVersion));
            Assert.That(asset.groups == null || asset.groups.Length == 0, Is.True);
            Assert.That(asset.packedVertices, Is.Not.Null);
            Assert.That(asset.packedVertices.Length, Is.GreaterThan(0));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void WriteAsset_Default_Keeps32ByteStride()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshBakerWindow.WriteAsset(asset, mesh, new Material[1], new ClusterMeshBakeSettings());
            Assert.That(asset.ResolvedVertexStride, Is.EqualTo(ClusterMeshLimits.ClusterVertexStride));
            Assert.That(ClusterMeshGeometry.TryReadGpuGeometry(asset, out _, out _, out string error), Is.True);
            Assert.That(error, Is.Null);
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void WriteAsset_TightRestOn_Writes24ByteStride()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshBakerWindow.WriteAsset(
                asset,
                mesh,
                new Material[1],
                new ClusterMeshBakeSettings { packTightRestVertices = true });
            Assert.That(asset.ResolvedVertexStride, Is.EqualTo(ClusterMeshLimits.TightVertexStride));
            Assert.That(ClusterMeshGeometry.TryReadGpuGeometry(asset, out _, out _, out string gpuError), Is.False);
            Assert.That(gpuError, Does.Contain("tight"));
            Assert.That(
                ClusterMeshGeometry.TryReadTightVertices(asset, out ClusterPackedVertexTight[] tight, out string tightError),
                Is.True);
            Assert.That(tightError, Is.Null);
            Assert.That(tight.Length, Is.EqualTo(asset.vertexCount));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void CopyFrom_NormalBakeClearsAnOldStreamingDescriptor()
        {
            var mesh = ClusterMeshTestMeshes.Triangle();
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.streamDescriptor = new ClusterMeshStreamDescriptor();

            ClusterMeshBakerWindow.WriteAsset(
                asset, mesh, new Material[1], new ClusterMeshBakeSettings());

            Assert.That(asset.streamDescriptor, Is.Null);
            Assert.That(asset.packedVertices.Length, Is.GreaterThan(0));
            Assert.That(asset.packedIndices.Length, Is.GreaterThan(0));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void FinalizeStatic_StreamingWritesPagedNodesAndRemovesEmbeddedGeometry()
        {
            const string assetPath = "Assets/ClusterMesh/Tests/__PageStreamRegression.asset";
            string sidecarPath = null;
            var mesh = ClusterMeshTestMeshes.Grid(24, 24);
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            try
            {
                var settings = new ClusterMeshBakeSettings
                {
                    enableStreaming = true,
                    buildLodHierarchy = true,
                    streamingPagePoolCapacity = 8
                };
                ClusterMeshBakerWindow.WriteAsset(asset, mesh, new Material[1], settings);
                AssetDatabase.CreateAsset(asset, assetPath);

                ClusterMeshStreamBaker.FinalizeStatic(asset, assetPath, settings);

                Assert.That(asset.UsesStreaming, Is.True);
                Assert.That(asset.packedVertices, Is.Empty);
                Assert.That(asset.packedIndices, Is.Empty);
                Assert.That(asset.streamDescriptor.pages.Length,
                    Is.GreaterThanOrEqualTo(asset.streamDescriptor.nodes.Length));
                Assert.That(asset.streamDescriptor.addresses.Length,
                    Is.EqualTo(asset.clusters.Length));
                for (int i = 0; i < asset.streamDescriptor.pages.Length; i++)
                    Assert.That(asset.streamDescriptor.pages[i].nodeIndex,
                        Is.InRange(0, asset.streamDescriptor.nodes.Length - 1));
                sidecarPath = asset.streamDescriptor.editorFilePath;
                Assert.That(File.Exists(Path.GetFullPath(sidecarPath)), Is.True);
            }
            finally
            {
                if (!string.IsNullOrEmpty(sidecarPath))
                    AssetDatabase.DeleteAsset(sidecarPath);
                AssetDatabase.DeleteAsset(assetPath);
                if (asset != null && !AssetDatabase.Contains(asset))
                    Object.DestroyImmediate(asset);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void FinalizeSkinned_MarksAndPersistsTheReorderedContainer()
        {
            const string assetPath = "Assets/ClusterMesh/Tests/__SkinnedPageStreamRegression.asset";
            string sidecarPath = null;
            var mesh = ClusterMeshTestMeshes.Grid(16, 16);
            var geometry = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            var asset = ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>();
            try
            {
                var settings = new ClusterMeshBakeSettings
                {
                    enableStreaming = true,
                    buildLodHierarchy = true,
                    streamingPagePoolCapacity = 8
                };
                ClusterMeshBakerWindow.WriteAsset(geometry, mesh, new Material[1], settings);
                asset.geometry = geometry;
                asset.skinVertexCount = geometry.vertexCount;
                asset.skinningVersion = ClusterSkinnedMeshAsset.CurrentSkinningVersion;
                asset.skinWeightStride = ClusterSkinnedMeshAsset.PackedSkinWeightStride;
                asset.packedSkinWeights = ClusterSkinnedMeshBaker.PackSkinWeights(
                    new ClusterSkinWeight[geometry.vertexCount]);
                asset.clips = new[]
                {
                    new ClusterSkinnedClip { name = "Test", duration = 1f, frameRate = 1f,
                        segmentCount = 1, cullFrameOffset = 0 }
                };
                asset.cullFrames = new ClusterSkinnedCullFrame[geometry.clusters.Length];
                for (int i = 0; i < asset.cullFrames.Length; i++)
                {
                    ClusterHeader cluster = geometry.clusters[i];
                    // coneApex.w is unused by culling; make it a stable source
                    // cluster id so the test verifies content reordering, not just
                    // the final array length.
                    cluster.coneApex.w = i + 1;
                    geometry.clusters[i] = cluster;
                    asset.cullFrames[i] = new ClusterSkinnedCullFrame
                    {
                        aabbCenter = cluster.aabbCenter,
                        aabbExtents = cluster.aabbExtents,
                        coneAxisCutoff = cluster.coneAxisCutoff,
                        coneApex = cluster.coneApex
                    };
                }
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.AddObjectToAsset(geometry, asset);
                AssetDatabase.SaveAssets();
                EditorUtility.ClearDirty(asset);
                EditorUtility.ClearDirty(geometry);

                ClusterMeshStreamBaker.FinalizeSkinned(asset, assetPath, settings);

                Assert.That(EditorUtility.IsDirty(asset), Is.True,
                    "Skinned container changes must be persisted, not only its geometry subasset.");
                sidecarPath = geometry.streamDescriptor.editorFilePath;
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ClusterSkinnedMeshAsset reloaded =
                    AssetDatabase.LoadAssetAtPath<ClusterSkinnedMeshAsset>(assetPath);
                Assert.That(reloaded.packedSkinWeights, Is.Empty);
                Assert.That(reloaded.geometry.UsesStreaming, Is.True);
                Assert.That(reloaded.TryGetCullFrames(out ClusterSkinnedCullFrame[] frames, out string error), Is.True);
                Assert.That(error, Is.Null);
                Assert.That(frames.Length, Is.EqualTo(reloaded.geometry.clusters.Length));
                for (int i = 0; i < frames.Length; i++)
                    Assert.That(frames[i].coneApex.w,
                        Is.EqualTo(reloaded.geometry.clusters[i].coneApex.w));
            }
            finally
            {
                if (!string.IsNullOrEmpty(sidecarPath))
                    AssetDatabase.DeleteAsset(sidecarPath);
                AssetDatabase.DeleteAsset(assetPath);
                if (asset != null && !AssetDatabase.Contains(asset)) Object.DestroyImmediate(asset);
                if (geometry != null && !AssetDatabase.Contains(geometry)) Object.DestroyImmediate(geometry);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
