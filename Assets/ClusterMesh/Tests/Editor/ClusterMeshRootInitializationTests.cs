using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshRootInitializationTests
    {
        const int PayloadBytes = 12 + 3 * ClusterMeshLimits.ClusterVertexStride + 2 * 4;
        const long DecodedBudget = 4096;
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly MethodInfo UpdateMethod = typeof(ClusterMeshPageRuntime).GetMethod("Update", PrivateInstance);
        static readonly PropertyInfo PendingBytes = typeof(ClusterMeshPageRuntime).GetProperty("PendingDecodedBytes", PrivateInstance);
        readonly List<ClusterMeshPageRuntime> runtimes = new List<ClusterMeshPageRuntime>();
        readonly List<ClusterMeshAsset> assets = new List<ClusterMeshAsset>();
        readonly List<Task> outstanding = new List<Task>();
        ClusterMeshSettings settings, previousSettings;
        Func<ClusterMeshStreamDescriptor, string> previousResolver;
        string previousRoot, temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Streaming residency tests require a graphics device; run without -nographics.");
            previousSettings = ClusterMeshSettings.OverrideForTests;
            previousResolver = ClusterMeshStreaming.FilePathResolver;
            previousRoot = ClusterMeshStreaming.RootPath;
            settings = ScriptableObject.CreateInstance<ClusterMeshSettings>();
            settings.EnableGlobalSharedGpuPool = true;
            settings.SharedGpuPoolPageCapacity = 8;
            ClusterMeshSettings.OverrideForTests = settings;
            ClusterMeshStreaming.FilePathResolver = null;
            ClusterMeshStreaming.RootPath = null;
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "ClusterMeshAdmission-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var runtime in runtimes) DisposeRuntime(runtime);
            while (ClusterMeshPageRuntime.ActivePageReads != 0) yield return null;
            foreach (Task task in outstanding)
                while (!task.IsCompleted) yield return null;
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            if (settings != null)
            {
                ClusterMeshSettings.OverrideForTests = previousSettings;
                ClusterMeshStreaming.FilePathResolver = previousResolver;
                ClusterMeshStreaming.RootPath = previousRoot;
                UnityEngine.Object.DestroyImmediate(settings);
            }
            if (temporaryDirectory != null) Directory.Delete(temporaryDirectory, true);
            runtimes.Clear(); assets.Clear(); outstanding.Clear();
        }

        [UnityTest]
        public IEnumerator SharedScratch_AllSixFormatsKeepOwnerDataIsolated()
        {
            foreach (int stride in new[] { 32, 24 })
            foreach (int weights in new[] { 0, 16, 8 })
            {
                var a = CreateRuntime("a-" + stride + "-" + weights, stride: stride, weightStride: weights, marker: 3);
                var b = CreateRuntime("b-" + stride + "-" + weights, stride: stride, weightStride: weights, marker: 7);
                yield return WaitUntil(() => a.IsReady && b.IsReady);
                CheckRoot(a, stride, weights, 3);
                CheckRoot(b, stride, weights, 7);
                a.RequestNode(1); b.RequestNode(1);
                yield return WaitUntil(() => a.IsNodeResident(1) && b.IsNodeResident(1));
                CheckRoot(a, stride, weights, 3);
                CheckRoot(b, stride, weights, 7);
                DisposeRuntime(a); DisposeRuntime(b);
            }
        }

        [Test]
        public void ValueTypeRequestsPreservePriorityEscalationAndAge()
        {
            var runtime = CreateRuntime("request-values");
            var queue = typeof(ClusterMeshPageRuntime).GetMethod("QueuePage", PrivateInstance);
            var requests = (IDictionary)typeof(ClusterMeshPageRuntime).GetField("_requests", PrivateInstance).GetValue(runtime);
            queue.Invoke(runtime, new object[] { 1, 10u });
            var value = requests[1];
            int firstTick = (int)value.GetType().GetField("firstTick").GetValue(value);
            typeof(ClusterMeshPageRuntime).GetField("_tick", PrivateInstance).SetValue(runtime, 19);
            queue.Invoke(runtime, new object[] { 1, 50u });
            queue.Invoke(runtime, new object[] { 1, 5u });
            value = requests[1];
            Assert.That(value.GetType().IsValueType, Is.True);
            Assert.That((uint)value.GetType().GetField("priority").GetValue(value), Is.EqualTo(50u));
            Assert.That((int)value.GetType().GetField("firstTick").GetValue(value), Is.EqualTo(firstTick));
        }

        static void CheckRoot(ClusterMeshPageRuntime runtime, int stride, int weights, float marker)
        {
            var table = new ClusterMeshStreamPageTableEntry[1]; runtime.PageTable.GetData(table, 0, 0, 1);
            int offset = (int)table[0].vertexBase;
            if (stride == 32)
            {
                var data = new ClusterPackedVertex[1]; runtime.Vertices.GetData(data, 0, offset, 1);
                Assert.That(data[0].position.x, Is.EqualTo(marker));
            }
            else
            {
                var data = new ClusterPackedVertexTight[1]; runtime.VerticesTight.GetData(data, 0, offset, 1);
                Assert.That(data[0].px, Is.EqualTo(marker));
            }
            var index = new uint[1]; runtime.Indices.GetData(index, 0, (int)table[0].indexBase, 1);
            Assert.That(index[0], Is.EqualTo(17u));
            if (weights == 16)
            {
                var data = new ClusterPackedSkinWeight[1]; runtime.Weights.GetData(data, 0, offset, 1);
                Assert.That(data[0].boneIndices01, Is.EqualTo((uint)marker));
            }
            else if (weights == 8)
            {
                var data = new uint[1]; runtime.Weights8.GetData(data, 0, offset * 2, 1);
                Assert.That(data[0], Is.EqualTo((uint)marker));
            }
        }

        [Test]
        public void CompactTables_PreservePageAndChildOrder()
        {
            var runtime = CreateRuntime("tables");
            var table = typeof(ClusterMeshPageRuntime).GetField("_nodePages", PrivateInstance).GetValue(runtime);
            Assert.That(table.GetType().GetField("items", PrivateInstance).GetValue(table), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(table.GetType().GetField("offsets", PrivateInstance).GetValue(table), Is.EqualTo(new[] { 0, 1, 4 }));
            var children = typeof(ClusterMeshPageRuntime).GetMethod("GetNodeChildren", PrivateInstance);
            var range = children.Invoke(runtime, new object[] { 0 });
            Assert.That(range.GetType().GetProperty("Length").GetValue(range), Is.EqualTo(1));
            Assert.That(range.GetType().GetProperty("Item").GetValue(range, new object[] { 0 }), Is.EqualTo(1));
            range = children.Invoke(runtime, new object[] { 1 });
            Assert.That(range.GetType().GetProperty("Length").GetValue(range), Is.Zero);
            Assert.That(typeof(ClusterMeshPageRuntime).GetField("_vertexStaging", PrivateInstance), Is.Null,
                "Upload scratch must no longer be allocated per runtime owner.");
        }

        [TestCase("missing", "has no pages")]
        [TestCase("ownership", "crosses node ownership")]
        [TestCase("capacity", "one complete detail node")]
        [TestCase("pageNode", "page 1 is invalid")]
        [TestCase("overlap", "overlap or are out of order")]
        [TestCase("limit", "exceeds supported resource limits")]
        public void LinearValidation_StillRejectsCorruptDescriptors(string kind, string error)
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ClusterMesh page streaming: .*" + error));
            var runtime = CreateRuntime("invalid-" + kind, d =>
            {
                switch (kind)
                {
                    case "missing": for (int i = 0; i < d.pages.Length; i++) d.pages[i].nodeIndex = 0; break;
                    case "ownership": d.addresses[1].pageId = 0; break;
                    case "capacity": d.poolPageCapacity = 3; break;
                    case "pageNode": d.pages[1].nodeIndex = 99; break;
                    case "overlap": d.pages[1].fileOffset = d.pages[0].fileOffset; break;
                    case "limit": d.vertexPageCapacity = 65537; break;
                }
            }, false);
            Assert.That(runtime.Failed, Is.True);
            Assert.That(runtime.Error, Does.Contain(error));
            runtime.Dispose(); runtime.Dispose();
        }

        IEnumerator WaitUntil(Func<bool> condition)
        {
            double deadline = EditorApplication.timeSinceStartup + 10;
            while (!condition())
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Streaming made no progress within ten seconds.");
                foreach (var runtime in runtimes)
                    if (!runtime.IsDisposed) Pump(runtime, 1, 1);
                yield return null;
            }
        }

        void Pump(ClusterMeshPageRuntime runtime, int reads, int uploads)
        {
            long pending = 0;
            foreach (var item in runtimes) pending += (long)PendingBytes.GetValue(item);
            Assert.That(pending, Is.LessThanOrEqualTo(DecodedBudget));
            var arguments = new object[] { reads, 1024L * 1024, DecodedBudget - pending, uploads, 1024L * 1024 };
            UpdateMethod.Invoke(runtime, arguments);
            Assert.That(runtime.Failed, Is.False, runtime.Error);
            pending = 0;
            foreach (var item in runtimes) pending += (long)PendingBytes.GetValue(item);
            Assert.That(pending, Is.LessThanOrEqualTo(DecodedBudget), "Fix must not increase the memory allowance.");
        }

        void DisposeRuntime(ClusterMeshPageRuntime runtime)
        {
            if (runtime.IsDisposed) return;
            var metadata = (Task)typeof(ClusterMeshPageRuntime).GetField("_metadataRead", PrivateInstance).GetValue(runtime);
            if (metadata != null) outstanding.Add(metadata);
            runtime.Dispose();
        }

        ClusterMeshPageRuntime CreateRuntime(string name, Action<ClusterMeshStreamDescriptor> alter = null, bool valid = true,
            int stride = ClusterMeshLimits.ClusterVertexStride, int weightStride = 0, float marker = 1f)
        {
            // One root page plus a three-page detail node. Both owners together
            // fit eight GPU slots, but one detail node exceeds our decode budget.
            string path = Path.Combine(temporaryDirectory, name + ".cmstream");
            int payloadBytes = 12 + 3 * stride + 8 + 3 * weightStride;
            var kind = weightStride == 0 ? ClusterMeshStreamKind.Static : ClusterMeshStreamKind.Skinned;
            byte[] payload;
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(3); writer.Write(2); writer.Write(weightStride == 0 ? 0 : 3);
                for (int i = 0; i < 3; i++) { writer.Write(marker); writer.Write(new byte[stride - 4]); }
                writer.Write(17u); writer.Write(29u);
                for (int i = 0; i < 3 && weightStride != 0; i++)
                { writer.Write((uint)marker); writer.Write(new byte[weightStride - 4]); }
                payload = memory.ToArray();
            }
            uint crc = 0xffffffffu;
            foreach (byte value in payload)
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
            }
            crc ^= 0xffffffffu;
            var pages = new ClusterMeshStreamPage[4];
            var addresses = new ClusterMeshStreamAddress[4];
            var clusters = new ClusterHeader[4];
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i] = new ClusterMeshStreamPage
                {
                    fileOffset = 16 + 40 * pages.Length + i * payloadBytes,
                    compressedLength = payloadBytes, uncompressedLength = payloadBytes,
                    vertexCount = 3, indexWordCount = 2, weightCount = weightStride == 0 ? 0 : 3, crc32 = crc,
                    nodeIndex = i == 0 ? 0 : 1,
                    flags = i == 0 ? ClusterMeshStreamPageFlags.Root : 0
                };
                addresses[i] = new ClusterMeshStreamAddress { pageId = (uint)i };
                clusters[i] = new ClusterHeader { vertexCount = 3, triangleCount = 1 };
            }
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(ClusterMeshStreaming.FileMagic); writer.Write(ClusterMeshStreaming.FormatVersion);
                writer.Write((int)kind); writer.Write(pages.Length);
                foreach (var page in pages)
                {
                    writer.Write(page.fileOffset); writer.Write(page.compressedLength); writer.Write(page.uncompressedLength);
                    writer.Write(page.vertexCount); writer.Write(page.indexWordCount); writer.Write(page.weightCount);
                    writer.Write(page.crc32); writer.Write(page.nodeIndex); writer.Write(page.flags);
                }
                foreach (var page in pages) writer.Write(payload);
            }
            var descriptor = new ClusterMeshStreamDescriptor
            {
                formatVersion = ClusterMeshStreaming.FormatVersion, kind = kind, weightStride = weightStride,
                fileName = path, editorFilePath = path, fileSize = new FileInfo(path).Length,
                poolPageCapacity = 8, vertexPageCapacity = 3, indexPageCapacity = 2,
                pages = pages, addresses = addresses,
                nodes = new[]
                {
                    new ClusterMeshStreamNode { clusterStart = 0, clusterCount = 1, parentNodeIndex = -1, lodLevel = 1 },
                    new ClusterMeshStreamNode { clusterStart = 1, clusterCount = 3, parentNodeIndex = 0, lodLevel = 0 }
                }
            };
            alter?.Invoke(descriptor);
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.vertexStride = stride; asset.clusters = clusters; asset.streamDescriptor = descriptor;
            assets.Add(asset);
            var runtime = (ClusterMeshPageRuntime)Activator.CreateInstance(typeof(ClusterMeshPageRuntime), PrivateInstance,
                null, new object[] { asset, descriptor }, null);
            runtimes.Add(runtime);
            if (valid)
            {
                Assert.That(runtime.UsesGlobalSharedGpuPool, Is.True);
                Assert.That(runtime.Failed, Is.False, runtime.Error);
            }
            return runtime;
        }
    }
}
