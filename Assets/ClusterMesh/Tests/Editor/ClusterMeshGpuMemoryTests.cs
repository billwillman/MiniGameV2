using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed partial class ClusterMeshStreamingAdmissionTests
    {
        [UnityTest] public IEnumerator CompactDetailsPreserveRootsAndEveryPageAcrossGrowth() => CheckGpuGrowth(false);
        [UnityTest] public IEnumerator TightVerticesPreserveRootsAndEveryPageAcrossGrowth() => CheckGpuGrowth(true);

        IEnumerator CheckGpuGrowth(bool tight)
        {
            settings.InitialDetailGpuKiB = 0;
            settings.DetailGpuGrowthKiB = 1;
            var runtime = CreateRuntime("grow-" + tight, 1, 1, 15, 16, vertexCapacity:4096, indexCapacity:4096, tight:tight);
            int initial = runtime.PhysicalPoolPageCapacity;
            Assert.That(initial, Is.EqualTo(2), "Only one root slot and one complete detail node should be allocated initially.");
            long before = ClusterMeshStreaming.SharedGpuBufferBytes;
            long grows = ClusterMeshStreaming.GpuGrowthCount;
            yield return WaitUntil(() => runtime.IsReady);
            var oldVertices = tight ? runtime.VerticesTight : runtime.Vertices;
            // Request all nodes without evicting an earlier one. Maximum cache
            // pages remain unchanged; growing merely makes that capacity real.
            for (int node = 1; node <= 15; node++) runtime.RequestNode(node);
            double deadline = EditorApplication.timeSinceStartup + 15;
            while (true)
            {
                bool complete = true;
                for (int node = 1; node <= 15; node++) complete &= runtime.IsNodeResident(node);
                if (complete && ClusterMeshStreaming.GpuMigrationBytes == 0) break;
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                ClusterMeshStreaming.Update();
                PumpWithBudget(runtime, 1024 * 1024);
                Assert.That(ClusterMeshStreaming.SharedGpuBufferBytes, Is.LessThanOrEqualTo(ClusterMeshStreaming.GpuMemoryBudgetBytes));
                yield return null;
            }
            Assert.That(ClusterMeshStreaming.GpuGrowthCount, Is.GreaterThan(grows));
            Assert.That(tight ? runtime.VerticesTight : runtime.Vertices, Is.Not.SameAs(oldVertices));
            Assert.That(runtime.PhysicalPoolPageCapacity, Is.EqualTo(16));
            Assert.That(runtime.SharedPageEvictions, Is.Zero);
            VerifyRootGpu(runtime, runtime.PageCount, tight); // Includes every fine page's GPU bytes.
            var table = new ClusterMeshStreamPageTableEntry[runtime.PageCount]; runtime.PageTable.GetData(table);
            Assert.That(table[1].vertexBase, Is.EqualTo(4096));
            Assert.That(table[2].vertexBase - table[1].vertexBase, Is.EqualTo(3));
            Assert.That(table[2].indexBase - table[1].indexBase, Is.EqualTo(2));
            long expectedIncrease = 14L * (3 * (tight ? 24 : 32) + 8);
            Assert.That(ClusterMeshStreaming.SharedGpuBufferBytes - before, Is.EqualTo(expectedIncrease));
        }

        [TestCase(4)] [TestCase(16)] [TestCase(24)] [TestCase(32)] [TestCase(40)]
        public void GpuCopyPreservesAllBitsForIndicesWeightsAndEveryVertexLayout(int stride)
        {
            var type = typeof(ClusterMeshStreaming).Assembly.GetType("ClusterMesh.ClusterMeshGpuBufferCopy");
            Assert.That((bool)type.GetProperty("Available").GetValue(null), Is.True);
            var input = new uint[19 * stride / 4];
            for (int i = 0; i < input.Length; i++) input[i] = unchecked(0xff810000u + (uint)(i * 977));
            using (var source = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 19, stride))
            using (var destination = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 37, stride))
            {
                source.SetData(input);
                type.GetMethod("Copy").Invoke(null, new object[] { source, destination });
                var output = new uint[input.Length]; destination.GetData(output);
                Assert.That(output, Is.EqualTo(input), "Includes NaN/packed attribute bit patterns; no float conversion is allowed.");
            }
        }

        static void ResetPressureLog()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            typeof(ClusterMeshStreaming).GetField("pressureLogged", flags).SetValue(null, false);
            typeof(ClusterMeshStreaming).GetField("recoveryUnavailableLogged", flags).SetValue(null, false);
            typeof(ClusterMeshStreaming).GetField("recoveryRequiredBytes", flags).SetValue(null, 0L);
        }

        [UnityTest]
        public IEnumerator ImpossibleRootBudgetLogsErrorAndRejectsInsteadOfWaitingForever()
        {
            ResetPressureLog();
            settings.MaxSharedGpuMegabytes = 1;
            settings.InitialDetailGpuKiB = 0;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^ClusterMesh GPU budget pressure:"));
            var runtime = CreateRuntime("budget-reject", 1, 1, 15, 16, expectFailure:true, vertexCapacity:65536, indexCapacity:4096);
            Assert.That(runtime.WaitingForGpuBudget, Is.True);
            Assert.That(runtime.PhysicalPoolPageCapacity, Is.Zero);
            Assert.That(runtime.Vertices, Is.Null);
            Assert.That(ClusterMeshStreaming.SharedGpuBufferBytes, Is.Zero);
            var map = (System.Collections.IDictionary)typeof(ClusterMeshStreaming).GetField("Runtimes", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            map.Add(runtime.Geometry, runtime);
            try
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^ClusterMesh GPU rebuild cannot free enough capacity:"));
                ClusterMeshStreaming.Update();
                Assert.That(runtime.WaitingForGpuBudget, Is.False);
                Assert.That(runtime.Failed, Is.True);
                Assert.That(runtime.Error, Does.Contain("MaxSharedGpuMegabytes"));
                Assert.That(runtime.RootReadOperations, Is.Zero);
                for (int i = 0; i < 8; i++) ClusterMeshStreaming.Update(); // No repeated errors or exceptions.
                settings.MaxSharedGpuMegabytes = 4;
                ClusterMeshStreaming.Retry(runtime.Geometry);
                var retry = ClusterMeshStreaming.Request(runtime.Geometry); runtimes.Add(retry);
                yield return WaitUntil(() => retry.IsReady);
                VerifyRootGpu(retry, 1, false);
            }
            finally { map.Remove(runtime.Geometry); }
        }

        [UnityTest]
        public IEnumerator BudgetPressureAutomaticallyShrinksAndRebuildsWithoutLosingRoots()
        {
            ResetPressureLog();
            settings.MaxSharedGpuMegabytes = 1;
            settings.InitialDetailGpuKiB = 1024;
            var first = CreateRuntime("shrink-first", 1, 1, 4000, 4096);
            var second = CreateRuntime("shrink-second", 1, 1, 4000, 4096);
            yield return WaitUntil(() => first.IsReady && second.IsReady);
            first.RequestNode(1); first.RequestNode(2);
            second.RequestNode(1); second.RequestNode(2);
            yield return WaitUntil(() => first.IsNodeResident(2) && second.IsNodeResident(2));
            long shrinks = ClusterMeshStreaming.GpuShrinkCount;
            long before = ClusterMeshStreaming.SharedGpuBufferBytes;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^ClusterMesh GPU budget pressure:"));
            var third = CreateRuntime("shrink-new-region", 1, 1, 4000, 4096, expectFailure:true);
            Assert.That(third.WaitingForGpuBudget, Is.True);
            var map = (System.Collections.IDictionary)typeof(ClusterMeshStreaming).GetField("Runtimes", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            foreach (var runtime in runtimes) map.Add(runtime.Geometry, runtime);
            try
            {
                double deadline = EditorApplication.timeSinceStartup + 15;
                long peak = before;
                while (!third.IsReady || ClusterMeshStreaming.GpuMigrationBytes != 0)
                {
                    Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                    ClusterMeshStreaming.Update();
                    peak = Math.Max(peak, ClusterMeshStreaming.SharedGpuBufferBytes);
                    Assert.That(peak, Is.LessThanOrEqualTo(ClusterMeshStreaming.GpuMemoryBudgetBytes));
                    Assert.That(first.IsReady && second.IsReady, Is.True, "Pinned roots remain ready throughout rebuild.");
                    Assert.That(third.Failed, Is.False, third.Error);
                    yield return null;
                }
                Assert.That(ClusterMeshStreaming.GpuShrinkCount, Is.GreaterThan(shrinks));
                Assert.That(first.SharedPageEvictions + second.SharedPageEvictions, Is.GreaterThan(0));
                Assert.That(first.IsNodeResident(0) && second.IsNodeResident(0), Is.True);
                foreach (var runtime in runtimes) VerifyRootGpu(runtime, 1, false);
                Assert.That(ClusterMeshStreaming.SharedGpuBufferBytes, Is.LessThanOrEqualTo(ClusterMeshStreaming.GpuSteadyBudgetBytes));
                TestContext.Out.WriteLine($"Automatic rebuild: before={before}, peak={peak}, after={ClusterMeshStreaming.SharedGpuBufferBytes}, reclaimed={ClusterMeshStreaming.GpuReclaimedBytes}");
            }
            finally { foreach (var runtime in runtimes) map.Remove(runtime.Geometry); }
        }

        [UnityTest]
        public IEnumerator GrowthAccountsForBothBuffersAndRecyclesDetailsAtTheLimit()
        {
            settings.MaxSharedGpuMegabytes = 1;
            settings.InitialDetailGpuKiB = 0;
            var runtime = CreateRuntime("budget-growth", 1, 1, 15, 16, vertexCapacity:16384, indexCapacity:4096);
            yield return WaitUntil(() => runtime.IsReady);
            long before = ClusterMeshStreaming.SharedGpuBufferBytes;
            long denials = ClusterMeshStreaming.GpuBudgetDenials;
            // Current buffer alone fits. Two copies exceed 1 MiB, even though
            // the tiny detail suffix would fit if we incorrectly used a delta.
            runtime.RequestNode(1); yield return WaitUntil(() => runtime.IsNodeResident(1));
            runtime.RequestNode(2);
            double deadline = EditorApplication.timeSinceStartup + 10;
            while (!runtime.IsNodeResident(2))
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                ClusterMeshStreaming.Update(); PumpWithBudget(runtime, 1024 * 1024); yield return null;
            }
            Assert.That(ClusterMeshStreaming.GpuBudgetDenials, Is.GreaterThan(denials));
            Assert.That(ClusterMeshStreaming.SharedGpuBufferBytes, Is.EqualTo(before));
            Assert.That(runtime.PhysicalPoolPageCapacity, Is.EqualTo(2));
            Assert.That(runtime.SharedPageEvictions, Is.GreaterThan(0));
            VerifyRootGpu(runtime, 1, false);
        }
    }
}
