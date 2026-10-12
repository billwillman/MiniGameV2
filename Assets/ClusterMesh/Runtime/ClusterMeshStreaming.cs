using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    public enum ClusterMeshStreamKind
    {
        Unknown = 0,
        Static = 1,
        Skinned = 2
    }

    public static class ClusterMeshStreamPageFlags
    {
        public const int Root = 1;
    }

    [Serializable]
    public sealed class ClusterMeshStreamDescriptor
    {
        public int formatVersion;
        public ClusterMeshStreamKind kind;
        public string fileName;
        public string editorFilePath;
        public string contentHash;
        public long fileSize;
        public int poolPageCapacity = 32;
        public int vertexPageCapacity = 4096;
        public int indexPageCapacity = 8192;
        public int weightStride;
        public ClusterMeshStreamNode[] nodes;
        public ClusterMeshStreamPage[] pages;
        public ClusterMeshStreamAddress[] addresses;

        public bool IsValid =>
            formatVersion == ClusterMeshStreaming.FormatVersion &&
            kind != ClusterMeshStreamKind.Unknown &&
            (!string.IsNullOrEmpty(fileName) || !string.IsNullOrEmpty(editorFilePath)) &&
            poolPageCapacity > 0 && vertexPageCapacity > 0 && indexPageCapacity > 0 &&
            nodes != null && pages != null && addresses != null;
    }

    // Exactly 64 bytes. The streamed culling compute shader consumes this layout.
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterMeshStreamNode
    {
        public int clusterStart;
        public int clusterCount;
        public int parentNodeIndex;
        public int materialIndex;
        public float lodError;
        public int lodLevel;
        public uint flags;
        public uint padding;
        public Vector4 aabbCenter;
        public Vector4 aabbExtents;
    }

    [Serializable]
    public struct ClusterMeshStreamPage
    {
        public long fileOffset;
        public int compressedLength;
        public int uncompressedLength;
        public int vertexCount;
        public int indexWordCount;
        public int weightCount;
        public uint crc32;
        // Kept in the descriptor and binary directory so one node can own pages.
        public int nodeIndex;
        public int flags;

        public bool root => (flags & ClusterMeshStreamPageFlags.Root) != 0;
    }

    // Exactly 16 bytes. pageId indexes descriptor.pages; offsets are page-local.
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterMeshStreamAddress
    {
        public uint pageId;
        public uint vertexOffset;
        public uint indexOffset;
        public uint reserved;
    }

    // Runtime physical mapping for a virtual page. This is intentionally separate
    // from ClusterMeshStreamAddress so page eviction never rewrites address data.
    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterMeshStreamPageTableEntry
    {
        public uint vertexBase;
        public uint indexBase;
        public uint weightBase;
        public uint flags;
    }

    internal struct ClusterMeshSharedPoolKey : IEquatable<ClusterMeshSharedPoolKey>
    {
        public int vertexStride;
        public int weightStride;
        public int vertexPageCapacity;
        public int indexPageCapacity;

        public bool Equals(ClusterMeshSharedPoolKey other) =>
            vertexStride == other.vertexStride && weightStride == other.weightStride &&
            vertexPageCapacity == other.vertexPageCapacity &&
            indexPageCapacity == other.indexPageCapacity;
        // Page tables store absolute buffer offsets, so smaller logical pages
        // can occupy larger physical slots. Vertex/skin encodings must match.
        public bool CanContain(ClusterMeshSharedPoolKey other) =>
            vertexStride == other.vertexStride && weightStride == other.weightStride &&
            vertexPageCapacity >= other.vertexPageCapacity &&
            indexPageCapacity >= other.indexPageCapacity;
        public override bool Equals(object obj) => obj is ClusterMeshSharedPoolKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = vertexStride;
                hash = hash * 397 ^ weightStride;
                hash = hash * 397 ^ vertexPageCapacity;
                return hash * 397 ^ indexPageCapacity;
            }
        }
    }

    // Virtual pages and their on-disk ownership stay unchanged. Small pinned
    // root pages may occupy disjoint ranges of one physical slot; page-table
    // bases already support these offsets. Detail slots remain independently
    // evictable. No extra GPU buffer or rebake is required.
    internal sealed class ClusterMeshRootPageLayout
    {
        public readonly int[] slots, vertexOffsets, indexOffsets;
        public int SlotCount { get; private set; }
        public int DetailVertexCapacity { get; private set; } = 1;
        public int DetailIndexCapacity { get; private set; } = 1;

        public ClusterMeshRootPageLayout(ClusterMeshStreamDescriptor descriptor, bool pack)
        {
            int count = descriptor.pages.Length;
            slots = new int[count]; vertexOffsets = new int[count]; indexOffsets = new int[count];
            int vertices = 0, indices = 0;
            for (int i = 0; i < count; i++)
            {
                slots[i] = -1;
                var page = descriptor.pages[i];
                if (!page.root && descriptor.nodes[page.nodeIndex].parentNodeIndex >= 0)
                {
                    DetailVertexCapacity = Math.Max(DetailVertexCapacity, page.vertexCount);
                    DetailIndexCapacity = Math.Max(DetailIndexCapacity, page.indexWordCount);
                    continue;
                }
                if (SlotCount == 0 || !pack ||
                    vertices + page.vertexCount > descriptor.vertexPageCapacity ||
                    indices + page.indexWordCount > descriptor.indexPageCapacity)
                {
                    SlotCount++; vertices = 0; indices = 0;
                }
                slots[i] = SlotCount - 1;
                vertexOffsets[i] = vertices; indexOffsets[i] = indices;
                vertices += page.vertexCount; indices += page.indexWordCount;
            }
        }
    }

    internal sealed partial class ClusterMeshSharedGpuPool : IDisposable
    {
        sealed class Reservation
        {
            public int rootPages;
            public int detailHeadroom;
            public int[] rootSlots;
        }

        sealed class Slot
        {
            public ClusterMeshPageRuntime owner;
            public int pageId = -1;
            public bool pinned;
            public int lastUsed;
        }

        readonly Slot[] _slots;
        readonly int[] _admissionSlots;
        int _admissionCount, _admissionCursor;
        readonly Dictionary<ClusterMeshPageRuntime, Reservation> _reservations =
            new Dictionary<ClusterMeshPageRuntime, Reservation>();
        ClusterMeshPageRuntime _admittedOwner;
        int _admittedNode = -1;
        int _spaceSerial = int.MinValue, _availableDetailSlots;

        public ClusterMeshSharedPoolKey Key { get; private set; }
        public int Capacity => _slots.Length;
        public int AllocationPageLimit { get; private set; }
        public int ReferenceCount => _reservations.Count;
        public bool HasPendingAdmission => _admittedOwner != null;
        public int ReservedRootPages { get; private set; }
        public int ReservedDetailHeadroom { get; private set; }
        public GraphicsBuffer Vertices { get; private set; }
        public GraphicsBuffer VerticesTight { get; private set; }
        public GraphicsBuffer Indices { get; private set; }
        public GraphicsBuffer Weights { get; private set; }
        public GraphicsBuffer Weights8 { get; private set; }

        public ClusterMeshSharedGpuPool(ClusterMeshSharedPoolKey key, int capacity, int allocationPageLimit,
            int rootSlots, int detailVertices, int detailIndices, int initialDetails, int minimumDetails)
        {
            Key = key;
            AllocationPageLimit = allocationPageLimit;
            _slots = new Slot[capacity];
            _admissionSlots = new int[capacity];
            for (int i = 0; i < capacity; i++) _slots[i] = new Slot();
            InitializeBuffers(rootSlots, detailVertices, detailIndices, initialDetails, minimumDetails);
        }

        public bool CanReserve(int rootPages, int detailHeadroom)
        {
            if (rootPages <= 0 || detailHeadroom < 0) return false;
            long roots = (long)ReservedRootPages + rootPages;
            long detail = (long)ReservedDetailHeadroom + detailHeadroom;
            if (roots > RootReservationLimit || roots > Capacity - detail) return false;
            int available = 0;
            for (int i = 0; i < RootReservationLimit; i++)
            {
                Slot slot = _slots[i];
                if (slot.owner == null || (!slot.pinned && slot.owner.CanEvictSharedPage(slot.pageId))) available++;
            }
            return available >= rootPages;
        }

        public void Acquire(ClusterMeshPageRuntime owner, int rootPages, int detailHeadroom)
        {
            if (owner == null || _reservations.ContainsKey(owner))
                throw new InvalidOperationException("ClusterMesh shared GPU pool owner is invalid or already acquired.");
            if (!CanReserve(rootPages, detailHeadroom))
                throw new InvalidOperationException("ClusterMesh shared GPU pool cannot reserve roots and its detail window.");
            var reservation = new Reservation
            {
                rootPages = rootPages,
                detailHeadroom = detailHeadroom,
                rootSlots = new int[rootPages]
            };
            // Reserve the physical root slots now. Later owners cannot steal
            // them while their FileStream reads are still in flight.
            int next = 0;
            for (int pass = 0; pass < 2 && next < rootPages; pass++)
                for (int i = 0; i < RootReservationLimit && next < rootPages; i++)
                {
                    Slot slot = _slots[i];
                    if (slot.owner != null && (pass == 0 || slot.pinned || !slot.owner.CanEvictSharedPage(slot.pageId))) continue;
                    if (slot.owner != null) slot.owner.OnSharedPageEvicted(slot.pageId);
                    slot.owner = owner; slot.pageId = -1; slot.pinned = true;
                    reservation.rootSlots[next++] = i;
                }
            _reservations.Add(owner, reservation);
            _spaceSerial = int.MinValue;
            ReservedRootPages += rootPages;
            ReservedDetailHeadroom += detailHeadroom;
            MinimumDetailSlots = Math.Min(Capacity - RootSlotCapacity, Math.Max(MinimumDetailSlots, owner.MaxDetailPageCount));
        }

        public bool TryAllocate(ClusterMeshPageRuntime owner, int pageId, int nodeIndex,
            bool pinned, int tick, out int slotIndex)
        {
            slotIndex = -1;
            if (pinned)
            {
                int rootSlot = owner.RootLayout.slots[pageId];
                if (rootSlot < 0 || !_reservations.TryGetValue(owner, out Reservation reservation)) return false;
                slotIndex = reservation.rootSlots[rootSlot];
                return true;
            }
            if (!pinned && !TryBeginNodeAdmission(owner, nodeIndex))
                return false;

            if (_admissionCursor >= _admissionCount) return false;
            slotIndex = _admissionSlots[_admissionCursor++];
            Slot selected = _slots[slotIndex];
            selected.owner = owner;
            selected.pageId = pageId;
            selected.pinned = pinned;
            selected.lastUsed = tick;
            _spaceSerial = int.MinValue;
            return true;
        }

        public bool TryBeginNodeAdmission(ClusterMeshPageRuntime owner, int nodeIndex)
        {
            if (_admittedOwner == null)
            {
                GrowForAdmission(owner.MissingNodePageCount(nodeIndex));
                // Check the ENTIRE node before reading its first page. A pool
                // with no recyclable space must queue requests, not pin decoded
                // memory and block other pools' I/O. Existing roots stay visible.
                if (_spaceSerial != ClusterMeshStreaming.UpdateSerial)
                {
                    _availableDetailSlots = 0;
                    for (int i = 0; i < AllocatedCapacity; i++)
                    {
                        Slot slot = _slots[i];
                        if (slot.owner == null || (!slot.pinned &&
                            ClusterMeshStreaming.UpdateSerial - slot.lastUsed >= ClusterMeshStreaming.ResidentGraceUpdates &&
                            slot.owner.CanEvictSharedPage(slot.pageId))) _availableDetailSlots++;
                    }
                    _spaceSerial = ClusterMeshStreaming.UpdateSerial;
                }
                // Existing pages of THIS node become protected on admission.
                // Counting them as victims would admit a batch with no actual
                // destination for its missing pages (partial-residency deadlock).
                if (_availableDetailSlots - owner.RecyclableNodePageCount(nodeIndex) < owner.MissingNodePageCount(nodeIndex))
                {
                    return false;
                }
                int needed = owner.MissingNodePageCount(nodeIndex);
                _admissionCount = _admissionCursor = 0;
                // Reserve actual destinations, not just a count. Neither a new
                // owner's roots nor a delayed GPU Touch can steal these slots
                // while FileStream reads are in flight. Pages already belonging
                // to this partial node are preserved, never counted as victims.
                for (int pass = 0; pass < 2 && _admissionCount < needed; pass++)
                    for (int ordinal = 0; ordinal < AllocatedCapacity && _admissionCount < needed; ordinal++)
                    {
                        // Prefer compact detail slots; keep spare root slots
                        // available for a compatible owner's future pinned roots.
                        int i = (ordinal + RootSlotCapacity) % AllocatedCapacity;
                        Slot slot = _slots[i];
                        if (slot.owner == null && pass != 0) continue;
                        if (slot.owner != null && (pass == 0 || !IsRecyclableSlot(i) ||
                            (ReferenceEquals(slot.owner, owner) && owner.PageBelongsToNode(slot.pageId, nodeIndex)))) continue;
                        _admissionSlots[_admissionCount++] = i;
                    }
                if (_admissionCount != needed) { _admissionCount = 0; return false; }
                for (int i = 0; i < _admissionCount; i++)
                {
                    Slot slot = _slots[_admissionSlots[i]];
                    if (slot.owner != null) slot.owner.OnSharedPageEvicted(slot.pageId);
                    slot.owner = owner; slot.pageId = -1; slot.pinned = false;
                    slot.lastUsed = ClusterMeshStreaming.UpdateSerial;
                }
                _spaceSerial = int.MinValue;
                _admittedOwner = owner;
                _admittedNode = nodeIndex;
            }
            return ReferenceEquals(_admittedOwner, owner) && _admittedNode == nodeIndex;
        }

        public void CompleteNodeAdmission(ClusterMeshPageRuntime owner, int nodeIndex)
        {
            if (!ReferenceEquals(_admittedOwner, owner) || _admittedNode != nodeIndex) return;
            for (int i = _admissionCursor; i < _admissionCount; i++)
            {
                Slot slot = _slots[_admissionSlots[i]];
                slot.owner = null; slot.pageId = -1; slot.pinned = false; slot.lastUsed = 0;
            }
            _admissionCount = _admissionCursor = 0;
            _admittedOwner = null;
            _admittedNode = -1;
            _spaceSerial = int.MinValue;
        }

        internal bool IsRecyclableSlot(int index)
        {
            if (index < 0 || index >= _slots.Length) return false;
            Slot slot = _slots[index];
            return slot.owner != null && !slot.pinned &&
                ClusterMeshStreaming.UpdateSerial - slot.lastUsed >= ClusterMeshStreaming.ResidentGraceUpdates &&
                slot.owner.CanEvictSharedPage(slot.pageId);
        }

        public void CancelAllocation(ClusterMeshPageRuntime owner, int pageId)
        {
            if (owner.RootLayout.slots[pageId] >= 0) return; // shared pinned range; Release owns its lifetime
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (!ReferenceEquals(slot.owner, owner) || slot.pageId != pageId) continue;
                slot.owner = null; slot.pageId = -1; slot.pinned = false; slot.lastUsed = 0;
                _spaceSerial = int.MinValue;
                break;
            }
        }

        public void Touch(int slotIndex, int tick)
        {
            if (slotIndex >= 0 && slotIndex < _slots.Length)
            {
                _slots[slotIndex].lastUsed = tick;
                _spaceSerial = int.MinValue;
            }
        }

        public void Release(ClusterMeshPageRuntime owner)
        {
            _spaceSerial = int.MinValue;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (!ReferenceEquals(slot.owner, owner)) continue;
                slot.owner = null; slot.pageId = -1; slot.pinned = false; slot.lastUsed = 0;
            }
            if (ReferenceEquals(_admittedOwner, owner))
            {
                _admittedOwner = null;
                _admittedNode = -1;
                _admissionCount = _admissionCursor = 0;
            }
            if (_reservations.TryGetValue(owner, out Reservation reservation))
            {
                _reservations.Remove(owner);
                ReservedRootPages = Mathf.Max(0, ReservedRootPages - reservation.rootPages);
                ReservedDetailHeadroom -= reservation.detailHeadroom;
            }
        }

        static int CheckedCapacity(int a, int b)
        {
            try { return Mathf.Max(1, checked(a * b)); }
            catch (OverflowException) { throw new InvalidOperationException("ClusterMesh shared GPU pool is too large."); }
        }

        public void Dispose()
        {
            _buffers?.Dispose(); _buffers = null;
            _retired?.Dispose(); _retired = null; _growing = false;
        }
    }

    public sealed class ClusterMeshPageRuntime : IDisposable
    {
        const int FileHeaderBytes = 16;
        const int DirectoryEntryBytes = 40;
        const int PayloadHeaderBytes = 12;
        const uint Resident = 1u;
        static readonly uint[] CrcTable = BuildCrcTable();

        sealed class Payload : IDisposable
        {
            public byte[] raw;
            public int length;
            public void Dispose()
            {
                if (raw == null) return;
                ArrayPool<byte>.Shared.Return(raw);
                raw = null;
                length = 0;
            }
        }

        sealed class Slot
        {
            public int pageId = -1;
            public bool pinned;
            public int lastUsed;
        }

        readonly ClusterMeshAsset _geometry;
        readonly ClusterMeshStreamDescriptor _descriptor;
        readonly ClusterMeshStreamPageTableEntry[] _pageTable;
        readonly uint[] _nodeResident;
        readonly ClusterMeshNodeIndexTable _nodePages;
        readonly ClusterMeshNodeIndexTable _nodeChildren;
        readonly int[] _rootNodes;
        readonly int[] _pageSlots;
        readonly Slot[] _slots;
        ClusterMeshSharedGpuPool _sharedPool;
        int _maxDetailPages;
        internal int MaxDetailPageCount => _maxDetailPages;
        long _waitingCapacityRevision, _waitingBudgetBytes;
        public bool WaitingForGpuBudget { get; private set; }
        readonly int _rootPageCount;
        internal readonly ClusterMeshRootPageLayout RootLayout;
        struct PageRequest
        {
            public uint priority;
            public int firstTick;
        }

        readonly Dictionary<int, PageRequest> _requests = new Dictionary<int, PageRequest>();
        readonly Dictionary<int, Task<Payload>> _reads = new Dictionary<int, Task<Payload>>();
        sealed class RootRead
        {
            public int[] ids;
            public Task<Payload[]> task;
            public long reservedBytes;
        }
        readonly List<RootRead> _rootBatches = new List<RootRead>();
        readonly HashSet<int> _rootReading = new HashSet<int>();
        readonly Dictionary<int, Payload> _completed = new Dictionary<int, Payload>();
        readonly List<int> _scratch = new List<int>();
        Comparison<int> _requestComparer;
        Task<byte[]> _metadataRead;
        ClusterMeshStreamReader _reader;
        string _path;
        int _tick;
        int _loadingNode = -1;
        bool _metadataReady;
        bool _disposed;
        bool _sharedPoolReleased;

        public ClusterMeshAsset Geometry => _geometry;
        public ClusterMeshStreamDescriptor Descriptor => _descriptor;
        public bool IsReady { get; private set; }
        public bool Failed { get; private set; }
        public string Error { get; private set; }
        public bool MetadataReady => _metadataReady;
        public bool IsDisposed => _disposed;
        public bool UsesGlobalSharedGpuPool => _sharedPool != null;
        // Logical pages remain unchanged on disk; uploads address the actual
        // shared pool's slots, which may be larger than this asset's pages.
        public int PhysicalVertexPageCapacity => _sharedPool != null ? _sharedPool.Key.vertexPageCapacity : _descriptor.vertexPageCapacity;
        public int PhysicalIndexPageCapacity => _sharedPool != null ? _sharedPool.Key.indexPageCapacity : _descriptor.indexPageCapacity;
        public int PhysicalPoolPageCapacity => _sharedPool != null ? _sharedPool.AllocatedCapacity : WaitingForGpuBudget ? 0 : _descriptor.poolPageCapacity;
        public int PoolAllocationPageLimit => _sharedPool != null ? _sharedPool.AllocationPageLimit : _descriptor.poolPageCapacity;
        public int NodeCount => _descriptor != null && _descriptor.nodes != null ? _descriptor.nodes.Length : 0;
        public int PageCount => _descriptor != null && _descriptor.pages != null ? _descriptor.pages.Length : 0;
        // GPU requests are per logical LOD node. A node can span several fixed
        // geometry pages and becomes resident only when all of them are uploaded.
        public int RequestWordCount => (NodeCount + 31) >> 5;
        internal int[] RootNodes => _rootNodes;
        internal ClusterMeshNodeIndexTable.Range GetNodeChildren(int nodeIndex) =>
            nodeIndex >= 0 && nodeIndex < NodeCount && _nodeChildren != null ? _nodeChildren[nodeIndex] : default;
        internal int InflightReadCount => _reads.Count + _rootBatches.Count;
        public int RootReadOperations { get; private set; }
        public int BatchedRootPages { get; private set; }
        public int RootUploadOperations { get; private set; }
        public int BatchedRootUploadPages { get; private set; }
        public long SharedPageEvictions { get; private set; }
        // Optional host hint, not a dependency on a map or camera. Higher values
        // get the existing global IO/upload budget first while roots are missing.
        // Ready assets return to ordinary round-robin detail scheduling.
        public int RootLoadPriority { get; set; }
        // Optional visible-detail hint. Every fourth central update ignores this
        // hint so off-screen owners retain progress even under continuous demand.
        public int DetailLoadPriority { get; set; }
        public int LogicalRootPageCount => _rootPageCount;
        public int PhysicalRootSlotCount => _sharedPool != null ? RootLayout.SlotCount : _rootPageCount;
        internal int MissingNodePageCount(int nodeIndex)
        {
            int count = 0;
            var pages = _nodePages[nodeIndex];
            for (int i = 0; i < pages.Length; i++) if (_pageSlots[pages[i]] < 0) count++;
            return count;
        }
        internal int RecyclableNodePageCount(int nodeIndex)
        {
            int count = 0;
            var pages = _nodePages[nodeIndex];
            for (int i = 0; i < pages.Length; i++)
                if (_sharedPool.IsRecyclableSlot(_pageSlots[pages[i]])) count++;
            return count;
        }
        internal bool PageBelongsToNode(int pageId, int nodeIndex) =>
            pageId >= 0 && pageId < PageCount && _descriptor.pages[pageId].nodeIndex == nodeIndex;
        internal long PendingDecodedBytes
        {
            get
            {
                long total = 0;
                foreach (int page in _reads.Keys) total += _descriptor.pages[page].uncompressedLength;
                foreach (var batch in _rootBatches) total += batch.reservedBytes;
                foreach (int page in _completed.Keys) total += _descriptor.pages[page].uncompressedLength;
                return total;
            }
        }

        public GraphicsBuffer Vertices { get; private set; }
        public GraphicsBuffer VerticesTight { get; private set; }
        public GraphicsBuffer Indices { get; private set; }
        public GraphicsBuffer Weights { get; private set; }
        public GraphicsBuffer Weights8 { get; private set; }
        public GraphicsBuffer Addresses { get; private set; }
        public GraphicsBuffer PageTable { get; private set; }
        public GraphicsBuffer NodeResident { get; private set; }
        public GraphicsBuffer Nodes { get; private set; }

        internal ClusterMeshPageRuntime(ClusterMeshAsset geometry, ClusterMeshStreamDescriptor descriptor)
        {
            _geometry = geometry;
            _descriptor = descriptor;
            if (geometry == null || descriptor == null || !descriptor.IsValid)
            {
                Fail("ClusterMesh streaming descriptor is missing or invalid.");
                return;
            }

            long validationStamp = 0; ClusterMeshCpuTiming.Begin(ref validationStamp);
            bool valid = ValidateDescriptor(out int[] pageCounts, out int rootPages, out int maxDetailPages);
            ClusterMeshCpuTiming.End("RootValidation", validationStamp);
            if (!valid) return;
            _pageTable = new ClusterMeshStreamPageTableEntry[descriptor.pages.Length];
            _nodeResident = new uint[descriptor.nodes.Length];
            _pageSlots = new int[descriptor.pages.Length];
            for (int i = 0; i < _pageSlots.Length; i++) _pageSlots[i] = -1;
            _rootPageCount = rootPages;
            _maxDetailPages = maxDetailPages;
            var settings = ClusterMeshSettings.OverrideForTests ?? ClusterMeshSettings.Loaded;
            RootLayout = new ClusterMeshRootPageLayout(descriptor, settings == null || settings.PackRootPages);
            ClusterMeshSharedGpuPool sharedPool = null;
            if (ClusterMeshStreaming.UseGlobalSharedGpuPool)
            {
                try
                {
                    sharedPool = ClusterMeshStreaming.AcquireSharedPool(
                        this, geometry.ResolvedVertexStride, descriptor, _rootPageCount,
                        maxDetailPages);
                    WaitingForGpuBudget = sharedPool == null;
                    _waitingCapacityRevision = ClusterMeshStreaming.GpuCapacityRevision;
                    _waitingBudgetBytes = ClusterMeshStreaming.GpuMemoryBudgetBytes;
                }
                catch (InvalidDataException ex)
                {
                    // A capacity rejection is not an allocation failure: falling
                    // back to the old, smaller local pool would hide starvation.
                    Fail(ex.Message);
                    return;
                }
                catch (Exception ex)
                {
                    // Never bypass the global budget by allocating a local pool.
                    Fail("ClusterMesh shared GPU pool allocation failed: " + ex.Message);
                    return;
                }
            }
            _sharedPool = sharedPool;
            if (_sharedPool == null && !WaitingForGpuBudget)
            {
                _slots = new Slot[descriptor.poolPageCapacity];
                for (int i = 0; i < _slots.Length; i++) _slots[i] = new Slot();
            }
            long topologyStamp = 0; ClusterMeshCpuTiming.Begin(ref topologyStamp);
            _nodePages = ClusterMeshNodeIndexTable.Pages(pageCounts, descriptor.pages);
            _nodeChildren = ClusterMeshNodeIndexTable.Children(descriptor.nodes);
            _rootNodes = BuildRootNodes(descriptor.nodes);
            ClusterMeshCpuTiming.End("RootTopology", topologyStamp);
            _path = ClusterMeshStreaming.ResolveFilePath(descriptor);
            if (string.IsNullOrEmpty(_path))
            {
                Fail("ClusterMesh streaming file path cannot be resolved.");
                return;
            }

            long bytes = (long)FileHeaderBytes + (long)DirectoryEntryBytes * PageCount;
            if (bytes > int.MaxValue)
            {
                Fail("ClusterMesh streaming directory is too large.");
                return;
            }
            _reader = new ClusterMeshStreamReader(_path);
            var reader = _reader;
            _metadataRead = Task.Run(() => ReadRange(reader, 0, (int)bytes));
        }

        public bool IsNodeResident(int nodeIndex) =>
            nodeIndex >= 0 && nodeIndex < _nodeResident.Length && _nodeResident[nodeIndex] != 0;

            // GPU request readback yields node ids; one node may own several pages.
        public void RequestNode(int nodeIndex, uint priority = 0u)
        {
            if (_disposed || Failed || nodeIndex < 0 || nodeIndex >= NodeCount)
                return;
            MarkNodeUsed(nodeIndex);
            var pages = _nodePages[nodeIndex];
            for (int i = 0; i < pages.Length; i++)
            {
                int page = pages[i];
                if (_pageSlots[page] < 0)
                    QueuePage(page, priority != 0u ? priority : 0x3f800000u);
            }
            if (ClusterMeshStreaming.EnablePrefetch && !ClusterMeshStreaming.GpuBudgetUnderPressure)
            {
                uint prefetchPriority = priority > 0x01000000u ? priority - 0x01000000u : 1u;
                var children = _nodeChildren[nodeIndex];
                for (int childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    var childPages = _nodePages[children[childIndex]];
                    for (int i = 0; i < childPages.Length; i++)
                        if (_pageSlots[childPages[i]] < 0)
                            QueuePage(childPages[i], prefetchPriority);
                }
            }
        }

        void QueuePage(int page, uint priority)
        {
            if (_requests.TryGetValue(page, out PageRequest existing))
            {
                if (priority > existing.priority)
                {
                    existing.priority = priority;
                    _requests[page] = existing;
                }
                return;
            }
            _requests.Add(page, new PageRequest { priority = priority, firstTick = _tick });
        }

        public void MarkNodeUsed(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= NodeCount)
                return;
            var pages = _nodePages[nodeIndex];
            for (int i = 0; i < pages.Length; i++)
            {
                int slot = _pageSlots[pages[i]];
                if (slot >= 0)
                {
                    if (_sharedPool != null)
                        _sharedPool.Touch(slot, ClusterMeshStreaming.UpdateSerial);
                    else _slots[slot].lastUsed = _tick;
                }
            }
        }

        internal void Update(ref int readSlots, ref long readBytes, ref long decodedBytes,
            ref int uploadSlots, ref long uploadBytes)
        {
            if (_disposed || Failed)
                return;
            if (WaitingForGpuBudget)
            {
                if (_waitingCapacityRevision == ClusterMeshStreaming.GpuCapacityRevision &&
                    _waitingBudgetBytes == ClusterMeshStreaming.GpuMemoryBudgetBytes) return;
                _waitingCapacityRevision = ClusterMeshStreaming.GpuCapacityRevision;
                _waitingBudgetBytes = ClusterMeshStreaming.GpuMemoryBudgetBytes;
                try
                {
                    _sharedPool = ClusterMeshStreaming.AcquireSharedPool(this, _geometry.ResolvedVertexStride,
                        _descriptor, _rootPageCount, _maxDetailPages);
                }
                catch (Exception ex) { Fail(ex.Message); return; }
                if (_sharedPool == null) return;
                WaitingForGpuBudget = false;
            }
            _sharedPool?.PollMigration();
            _tick++;
            if (!_metadataReady)
            {
                PollMetadata();
                if(!_metadataReady)return;
            }
            StartReads(ref readSlots, ref readBytes, ref decodedBytes);
            CollectReads();
            UploadPages(ref uploadSlots, ref uploadBytes);
            IsReady = AllRootPagesResident();
        }

        void PollMetadata()
        {
            if (_metadataRead == null || !_metadataRead.IsCompleted)
                return;
            try
            {
                if (_metadataRead.IsFaulted || _metadataRead.IsCanceled)
                    throw new IOException(TaskError(_metadataRead, "Unable to read ClusterMesh stream metadata."));
                ValidateDirectory(_metadataRead.Result);
                CreateGpuBuffers();
                _metadataReady = true;
                RequestRoots();
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
            finally
            {
                _metadataRead = null;
            }
        }

        void StartReads(ref int readSlots, ref long readBytes, ref long decodedBytes)
        {
            if (readSlots <= 0 || readBytes <= 0 || decodedBytes <= 0)
                return;
            _scratch.Clear();
            foreach (int id in _requests.Keys)
            {
                if (_pageSlots[id] >= 0 || _reads.ContainsKey(id) || _rootReading.Contains(id) || _completed.ContainsKey(id))
                    continue;
                _scratch.Add(id);
            }
            _scratch.Sort(_requestComparer ?? (_requestComparer = CompareRequestedPages));
            for (int i = 0; i < _scratch.Count && readSlots > 0; i++)
            {
                int id = _scratch[i];
                ClusterMeshStreamPage page = _descriptor.pages[id];
                if (_loadingNode >= 0 && !IsRootPage(id) && page.nodeIndex != _loadingNode)
                    continue;
                if (page.compressedLength > readBytes || page.uncompressedLength > decodedBytes)
                    continue;
                if (IsRootPage(id) && TryStartRootBatch(ref i, ref readSlots, ref readBytes, ref decodedBytes))
                    continue;
                if (!IsRootPage(id))
                {
                    // Acquire admission before reading: pages which cannot be
                    // uploaded must not fill the finite decoded memory budget.
                    if (_sharedPool != null && !_sharedPool.TryBeginNodeAdmission(this, page.nodeIndex))
                        continue;
                    _loadingNode = page.nodeIndex;
                }
                // File open and even synchronously-completed cached reads must
                // not bring inflate/CRC back onto the Unity main thread.
                if (!ClusterMeshInflatePool.TryRent(out var inflater)) break;
                Task<Payload> task;
                try { task = StartPageRead(_reader, page, _geometry.ResolvedVertexStride, _descriptor.weightStride, inflater); }
                catch { ClusterMeshInflatePool.Return(inflater); throw; }
                _reads.Add(id, task);
                if (IsRootPage(id)) RootReadOperations++;
                readSlots--;
                readBytes -= page.compressedLength;
                decodedBytes -= page.uncompressedLength;
            }
        }

        // Keep the closure in its own method: a lambda in the loop makes the
        // compiler allocate its capture object before the admission/continue
        // checks. Only admitted reads need a task; no extra retained cache.
        static Task<Payload> StartPageRead(ClusterMeshStreamReader reader,ClusterMeshStreamPage page,int stride,int weightStride,ClusterMeshPageInflater inflater)
        {
            return Task.Run(() =>
            {
                try { return ReadPage(reader, page, stride, weightStride, inflater); }
                finally { ClusterMeshInflatePool.Return(inflater); }
            });
        }

        bool TryStartRootBatch(ref int cursor, ref int readSlots, ref long readBytes, ref long decodedBytes)
        {
            var settings = ClusterMeshSettings.OverrideForTests ?? ClusterMeshSettings.Loaded;
            int limit = settings != null ? settings.MaxRootPagesPerRead : 16;
            if (limit <= 1) return false;
            long maxBytes = Math.Min(readBytes, (settings != null ? settings.MaxRootReadKiB : 256) * 1024L);
            long packed = 0, decoded = 0, end = _descriptor.pages[_scratch[cursor]].fileOffset;
            int count = 0;
            while (cursor + count < _scratch.Count && count < limit)
            {
                int id = _scratch[cursor + count];
                var page = _descriptor.pages[id];
                if (!IsRootPage(id) || page.fileOffset != end) break;
                long nextPacked = packed + page.compressedLength, nextDecoded = decoded + page.uncompressedLength;
                // The contiguous compressed staging range is charged as well;
                // batching cannot silently expand the existing decode allowance.
                if (nextPacked > maxBytes || nextPacked + nextDecoded > decodedBytes) break;
                packed = nextPacked; decoded = nextDecoded; end += page.compressedLength; count++;
            }
            if (count < 2) return false; // single-page progress works even with tiny budgets
            if (!ClusterMeshInflatePool.TryRent(out var inflater)) return false;
            bool transferred = false;
            try
            {
                var ids = new int[count]; var pages = new ClusterMeshStreamPage[count];
                for (int n = 0; n < count; n++)
                { ids[n] = _scratch[cursor + n]; pages[n] = _descriptor.pages[ids[n]]; _rootReading.Add(ids[n]); }
                var task = StartRootBatchRead(_reader, pages, _geometry.ResolvedVertexStride, _descriptor.weightStride, inflater);
                transferred = true;
                _rootBatches.Add(new RootRead { ids = ids, reservedBytes = packed + decoded, task = task });
            }
            finally { if (!transferred) ClusterMeshInflatePool.Return(inflater); }
            RootReadOperations++; BatchedRootPages += count;
            readSlots--; readBytes -= packed; decodedBytes -= packed + decoded; cursor += count - 1;
            return true;
        }

        // Allocate the capture only after a batch is admitted. Keeping this
        // lambda in TryStartRootBatch also allocates on its early-return paths.
        static Task<Payload[]> StartRootBatchRead(ClusterMeshStreamReader reader, ClusterMeshStreamPage[] pages, int stride, int weightStride, ClusterMeshPageInflater inflater)
        {
            return Task.Run(() =>
            {
                try { return ReadRootBatch(reader, pages, stride, weightStride, inflater); }
                finally { ClusterMeshInflatePool.Return(inflater); }
            });
        }

        int CompareRequestedPages(int a, int b)
        {
            PageRequest ra = _requests[a];
            PageRequest rb = _requests[b];
            ulong pa = (ulong)ra.priority + ((ulong)Mathf.Min(4095, _tick - ra.firstTick) << 20);
            ulong pb = (ulong)rb.priority + ((ulong)Mathf.Min(4095, _tick - rb.firstTick) << 20);
            int priority = pb.CompareTo(pa);
            if (priority == 0 && IsRootPage(a) && IsRootPage(b))
                return _descriptor.pages[a].fileOffset.CompareTo(_descriptor.pages[b].fileOffset);
            return priority != 0 ? priority : ra.firstTick.CompareTo(rb.firstTick);
        }

        void CollectReads()
        {
            for (int n = _rootBatches.Count - 1; n >= 0; n--)
            {
                var batch = _rootBatches[n];
                if (!batch.task.IsCompleted) continue;
                _rootBatches.RemoveAt(n);
                foreach (int id in batch.ids) _rootReading.Remove(id);
                if (batch.task.IsFaulted || batch.task.IsCanceled)
                { Fail(TaskError(batch.task, "Unable to read ClusterMesh root batch.")); return; }
                for (int p = 0; p < batch.ids.Length; p++) _completed.Add(batch.ids[p], batch.task.Result[p]);
            }
            _scratch.Clear();
            foreach (KeyValuePair<int, Task<Payload>> pair in _reads)
            {
                if (pair.Value.IsCompleted)
                    _scratch.Add(pair.Key);
            }
            for (int i = 0; i < _scratch.Count; i++)
            {
                int id = _scratch[i];
                Task<Payload> task = _reads[id];
                _reads.Remove(id);
                if (task.IsFaulted || task.IsCanceled)
                {
                    Fail(TaskError(task, "Unable to read ClusterMesh stream page " + id + "."));
                    return;
                }
                _completed[id] = task.Result;
            }
        }

        void UploadPages(ref int uploadSlots, ref long uploadBytes)
        {
            if (uploadSlots <= 0 || uploadBytes <= 0)
                return;
            _scratch.Clear();
            foreach (int id in _completed.Keys)
                _scratch.Add(id);
            _scratch.Sort(_requestComparer ?? (_requestComparer = CompareRequestedPages));
            bool nodesChanged = false;
            for (int i = 0; i < _scratch.Count && uploadSlots > 0; i++)
            {
                int id = _scratch[i];
                int nodeIndex = _descriptor.pages[id].nodeIndex;
                if (_loadingNode >= 0 && !IsRootPage(id) && nodeIndex != _loadingNode)
                    continue;
                int bytes = _descriptor.pages[id].uncompressedLength - PayloadHeaderBytes;
                if (bytes > uploadBytes)
                    continue;
                int count = RootUploadCount(i, uploadBytes, ref bytes);
                Payload payload = _completed[id];
                try
                {
                    if (!(count > 1 ? UploadRootBatch(id, count) : UploadPage(id, payload, ref nodesChanged)))
                        continue; // no evictable slot yet; retain decoded payload.
                }
                catch (Exception ex)
                {
                    _sharedPool?.CancelAllocation(this, id);
                    payload.Dispose();
                    _completed.Remove(id);
                    Fail("Unable to upload ClusterMesh stream page " + id + ": " + ex.Message);
                    return;
                }
                for (int p = 0; p < count; p++)
                {
                    int completedId = id + p;
                    _completed[completedId].Dispose();
                    _completed.Remove(completedId);
                    _requests.Remove(completedId);
                }
                if (IsRootPage(id))
                {
                    RootUploadOperations++;
                    if (count > 1) BatchedRootUploadPages += count;
                }
                i += count - 1;
                uploadSlots--;
                uploadBytes -= bytes;
            }
        }

        int RootUploadCount(int cursor, long budget, ref int bytes)
        {
            int first = _scratch[cursor];
            // Local/non-packed pools and skin weights retain the existing path.
            // A batch never spans owners, slots, missing pages or virtual IDs.
            if (_sharedPool == null || !IsRootPage(first) || _descriptor.weightStride != 0 ||
                _descriptor.kind != ClusterMeshStreamKind.Static) return 1;
            var settings = ClusterMeshSettings.OverrideForTests ?? ClusterMeshSettings.Loaded;
            int limit = settings != null ? settings.MaxRootPagesPerUpload : 16;
            int count = 1;
            var page = _descriptor.pages[first];
            int vertexEnd = RootLayout.vertexOffsets[first] + page.vertexCount;
            int indexEnd = RootLayout.indexOffsets[first] + page.indexWordCount;
            while (count < limit && cursor + count < _scratch.Count)
            {
                int id = _scratch[cursor + count];
                if (id != first + count || RootLayout.slots[id] != RootLayout.slots[first] ||
                    RootLayout.vertexOffsets[id] != vertexEnd || RootLayout.indexOffsets[id] != indexEnd) break;
                page = _descriptor.pages[id];
                int next = page.uncompressedLength - PayloadHeaderBytes;
                if ((long)bytes + next > budget) break;
                bytes += next; vertexEnd += page.vertexCount; indexEnd += page.indexWordCount; count++;
            }
            return count;
        }

        bool UploadRootBatch(int first, int count)
        {
            var firstPage = _descriptor.pages[first];
            if (!_sharedPool.TryAllocate(this, first, firstPage.nodeIndex, true,
                ClusterMeshStreaming.UpdateSerial, out int slot)) return false;
            int last = first + count - 1;
            int vertices = RootLayout.vertexOffsets[last] + _descriptor.pages[last].vertexCount - RootLayout.vertexOffsets[first];
            int indices = RootLayout.indexOffsets[last] + _descriptor.pages[last].indexWordCount - RootLayout.indexOffsets[first];
            int vertexBase = VertexSlotBase(slot) + RootLayout.vertexOffsets[first];
            int indexBase = IndexSlotBase(slot) + RootLayout.indexOffsets[first];
            bool tight = _geometry.ResolvedVertexStride == ClusterMeshLimits.TightVertexStride;
            // Reuse the existing one-physical-page upload scratch. No new decoded
            // buffer, GPU slot, cache or per-batch allocation is introduced.
            var scratch = ClusterMeshUploadScratch.Acquire(tight, vertices, indices, 0, 0);
            try
            {
                int v = 0, ix = 0;
                for (int id = first; id <= last; id++)
                {
                    var page = _descriptor.pages[id]; byte[] raw = _completed[id].raw;
                    if (tight) CopyBytesToStructs(raw, PayloadHeaderBytes, scratch.tightVertices, page.vertexCount, v);
                    else CopyBytesToStructs(raw, PayloadHeaderBytes, scratch.vertices, page.vertexCount, v);
                    Buffer.BlockCopy(raw, PayloadHeaderBytes + page.vertexCount * _geometry.ResolvedVertexStride,
                        scratch.indices, ix * 4, page.indexWordCount * 4);
                    v += page.vertexCount; ix += page.indexWordCount;
                }
                if (vertices > 0)
                {
                    if (tight) VerticesTight.SetData(scratch.tightVertices, 0, vertexBase, vertices);
                    else Vertices.SetData(scratch.vertices, 0, vertexBase, vertices);
                }
                if (indices > 0) Indices.SetData(scratch.indices, 0, indexBase, indices);
            }
            finally { ClusterMeshUploadScratch.Release(); }
            // Publish residency only after all payload uploads succeeded. An
            // incomplete node still falls back to its resident ancestor as before.
            for (int id = first; id <= last; id++)
            {
                int vb = VertexSlotBase(slot) + RootLayout.vertexOffsets[id];
                _pageSlots[id] = slot;
                _pageTable[id] = new ClusterMeshStreamPageTableEntry { vertexBase = (uint)vb, weightBase = (uint)vb,
                    indexBase = (uint)(IndexSlotBase(slot) + RootLayout.indexOffsets[id]), flags = Resident };
            }
            PageTable.SetData(_pageTable, first, first, count);
            int previousNode = -1;
            for (int id = first; id <= last; id++)
            {
                int node = _descriptor.pages[id].nodeIndex;
                if (node == previousNode) continue;
                RecomputeNodeResident(node); NodeResident.SetData(_nodeResident, node, node, 1); previousNode = node;
            }
            return true;
        }

        bool UploadPage(int pageId, Payload payload, ref bool nodesChanged)
        {
            ClusterMeshStreamPage page = _descriptor.pages[pageId];
            int slot;
            if (_sharedPool != null)
            {
                if (!_sharedPool.TryAllocate(this, pageId, page.nodeIndex,
                    IsRootPage(pageId), ClusterMeshStreaming.UpdateSerial, out slot))
                    return false;
            }
            else
            {
                slot = FindSlot();
                if (slot < 0) return false;
                if (_slots[slot].pageId >= 0)
                {
                    int old = _slots[slot].pageId;
                    int oldNode = _descriptor.pages[old].nodeIndex;
                    _pageSlots[old] = -1;
                    _pageTable[old] = default;
                    RecomputeNodeResident(oldNode);
                    PageTable.SetData(_pageTable, old, old, 1);
                    NodeResident.SetData(_nodeResident, oldNode, oldNode, 1);
                    nodesChanged = true;
                }
            }

            if (!IsRootPage(pageId) && _loadingNode < 0)
                _loadingNode = page.nodeIndex;
            int vertexBase = VertexSlotBase(slot);
            int indexBase = IndexSlotBase(slot);
            if (_sharedPool != null && RootLayout.slots[pageId] >= 0)
            {
                vertexBase += RootLayout.vertexOffsets[pageId];
                indexBase += RootLayout.indexOffsets[pageId];
            }
            var scratch = ClusterMeshUploadScratch.Acquire(
                _geometry.ResolvedVertexStride == ClusterMeshLimits.TightVertexStride,
                page.vertexCount, page.indexWordCount, page.weightCount, _descriptor.weightStride);
            try
            {
                byte[] raw = payload.raw;
                int cursor = PayloadHeaderBytes;
                if (_geometry.ResolvedVertexStride == ClusterMeshLimits.TightVertexStride)
                {
                    CopyBytesToStructs(raw, cursor, scratch.tightVertices, page.vertexCount);
                    if (page.vertexCount > 0) VerticesTight.SetData(scratch.tightVertices, 0, vertexBase, page.vertexCount);
                }
                else
                {
                    CopyBytesToStructs(raw, cursor, scratch.vertices, page.vertexCount);
                    if (page.vertexCount > 0) Vertices.SetData(scratch.vertices, 0, vertexBase, page.vertexCount);
                }
                cursor += page.vertexCount * _geometry.ResolvedVertexStride;
                if (page.indexWordCount > 0)
                {
                    Buffer.BlockCopy(raw, cursor, scratch.indices, 0, page.indexWordCount * 4);
                    Indices.SetData(scratch.indices, 0, indexBase, page.indexWordCount);
                }
                cursor += page.indexWordCount * 4;
                if (_descriptor.weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride)
                {
                    CopyBytesToStructs(raw, cursor, scratch.weights, page.weightCount);
                    if (page.weightCount > 0) Weights.SetData(scratch.weights, 0, vertexBase, page.weightCount);
                }
                else if (_descriptor.weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride8)
                {
                    int count = page.weightCount * 2;
                    if (count > 0)
                    {
                        Buffer.BlockCopy(raw, cursor, scratch.weights8, 0, count * 4);
                        Weights8.SetData(scratch.weights8, 0, vertexBase * 2, count);
                    }
                }

            }
            finally { ClusterMeshUploadScratch.Release(); }

            if (_sharedPool == null)
            {
                _slots[slot].pageId = pageId;
                _slots[slot].pinned = IsRootPage(pageId);
                _slots[slot].lastUsed = _tick;
            }
            _pageSlots[pageId] = slot;
            _pageTable[pageId] = new ClusterMeshStreamPageTableEntry
            {
                vertexBase = (uint)vertexBase,
                indexBase = (uint)indexBase,
                weightBase = (uint)vertexBase,
                flags = Resident
            };
            RecomputeNodeResident(page.nodeIndex);
            PageTable.SetData(_pageTable, pageId, pageId, 1);
            NodeResident.SetData(_nodeResident, page.nodeIndex, page.nodeIndex, 1);
            if (_loadingNode == page.nodeIndex && IsNodeResident(page.nodeIndex))
            {
                _loadingNode = -1;
                _sharedPool?.CompleteNodeAdmission(this, page.nodeIndex);
            }
            nodesChanged = true;
            return true;
        }

        int FindSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].pageId < 0) return i;
            int result = -1;
            int oldest = int.MaxValue;
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (slot.pinned || slot.lastUsed >= oldest ||
                    _tick - slot.lastUsed < ClusterMeshStreaming.ResidentGraceUpdates ||
                    (slot.pageId >= 0 && _descriptor.pages[slot.pageId].nodeIndex == _loadingNode))
                    continue;
                oldest = slot.lastUsed;
                result = i;
            }
            return result;
        }

        internal bool CanEvictSharedPage(int pageId)
        {
            return !_disposed && !Failed && pageId >= 0 && pageId < PageCount &&
                _descriptor.pages[pageId].nodeIndex != _loadingNode;
        }

        internal void OnSharedPageEvicted(int pageId)
        {
            if (_disposed || Failed || pageId < 0 || pageId >= PageCount) return;
            SharedPageEvictions++;
            int node = _descriptor.pages[pageId].nodeIndex;
            _pageSlots[pageId] = -1;
            _pageTable[pageId] = default;
            RecomputeNodeResident(node);
            if (PageTable != null) PageTable.SetData(_pageTable, pageId, pageId, 1);
            if (NodeResident != null) NodeResident.SetData(_nodeResident, node, node, 1);
        }

        void RecomputeNodeResident(int node)
        {
            if (node < 0 || node >= NodeCount)
                return;
            var pages = _nodePages[node];
            bool all = pages.Length > 0;
            for (int i = 0; i < pages.Length; i++)
            {
                if (_pageSlots[pages[i]] < 0) { all = false; break; }
            }
            _nodeResident[node] = all ? 1u : 0u;
        }

        static int[] BuildRootNodes(ClusterMeshStreamNode[] nodes)
        {
            int count = 0;
            for (int i = 0; i < nodes.Length; i++) if (nodes[i].parentNodeIndex < 0) count++;
            var roots = new int[count];
            int cursor = 0;
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].parentNodeIndex < 0) roots[cursor++] = i;
            return roots;
        }

        bool IsRootPage(int page)
        {
            ClusterMeshStreamPage p = _descriptor.pages[page];
            return p.root || _descriptor.nodes[p.nodeIndex].parentNodeIndex < 0;
        }

        void RequestRoots()
        {
            int roots = 0;
            for (int i = 0; i < PageCount; i++)
            {
                if (!IsRootPage(i)) continue;
                QueuePage(i, uint.MaxValue);
                roots++;
            }
            if (roots == 0)
                Fail("ClusterMesh streaming descriptor has no root pages.");
            else if (roots > (_sharedPool != null ? _sharedPool.Capacity : _slots.Length))
                Fail("ClusterMesh streaming root pages exceed the fixed page pool.");
        }

        bool AllRootPagesResident()
        {
            if (!_metadataReady) return false;
            for (int i = 0; i < PageCount; i++)
                if (IsRootPage(i) && _pageSlots[i] < 0) return false;
            return true;
        }

        int VertexSlotBase(int slot) => _sharedPool != null ? _sharedPool.VertexBase(slot) : slot * PhysicalVertexPageCapacity;
        int IndexSlotBase(int slot) => _sharedPool != null ? _sharedPool.IndexBase(slot) : slot * PhysicalIndexPageCapacity;
        internal void RefreshSharedGpuBuffers()
        {
            Vertices = _sharedPool.Vertices;
            VerticesTight = _sharedPool.VerticesTight;
            Indices = _sharedPool.Indices;
            Weights = _sharedPool.Weights;
            Weights8 = _sharedPool.Weights8;
        }

        void CreateGpuBuffers()
        {
            if (_sharedPool != null)
            {
                Vertices = _sharedPool.Vertices;
                VerticesTight = _sharedPool.VerticesTight;
                Indices = _sharedPool.Indices;
                Weights = _sharedPool.Weights;
                Weights8 = _sharedPool.Weights8;
            }
            else
            {
                int vertices = Capacity(_descriptor.poolPageCapacity, _descriptor.vertexPageCapacity);
                int indices = Capacity(_descriptor.poolPageCapacity, _descriptor.indexPageCapacity);
                bool tight = _geometry.ResolvedVertexStride == ClusterMeshLimits.TightVertexStride;
                Vertices = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    tight ? 1 : vertices, ClusterMeshLimits.ClusterVertexStride);
                VerticesTight = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    tight ? vertices : 1, ClusterMeshLimits.TightVertexStride);
                Indices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indices, 4);
                bool weights4 = _descriptor.weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride;
                bool weights8 = _descriptor.weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride8;
                Weights = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    weights4 ? vertices : 1, ClusterSkinnedMeshAsset.PackedSkinWeightStride);
                Weights8 = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    weights8 ? Capacity(vertices, 2) : 2, 4);
            }
            Addresses = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, _descriptor.addresses.Length), 16);
            PageTable = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, PageCount), 16);
            NodeResident = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, NodeCount), 4);
            Nodes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, NodeCount), 64);
            Addresses.SetData(_descriptor.addresses.Length > 0 ? _descriptor.addresses : new ClusterMeshStreamAddress[1]);
            PageTable.SetData(PageCount > 0 ? _pageTable : new ClusterMeshStreamPageTableEntry[1]);
            NodeResident.SetData(NodeCount > 0 ? _nodeResident : new uint[1]);
            Nodes.SetData(NodeCount > 0 ? _descriptor.nodes : new ClusterMeshStreamNode[1]);
        }

        bool ValidateDescriptor(out int[] pageCounts, out int rootPageCount, out int maxDetailPages)
        {
            pageCounts = null;
            rootPageCount = 0;
            maxDetailPages = 0;
            if (_descriptor.poolPageCapacity > 4096 || _descriptor.vertexPageCapacity > 65536 ||
                _descriptor.indexPageCapacity > 131072 || NodeCount > 1_000_000 || PageCount > 1_000_000)
                return FailInvalid("ClusterMesh streaming descriptor exceeds supported resource limits.");
            if (_descriptor.weightStride != 0 &&
                _descriptor.weightStride != ClusterSkinnedMeshAsset.PackedSkinWeightStride &&
                _descriptor.weightStride != ClusterSkinnedMeshAsset.PackedSkinWeightStride8)
                return FailInvalid("ClusterMesh streaming weight stride is unsupported.");
            if ((_descriptor.kind == ClusterMeshStreamKind.Static && _descriptor.weightStride != 0) ||
                (_descriptor.kind == ClusterMeshStreamKind.Skinned && _descriptor.weightStride == 0))
                return FailInvalid("ClusterMesh streaming kind and skin-weight layout do not match.");
            int clusterCount = _geometry.clusters != null ? _geometry.clusters.Length : 0;
            if (clusterCount <= 0 || _descriptor.addresses.Length != clusterCount)
                return FailInvalid("ClusterMesh streaming address metadata does not match geometry clusters.");
            for (int i = 0; i < NodeCount; i++)
            {
                ClusterMeshStreamNode n = _descriptor.nodes[i];
                if (n.clusterStart < 0 || n.clusterCount <= 0 || n.parentNodeIndex < -1 ||
                    n.parentNodeIndex >= NodeCount || n.parentNodeIndex == i ||
                    n.clusterStart > clusterCount - n.clusterCount)
                    return FailInvalid("ClusterMesh streaming node " + i + " is invalid.");
            }
            pageCounts = new int[NodeCount + 1];
            long previousPageEnd = FileHeaderBytes + (long)DirectoryEntryBytes * PageCount;
            for (int i = 0; i < PageCount; i++)
            {
                ClusterMeshStreamPage p = _descriptor.pages[i];
                if (p.nodeIndex < 0 || p.nodeIndex >= NodeCount || p.fileOffset < 0 ||
                    p.compressedLength <= 0 || p.compressedLength > p.uncompressedLength ||
                    p.uncompressedLength < PayloadHeaderBytes ||
                    p.vertexCount < 0 || p.vertexCount > _descriptor.vertexPageCapacity ||
                    p.indexWordCount < 0 || p.indexWordCount > _descriptor.indexPageCapacity ||
                    p.weightCount < 0 || p.weightCount > _descriptor.vertexPageCapacity)
                    return FailInvalid("ClusterMesh streaming page " + i + " is invalid.");
                long expected = PayloadHeaderBytes + (long)p.vertexCount * _geometry.ResolvedVertexStride +
                    (long)p.indexWordCount * 4L + (long)p.weightCount * _descriptor.weightStride;
                if (expected != p.uncompressedLength)
                    return FailInvalid("ClusterMesh streaming page payload length is invalid.");
                if (p.fileOffset < previousPageEnd)
                    return FailInvalid("ClusterMesh streaming pages overlap or are out of order.");
                long end;
                try { end = checked(p.fileOffset + (long)p.compressedLength); }
                catch (OverflowException) { return FailInvalid("ClusterMesh streaming page range overflows."); }
                if (_descriptor.fileSize > 0 && end > _descriptor.fileSize)
                    return FailInvalid("ClusterMesh streaming page lies outside the stream file.");
                previousPageEnd = end;
                pageCounts[p.nodeIndex + 1]++;
                if (IsRootPage(i)) rootPageCount++;
                if (_descriptor.weightStride == 0 && p.weightCount != 0)
                    return FailInvalid("ClusterMesh static streaming page has skin weights.");
                if (_descriptor.weightStride != 0 && p.weightCount != p.vertexCount)
                    return FailInvalid("ClusterMesh skinned streaming page weights do not match its vertices.");
            }
            for (int i = 0; i < _descriptor.addresses.Length; i++)
            {
                ClusterMeshStreamAddress address = _descriptor.addresses[i];
                if (address.pageId >= PageCount)
                    return FailInvalid("ClusterMesh streaming address has an invalid page id.");
                ClusterMeshStreamPage page = _descriptor.pages[address.pageId];
                ClusterHeader cluster = _geometry.clusters[i];
                long vertexEnd = (long)address.vertexOffset + cluster.vertexCount;
                long logicalIndexEnd = (long)address.indexOffset + (long)cluster.triangleCount * 3L;
                if (vertexEnd > page.vertexCount || logicalIndexEnd > (long)page.indexWordCount * 2L)
                    return FailInvalid("ClusterMesh streaming cluster address exceeds its page payload.");
            }
            for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
            {
                ClusterMeshStreamNode node = _descriptor.nodes[nodeIndex];
                if (pageCounts[nodeIndex + 1] == 0)
                    return FailInvalid("ClusterMesh streaming node " + nodeIndex + " has no pages.");
                int end = node.clusterStart + node.clusterCount;
                for (int cluster = node.clusterStart; cluster < end; cluster++)
                {
                    uint pageId = _descriptor.addresses[cluster].pageId;
                    if (_descriptor.pages[pageId].nodeIndex != nodeIndex)
                        return FailInvalid("ClusterMesh streaming cluster address crosses node ownership.");
                }
            }
            for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
            {
                if (_descriptor.nodes[nodeIndex].parentNodeIndex < 0)
                    continue;
                int nodePages = pageCounts[nodeIndex + 1];
                maxDetailPages = Math.Max(maxDetailPages, nodePages);
                if (rootPageCount + nodePages > _descriptor.poolPageCapacity)
                    return FailInvalid(
                        "ClusterMesh streaming pool cannot hold root pages and one complete detail node.");
            }
            return true;
        }

        void ValidateDirectory(byte[] data)
        {
            if (data == null || data.Length < FileHeaderBytes)
                throw new InvalidDataException("ClusterMesh stream header is truncated.");
            if (ReadU32(data, 0) != ClusterMeshStreaming.FileMagic ||
                ReadI32(data, 4) != _descriptor.formatVersion ||
                ReadI32(data, 8) != (int)_descriptor.kind ||
                ReadI32(data, 12) != PageCount)
                throw new InvalidDataException("ClusterMesh stream header does not match the descriptor.");
            if (data.LongLength != (long)FileHeaderBytes + (long)DirectoryEntryBytes * PageCount)
                throw new InvalidDataException("ClusterMesh stream directory is truncated.");
            for (int i = 0; i < PageCount; i++)
            {
                int o = FileHeaderBytes + i * DirectoryEntryBytes;
                ClusterMeshStreamPage p = _descriptor.pages[i];
                if (ReadI64(data, o) != p.fileOffset || ReadI32(data, o + 8) != p.compressedLength ||
                    ReadI32(data, o + 12) != p.uncompressedLength || ReadI32(data, o + 16) != p.vertexCount ||
                    ReadI32(data, o + 20) != p.indexWordCount || ReadI32(data, o + 24) != p.weightCount ||
                    ReadU32(data, o + 28) != p.crc32 || ReadI32(data, o + 32) != p.nodeIndex ||
                    ReadI32(data, o + 36) != p.flags)
                    throw new InvalidDataException("ClusterMesh stream directory entry " + i + " does not match its descriptor.");
            }
        }

        static Payload ReadPage(ClusterMeshStreamReader reader, ClusterMeshStreamPage page, int vertexStride, int weightStride, ClusterMeshPageInflater inflater)
        {
            byte[] packed = ArrayPool<byte>.Shared.Rent(page.compressedLength);
            byte[] raw = null;
            try
            {
                reader.Read(page.fileOffset, packed, page.compressedLength);
                if (page.compressedLength == page.uncompressedLength)
                {
                    raw = packed;
                    packed = null;
                }
                else
                {
                    raw = ArrayPool<byte>.Shared.Rent(page.uncompressedLength);
                    inflater.Inflate(packed, 0, page.compressedLength, raw, page.uncompressedLength);
                }
                reader.ThrowIfDisposed();
                ValidatePayload(raw, page, vertexStride, weightStride);
                var result = new Payload { raw = raw, length = page.uncompressedLength };
                raw = null;
                return result;
            }
            finally
            {
                if (packed != null) ArrayPool<byte>.Shared.Return(packed);
                if (raw != null) ArrayPool<byte>.Shared.Return(raw);
            }
        }

        static void ValidatePayload(byte[] raw, ClusterMeshStreamPage page, int vertexStride, int weightStride)
        {
            if (Crc32(raw, page.uncompressedLength) != page.crc32)
                throw new InvalidDataException("ClusterMesh stream page checksum is invalid.");
            if (ReadI32(raw, 0) != page.vertexCount || ReadI32(raw, 4) != page.indexWordCount || ReadI32(raw, 8) != page.weightCount)
                throw new InvalidDataException("ClusterMesh stream page payload counts do not match.");
            long expected = PayloadHeaderBytes + (long)page.vertexCount * vertexStride + (long)page.indexWordCount * 4 + (long)page.weightCount * weightStride;
            if (expected != page.uncompressedLength) throw new InvalidDataException("ClusterMesh stream page payload layout is invalid.");
        }

        static Payload[] ReadRootBatch(ClusterMeshStreamReader reader, ClusterMeshStreamPage[] pages, int vertexStride, int weightStride, ClusterMeshPageInflater inflater)
        {
            int length = checked((int)(pages[pages.Length - 1].fileOffset + pages[pages.Length - 1].compressedLength - pages[0].fileOffset));
            byte[] packed = ArrayPool<byte>.Shared.Rent(length);
            var results = new Payload[pages.Length];
            try
            {
                reader.Read(pages[0].fileOffset, packed, length);
                for (int n = 0; n < pages.Length; n++)
                {
                    var page = pages[n]; int offset = checked((int)(page.fileOffset - pages[0].fileOffset));
                    reader.ThrowIfDisposed();
                    var result = new Payload { raw = ArrayPool<byte>.Shared.Rent(page.uncompressedLength), length = page.uncompressedLength };
                    results[n] = result;
                    if (page.compressedLength == page.uncompressedLength)
                        Buffer.BlockCopy(packed, offset, result.raw, 0, page.uncompressedLength);
                    else
                        inflater.Inflate(packed, offset, page.compressedLength, result.raw, page.uncompressedLength);
                    ValidatePayload(result.raw, page, vertexStride, weightStride);
                }
                return results;
            }
            catch { foreach (var result in results) result?.Dispose(); throw; }
            finally { ArrayPool<byte>.Shared.Return(packed); }
        }

        static byte[] ReadRange(ClusterMeshStreamReader reader, long offset, int length)
        {
            if (offset < 0 || length < 0) throw new IOException("ClusterMesh stream range is invalid.");
            var result = new byte[length];
            reader.Read(offset, result, length);
            return result;
        }

        static void CopyBytesToStructs<T>(byte[] bytes, int sourceOffset, T[] destination, int count, int destinationOffset = 0) where T : struct
        {
            if (count == 0) return;
            int stride = Marshal.SizeOf<T>();
            int byteCount = checked(count * stride);
            if (bytes == null || destination == null || destinationOffset < 0 || destinationOffset > destination.Length - count ||
                sourceOffset < 0 || sourceOffset > bytes.Length - byteCount)
                throw new InvalidDataException("ClusterMesh structured page data is invalid.");
            GCHandle h = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try { Marshal.Copy(bytes, sourceOffset, IntPtr.Add(h.AddrOfPinnedObject(), checked(destinationOffset * stride)), byteCount); }
            finally { h.Free(); }
        }

        static int Capacity(int a, int b)
        {
            try { return Mathf.Max(1, checked(a * b)); }
            catch (OverflowException) { throw new InvalidOperationException("ClusterMesh stream page pool is too large."); }
        }

        static int ReadI32(byte[] data, int o)
        {
            if (data == null || o < 0 || o > data.Length - 4) throw new InvalidDataException("ClusterMesh stream integer is out of bounds.");
            return data[o] | data[o + 1] << 8 | data[o + 2] << 16 | data[o + 3] << 24;
        }
        static uint ReadU32(byte[] data, int o) => unchecked((uint)ReadI32(data, o));
        static long ReadI64(byte[] data, int o) => unchecked((long)((ulong)ReadU32(data, o) | ((ulong)ReadU32(data, o + 4) << 32)));
        static uint Crc32(byte[] data, int length)
        {
            uint crc = 0xffffffffu;
            for (int i = 0; data != null && i < length; i++)
                crc = CrcTable[(crc ^ data[i]) & 0xffu] ^ (crc >> 8);
            return ~crc;
        }
        static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1u) != 0 ? (value >> 1) ^ 0xedb88320u : value >> 1;
                table[i] = value;
            }
            return table;
        }
        static string TaskError(Task task, string fallback) =>
            task != null && task.Exception != null && task.Exception.GetBaseException() != null
                ? task.Exception.GetBaseException().Message : fallback;
        bool FailInvalid(string text) { Fail(text); return false; }
        internal void RejectGpuBudget()
        {
            WaitingForGpuBudget = false;
            Fail("GPU budget cannot fit pinned roots and a complete detail node after automatic rebuild. Adjust Tools/ClusterMesh/通用设置 → 共享几何总上限 MiB (MaxSharedGpuMegabytes), then retry the rejected asset.", false);
        }
        void Fail(string text, bool log = true)
        {
            if (Failed) return;
            Failed = true;
            IsReady = false;
            Error = text;
            _reader?.Dispose();
            ReleaseMetadataRead();
            ReleasePendingPayloads();
            ReleaseGpuBuffers();
            if (log) Debug.LogError("ClusterMesh page streaming: " + text);
        }

        void ReleaseGpuBuffers()
        {
            if (_sharedPool == null)
            {
                Vertices?.Dispose(); VerticesTight?.Dispose(); Indices?.Dispose();
                Weights?.Dispose(); Weights8?.Dispose();
            }
            else if (!_sharedPoolReleased)
            {
                ClusterMeshStreaming.ReleaseSharedPool(_sharedPool, this);
                _sharedPoolReleased = true;
            }
            Addresses?.Dispose();
            PageTable?.Dispose(); NodeResident?.Dispose(); Nodes?.Dispose();
            Vertices = null; VerticesTight = null; Indices = null;
            Weights = null; Weights8 = null; Addresses = null;
            PageTable = null; NodeResident = null; Nodes = null;
        }

        void ReleasePendingPayloads()
        {
            foreach (var batch in _rootBatches)
                batch.task.ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion) foreach (var payload in t.Result) payload?.Dispose();
                    else { var ignored = t.Exception; }
                }, TaskScheduler.Default);
            _rootBatches.Clear(); _rootReading.Clear();
            foreach (Payload payload in _completed.Values) payload?.Dispose();
            _completed.Clear();
            foreach (Task<Payload> task in _reads.Values)
            {
                task.ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion) t.Result?.Dispose();
                    else { var ignored = t.Exception; }
                }, TaskScheduler.Default);
            }
            _reads.Clear();
        }

        void ReleaseMetadataRead()
        {
            if (_metadataRead == null) return;
            // A retired worker can fail after unload. Observe that failure even
            // when the main-thread polling path will never run again.
            _metadataRead.ContinueWith(t => { var ignored = t.Exception; }, TaskScheduler.Default);
            _metadataRead = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsReady = false;
            _reader?.Dispose();
            ReleaseMetadataRead();
            _requests.Clear();
            ReleasePendingPayloads();
            ReleaseGpuBuffers();
        }
    }

    public static partial class ClusterMeshStreaming
    {
        // 'CMPS' in little-endian byte order.
        public const uint FileMagic = 0x53504d43u;
        public const int FormatVersion = 1;
        static readonly Dictionary<ClusterMeshAsset, ClusterMeshPageRuntime> Runtimes =
            new Dictionary<ClusterMeshAsset, ClusterMeshPageRuntime>();
        static readonly Dictionary<ClusterMeshAsset, int> References =
            new Dictionary<ClusterMeshAsset, int>();
        static readonly List<ClusterMeshAsset> Stale = new List<ClusterMeshAsset>();
        static readonly List<ClusterMeshPageRuntime> Active = new List<ClusterMeshPageRuntime>();
        struct DispatchEntry
        {
            public ClusterMeshPageRuntime runtime;
            public int priority, order;
        }
        static readonly List<DispatchEntry> Dispatch = new List<DispatchEntry>();
        static void SortDispatch()
        {
            // Mono's generic sort adapter allocates for this value-type list.
            // The active-owner list is small; stable in-place insertion preserves
            // round-robin order within each priority without an adapter or boxing.
            for (int i = 1; i < Dispatch.Count; i++)
            {
                var item = Dispatch[i]; int j = i - 1;
                while (j >= 0 && Dispatch[j].priority < item.priority)
                { Dispatch[j + 1] = Dispatch[j]; j--; }
                Dispatch[j + 1] = item;
            }
        }
        static readonly List<ClusterMeshSharedGpuPool> SharedPools = new List<ClusterMeshSharedGpuPool>();
        static int _lastUpdateFrame = int.MinValue;
        static int _roundRobinStart;
        static int _updateSerial;

        static int maxConcurrentReads = 8;
        public static int MaxConcurrentReads
        {
            get => maxConcurrentReads;
            set { maxConcurrentReads = Math.Max(0, value); ClusterMeshInflatePool.SetLimit(maxConcurrentReads); }
        }
        public static int ActiveDecodeContexts => ClusterMeshInflatePool.Active;
        public static int RetainedDecodeContexts => ClusterMeshInflatePool.Retained;
        // Fixed array payload only; excludes object/array headers and page data.
        public static long DecodeWorkspacePayloadBytes => ClusterMeshInflatePool.WorkspacePayloadBytes;
        public static int MaxUploadsPerUpdate { get; set; } = 4;
        public static long MaxReadBytesPerUpdate { get; set; } = 8L * 1024L * 1024L;
        public static long MaxUploadBytesPerUpdate { get; set; } = 8L * 1024L * 1024L;
        public static long MaxDecodedBytes { get; set; } = 32L * 1024L * 1024L;
        public static int ResidentGraceUpdates { get; set; } = 12;
        public static bool EnablePrefetch { get; set; } = true;
        public static int SharedGpuPoolCount => SharedPools.Count;
        public static long UploadScratchBytes => ClusterMeshUploadScratch.RetainedBytes;
        // Allocated shared geometry buffers only, not total device/process memory.
        public static long SharedGpuBufferBytes
        {
            get
            {
                long bytes = 0;
                foreach (var pool in SharedPools)
                    bytes += pool.BufferBytes;
                return bytes;
            }
        }
        internal static int UpdateSerial => _updateSerial;

        internal static bool UseGlobalSharedGpuPool
        {
            get
            {
                ClusterMeshSettings settings = ClusterMeshSettings.OverrideForTests ?? ClusterMeshSettings.Loaded;
                return settings == null || settings.EnableGlobalSharedGpuPool;
            }
        }

        public static string RootPath { get; set; }
        public static Func<ClusterMeshStreamDescriptor, string> FilePathResolver { get; set; }

        internal static int CalculateSharedPoolCapacity(
            ClusterMeshStreamDescriptor descriptor, int requestedCapacity,
            int rootPages, int maxDetailPages, out int detailWindow)
        {
            const int limit = 4096;
            if (descriptor == null || descriptor.pages == null || rootPages <= 0 ||
                rootPages > descriptor.pages.Length || maxDetailPages < 0 ||
                maxDetailPages > descriptor.pages.Length - rootPages)
                throw new InvalidDataException("ClusterMesh shared GPU pool reservation metadata is invalid.");

            // Preserve the original fixed pool budget. Detail demand must not
            // silently double GPU allocations.
            requestedCapacity = Mathf.Max(8, requestedCapacity);
            detailWindow = maxDetailPages;
            long required = Math.Max(descriptor.poolPageCapacity, (long)requestedCapacity);
            if ((long)rootPages + maxDetailPages > required)
                throw new InvalidDataException("ClusterMesh pool cannot hold roots and a complete detail node within its fixed budget.");
            if (required > limit)
                throw new InvalidDataException("ClusterMesh shared GPU pool requires " + required +
                    " pages (roots=" + rootPages + ", detailWindow=" + detailWindow +
                    ", requested=" + requestedCapacity + "), exceeding the 4096-page limit; " +
                    "the fixed budget was not expanded. Split the streaming asset or reduce its root footprint.");
            return Mathf.NextPowerOfTwo((int)required);
        }

        internal static ClusterMeshSharedGpuPool AcquireSharedPool(
            ClusterMeshPageRuntime owner, int vertexStride,
            ClusterMeshStreamDescriptor descriptor, int rootPages, int detailHeadroom)
        {
            if (owner == null || descriptor == null || rootPages <= 0) return null;
            ClusterMeshSettings settings = ClusterMeshSettings.OverrideForTests ?? ClusterMeshSettings.Loaded;
            int requestedCapacity = settings != null ? settings.SharedGpuPoolPageCapacity : 256;
            int capacity = CalculateSharedPoolCapacity(descriptor, requestedCapacity,
                rootPages, detailHeadroom, out int detailWindow);
            int physicalRoots = owner.RootLayout.SlotCount;
            // Preserve the owner's usable detail cache, not just one node or an
            // arbitrary requestedCapacity slice. Otherwise sharing a root-heavy
            // 1024-slot pool can silently reduce its detail cache to 256 slots,
            // thrash the visible LOD working set and serialize its first frame.
            // Root-only/small assets reserve only the pages they can ever use.
            detailWindow = Mathf.Min(capacity - physicalRoots,
                descriptor.pages.Length - rootPages);
            var key = new ClusterMeshSharedPoolKey
            {
                vertexStride = vertexStride,
                weightStride = descriptor.weightStride,
                vertexPageCapacity = descriptor.vertexPageCapacity,
                indexPageCapacity = descriptor.indexPageCapacity
            };
            // Best-fit reuse does not resize any buffer. Do not require identical
            // page dimensions/capacity: real baked assets differ by a few words.
            // Sum owner working windows so sharing cannot steal a neighbour's
            // existing fine-LOD capacity merely because its pages are not hot yet.
            ClusterMeshSharedGpuPool best = null;
            long bestSlotBytes = long.MaxValue;
            for (int i = 0; i < SharedPools.Count; i++)
            {
                ClusterMeshSharedGpuPool candidate = SharedPools[i];
                if (!candidate.Key.CanContain(key) ||
                    (detailWindow > 0 && (candidate.DetailVertexCapacity < owner.RootLayout.DetailVertexCapacity ||
                        candidate.DetailIndexCapacity < owner.RootLayout.DetailIndexCapacity)) ||
                    !candidate.CanReserve(physicalRoots, detailWindow))
                    continue;
                long slotBytes = (long)candidate.Key.vertexPageCapacity * (vertexStride + descriptor.weightStride) +
                    (long)candidate.Key.indexPageCapacity * 4;
                if (best == null || slotBytes < bestSlotBytes ||
                    (slotBytes == bestSlotBytes && candidate.Capacity < best.Capacity))
                { best = candidate; bestSlotBytes = slotBytes; }
            }
            if (best != null)
            { best.Acquire(owner, physicalRoots, detailWindow); return best; }
            int rootSlots = capacity - detailWindow;
            int detailVertices = owner.RootLayout.DetailVertexCapacity;
            int detailIndices = owner.RootLayout.DetailIndexCapacity;
            int initialDetails = ClusterMeshSharedGpuPool.InitialDetails(key, detailWindow, detailHeadroom, detailVertices, detailIndices);
            long initialBytes = ClusterMeshSharedGpuPool.EstimateBytes(key, rootSlots, initialDetails, detailVertices, detailIndices);
            if (!CanAllocateGpuBytes(initialBytes)) return null;
            long poolStamp=0;ClusterMeshCpuTiming.Begin(ref poolStamp);
            var pool = new ClusterMeshSharedGpuPool(key, capacity,
                Math.Max(descriptor.poolPageCapacity, Math.Max(8, requestedCapacity)),
                rootSlots, detailVertices, detailIndices, initialDetails, Math.Min(detailWindow, detailHeadroom));
            ClusterMeshCpuTiming.End("SharedPool",poolStamp);
            try { pool.Acquire(owner, physicalRoots, detailWindow); }
            catch { pool.Dispose(); throw; }
            SharedPools.Add(pool);
            return pool;
        }

        internal static void ReleaseSharedPool(
            ClusterMeshSharedGpuPool pool, ClusterMeshPageRuntime owner)
        {
            if (pool == null) return;
            pool.Release(owner);
            NoteGpuCapacityReleased();
            if (pool.ReferenceCount > 0) return;
            SharedPools.Remove(pool);
            pool.Dispose();
        }

        static void ApplyProjectStreamingSettings()
        {
            ClusterMeshSettings settings = ClusterMeshSettings.OverrideForTests ?? ClusterMeshSettings.Loaded;
            if (settings == null) return;
            MaxConcurrentReads = settings.MaxConcurrentPageReads;
            MaxUploadsPerUpdate = settings.MaxPageUploadsPerUpdate;
            MaxReadBytesPerUpdate = (long)settings.MaxReadMegabytesPerUpdate * 1024L * 1024L;
            MaxUploadBytesPerUpdate = (long)settings.MaxUploadMegabytesPerUpdate * 1024L * 1024L;
            MaxDecodedBytes = (long)settings.MaxDecodedMegabytes * 1024L * 1024L;
            ResidentGraceUpdates = settings.StreamingResidentGraceUpdates;
            EnablePrefetch = settings.EnableStreamingPrefetch;
        }

        public static ClusterMeshPageRuntime Request(ClusterMeshAsset geometry)
        {
            // This is the hard legacy boundary: old assets never create a stream runtime.
            if (geometry == null || geometry.streamDescriptor == null || !geometry.streamDescriptor.IsValid)
                return null;
            if (Runtimes.TryGetValue(geometry, out ClusterMeshPageRuntime existing) && existing != null)
                return existing;
            var runtime = new ClusterMeshPageRuntime(geometry, geometry.streamDescriptor);
            Runtimes[geometry] = runtime;
            return runtime;
        }

        public static ClusterMeshPageRuntime Acquire(ClusterMeshAsset geometry)
        {
            ClusterMeshPageRuntime runtime = Request(geometry);
            if (runtime == null)
                return null;
            References.TryGetValue(geometry, out int count);
            References[geometry] = checked(count + 1);
            return runtime;
        }

        // Safe for runtime Batcher and SceneView to both call in one frame.
        public static void Update()
        {
            ApplyProjectStreamingSettings();
            int frame = Time.frameCount;
            // Time.frameCount can stay constant while editing. The manager is
            // intentionally safe to pump more than once there so async FileStream
            // completions cannot stall until Play starts.
            if (Application.isPlaying && _lastUpdateFrame == frame) return;
            _lastUpdateFrame = frame;
            _updateSerial = _updateSerial == int.MaxValue ? 1 : _updateSerial + 1;
            foreach (var pool in SharedPools) pool.PollMigration();
            RecoverGpuBudget();
            Stale.Clear();
            Active.Clear();
            int inflightReads = 0;
            long pendingDecoded = 0;
            foreach (KeyValuePair<ClusterMeshAsset, ClusterMeshPageRuntime> pair in Runtimes)
            {
                if (pair.Key == null || pair.Value == null) { Stale.Add(pair.Key); continue; }
                Active.Add(pair.Value);
                inflightReads += pair.Value.InflightReadCount;
                pendingDecoded += pair.Value.PendingDecodedBytes;
            }
            int readSlots = Mathf.Max(0, MaxConcurrentReads - inflightReads);
            long readBytes = Math.Max(0L, MaxReadBytesPerUpdate);
            long decodedBytes = Math.Max(0L, MaxDecodedBytes - pendingDecoded);
            int uploadSlots = Mathf.Max(0, MaxUploadsPerUpdate);
            long uploadBytes = Math.Max(0L, MaxUploadBytesPerUpdate);
            if (Active.Count > 0)
            {
                int start = _roundRobinStart % Active.Count;
                Dispatch.Clear();
                for (int i = 0; i < Active.Count; i++)
                {
                    var runtime = Active[(start + i) % Active.Count];
                    Dispatch.Add(new DispatchEntry { runtime = runtime, order = i,
                        priority = runtime.IsReady ? (_updateSerial % 4 == 0 ? 0 : runtime.DetailLoadPriority) : runtime.RootLoadPriority });
                }
                // Snapshot priorities before pumping: readiness changes during
                // an update must not visit a runtime twice or skip another one.
                SortDispatch();
                    for (int i = 0; i < Active.Count; i++)
                        Dispatch[i].runtime.Update(
                            ref readSlots, ref readBytes, ref decodedBytes,
                            ref uploadSlots, ref uploadBytes);
                _roundRobinStart = (start + 1) % Active.Count;
            }
            for (int i = 0; i < Stale.Count; i++)
            {
                ClusterMeshAsset asset = Stale[i];
                // A destroyed UnityEngine.Object compares equal to null while it
                // still remains a valid dictionary key. Always dispose by key.
                if (Runtimes.TryGetValue(asset, out ClusterMeshPageRuntime runtime)) runtime.Dispose();
                Runtimes.Remove(asset);
                References.Remove(asset);
            }
        }

        public static void UpdateFromRenderLoop()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            Update();
        }

        public static void Release(ClusterMeshAsset geometry)
        {
            if (geometry == null) return;
            if (References.TryGetValue(geometry, out int count) && count > 1)
            {
                References[geometry] = count - 1;
                return;
            }
            References.Remove(geometry);
            if (Runtimes.TryGetValue(geometry, out ClusterMeshPageRuntime runtime))
            {
                // Retain a failed state so render callbacks do not reopen the same
                // missing/corrupt file every frame. DisposeAll (play transition,
                // domain reload or quit) clears it and permits a clean retry.
                if (runtime.Failed) return;
                runtime.Dispose();
            }
            Runtimes.Remove(geometry);
            if (Runtimes.Count == 0) ClusterMeshInflatePool.Clear();
        }
        public static void Release(ClusterMeshPageRuntime runtime)
        {
            if (runtime == null) return;
            ClusterMeshAsset geometry = runtime.Geometry;
            if (geometry == null || !Runtimes.TryGetValue(geometry, out ClusterMeshPageRuntime current) ||
                !ReferenceEquals(current, runtime))
            {
                runtime.Dispose();
                return;
            }
            Release(geometry);
        }

        /// <summary>
        /// Invalidates one asset after its external file has been installed,
        /// replaced, or its resolver mapping has changed. Existing contexts will
        /// reacquire the asset on their next render callback.
        /// </summary>
        public static void Retry(ClusterMeshAsset geometry)
        {
            if (geometry == null) return;
            if (Runtimes.TryGetValue(geometry, out ClusterMeshPageRuntime runtime))
                runtime.Dispose();
            Runtimes.Remove(geometry);
            References.Remove(geometry);
        }
        public static void DisposeAll()
        {
            foreach (KeyValuePair<ClusterMeshAsset, ClusterMeshPageRuntime> pair in Runtimes) pair.Value?.Dispose();
            for (int i = SharedPools.Count - 1; i >= 0; i--) SharedPools[i]?.Dispose();
            SharedPools.Clear();
            ClusterMeshUploadScratch.Clear();
            ClusterMeshInflatePool.Clear();
            Runtimes.Clear(); References.Clear(); Stale.Clear(); Active.Clear(); Dispatch.Clear(); _lastUpdateFrame = int.MinValue;
            _roundRobinStart = 0;
            _updateSerial = 0;
            lastGpuGrowthSerial = gpuBudgetPressureSerial = int.MinValue;
            GpuGrowthCount = GpuBudgetDenials = GpuShrinkCount = GpuReclaimedBytes = 0;
            recoveryRequiredBytes = pressureLoggedBudget = 0;
            pressureLogged = recoveryUnavailableLogged = false;
            NoteGpuCapacityReleased();
        }

        internal static string ResolveFilePath(ClusterMeshStreamDescriptor descriptor)
        {
            if (descriptor == null) return null;
            // Project/game code owns location policy. This supports writable,
            // hot-update and custom install directories without coupling the format
            // to StreamingAssets.
            string resolved = FilePathResolver != null ? FilePathResolver(descriptor) : null;
            if (!string.IsNullOrEmpty(resolved)) return Path.GetFullPath(resolved);
            if (!string.IsNullOrEmpty(RootPath) && !string.IsNullOrEmpty(descriptor.fileName))
                return Path.GetFullPath(Path.Combine(RootPath, descriptor.fileName));
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(descriptor.editorFilePath))
            {
                if (Path.IsPathRooted(descriptor.editorFilePath)) return descriptor.editorFilePath;
                string root = Directory.GetParent(Application.dataPath).FullName;
                return Path.GetFullPath(Path.Combine(root, descriptor.editorFilePath));
            }
#endif
            if (string.IsNullOrEmpty(descriptor.fileName)) return null;
            if (Path.IsPathRooted(descriptor.fileName)) return descriptor.fileName;
            return Path.GetFullPath(Path.Combine(
                Application.persistentDataPath, "ClusterMesh", descriptor.fileName));
        }
    }
}
