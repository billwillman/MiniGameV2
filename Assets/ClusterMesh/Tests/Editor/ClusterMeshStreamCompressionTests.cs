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
        }
        ClusterMeshAsset Bake(string name, ClusterMeshPageCompression compression)
        {
            var settings = new ClusterMeshBakeSettings { enableStreaming = true, streamingCompression = compression };
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
        [Test] public void DefaultIsUncompressedAndBothModesHaveIdenticalPayloads()
        {
            Assert.That(new ClusterMeshBakeSettings().streamingCompression, Is.EqualTo(ClusterMeshPageCompression.None));
            var raw = Bake("Raw", ClusterMeshPageCompression.None).streamDescriptor;
            var zipped = Bake("Fast", ClusterMeshPageCompression.FastDeflate).streamDescriptor;
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
        [UnityTest] public IEnumerator UncompressedBakeLoadsThroughReusedReader() => VerifyRuntime(ClusterMeshPageCompression.None);
        [UnityTest] public IEnumerator CompressedBakeLoadsThroughReusedReader() => VerifyRuntime(ClusterMeshPageCompression.FastDeflate);
        IEnumerator VerifyRuntime(ClusterMeshPageCompression compression)
        {
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
        }
        [Test] public void UnknownCompressionModeIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Bake("Invalid", (ClusterMeshPageCompression)99));
        }
    }
}
