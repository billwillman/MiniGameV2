using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed partial class ClusterMeshStreamingAdmissionTests
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

        delegate void StartReadPump(ref int slots,ref long readBytes,ref long decodedBytes);

        delegate bool RootBatchPump(ref int cursor, ref int slots, ref long readBytes, ref long decodedBytes);

        [UnityTest] public IEnumerator RootBatchDisabledDoesNotAllocateClosures() => CheckDeclinedRootBatchAllocations(1);
        [UnityTest] public IEnumerator SingleRootPageDoesNotAllocateBatchClosures() => CheckDeclinedRootBatchAllocations(16);

        IEnumerator CheckDeclinedRootBatchAllocations(int limit)
        {
            var runtime = CreateRuntime("declined-root-batch-" + limit, detailNodeCount: 0);
            yield return WaitUntil(() => runtime.IsReady);
            var scratch = (List<int>)typeof(ClusterMeshPageRuntime).GetField("_scratch", PrivateInstance).GetValue(runtime);
            scratch.Clear(); scratch.Add(0);
            var method = typeof(ClusterMeshPageRuntime).GetMethod("TryStartRootBatch", PrivateInstance);
            var pump = (RootBatchPump)Delegate.CreateDelegate(typeof(RootBatchPump), runtime, method);
            int previousLimit = settings.MaxRootPagesPerRead;
            int previousOperations = runtime.RootReadOperations, previousPages = runtime.BatchedRootPages;
            settings.MaxRootPagesPerRead = limit;
            try
            {
                bool incorrectlyStarted = false, budgetChanged = false;
                Action declined = () =>
                {
                    int cursor = 0, slots = 8;
                    long readBytes = 1024 * 1024, decodedBytes = 1024 * 1024;
                    incorrectlyStarted |= pump(ref cursor, ref slots, ref readBytes, ref decodedBytes);
                    budgetChanged |= cursor != 0 || slots != 8 || readBytes != 1024 * 1024 || decodedBytes != 1024 * 1024;
                };
                for (int i = 0; i < 16; i++) declined();
                Assert.That(ClusterMeshGcTests.Measure(() => GC.KeepAlive(new byte[1024]), 1), Is.GreaterThan(0));
                long events = ClusterMeshGcTests.Measure(declined, 128);
                TestContext.Out.WriteLine($"Declined root batch limit={limit}: {events} GC.Alloc events / 128 calls.");
                Assert.That(incorrectlyStarted, Is.False);
                Assert.That(budgetChanged, Is.False);
                Assert.That(runtime.RootReadOperations, Is.EqualTo(previousOperations));
                Assert.That(runtime.BatchedRootPages, Is.EqualTo(previousPages));
                Assert.That((long)PendingBytes.GetValue(runtime), Is.Zero, "Declined batches must not retain payloads.");
                Assert.That(events, Is.Zero, "Declined root batches must not allocate task closures.");
            }
            finally { settings.MaxRootPagesPerRead = previousLimit; }
        }

        [UnityTest] public IEnumerator BlockedReadBudgetDoesNotAllocateClosures()=>CheckBlockedReadAllocations(0);
        [UnityTest] public IEnumerator BlockedDecodedBudgetDoesNotAllocateClosures()=>CheckBlockedReadAllocations(1);
        [UnityTest] public IEnumerator BlockedLoadingNodeDoesNotAllocateClosures()=>CheckBlockedReadAllocations(2);

        IEnumerator CheckBlockedReadAllocations(int reason)
        {
            var runtime=CreateRuntime("blocked-allocation-"+reason,detailNodeCount:2);
            yield return WaitUntil(()=>runtime.IsReady);
            runtime.RequestNode(1);
            var method=typeof(ClusterMeshPageRuntime).GetMethod("StartReads",PrivateInstance);
            var pump=(StartReadPump)Delegate.CreateDelegate(typeof(StartReadPump),runtime,method);
            var loading=typeof(ClusterMeshPageRuntime).GetField("_loadingNode",PrivateInstance);
            int previous=(int)loading.GetValue(runtime);
            if(reason==2)loading.SetValue(runtime,2);
            try
            {
                Action blocked=()=>{int slots=8;long readBytes=reason==0?1:1024*1024,decodedBytes=reason==1?1:1024*1024;pump(ref slots,ref readBytes,ref decodedBytes);};
                for(int i=0;i<16;i++)blocked();
                Assert.That(ClusterMeshGcTests.Measure(()=>GC.KeepAlive(new byte[1024]),1),Is.GreaterThan(0));
                long events=ClusterMeshGcTests.Measure(blocked,128);
                TestContext.Out.WriteLine($"Blocked reads reason={reason}: {events} GC.Alloc events / 128 pumps.");
                Assert.That((long)PendingBytes.GetValue(runtime),Is.Zero,"Skipped pages must not queue payloads or grow retained IO memory.");
                Assert.That(events,Is.Zero,"Pending pages must not allocate task closures until their read is admitted.");
            }
            finally{loading.SetValue(runtime,previous);}
            yield return WaitUntil(()=>runtime.IsNodeResident(1));
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
            Assert.That(PoolValue(first, "ReservedDetailHeadroom"), Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator SharedPool_CompatibleSmallerPagesUsePhysicalStridesForRootsAndDetails()
        {
            var first = CreateRuntime("large-page-owner", 3, 3, 1, 16, vertexCapacity:9, indexCapacity:6);
            long bytes = ClusterMeshStreaming.SharedGpuBufferBytes;
            var second = CreateRuntime("small-page-owner", 4, 3, 1, 8, vertexCapacity:6, indexCapacity:4);
            Assert.That(SharedPool(second), Is.SameAs(SharedPool(first)), "An existing larger compatible pool must be reused without reallocating it.");
            Assert.That(ClusterMeshStreaming.SharedGpuBufferBytes, Is.EqualTo(bytes));
            yield return WaitUntil(() => first.IsReady && second.IsReady);
            VerifyRootGpu(first,3,false); VerifyRootGpu(second,4,false);
            first.RequestNode(1); second.RequestNode(1);
            yield return WaitUntil(() => first.IsNodeResident(1) && second.IsNodeResident(1));
            var occupied=new HashSet<uint>();
            foreach(var runtime in new[]{first,second})
            {
                var table=new ClusterMeshStreamPageTableEntry[runtime.PageCount];runtime.PageTable.GetData(table);
                for(int p=0;p<table.Length;p++)
                {
                    Assert.That(table[p].flags,Is.EqualTo(1));
                    var v=new ClusterPackedVertex[1];runtime.Vertices.GetData(v,0,(int)table[p].vertexBase,1);
                    Assert.That(v[0].position.x,Is.EqualTo(p+1),"Sharing must not address another owner's payload.");
                    Assert.That(occupied.Add(table[p].vertexBase),Is.True,"Every live page needs a disjoint physical range.");
                    var ix=new uint[1];runtime.Indices.GetData(ix,0,(int)table[p].indexBase,1);
                    Assert.That(ix[0],Is.EqualTo(p%2==0?0x00010000u:0x00020001u));
                }
            }
            DisposeRuntime(first);
            Assert.That(ClusterMeshStreaming.SharedGpuBufferBytes,Is.EqualTo(bytes),"A shared pool stays alive for its last owner.");
            VerifyRootGpu(second,4,false);
        }

        [Test]
        public void SharedPool_IncompatibleLayoutsCannotAlias()
        {
            var first=CreateRuntime("plain",1,detailNodeCount:0,vertexCapacity:9,indexCapacity:6);
            var tight=CreateRuntime("tight",1,detailNodeCount:0,vertexCapacity:6,indexCapacity:4,tight:true);
            var wide=CreateRuntime("wide-index",1,detailNodeCount:0,vertexCapacity:6,indexCapacity:8);
            Assert.That(SharedPool(tight),Is.Not.SameAs(SharedPool(first)));
            Assert.That(SharedPool(wide),Is.Not.SameAs(SharedPool(first)));
        }

        [Test]
        public void SharedPool_SimultaneousOwnersReserveTheirCombinedDetailWorkingSet()
        {
            var first=CreateRuntime("working-set-a");
            var second=CreateRuntime("working-set-b");
            var third=CreateRuntime("working-set-overflow");
            Assert.That(SharedPool(second),Is.SameAs(SharedPool(first)));
            Assert.That(SharedPool(third),Is.Not.SameAs(SharedPool(first)),"Three hot three-page nodes cannot coexist with three roots in eight slots.");
            DisposeRuntime(second);
            var replacement=CreateRuntime("working-set-replacement");
            Assert.That(SharedPool(replacement),Is.SameAs(SharedPool(first)),"Releasing an owner returns its detail reservation.");
        }

        [Test]
        public void SharedPool_LargerBakedPoolRetainsItsEntireUsableDetailCache()
        {
            // A baked asset's budget can exceed the default requested eight.
            // Reserving only eight detail slots would let new roots steal the
            // old owner's existing thirteen-slot working cache.
            var first=CreateRuntime("large-working-cache",3,1,15,16);
            var other=CreateRuntime("cannot-steal-working-cache",1,detailNodeCount:0);
            Assert.That(PoolValue(first,"ReservedDetailHeadroom"),Is.EqualTo(13));
            Assert.That(SharedPool(other),Is.Not.SameAs(SharedPool(first)));
            Assert.That(PoolValue(first,"Capacity"),Is.EqualTo(16));
        }

        [Test]
        public void SharedPool_SelectsSmallestCompatiblePhysicalSlot()
        {
            var large=CreateRuntime("wide-vertex-pool",1,detailNodeCount:0,vertexCapacity:9,indexCapacity:6);
            var small=CreateRuntime("wide-index-pool",1,detailNodeCount:0,vertexCapacity:6,indexCapacity:8);
            Assert.That(SharedPool(small),Is.Not.SameAs(SharedPool(large)));
            var next=CreateRuntime("best-fit-owner",1,detailNodeCount:0);
            Assert.That(SharedPool(next),Is.SameAs(SharedPool(small)));
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
            Assert.That(runtime.Vertices.count, Is.EqualTo(5 * 9 + 3 * 3));
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

        [UnityTest] public IEnumerator AdjacentRootsUseOneRead()=>VerifyRootBatch(false);
        [UnityTest] public IEnumerator PackedRootsUploadAsOneBoundedBatch()=>VerifyUploadBatch(false);
        [UnityTest] public IEnumerator TightPackedRootsUploadAsOneBoundedBatch()=>VerifyUploadBatch(true);
        IEnumerator VerifyUploadBatch(bool tight)
        {
            var runtime=CreateRuntime("upload-packed",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8,tight:tight,individualRoots:true);
            yield return PrepareUploads(runtime,4);
            var args=new object[]{0,0L,0L,1,1024L*1024};UpdateMethod.Invoke(runtime,args);
            Assert.That((int)args[3],Is.Zero);Assert.That(runtime.IsReady,Is.True);
            Assert.That(runtime.RootUploadOperations,Is.EqualTo(1));Assert.That(runtime.BatchedRootUploadPages,Is.EqualTo(4));
            Assert.That((long)args[4],Is.EqualTo(1024L*1024-4*(3*runtime.Geometry.ResolvedVertexStride+8)));
            Assert.That(PoolValue(runtime,"Capacity"),Is.EqualTo(8));
            VerifyRootGpu(runtime,4,tight);
        }
        [UnityTest] public IEnumerator RootUploadBatchHonorsByteBudgetAndPartialNodeResidency()
        {
            var runtime=CreateRuntime("upload-bytes",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8);
            yield return PrepareUploads(runtime,4);
            long bytes=2*(PayloadBytes-12);var args=new object[]{0,0L,0L,1,bytes};UpdateMethod.Invoke(runtime,args);
            Assert.That((long)args[4],Is.Zero);Assert.That(runtime.BatchedRootUploadPages,Is.EqualTo(2));
            Assert.That(runtime.IsReady,Is.False);Assert.That(runtime.IsNodeResident(0),Is.False);
            args=new object[]{0,0L,0L,1,bytes};UpdateMethod.Invoke(runtime,args);
            Assert.That(runtime.IsReady,Is.True);Assert.That(runtime.RootUploadOperations,Is.EqualTo(2));VerifyRootGpu(runtime,4,false);
        }
        [UnityTest] public IEnumerator RootUploadDoesNotCrossSlotsOrWaitForFullBatch()
        {
            var runtime=CreateRuntime("upload-slots",4,detailNodeCount:0,vertexCapacity:6,indexCapacity:4);
            yield return PrepareUploads(runtime,4);
            var args=new object[]{0,0L,0L,1,1024L*1024};UpdateMethod.Invoke(runtime,args);
            Assert.That(runtime.IsReady,Is.False);Assert.That(runtime.BatchedRootUploadPages,Is.EqualTo(2));
            args=new object[]{0,0L,0L,1,1024L*1024};UpdateMethod.Invoke(runtime,args);
            Assert.That(runtime.IsReady,Is.True);VerifyRootGpu(runtime,4,false);
        }
        [UnityTest] public IEnumerator RootUploadDisabledRetainsSinglePageQuota()
        {
            settings.MaxRootPagesPerUpload=1;
            var runtime=CreateRuntime("upload-off",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8);
            yield return PrepareUploads(runtime,4);
            var args=new object[]{0,0L,0L,1,1024L*1024};UpdateMethod.Invoke(runtime,args);
            Assert.That(runtime.IsReady,Is.False);Assert.That(runtime.RootUploadOperations,Is.EqualTo(1));
            Assert.That(runtime.BatchedRootUploadPages,Is.Zero);
            PumpWithBudget(runtime,1024*1024);Assert.That(runtime.IsReady,Is.True);VerifyRootGpu(runtime,4,false);
        }
        [UnityTest] public IEnumerator RootUploadMissingMiddleCannotOverwriteOrExposeMissingPage()
        {
            var runtime=CreateRuntime("upload-gap",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8);
            yield return PrepareUploads(runtime,4);
            var complete=(IDictionary)typeof(ClusterMeshPageRuntime).GetField("_completed",PrivateInstance).GetValue(runtime);
            object middle=complete[1];complete.Remove(1);
            try
            {
                var args=new object[]{0,0L,0L,1,1024L*1024};UpdateMethod.Invoke(runtime,args);
                Assert.That(runtime.IsReady,Is.False);Assert.That(runtime.BatchedRootUploadPages,Is.Zero);
                var table=new ClusterMeshStreamPageTableEntry[4];runtime.PageTable.GetData(table);
                Assert.That(table[0].flags,Is.EqualTo(1));Assert.That(table[1].flags|table[2].flags|table[3].flags,Is.Zero);
            }
            finally{complete.Add(1,middle);}
            var rest=new object[]{0,0L,0L,1,1024L*1024};UpdateMethod.Invoke(runtime,rest);
            Assert.That(runtime.IsReady,Is.True);Assert.That(runtime.BatchedRootUploadPages,Is.EqualTo(3));VerifyRootGpu(runtime,4,false);
        }
        [UnityTest] public IEnumerator RootPriorityUsesGlobalQuotaAndReturnsToRoundRobinWhenReady()
        {
            var low=CreateRuntime("priority-low",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8);
            var high=CreateRuntime("priority-high",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8);
            yield return PrepareUploads(low,4);yield return PrepareUploads(high,4);
            const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
            var map=(IDictionary)typeof(ClusterMeshStreaming).GetField("Runtimes",flags).GetValue(null);
            var rotation=typeof(ClusterMeshStreaming).GetField("_roundRobinStart",flags);object previous=rotation.GetValue(null);
            settings.MaxPageUploadsPerUpdate=1;high.RootLoadPriority=2;
            map.Add(low.Geometry,low);map.Add(high.Geometry,high);rotation.SetValue(null,0);
            try
            {
                ClusterMeshStreaming.Update();
                Assert.That(high.IsReady,Is.True,"Visible terrain must receive the shared upload quota first.");
                Assert.That(low.IsReady,Is.False,"Priority must not create a second upload budget.");
                ClusterMeshStreaming.Update();Assert.That(low.IsReady,Is.True,"Ready high-priority assets cannot starve queued roots.");
            }
            finally{map.Remove(low.Geometry);map.Remove(high.Geometry);rotation.SetValue(null,previous);}
        }
        IEnumerator PrepareUploads(ClusterMeshPageRuntime runtime,int count)
        {
            var complete=(IDictionary)typeof(ClusterMeshPageRuntime).GetField("_completed",PrivateInstance).GetValue(runtime);
            double until=EditorApplication.timeSinceStartup+10;
            while(complete.Count<count)
            {Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(until));PumpWithBudget(runtime,1024*1024,0);yield return null;}
        }
        [UnityTest] public IEnumerator DetailPriorityLeavesRegularProgressForBackgroundRoots()
        {
            var high=CreateRuntime("detail-priority",1);
            yield return PrepareUploads(high,1);PumpWithBudget(high,1024*1024);Assert.That(high.IsReady,Is.True);
            var low=CreateRuntime("background-roots",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8);
            yield return PrepareUploads(low,4);
            high.RequestNode(1);yield return PrepareUploads(high,3);high.DetailLoadPriority=1;
            const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
            var map=(IDictionary)typeof(ClusterMeshStreaming).GetField("Runtimes",flags).GetValue(null);
            var rotation=typeof(ClusterMeshStreaming).GetField("_roundRobinStart",flags);
            var serial=typeof(ClusterMeshStreaming).GetField("_updateSerial",flags);
            object previous=rotation.GetValue(null),previousSerial=serial.GetValue(null);
            settings.MaxPageUploadsPerUpdate=1;
            map.Add(low.Geometry,low);map.Add(high.Geometry,high);rotation.SetValue(null,0);serial.SetValue(null,0);
            try
            {
                ClusterMeshStreaming.Update();Assert.That(low.IsReady,Is.False);
                var table=new ClusterMeshStreamPageTableEntry[high.PageCount];high.PageTable.GetData(table);
                Assert.That(table[1].flags|table[2].flags|table[3].flags,Is.EqualTo(1),"Visible detail receives upload priority.");
                int lowIndex=0;foreach(DictionaryEntry pair in map){if(ReferenceEquals(pair.Key,low.Geometry))break;lowIndex++;}
                rotation.SetValue(null,lowIndex);serial.SetValue(null,3);ClusterMeshStreaming.Update();
                Assert.That(low.IsReady,Is.True,"Every fourth update returns to round-robin for background progress.");
            }
            finally{map.Remove(low.Geometry);map.Remove(high.Geometry);rotation.SetValue(null,previous);serial.SetValue(null,previousSerial);}
        }
        [UnityTest] public IEnumerator WarmPriorityDispatchDoesNotAllocate()
        {
            var a=CreateRuntime("dispatch-gc-a",1,detailNodeCount:0);
            var b=CreateRuntime("dispatch-gc-b",1,detailNodeCount:0);
            yield return PrepareUploads(a,1);yield return PrepareUploads(b,1);
            PumpWithBudget(a,1024*1024);PumpWithBudget(b,1024*1024);
            const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
            var map=(IDictionary)typeof(ClusterMeshStreaming).GetField("Runtimes",flags).GetValue(null);
            map.Add(a.Geometry,a);map.Add(b.Geometry,b);b.DetailLoadPriority=1;
            try
            {
                for(int i=0;i<8;i++)ClusterMeshStreaming.Update();
                Assert.That(ClusterMeshGcTests.Measure(ClusterMeshStreaming.Update,64),Is.Zero,"Warm central priority dispatch must not allocate.");
            }
            finally{map.Remove(a.Geometry);map.Remove(b.Geometry);}
        }
        static void VerifyRootGpu(ClusterMeshPageRuntime runtime,int count,bool tight)
        {
            var table=new ClusterMeshStreamPageTableEntry[runtime.PageCount];runtime.PageTable.GetData(table);
            for(int n=0;n<count;n++)
            {
                Assert.That(table[n].flags,Is.EqualTo(1));
                if(tight){var v=new ClusterPackedVertexTight[1];runtime.VerticesTight.GetData(v,0,(int)table[n].vertexBase,1);Assert.That(v[0].px,Is.EqualTo(n+1));}
                else{var v=new ClusterPackedVertex[1];runtime.Vertices.GetData(v,0,(int)table[n].vertexBase,1);Assert.That(v[0].position.x,Is.EqualTo(n+1));}
                var indices=new uint[2];runtime.Indices.GetData(indices,0,(int)table[n].indexBase,2);Assert.That(indices[0],Is.EqualTo(n%2==0?0x00010000u:0x00020001u));
                Assert.That(runtime.IsNodeResident(runtime.Descriptor.pages[n].nodeIndex),Is.True);
            }
        }
        [UnityTest] public IEnumerator CompressedAdjacentRootsUseOneRead()=>VerifyRootBatch(true);
        IEnumerator VerifyRootBatch(bool compressed)
        {
            var runtime=CreateRuntime("batch",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8,compress:compressed);
            double until=EditorApplication.timeSinceStartup+10;
            while(!runtime.IsReady)
            {
                Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(until));
                PumpWithBudget(runtime,1024*1024);yield return null;
            }
            Assert.That(runtime.RootReadOperations,Is.EqualTo(1));
            Assert.That(runtime.BatchedRootPages,Is.EqualTo(4));
            Assert.That(PoolValue(runtime,"Capacity"),Is.EqualTo(8));
            var data=new ClusterPackedVertex[12];runtime.Vertices.GetData(data,0,0,12);
            for(int n=0;n<4;n++)Assert.That(data[n*3].position.x,Is.EqualTo(n+1),"Batch slice boundaries must not alias.");
        }
        [UnityTest] public IEnumerator NonAdjacentRootsRemainSeparateReads()
        {
            var runtime=CreateRuntime("gapped",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8,gap:17);
            double until=EditorApplication.timeSinceStartup+10;
            while(!runtime.IsReady){Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(until));PumpWithBudget(runtime,1024*1024);yield return null;}
            Assert.That(runtime.RootReadOperations,Is.EqualTo(4));Assert.That(runtime.BatchedRootPages,Is.Zero);
        }
        [UnityTest] public IEnumerator RootBatchFallsBackWithinOnePageDecodeBudget()
        {
            var runtime=CreateRuntime("tiny-batch",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8);
            double until=EditorApplication.timeSinceStartup+10;
            while(!runtime.IsReady){Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(until));PumpWithBudget(runtime,PayloadBytes);yield return null;}
            Assert.That(runtime.RootReadOperations,Is.EqualTo(4));Assert.That(runtime.BatchedRootPages,Is.Zero);
        }
        [UnityTest] public IEnumerator CorruptRootBatchFailsWithoutPartialUploads()
        {
            var runtime=CreateRuntime("bad-batch",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8,corruptPage:2);
            LogAssert.Expect(LogType.Error,"ClusterMesh page streaming: ClusterMesh stream page checksum is invalid.");
            double until=EditorApplication.timeSinceStartup+10;
            while(!runtime.Failed)
            {
                Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(until));
                var arguments=new object[]{1,1024L*1024,1024L*1024-(long)PendingBytes.GetValue(runtime),8,1024L*1024};
                UpdateMethod.Invoke(runtime,arguments);yield return null;
            }
            Assert.That(runtime.IsReady,Is.False);Assert.That((long)PendingBytes.GetValue(runtime),Is.Zero);
        }
        [UnityTest] public IEnumerator DisposingInflightRootBatchReleasesStreams()
        {
            var runtime=CreateRuntime("dispose-batch",4,detailNodeCount:0,vertexCapacity:12,indexCapacity:8);
            double until=EditorApplication.timeSinceStartup+10;
            while(runtime.RootReadOperations==0)
            {Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(until));PumpWithBudget(runtime,1024*1024,0);yield return null;}
            DisposeRuntime(runtime);foreach(var task in outstanding)while(!task.IsCompleted)yield return null;
            using(var file=new FileStream(runtime.Descriptor.fileName,FileMode.Open,FileAccess.Read,FileShare.None))Assert.That(file.Length,Is.GreaterThan(0));
            Assert.That((long)PendingBytes.GetValue(runtime),Is.Zero);
        }
        static void PumpWithBudget(ClusterMeshPageRuntime runtime,long budget,int uploads=8)
        {
            var arguments=new object[]{1,1024L*1024,budget-(long)PendingBytes.GetValue(runtime),uploads,1024L*1024};
            UpdateMethod.Invoke(runtime,arguments);Assert.That(runtime.Failed,Is.False,runtime.Error);
            Assert.That((long)PendingBytes.GetValue(runtime),Is.LessThanOrEqualTo(budget));
        }

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
            var batches=(IEnumerable)typeof(ClusterMeshPageRuntime).GetField("_rootBatches",PrivateInstance).GetValue(runtime);
            foreach(var batch in batches)outstanding.Add((Task)batch.GetType().GetField("task").GetValue(batch));
            runtime.Dispose();
        }

        ClusterMeshPageRuntime CreateRuntime(string name, int rootPages = 1,
            int detailNodePages = 3, int detailNodeCount = 1, int poolCapacity = 8, bool expectFailure = false,
            int vertexCapacity = 3, int indexCapacity = 2, bool compress=false, int gap=0, int corruptPage=-1,
            bool tight=false, bool individualRoots=false)
        {
            // Default: one root page plus a three-page detail node. Both owners
            // fit eight GPU slots, but one detail node exceeds the decode budget.
            string path = Path.Combine(temporaryDirectory, name + ".cmstream");
            byte[] payload;
            int stride=tight?ClusterMeshLimits.TightVertexStride:ClusterMeshLimits.ClusterVertexStride;
            int payloadLength=12+3*stride+8;
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(3); writer.Write(2); writer.Write(0);
                writer.Write(new byte[payloadLength - 12]);
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
                Buffer.BlockCopy(BitConverter.GetBytes(i%2==0?0x00010000u:0x00020001u),0,payloads[i],12+3*stride,4);
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
                    compressedLength = payloadLength, uncompressedLength = payloadLength,
                    vertexCount = 3, indexWordCount = 2, crc32 = crc,
                    nodeIndex = i < rootPages ? (individualRoots?i:0) : (individualRoots?rootPages:1) + (i - rootPages) / detailNodePages,
                    flags = i < rootPages ? ClusterMeshStreamPageFlags.Root : 0
                };
                addresses[i] = new ClusterMeshStreamAddress { pageId = (uint)i };
                clusters[i] = new ClusterHeader { vertexCount = 3, triangleCount = 1 };
            }
            long offset=16+40*pages.Length;
            for(int i=0;i<pages.Length;i++)
            {
                if(compress)
                {
                    using(var packed=new MemoryStream())
                    {
                        using(var zip=new DeflateStream(packed,System.IO.Compression.CompressionLevel.Optimal,true))zip.Write(payloads[i],0,payloads[i].Length);
                        payloads[i]=packed.ToArray();
                    }
                }
                if(i==corruptPage)payloads[i][payloads[i].Length-1]^=1;
                pages[i].fileOffset=offset;pages[i].compressedLength=payloads[i].Length;offset+=payloads[i].Length+gap;
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
                foreach (var bytes in payloads){writer.Write(bytes);if(gap>0)writer.Write(new byte[gap]);}
            }
            int rootNodeCount=individualRoots?rootPages:1;
            var nodes = new ClusterMeshStreamNode[rootNodeCount + detailNodeCount];
            for(int i=0;i<rootNodeCount;i++)nodes[i] = new ClusterMeshStreamNode
            { clusterStart = individualRoots?i:0, clusterCount = individualRoots?1:rootPages, parentNodeIndex = -1, lodLevel = 1 };
            for (int i = 0; i < detailNodeCount; i++)
                nodes[i + rootNodeCount] = new ClusterMeshStreamNode
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
            asset.vertexStride = stride; asset.clusters = clusters; asset.streamDescriptor = descriptor;
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
