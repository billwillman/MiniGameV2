using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshStreamCompressionTests
    {
        string folder;
        Mesh mesh;
        ClusterMeshSettings runtimeSettings, previousSettings;
        readonly List<ClusterMeshAsset> assets = new List<ClusterMeshAsset>();
        [SetUp] public void SetUp()
        {
            folder = "Assets/ClusterMesh/Tests/__Compression" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/ClusterMesh/Tests", Path.GetFileName(folder));
            mesh = ClusterMeshTestMeshes.Grid(16, 16);
        }
        [TearDown] public void TearDown()
        {
            foreach (var asset in assets) ClusterMeshStreaming.Release(asset);
            assets.Clear();
            AssetDatabase.DeleteAsset(folder);
            Object.DestroyImmediate(mesh);
            if (runtimeSettings != null)
            {
                ClusterMeshSettings.OverrideForTests = previousSettings;
                ClusterMeshStreaming.ReuseDecompressionContexts = false;
                Object.DestroyImmediate(runtimeSettings);
            }
        }
        ClusterMeshAsset Bake(string name, ClusterMeshPageCompression? compression = null)
        {
            var settings = new ClusterMeshBakeSettings { enableStreaming = true };
            if (compression.HasValue) settings.streamingCompression = compression.Value;
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(ClusterMeshBaker.Bake(mesh, new Material[1], settings), mesh, settings);
            string path = folder + "/" + name + ".asset";
            AssetDatabase.CreateAsset(asset, path); assets.Add(asset);
            ClusterMeshStreamBaker.FinalizeStatic(asset, path, settings);
            return asset;
        }
        static byte[] ReadPayload(ClusterMeshStreamDescriptor descriptor, int index)
        {
            var p = descriptor.pages[index];
            var packed = new byte[p.compressedLength];
            using (var file = File.OpenRead(descriptor.editorFilePath))
            { file.Position = p.fileOffset; int at = 0; while (at < packed.Length) { int n = file.Read(packed, at, packed.Length - at); Assert.That(n, Is.GreaterThan(0)); at += n; } }
            if (p.compressedLength == p.uncompressedLength) return packed;
            using (var input = new MemoryStream(packed))
            using (var zip = new DeflateStream(input, CompressionMode.Decompress))
            using (var raw = new MemoryStream())
            { zip.CopyTo(raw); Assert.That(raw.Length, Is.EqualTo(p.uncompressedLength)); return raw.ToArray(); }
        }
        [Test] public void DefaultIsFastDeflateAndBothModesHaveIdenticalPayloads()
        {
            Assert.That(new ClusterMeshBakeSettings().streamingCompression, Is.EqualTo(ClusterMeshPageCompression.FastDeflate));
            var raw = Bake("Raw", ClusterMeshPageCompression.None).streamDescriptor;
            var zipped = Bake("Fast", ClusterMeshPageCompression.FastDeflate).streamDescriptor;
            var defaultBake = Bake("Default").streamDescriptor;
            Assert.That(defaultBake.contentHash, Is.EqualTo(zipped.contentHash), "Default baking must use the compressed storage recipe");
            Assert.That(raw.pages.Length, Is.EqualTo(zipped.pages.Length));
            Assert.That(raw.contentHash, Is.Not.EqualTo(zipped.contentHash), "Storage choice must change immutable stream identity");
            bool compressed = false;
            for (int i = 0; i < raw.pages.Length; i++)
            {
                Assert.That(raw.pages[i].compressedLength, Is.EqualTo(raw.pages[i].uncompressedLength));
                Assert.That(zipped.pages[i].compressedLength, Is.LessThanOrEqualTo(zipped.pages[i].uncompressedLength));
                compressed |= zipped.pages[i].compressedLength < zipped.pages[i].uncompressedLength;
                Assert.That(ReadPayload(raw, i), Is.EqualTo(ReadPayload(zipped, i)));
                Assert.That(raw.pages[i].crc32, Is.EqualTo(zipped.pages[i].crc32));
            }
            Assert.That(compressed, Is.True, "Fixture must exercise the compressed branch too");
        }
        [Test] public void NewBakerWindowDefaultsToFastDeflate()
        {
            var window = ScriptableObject.CreateInstance<ClusterMeshBakerWindow>();
            try
            {
                var field = typeof(ClusterMeshBakerWindow).GetField("_settings",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                var settings = (ClusterMeshBakeSettings)field.GetValue(window);
                Assert.That(settings.streamingCompression, Is.EqualTo(ClusterMeshPageCompression.FastDeflate));
                settings.streamingCompression = ClusterMeshPageCompression.None;
                Assert.That(((ClusterMeshBakeSettings)field.GetValue(window)).streamingCompression,
                    Is.EqualTo(ClusterMeshPageCompression.None), "Explicit opt-out must remain available");
            }
            finally { Object.DestroyImmediate(window); }
        }
        [UnityTest] public IEnumerator UncompressedBakeLoadsThroughReusedReader() => VerifyRuntime(ClusterMeshPageCompression.None);
        [UnityTest] public IEnumerator CompressedBakeLoadsThroughReusedReader() => VerifyRuntime(ClusterMeshPageCompression.FastDeflate);
        [UnityTest] public IEnumerator CompressedBakeLoadsWithReusedDecoderSinglePages() => VerifyRuntime(ClusterMeshPageCompression.FastDeflate, true, 1);
        [UnityTest] public IEnumerator CompressedBakeLoadsWithReusedDecoderRootBatches() => VerifyRuntime(ClusterMeshPageCompression.FastDeflate, true, 16);
        [UnityTest] public IEnumerator UncompressedBakeLoadsWithReuseEnabled() => VerifyRuntime(ClusterMeshPageCompression.None, true, 16);
        IEnumerator VerifyRuntime(ClusterMeshPageCompression compression, bool reuse = false, int rootBatch = 16)
        {
            previousSettings = ClusterMeshSettings.OverrideForTests;
            runtimeSettings = ScriptableObject.CreateInstance<ClusterMeshSettings>();
            runtimeSettings.ReuseDecompressionContexts = reuse;
            runtimeSettings.MaxConcurrentPageReads = 2;
            runtimeSettings.MaxRootPagesPerRead = rootBatch;
            ClusterMeshSettings.OverrideForTests = runtimeSettings;
            var asset = Bake("Runtime", compression);
            var runtime = ClusterMeshStreaming.Acquire(asset);
            Assert.That(runtime, Is.Not.Null);
            double until = EditorApplication.timeSinceStartup + 15;
            while (!runtime.IsReady)
            {
                Assert.That(runtime.Failed, Is.False, runtime.Error);
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(until));
                ClusterMeshStreaming.Update(); yield return null;
            }
            Assert.That(runtime.MetadataReady, Is.True);
            Assert.That(ClusterMeshStreaming.ReuseDecompressionContexts, Is.EqualTo(reuse));
            Assert.That(ClusterMeshStreaming.ActiveDecodeContexts, Is.Zero);
            Assert.That(ClusterMeshStreaming.RetainedDecodeContexts, Is.InRange(reuse ? 1 : 0, reuse ? 2 : 0));
        }
        [Test] public void UnknownCompressionModeIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Bake("Invalid", (ClusterMeshPageCompression)99));
        }
    }
}
