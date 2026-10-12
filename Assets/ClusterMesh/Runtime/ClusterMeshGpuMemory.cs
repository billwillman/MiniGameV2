using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    // Offsets never move when the detail suffix grows. Existing page tables,
    // root packing and pending whole-node reservations remain valid.
    internal sealed partial class ClusterMeshSharedGpuPool
    {
        public int RootSlotCapacity { get; private set; }
        public int DetailVertexCapacity { get; private set; }
        public int DetailIndexCapacity { get; private set; }
        public int AllocatedCapacity { get; private set; }
        BufferSet _buffers, _retired;
        AsyncGPUReadbackRequest _copyCompletion;
        bool _growing;
        long _lastGrowthAttempt = -1;
        bool _trimmedForBudget;
        public bool MigrationPending => _growing;
        public int MinimumDetailSlots { get; private set; }
        int RootReservationLimit => DetailVertexCapacity >= Key.vertexPageCapacity && DetailIndexCapacity >= Key.indexPageCapacity
            ? AllocatedCapacity : RootSlotCapacity;
        int MinimumRetainedCapacity
        {
            get
            {
                int target = RootSlotCapacity + MinimumDetailSlots;
                for (int i = target; i < AllocatedCapacity; i++) if (_slots[i].pinned) target = i + 1;
                return target;
            }
        }
        public long MinimumBufferBytes => EstimateBytes(Key, RootSlotCapacity, MinimumRetainedCapacity - RootSlotCapacity, DetailVertexCapacity, DetailIndexCapacity);
        public long ReclaimableBytes => _growing || _admittedOwner != null ? 0 : Math.Max(0, BufferBytes - MinimumBufferBytes);

        public long BufferBytes => (_buffers == null ? 0 : _buffers.Bytes) + MigrationBytes;
        public long MigrationBytes => _retired == null ? 0 : _retired.Bytes;
        public int VertexBase(int slot) => slot < RootSlotCapacity ? slot * Key.vertexPageCapacity :
            RootSlotCapacity * Key.vertexPageCapacity + (slot - RootSlotCapacity) * DetailVertexCapacity;
        public int IndexBase(int slot) => slot < RootSlotCapacity ? slot * Key.indexPageCapacity :
            RootSlotCapacity * Key.indexPageCapacity + (slot - RootSlotCapacity) * DetailIndexCapacity;

        internal static long EstimateBytes(ClusterMeshSharedPoolKey key, int roots, int details, int detailVertices, int detailIndices)
        {
            long vertices = (long)roots * key.vertexPageCapacity + (long)details * detailVertices;
            long indices = (long)roots * key.indexPageCapacity + (long)details * detailIndices;
            // Inactive bindings still need small, valid dummy buffers.
            return vertices * (key.vertexStride + key.weightStride) + indices * 4 +
                (key.vertexStride == ClusterMeshLimits.TightVertexStride ? 32 : 24) +
                (key.weightStride == 16 ? 8 : key.weightStride == 8 ? 16 : 24);
        }

        internal static int InitialDetails(ClusterMeshSharedPoolKey key, int maximum, int minimum,
            int vertices, int indices)
        {
            if (!ClusterMeshGpuBufferCopy.Available) return maximum;
            long bytes = (long)vertices * (key.vertexStride + key.weightStride) + (long)indices * 4;
            return Math.Min(maximum, Math.Max(minimum, (int)Math.Min(int.MaxValue,
                ClusterMeshStreaming.InitialGpuDetailBytes / Math.Max(1, bytes))));
        }

        void InitializeBuffers(int rootSlots, int detailVertices, int detailIndices, int initialDetails, int minimumDetails)
        {
            RootSlotCapacity = rootSlots;
            DetailVertexCapacity = detailVertices;
            DetailIndexCapacity = detailIndices;
            MinimumDetailSlots = minimumDetails;
            AllocatedCapacity = rootSlots + initialDetails;
            SetBuffers(new BufferSet(Key, VertexBase(AllocatedCapacity), IndexBase(AllocatedCapacity)));
        }

        void SetBuffers(BufferSet buffers)
        {
            _buffers = buffers;
            Vertices = buffers.vertices; VerticesTight = buffers.tight; Indices = buffers.indices;
            Weights = buffers.weights; Weights8 = buffers.weights8;
            foreach (var pair in _reservations) pair.Key.RefreshSharedGpuBuffers();
        }

        // Called before admission, never during a node's in-flight uploads.
        void GrowForAdmission(int missing)
        {
            PollMigration();
            // Keep recovered capacity available for newly visible roots. Do not
            // immediately grow the same cache back and trigger rebuild thrashing.
            if (_trimmedForBudget && ClusterMeshStreaming.SharedGpuBufferBytes > ClusterMeshStreaming.GpuSteadyBudgetBytes / 2) return;
            _trimmedForBudget = false;
            if (_growing || AllocatedCapacity >= Capacity || _lastGrowthAttempt == ClusterMeshStreaming.UpdateSerial) return;
            int free = 0;
            for (int i = 0; i < AllocatedCapacity; i++) if (_slots[i].owner == null) free++;
            if (free >= missing) return;
            _lastGrowthAttempt = ClusterMeshStreaming.UpdateSerial;
            if (!ClusterMeshStreaming.CanStartGpuGrowth || !ClusterMeshGpuBufferCopy.Available) return;
            long slotBytes = (long)DetailVertexCapacity * (Key.vertexStride + Key.weightStride) + (long)DetailIndexCapacity * 4;
            int increment = (int)Math.Min(int.MaxValue, Math.Max(1, ClusterMeshStreaming.GpuGrowthBytes / Math.Max(1, slotBytes)));
            int target = Math.Min(Capacity, AllocatedCapacity + Math.Max(missing - free, increment));
            long newBytes = EstimateBytes(Key, RootSlotCapacity, target - RootSlotCapacity, DetailVertexCapacity, DetailIndexCapacity);
            // Both buffers are live until the GPU copy has completed. Never
            // count just their difference or read geometry back to the CPU.
            if (!ClusterMeshStreaming.CanGrowGpuBytes(newBytes, _buffers.Bytes)) return;
            Rebuild(target);
            ClusterMeshStreaming.NoteGpuGrowth();
        }

        internal bool TryShrinkForBudget()
        {
            if (ReclaimableBytes <= 0 || !ClusterMeshGpuBufferCopy.Available) return false;
            if (MinimumBufferBytes > ClusterMeshStreaming.GpuMemoryBudgetBytes - ClusterMeshStreaming.SharedGpuBufferBytes) return false;
            int target = MinimumRetainedCapacity;
            // No in-flight node may be truncated. Completed fine pages can fall
            // back to their pinned parents even if they were recently visible.
            for (int i = target; i < AllocatedCapacity; i++)
                if (_slots[i].owner != null && !_slots[i].owner.CanEvictSharedPage(_slots[i].pageId)) return false;
            long reclaimed = _buffers.Bytes - MinimumBufferBytes;
            Rebuild(target);
            _trimmedForBudget = true;
            ClusterMeshStreaming.NoteGpuShrink(reclaimed);
            return true;
        }

        void Rebuild(int target)
        {
            BufferSet replacement = new BufferSet(Key, VertexBase(target), IndexBase(target));
            try
            {
                ClusterMeshGpuBufferCopy.Copy(_buffers.vertices, replacement.vertices);
                ClusterMeshGpuBufferCopy.Copy(_buffers.tight, replacement.tight);
                ClusterMeshGpuBufferCopy.Copy(_buffers.indices, replacement.indices);
                ClusterMeshGpuBufferCopy.Copy(_buffers.weights, replacement.weights);
                ClusterMeshGpuBufferCopy.Copy(_buffers.weights8, replacement.weights8);
                // Only a completion token, not geometry readback; no callback
                // closure or synchronous GPU wait on the main thread.
                _copyCompletion = AsyncGPUReadback.Request(replacement.indices, 4, 0);
            }
            catch { replacement.Dispose(); throw; }
            // Publish eviction before switching buffers. Root ranges and the
            // retained prefix keep their addresses; no root reread is needed.
            for (int i = target; i < AllocatedCapacity; i++)
            {
                Slot slot = _slots[i];
                if (slot.owner != null) slot.owner.OnSharedPageEvicted(slot.pageId);
                slot.owner = null; slot.pageId = -1; slot.pinned = false; slot.lastUsed = 0;
            }
            _retired = _buffers;
            _growing = true;
            AllocatedCapacity = target;
            SetBuffers(replacement);
            _spaceSerial = int.MinValue;
        }

        internal void PollMigration()
        {
            if (!_growing || !_copyCompletion.done) return;
            _retired.Dispose(); _retired = null; _growing = false;
            ClusterMeshStreaming.NoteGpuCapacityReleased();
        }

        sealed class BufferSet : IDisposable
        {
            public GraphicsBuffer vertices, tight, indices, weights, weights8;
            public long Bytes { get; private set; }
            public BufferSet(ClusterMeshSharedPoolKey key, int vertexCount, int indexCount)
            {
                try
                {
                    bool packed = key.vertexStride == ClusterMeshLimits.TightVertexStride;
                    vertices = Make(packed ? 1 : vertexCount, packed ? 32 : key.vertexStride);
                    tight = Make(packed ? vertexCount : 1, 24);
                    indices = Make(indexCount, 4);
                    weights = Make(key.weightStride == 16 ? vertexCount : 1, 16);
                    weights8 = Make(key.weightStride == 8 ? checked(vertexCount * 2) : 2, 4);
                }
                catch { Dispose(); throw; }
            }
            GraphicsBuffer Make(int count, int stride)
            {
                var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Math.Max(1, count), stride);
                Bytes += (long)buffer.count * stride;
                return buffer;
            }
            public void Dispose()
            {
                vertices?.Dispose(); tight?.Dispose(); indices?.Dispose(); weights?.Dispose(); weights8?.Dispose();
            }
        }
    }

    internal static class ClusterMeshGpuBufferCopy
    {
        static ComputeShader shader;
        static bool loaded;
        static readonly int Source = Shader.PropertyToID("_Source"), Destination = Shader.PropertyToID("_Destination");
        static readonly int Count = Shader.PropertyToID("_Count"), Offset = Shader.PropertyToID("_Offset");
        static readonly int[] kernels = new int[11];
        public static bool Available
        {
            get
            {
                if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback) return false;
                if (!loaded)
                {
                    loaded = true;
                    shader = Resources.Load<ComputeShader>("ClusterMeshBufferCopy");
                    if (shader != null)
                        foreach (int stride in new[] { 4, 16, 24, 32, 40 }) kernels[stride / 4] = shader.FindKernel("Copy" + stride);
                }
                return shader != null;
            }
        }
        public static void Copy(GraphicsBuffer source, GraphicsBuffer destination)
        {
            int kernel = kernels[source.stride / 4];
            shader.SetBuffer(kernel, Source, source); shader.SetBuffer(kernel, Destination, destination);
            int count = Math.Min(source.count, destination.count);
            shader.SetInt(Count, count);
            const int chunk = 65535 * 64;
            for (int offset = 0; offset < count; offset += chunk)
            {
                shader.SetInt(Offset, offset);
                shader.Dispatch(kernel, (Math.Min(chunk, count - offset) + 63) / 64, 1, 1);
            }
        }
    }

    public static partial class ClusterMeshStreaming
    {
        static ClusterMeshSettings GpuSettings => ClusterMeshSettings.OverrideForTests ?? ClusterMeshSettings.Loaded;
        public static long GpuMemoryBudgetBytes => (long)(GpuSettings != null ? GpuSettings.MaxSharedGpuMegabytes : 256) * 1048576;
        internal static long InitialGpuDetailBytes => (long)(GpuSettings != null ? GpuSettings.InitialDetailGpuKiB : 1024) * 1024;
        internal static long GpuGrowthBytes => (long)(GpuSettings != null ? GpuSettings.DetailGpuGrowthKiB : 1024) * 1024;
        public static long GpuGrowthCount { get; private set; }
        public static long GpuShrinkCount { get; private set; }
        public static long GpuReclaimedBytes { get; private set; }
        public static long GpuBudgetDenials { get; private set; }
        // Reserve one eighth for overlap during automatic rebuilds. This is
        // part of (not additional to) the externally configured hard limit.
        public static long GpuRebuildReserveBytes => GpuMemoryBudgetBytes / 8;
        public static long GpuSteadyBudgetBytes => GpuMemoryBudgetBytes - GpuRebuildReserveBytes;
        public static long GpuMigrationBytes
        {
            get { long bytes = 0; foreach (var pool in SharedPools) bytes += pool.MigrationBytes; return bytes; }
        }
        public static int GpuBudgetWaitingAssets
        {
            get { int count = 0; foreach (var pair in Runtimes) if (pair.Value.WaitingForGpuBudget) count++; return count; }
        }
        static int lastGpuGrowthSerial = int.MinValue, gpuBudgetPressureSerial = int.MinValue;
        internal static long GpuCapacityRevision { get; private set; }
        static long recoveryRequiredBytes, pressureLoggedBudget;
        static bool pressureLogged, recoveryUnavailableLogged;
        internal static bool CanStartGpuGrowth => lastGpuGrowthSerial != UpdateSerial && recoveryRequiredBytes == 0 && !GpuBudgetUnderPressure;
        internal static bool GpuBudgetUnderPressure => gpuBudgetPressureSerial != int.MinValue &&
            unchecked((uint)(UpdateSerial - gpuBudgetPressureSerial)) < 12;
        internal static bool CanAllocateGpuBytes(long bytes)
        {
            if (bytes <= GpuSteadyBudgetBytes - SharedGpuBufferBytes) return true;
            GpuBudgetDenials++; gpuBudgetPressureSerial = UpdateSerial;
            recoveryRequiredBytes = Math.Max(recoveryRequiredBytes, bytes);
            if (!pressureLogged || pressureLoggedBudget != GpuMemoryBudgetBytes)
            {
                pressureLogged = true; recoveryUnavailableLogged = false; pressureLoggedBudget = GpuMemoryBudgetBytes;
                Debug.LogError($"ClusterMesh GPU budget pressure: allocated={SharedGpuBufferBytes / 1048576.0:F2} MiB, requested={bytes / 1048576.0:F2} MiB, limit={GpuMemoryBudgetBytes / 1048576} MiB (includes rebuild reserve). Automatically shrinking detail caches and rebuilding; roots are preserved. Settings: Tools/ClusterMesh/通用设置 → 共享几何总上限 MiB, or streamingSettings.MaxSharedGpuMegabytes. This message does not throw an exception.");
            }
            return false;
        }
        internal static bool CanGrowGpuBytes(long replacementBytes, long oldBytes)
        {
            long current = SharedGpuBufferBytes;
            if (replacementBytes <= GpuMemoryBudgetBytes - current &&
                replacementBytes - oldBytes <= GpuSteadyBudgetBytes - current) return true;
            GpuBudgetDenials++; gpuBudgetPressureSerial = UpdateSerial;
            return false;
        }
        // Returns immediately. Only an actual asynchronous rebuild may delay a
        // request; an impossible root footprint is reported instead of parked forever.
        static void RecoverGpuBudget()
        {
            if (SharedGpuBufferBytes > GpuSteadyBudgetBytes) recoveryRequiredBytes = Math.Max(1, recoveryRequiredBytes);
            if (recoveryRequiredBytes == 0) return;
            if (recoveryRequiredBytes <= GpuSteadyBudgetBytes - SharedGpuBufferBytes)
            {
                recoveryRequiredBytes = 0; NoteGpuCapacityReleased(); return;
            }
            foreach (var pool in SharedPools) if (pool.MigrationPending) return;
            ClusterMeshSharedGpuPool best = null;
            foreach (var pool in SharedPools)
                if (pool.ReclaimableBytes > 0 && pool.MinimumBufferBytes <= GpuMemoryBudgetBytes - SharedGpuBufferBytes &&
                    (best == null || pool.ReclaimableBytes > best.ReclaimableBytes)) best = pool;
            if (best != null && best.TryShrinkForBudget()) return;
            // A node already being read must be allowed to complete first. This
            // is bounded by the existing I/O/error handling, not an idle wait.
            foreach (var pool in SharedPools) if (pool.HasPendingAdmission) return;
            if (!recoveryUnavailableLogged)
            {
                recoveryUnavailableLogged = true;
                Debug.LogError($"ClusterMesh GPU rebuild cannot free enough capacity: allocated={SharedGpuBufferBytes / 1048576.0:F2} MiB, required={recoveryRequiredBytes / 1048576.0:F2} MiB, reclaimed={GpuReclaimedBytes / 1048576.0:F2} MiB. Pinned roots or rebuild overlap exceed the budget. Existing geometry remains visible; new requests are rejected, not left waiting. Settings: Tools/ClusterMesh/通用设置 → 共享几何总上限 MiB / streamingSettings.MaxSharedGpuMegabytes.");
            }
            foreach (var pair in Runtimes) if (pair.Value.WaitingForGpuBudget) pair.Value.RejectGpuBudget();
            recoveryRequiredBytes = 0;
        }
        internal static void NoteGpuGrowth() { lastGpuGrowthSerial = UpdateSerial; GpuGrowthCount++; }
        internal static void NoteGpuShrink(long bytes) { lastGpuGrowthSerial = UpdateSerial; GpuShrinkCount++; GpuReclaimedBytes += bytes; }
        internal static void NoteGpuCapacityReleased() { GpuCapacityRevision++; }
    }
}
