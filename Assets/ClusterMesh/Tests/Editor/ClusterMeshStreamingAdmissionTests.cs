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
    public sealed class ClusterMeshStreamingAdmissionTests
    {
        const int PayloadBytes = 12 + 3 * ClusterMeshLimits.ClusterVertexStride + 2 * 4;
        const long DecodedBudget = 2 * PayloadBytes;
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly MethodInfo UpdateMethod = typeof(ClusterMeshPageRuntime).GetMethod("Update", PrivateInstance);
        static readonly PropertyInfo PendingBytes = typeof(ClusterMeshPageRuntime).GetProperty("PendingDecodedBytes", PrivateInstance);
        static readonly MethodInfo SharedCapacityMethod = typeof(ClusterMeshStreaming).GetMethod(
            "CalculateSharedPoolCapacity", BindingFlags.Static | BindingFlags.NonPublic);
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
        public IEnumerator SharedPool_ReadAdmissionMakesProgressWithLessThanOneNodeOfDecodedMemory()
        {
            var first = CreateRuntime("first");
            var second = CreateRuntime("second");
            yield return WaitUntil(() => first.IsReady && second.IsReady);
            Assert.That(first.Vertices, Is.SameAs(second.Vertices), "The regression must exercise one shared pool.");
            first.RequestNode(1); second.RequestNode(1);

            // Only one page of a three-page node is read. Pause uploads so a
            // competing owner would consume the remaining decode slot on the
            // old implementation, before upload-time admission had been taken.
            Pump(first, 1, 0);
            Pump(second, 1, 0);
            Assert.That((long)PendingBytes.GetValue(first), Is.EqualTo(PayloadBytes));
            Assert.That((long)PendingBytes.GetValue(second), Is.Zero,
                "A node blocked from uploading must not occupy the finite decoded budget.");

            yield return WaitUntil(() => first.IsNodeResident(1) && second.IsNodeResident(1));
            Assert.That(first.IsReady && second.IsReady, Is.True);
        }

        [UnityTest]
        public IEnumerator SharedPool_DisposingReadAdmittedOwnerUnblocksWaitingOwner()
        {
            var first = CreateRuntime("disposed");
            var second = CreateRuntime("waiting");
            yield return WaitUntil(() => first.IsReady && second.IsReady);
            first.RequestNode(1); second.RequestNode(1);
            Pump(first, 1, 0);
            Pump(second, 1, 0);
            Assert.That((long)PendingBytes.GetValue(second), Is.Zero);
            DisposeRuntime(first);
            yield return WaitUntil(() => second.IsNodeResident(1));
            Assert.That(second.IsReady, Is.True);
        }

        [TestCase(51, 256)]
        [TestCase(980, 1024)]
        [TestCase(4096, 4096)]
        public void SharedPool_RootOnlyGeometryDoesNotAddDetailCapacity(int rootPages, int expectedCapacity)
        {
            int window;
            int capacity = CalculateCapacity(rootPages, rootPages, 0, rootPages, 256, out window);
            Assert.That(window, Is.Zero, "Root-only ocean assets have no detail demand.");
            Assert.That(capacity, Is.EqualTo(expectedCapacity));
        }

        [Test]
        public void SharedPool_RootHeavyGeometryDoesNotExpandTheOriginalBudget()
        {
            int window;
            int capacity = CalculateCapacity(2810, 980, 1, 981, 256, out window);
            Assert.That(window, Is.EqualTo(1));
            Assert.That(capacity, Is.EqualTo(1024));
            Assert.That(capacity - 980, Is.GreaterThanOrEqualTo(window));
        }

        [Test]
        public void SharedPool_DetailDemandDoesNotDoubleTheOriginalBudget()
        {
            // The South America runtime fixture has 382 pinned roots and over
            // 200 hot detail pages. Rounding roots alone to 512 left only 130.
            int window;
            int capacity = CalculateCapacity(1143, 382, 1, 383, 256, out window);
            Assert.That(window, Is.EqualTo(1));
            Assert.That(capacity, Is.EqualTo(512));
            Assert.That(capacity - 382, Is.GreaterThanOrEqualTo(window));
        }

        [Test]
        public void SharedPool_DetailWindowIsClippedToActualDetailPages()
        {
            int window;
            Assert.That(CalculateCapacity(4, 1, 3, 8, 256, out window), Is.EqualTo(256));
            Assert.That(window, Is.EqualTo(3));
        }

        [Test]
        public void SharedPool_EightPageBudgetStillSharesTwoOwners()
        {
            var first = CreateRuntime("capacity-eight-first");
            var second = CreateRuntime("capacity-eight-second");
            Assert.That(SharedPool(first), Is.SameAs(SharedPool(second)));
            Assert.That(PoolValue(first, "Capacity"), Is.EqualTo(8));
            Assert.That(PoolValue(first, "ReservedRootPages"), Is.EqualTo(2));
            Assert.That(PoolValue(first, "ReservedDetailHeadroom"), Is.EqualTo(3));
        }

        [Test]
        public void SharedPool_NewOwnersCannotConsumeExistingDetailWindow()
        {
            // Three pinned roots + four one-page detail nodes reserve a window
            // of four, though admission needs only one page for a complete node.
            var first = CreateRuntime("window-owner", 3, 1, 4);
            var fittingRootOnly = CreateRuntime("fitting-root", 1, 0, 0);
            var overflowingRootOnly = CreateRuntime("overflowing-root", 1, 0, 0);
            Assert.That(SharedPool(first), Is.SameAs(SharedPool(fittingRootOnly)));
            Assert.That(SharedPool(first), Is.Not.SameAs(SharedPool(overflowingRootOnly)),
                "A fifth pinned root must use another pool, not reduce the four-page detail window.");
            Assert.That(PoolValue(first, "ReservedRootPages"), Is.EqualTo(4));
            Assert.That(PoolValue(first, "ReservedDetailHeadroom"), Is.EqualTo(4));

            DisposeRuntime(first);
            Assert.That(PoolValue(fittingRootOnly, "ReservedDetailHeadroom"), Is.Zero,
                "Releasing the detail owner releases its window reservation.");
            var afterRelease = CreateRuntime("after-release", 4, 0, 0);
            Assert.That(SharedPool(afterRelease), Is.SameAs(SharedPool(fittingRootOnly)));
        }

        [Test]
        public void SharedPool_CapacityLimitRejectsRatherThanShrinkingReservation()
        {
            int window;
            Assert.That(CalculateCapacity(4096, 3968, 1, 3969, 256, out window), Is.EqualTo(4096));
            Assert.That(window, Is.EqualTo(1));
            var error = Assert.Throws<TargetInvocationException>(() =>
                CalculateCapacity(4097, 4096, 1, 4097, 256, out window));
            Assert.That(error.InnerException, Is.TypeOf<InvalidDataException>());
            StringAssert.Contains("4096-page limit", error.InnerException.Message);
            StringAssert.Contains("was not expanded", error.InnerException.Message);
        }

        [Test]
        public void SharedPool_CapacityRejectionDoesNotFallBackToSmallerLocalPool()
        {
            settings.SharedGpuPoolPageCapacity = 256;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "ClusterMesh page streaming: .*exceeds supported resource limits"));
            var runtime = CreateRuntime("limit-rejected", 4096, 1, 1, 4097, true);
            Assert.That(runtime.Failed, Is.True);
            StringAssert.Contains("exceeds supported resource limits", runtime.Error);
            Assert.That(SharedPool(runtime), Is.Null);
            Assert.That(typeof(ClusterMeshPageRuntime).GetField("_slots", PrivateInstance).GetValue(runtime), Is.Null);
            Assert.That(typeof(ClusterMeshPageRuntime).GetField("_metadataRead", PrivateInstance).GetValue(runtime), Is.Null,
                "Rejected runtime must not start loading a silently smaller fallback pool.");
            Assert.That(runtime.Descriptor.poolPageCapacity, Is.EqualTo(4097));
        }

        [UnityTest]
        public IEnumerator SharedPool_PackedRootsUseDisjointGpuRangesWithoutGrowingBuffers()
        {
            var runtime = CreateRuntime("packed-roots", 3, 3, 1, 8, false, 9, 6);
            yield return WaitUntil(() => runtime.IsReady);
            Assert.That(runtime.LogicalRootPageCount, Is.EqualTo(3));
            Assert.That(runtime.PhysicalRootSlotCount, Is.EqualTo(1));
            Assert.That(PoolValue(runtime, "Capacity"), Is.EqualTo(8));
            Assert.That(runtime.Vertices.count, Is.EqualTo(8 * 9));
            var table = new ClusterMeshStreamPageTableEntry[runtime.PageCount];
            runtime.PageTable.GetData(table);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(table[i].vertexBase, Is.EqualTo(i * 3));
                Assert.That(table[i].indexBase, Is.EqualTo(i * 2));
                Assert.That(table[i].flags, Is.EqualTo(1));
                var vertex = new ClusterPackedVertex[1];
                runtime.Vertices.GetData(vertex, 0, (int)table[i].vertexBase, 1);
                Assert.That(vertex[0].position.x, Is.EqualTo(i + 1), "Each packed root must retain its own payload.");
            }
            runtime.RequestNode(1);
            yield return WaitUntil(() => runtime.IsNodeResident(1));
            runtime.PageTable.GetData(table);
            for (int i = 3; i < table.Length; i++) Assert.That(table[i].vertexBase, Is.GreaterThanOrEqualTo(9));
            Assert.That(runtime.IsNodeResident(0), Is.True, "Detail loading must never overwrite packed pinned roots.");
        }

        [UnityTest]
        public IEnumerator SharedPool_AdmissionReservesDestinationsAcrossReadsAndNewOwners()
        {
            var first = CreateRuntime("reserved-reader");
            yield return WaitUntil(() => first.IsReady);
            first.RequestNode(1); Pump(first, 1, 0);
            object pool = SharedPool(first);
            Assert.That((int)pool.GetType().GetField("_admissionCount", PrivateInstance).GetValue(pool), Is.EqualTo(3));
            var second = CreateRuntime("new-root-owner", 4, 0, 0);
            Assert.That(SharedPool(second), Is.SameAs(pool));
            yield return WaitUntil(() => first.IsNodeResident(1) && second.IsReady);
            Assert.That(PoolValue(first, "Capacity"), Is.EqualTo(8));
        }

        [UnityTest]
        public IEnumerator SharedPool_ReservedBatchSurvivesOldPagesBecomingHot()
        {
            var runtime = CreateRuntime("touch-during-read", 1, 3, 3);
            yield return WaitUntil(() => runtime.IsReady);
            runtime.RequestNode(1); runtime.RequestNode(2);
            yield return WaitUntil(() => runtime.IsNodeResident(1) && runtime.IsNodeResident(2));
            object pool = SharedPool(runtime);
            var slots = (Array)pool.GetType().GetField("_slots", PrivateInstance).GetValue(pool);
            foreach (object slot in slots)
            {
                int id = (int)slot.GetType().GetField("pageId").GetValue(slot);
                if (id >= 1 && id <= 3) slot.GetType().GetField("lastUsed").SetValue(slot, -1000);
            }
            pool.GetType().GetField("_spaceSerial", PrivateInstance).SetValue(pool, int.MinValue);
            runtime.RequestNode(3); Pump(runtime, 1, 0);
            runtime.MarkNodeUsed(1); // delayed GPU residency feedback during I/O
            yield return WaitUntil(() => runtime.IsNodeResident(3));
            Assert.That(runtime.IsNodeResident(0) && runtime.IsNodeResident(2), Is.True);
            var table = new ClusterMeshStreamPageTableEntry[runtime.PageCount]; runtime.PageTable.GetData(table);
            Assert.That(table[7].vertexBase, Is.Not.EqualTo(table[8].vertexBase));
            Assert.That(table[7].vertexBase, Is.Not.EqualTo(table[9].vertexBase));
            Assert.That(table[8].vertexBase, Is.Not.EqualTo(table[9].vertexBase));
        }

        [UnityTest]
        public IEnumerator SharedPool_PartialNodeCannotCountItsOwnPagesAsUploadDestinations()
        {
            var runtime = CreateRuntime("partial-self", 1, 3, 3);
            yield return WaitUntil(() => runtime.IsReady);
            runtime.RequestNode(1); runtime.RequestNode(2);
            yield return WaitUntil(() => runtime.IsNodeResident(1) && runtime.IsNodeResident(2));
            object pool = SharedPool(runtime);
            var slots = (Array)pool.GetType().GetField("_slots", PrivateInstance).GetValue(pool);
            var pageSlots = (int[])typeof(ClusterMeshPageRuntime).GetField("_pageSlots", PrivateInstance).GetValue(runtime);
            int victim = pageSlots[1];
            typeof(ClusterMeshPageRuntime).GetMethod("OnSharedPageEvicted", PrivateInstance).Invoke(runtime, new object[]{1});
            // Construct the valid allocator state after later nodes evicted part
            // of node 1: all slots occupied, only node 1's own pages are cold.
            int replacement = 7;
            int serial = (int)typeof(ClusterMeshStreaming).GetProperty("UpdateSerial", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            for (int i = 0; i < slots.Length; i++)
            {
                object slot = slots.GetValue(i); var type = slot.GetType();
                type.GetField("lastUsed").SetValue(slot, serial);
                if (i == victim || type.GetField("owner").GetValue(slot) == null)
                {
                    type.GetField("owner").SetValue(slot, runtime);
                    type.GetField("pageId").SetValue(slot, replacement);
                    type.GetField("pinned").SetValue(slot, false);
                    type.GetField("lastUsed").SetValue(slot, serial);
                    pageSlots[replacement++] = i;
                }
                int id = (int)type.GetField("pageId").GetValue(slot);
                if (id == 2 || id == 3) type.GetField("lastUsed").SetValue(slot, -1000);
            }
            pool.GetType().GetField("_spaceSerial", PrivateInstance).SetValue(pool, int.MinValue);
            runtime.RequestNode(1); // Request normally refreshes resident pages; emulate a stale queued request below.
            foreach (object slot in slots)
            {
                int id = (int)slot.GetType().GetField("pageId").GetValue(slot);
                if (id == 2 || id == 3) slot.GetType().GetField("lastUsed").SetValue(slot, -1000);
            }
            pool.GetType().GetField("_spaceSerial", PrivateInstance).SetValue(pool, int.MinValue);
            Pump(runtime, 1, 0);
            Assert.That((long)PendingBytes.GetValue(runtime), Is.Zero);
            Assert.That(pool.GetType().GetField("_admittedOwner", PrivateInstance).GetValue(pool), Is.Null);
        }

        [UnityTest]
        public IEnumerator SharedPool_FixedCapacityQueuesBeforeReadingWhenHotPagesFillThePool()
        {
            var runtime = CreateRuntime("fixed-pressure", 1, 3, 3);
            yield return WaitUntil(() => runtime.IsReady);
            runtime.RequestNode(1); runtime.RequestNode(2);
            yield return WaitUntil(() => runtime.IsNodeResident(1) && runtime.IsNodeResident(2));
            runtime.RequestNode(3);
            Pump(runtime, 1, 0);
            Assert.That((long)PendingBytes.GetValue(runtime), Is.Zero, "No decoded memory may be held by a batch without enough recyclable GPU space.");
            Assert.That(runtime.IsReady, Is.True);
            Assert.That(runtime.IsNodeResident(3), Is.False);
            Assert.That(PoolValue(runtime, "Capacity"), Is.EqualTo(8));
        }

        static int CalculateCapacity(int pages, int roots, int largestDetailNode,
            int bakedCapacity, int requestedCapacity, out int window)
        {
            Assert.That(SharedCapacityMethod, Is.Not.Null);
            var descriptor = new ClusterMeshStreamDescriptor
            { pages = new ClusterMeshStreamPage[pages], poolPageCapacity = bakedCapacity };
            var arguments = new object[] { descriptor, requestedCapacity, roots, largestDetailNode, 0 };
            int capacity = (int)SharedCapacityMethod.Invoke(null, arguments);
            window = (int)arguments[4];
            return capacity;
        }

        static object SharedPool(ClusterMeshPageRuntime runtime) =>
            typeof(ClusterMeshPageRuntime).GetField("_sharedPool", PrivateInstance).GetValue(runtime);

        static int PoolValue(ClusterMeshPageRuntime runtime, string name)
        {
            object pool = SharedPool(runtime);
            Assert.That(pool, Is.Not.Null);
            return (int)pool.GetType().GetProperty(name).GetValue(pool);
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
            var reads = (IDictionary)typeof(ClusterMeshPageRuntime).GetField("_reads", PrivateInstance).GetValue(runtime);
            foreach (Task task in reads.Values) outstanding.Add(task);
            runtime.Dispose();
        }

        ClusterMeshPageRuntime CreateRuntime(string name, int rootPages = 1,
            int detailNodePages = 3, int detailNodeCount = 1, int poolCapacity = 8, bool expectFailure = false,
            int vertexCapacity = 3, int indexCapacity = 2)
        {
            // Default: one root page plus a three-page detail node. Both owners
            // fit eight GPU slots, but one detail node exceeds the decode budget.
            string path = Path.Combine(temporaryDirectory, name + ".cmstream");
            byte[] payload;
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(3); writer.Write(2); writer.Write(0);
                writer.Write(new byte[PayloadBytes - 12]);
                payload = memory.ToArray();
            }
            int pageCount = checked(rootPages + detailNodePages * detailNodeCount);
            var payloads = new byte[pageCount][];
            var pages = new ClusterMeshStreamPage[pageCount];
            var addresses = new ClusterMeshStreamAddress[pageCount];
            var clusters = new ClusterHeader[pageCount];
            for (int i = 0; i < pages.Length; i++)
            {
                payloads[i] = (byte[])payload.Clone();
                Buffer.BlockCopy(BitConverter.GetBytes((float)(i + 1)), 0, payloads[i], 12, 4);
                uint crc = 0xffffffffu;
                foreach (byte value in payloads[i])
                {
                    crc ^= value;
                    for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
                }
                crc ^= 0xffffffffu;
                pages[i] = new ClusterMeshStreamPage
                {
                    fileOffset = 16 + 40 * pages.Length + i * PayloadBytes,
                    compressedLength = PayloadBytes, uncompressedLength = PayloadBytes,
                    vertexCount = 3, indexWordCount = 2, crc32 = crc,
                    nodeIndex = i < rootPages ? 0 : 1 + (i - rootPages) / detailNodePages,
                    flags = i < rootPages ? ClusterMeshStreamPageFlags.Root : 0
                };
                addresses[i] = new ClusterMeshStreamAddress { pageId = (uint)i };
                clusters[i] = new ClusterHeader { vertexCount = 3, triangleCount = 1 };
            }
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(ClusterMeshStreaming.FileMagic); writer.Write(ClusterMeshStreaming.FormatVersion);
                writer.Write((int)ClusterMeshStreamKind.Static); writer.Write(pages.Length);
                foreach (var page in pages)
                {
                    writer.Write(page.fileOffset); writer.Write(page.compressedLength); writer.Write(page.uncompressedLength);
                    writer.Write(page.vertexCount); writer.Write(page.indexWordCount); writer.Write(page.weightCount);
                    writer.Write(page.crc32); writer.Write(page.nodeIndex); writer.Write(page.flags);
                }
                foreach (var bytes in payloads) writer.Write(bytes);
            }
            var nodes = new ClusterMeshStreamNode[1 + detailNodeCount];
            nodes[0] = new ClusterMeshStreamNode
            { clusterStart = 0, clusterCount = rootPages, parentNodeIndex = -1, lodLevel = 1 };
            for (int i = 0; i < detailNodeCount; i++)
                nodes[i + 1] = new ClusterMeshStreamNode
                {
                    clusterStart = rootPages + i * detailNodePages,
                    clusterCount = detailNodePages, parentNodeIndex = 0, lodLevel = 0
                };
            var descriptor = new ClusterMeshStreamDescriptor
            {
                formatVersion = ClusterMeshStreaming.FormatVersion, kind = ClusterMeshStreamKind.Static,
                fileName = path, editorFilePath = path, fileSize = new FileInfo(path).Length,
                poolPageCapacity = poolCapacity, vertexPageCapacity = vertexCapacity, indexPageCapacity = indexCapacity,
                pages = pages, addresses = addresses,
                nodes = nodes
            };
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.vertexStride = ClusterMeshLimits.ClusterVertexStride; asset.clusters = clusters; asset.streamDescriptor = descriptor;
            assets.Add(asset);
            var runtime = (ClusterMeshPageRuntime)Activator.CreateInstance(typeof(ClusterMeshPageRuntime), PrivateInstance,
                null, new object[] { asset, descriptor }, null);
            runtimes.Add(runtime);
            if (!expectFailure)
            {
                Assert.That(runtime.UsesGlobalSharedGpuPool, Is.True);
                Assert.That(runtime.Failed, Is.False, runtime.Error);
            }
            return runtime;
        }
    }
}
