using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

    internal sealed class ClusterMeshSharedGpuPool : IDisposable
    {
        sealed class Reservation
        {
            public int rootPages;
            public int detailHeadroom;
        }

        sealed class Slot
        {
            public ClusterMeshPageRuntime owner;
            public int pageId = -1;
            public bool pinned;
            public int lastUsed;
        }

        readonly Slot[] _slots;
        readonly Dictionary<ClusterMeshPageRuntime, Reservation> _reservations =
            new Dictionary<ClusterMeshPageRuntime, Reservation>();
        ClusterMeshPageRuntime _admittedOwner;
        int _admittedNode = -1;

        public ClusterMeshSharedPoolKey Key { get; }
        public int Capacity => _slots.Length;
        public int ReferenceCount => _reservations.Count;
        public int ReservedRootPages { get; private set; }
        public int ReservedDetailHeadroom { get; private set; }
        public GraphicsBuffer Vertices { get; }
        public GraphicsBuffer VerticesTight { get; }
        public GraphicsBuffer Indices { get; }
        public GraphicsBuffer Weights { get; }
        public GraphicsBuffer Weights8 { get; }

        public ClusterMeshSharedGpuPool(ClusterMeshSharedPoolKey key, int capacity)
        {
            Key = key;
            _slots = new Slot[capacity];
            for (int i = 0; i < capacity; i++) _slots[i] = new Slot();
            try
            {
                int vertexCapacity = CheckedCapacity(capacity, key.vertexPageCapacity);
                int indexCapacity = CheckedCapacity(capacity, key.indexPageCapacity);
                bool tight = key.vertexStride == ClusterMeshLimits.TightVertexStride;
                Vertices = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    tight ? 1 : vertexCapacity, ClusterMeshLimits.ClusterVertexStride);
                VerticesTight = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    tight ? vertexCapacity : 1, ClusterMeshLimits.TightVertexStride);
                Indices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indexCapacity, 4);
                bool weights4 = key.weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride;
                bool weights8 = key.weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride8;
                Weights = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    weights4 ? vertexCapacity : 1, ClusterSkinnedMeshAsset.PackedSkinWeightStride);
                Weights8 = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    weights8 ? CheckedCapacity(vertexCapacity, 2) : 2, 4);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool CanReserve(int rootPages, int detailHeadroom)
        {
            int roots = ReservedRootPages + rootPages;
            int detail = Mathf.Max(ReservedDetailHeadroom, detailHeadroom);
            return roots <= Capacity - detail;
        }

        public void Acquire(ClusterMeshPageRuntime owner, int rootPages, int detailHeadroom)
        {
            if (owner == null || _reservations.ContainsKey(owner))
                throw new InvalidOperationException("ClusterMesh shared GPU pool owner is invalid or already acquired.");
            _reservations.Add(owner, new Reservation
            {
                rootPages = rootPages,
                detailHeadroom = detailHeadroom
            });
            ReservedRootPages += rootPages;
            ReservedDetailHeadroom = Mathf.Max(ReservedDetailHeadroom, detailHeadroom);
        }

        public bool TryAllocate(ClusterMeshPageRuntime owner, int pageId, int nodeIndex,
            bool pinned, int tick, out int slotIndex)
        {
            slotIndex = -1;
            if (!pinned)
            {
                if (_admittedOwner == null)
                {
                    _admittedOwner = owner;
                    _admittedNode = nodeIndex;
                }
                else if (!ReferenceEquals(_admittedOwner, owner) || _admittedNode != nodeIndex)
                    return false;
            }

            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].owner == null) { slotIndex = i; break; }
            if (slotIndex < 0)
            {
                int oldest = int.MaxValue;
                for (int i = 0; i < _slots.Length; i++)
                {
                    Slot slot = _slots[i];
                    if (slot.pinned || slot.lastUsed >= oldest ||
                        tick - slot.lastUsed < ClusterMeshStreaming.ResidentGraceUpdates ||
                        !slot.owner.CanEvictSharedPage(slot.pageId))
                        continue;
                    oldest = slot.lastUsed;
                    slotIndex = i;
                }
            }
            if (slotIndex < 0)
                return false;

            Slot selected = _slots[slotIndex];
            if (selected.owner != null)
                selected.owner.OnSharedPageEvicted(selected.pageId);
            selected.owner = owner;
            selected.pageId = pageId;
            selected.pinned = pinned;
            selected.lastUsed = tick;
            return true;
        }

        public void CompleteNodeAdmission(ClusterMeshPageRuntime owner, int nodeIndex)
        {
            if (!ReferenceEquals(_admittedOwner, owner) || _admittedNode != nodeIndex) return;
            _admittedOwner = null;
            _admittedNode = -1;
        }

        public void CancelAllocation(ClusterMeshPageRuntime owner, int pageId)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (!ReferenceEquals(slot.owner, owner) || slot.pageId != pageId) continue;
                slot.owner = null; slot.pageId = -1; slot.pinned = false; slot.lastUsed = 0;
                break;
            }
        }

        public void Touch(int slotIndex, int tick)
        {
            if (slotIndex >= 0 && slotIndex < _slots.Length)
                _slots[slotIndex].lastUsed = tick;
        }

        public void Release(ClusterMeshPageRuntime owner)
        {
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
            }
            if (_reservations.TryGetValue(owner, out Reservation reservation))
            {
                _reservations.Remove(owner);
                ReservedRootPages = Mathf.Max(0, ReservedRootPages - reservation.rootPages);
                ReservedDetailHeadroom = 0;
                foreach (Reservation remaining in _reservations.Values)
                    ReservedDetailHeadroom = Mathf.Max(
                        ReservedDetailHeadroom, remaining.detailHeadroom);
            }
        }

        static int CheckedCapacity(int a, int b)
        {
            try { return Mathf.Max(1, checked(a * b)); }
            catch (OverflowException) { throw new InvalidOperationException("ClusterMesh shared GPU pool is too large."); }
        }

        public void Dispose()
        {
            Vertices?.Dispose(); VerticesTight?.Dispose(); Indices?.Dispose();
            Weights?.Dispose(); Weights8?.Dispose();
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
        readonly int[][] _nodePages;
        readonly int[][] _nodeChildren;
        readonly int[] _rootNodes;
        readonly int[] _pageSlots;
        readonly Slot[] _slots;
        readonly ClusterMeshSharedGpuPool _sharedPool;
        readonly int _rootPageCount;
        readonly ClusterPackedVertex[] _vertexStaging;
        readonly ClusterPackedVertexTight[] _tightVertexStaging;
        readonly uint[] _indexStaging;
        readonly ClusterPackedSkinWeight[] _weightStaging;
        readonly uint[] _weight8Staging;
        sealed class PageRequest
        {
            public uint priority;
            public int firstTick;
        }

        readonly Dictionary<int, PageRequest> _requests = new Dictionary<int, PageRequest>();
        readonly Dictionary<int, Task<Payload>> _reads = new Dictionary<int, Task<Payload>>();
        readonly Dictionary<int, Payload> _completed = new Dictionary<int, Payload>();
        readonly List<int> _scratch = new List<int>();
        Task<byte[]> _metadataRead;
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
        public int NodeCount => _descriptor != null && _descriptor.nodes != null ? _descriptor.nodes.Length : 0;
        public int PageCount => _descriptor != null && _descriptor.pages != null ? _descriptor.pages.Length : 0;
        // GPU requests are per logical LOD node. A node can span several fixed
        // geometry pages and becomes resident only when all of them are uploaded.
        public int RequestWordCount => (NodeCount + 31) >> 5;
        internal int[] RootNodes => _rootNodes;
        internal int[] GetNodeChildren(int nodeIndex) =>
            nodeIndex >= 0 && nodeIndex < NodeCount ? _nodeChildren[nodeIndex] : Array.Empty<int>();
        internal int InflightReadCount => _reads.Count;
        internal long PendingDecodedBytes
        {
            get
            {
                long total = 0;
                foreach (int page in _reads.Keys) total += _descriptor.pages[page].uncompressedLength;
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

            if (!ValidateDescriptor())
                return;
            _pageTable = new ClusterMeshStreamPageTableEntry[descriptor.pages.Length];
            _nodeResident = new uint[descriptor.nodes.Length];
            _pageSlots = new int[descriptor.pages.Length];
            for (int i = 0; i < _pageSlots.Length; i++) _pageSlots[i] = -1;
            _rootPageCount = CountRootPages();
            ClusterMeshSharedGpuPool sharedPool = null;
            if (ClusterMeshStreaming.UseGlobalSharedGpuPool)
            {
                try
                {
                    sharedPool = ClusterMeshStreaming.AcquireSharedPool(
                        this, geometry.ResolvedVertexStride, descriptor, _rootPageCount,
                        CountMaxDetailNodePages());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("ClusterMesh shared GPU pool fallback: " + ex.Message);
                }
            }
            _sharedPool = sharedPool;
            if (_sharedPool == null)
            {
                _slots = new Slot[descriptor.poolPageCapacity];
                for (int i = 0; i < _slots.Length; i++) _slots[i] = new Slot();
            }
            bool tight = geometry.ResolvedVertexStride == ClusterMeshLimits.TightVertexStride;
            _vertexStaging = tight ? null : new ClusterPackedVertex[descriptor.vertexPageCapacity];
            _tightVertexStaging = tight ? new ClusterPackedVertexTight[descriptor.vertexPageCapacity] : null;
            _indexStaging = new uint[descriptor.indexPageCapacity];
            _weightStaging = descriptor.weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride
                ? new ClusterPackedSkinWeight[descriptor.vertexPageCapacity] : null;
            _weight8Staging = descriptor.weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride8
                ? new uint[checked(descriptor.vertexPageCapacity * 2)] : null;
            _nodePages = BuildNodePages(descriptor.nodes.Length, descriptor.pages);
            _nodeChildren = BuildNodeChildren(descriptor.nodes);
            _rootNodes = BuildRootNodes(descriptor.nodes);
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
            _metadataRead = ReadRangeAsync(_path, 0, (int)bytes);
        }

        public bool IsNodeResident(int nodeIndex) =>
            nodeIndex >= 0 && nodeIndex < _nodeResident.Length && _nodeResident[nodeIndex] != 0;

            // GPU request readback yields node ids; one node may own several pages.
        public void RequestNode(int nodeIndex, uint priority = 0u)
        {
            if (_disposed || Failed || nodeIndex < 0 || nodeIndex >= NodeCount)
                return;
            MarkNodeUsed(nodeIndex);
            int[] pages = _nodePages[nodeIndex];
            for (int i = 0; i < pages.Length; i++)
            {
                int page = pages[i];
                if (_pageSlots[page] < 0)
                    QueuePage(page, priority != 0u ? priority : 0x3f800000u);
            }
            if (ClusterMeshStreaming.EnablePrefetch)
            {
                uint prefetchPriority = priority > 0x01000000u ? priority - 0x01000000u : 1u;
                int[] children = _nodeChildren[nodeIndex];
                for (int childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    int[] childPages = _nodePages[children[childIndex]];
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
                if (priority > existing.priority) existing.priority = priority;
                return;
            }
            _requests.Add(page, new PageRequest { priority = priority, firstTick = _tick });
        }

        public void MarkNodeUsed(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= NodeCount)
                return;
            int[] pages = _nodePages[nodeIndex];
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
            _tick++;
            if (!_metadataReady)
            {
                PollMetadata();
                return;
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
                if (_pageSlots[id] >= 0 || _reads.ContainsKey(id) || _completed.ContainsKey(id))
                    continue;
                _scratch.Add(id);
            }
            _scratch.Sort(CompareRequestedPages);
            for (int i = 0; i < _scratch.Count && readSlots > 0; i++)
            {
                int id = _scratch[i];
                ClusterMeshStreamPage page = _descriptor.pages[id];
                if (_loadingNode >= 0 && !IsRootPage(id) && page.nodeIndex != _loadingNode)
                    continue;
                if (page.compressedLength > readBytes || page.uncompressedLength > decodedBytes)
                    continue;
                _reads.Add(id, ReadPageAsync(_path, _descriptor.pages[id],
                    _geometry.ResolvedVertexStride, _descriptor.weightStride));
                readSlots--;
                readBytes -= page.compressedLength;
                decodedBytes -= page.uncompressedLength;
            }
        }

        int CompareRequestedPages(int a, int b)
        {
            PageRequest ra = _requests[a];
            PageRequest rb = _requests[b];
            ulong pa = (ulong)ra.priority + ((ulong)Mathf.Min(4095, _tick - ra.firstTick) << 20);
            ulong pb = (ulong)rb.priority + ((ulong)Mathf.Min(4095, _tick - rb.firstTick) << 20);
            int priority = pb.CompareTo(pa);
            return priority != 0 ? priority : ra.firstTick.CompareTo(rb.firstTick);
        }

        void CollectReads()
        {
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
            _scratch.Sort(CompareRequestedPages);
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
                Payload payload = _completed[id];
                try
                {
                    if (!UploadPage(id, payload, ref nodesChanged))
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
                payload.Dispose();
                _completed.Remove(id);
                _requests.Remove(id);
                uploadSlots--;
                uploadBytes -= bytes;
            }
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
            int vertexBase = slot * _descriptor.vertexPageCapacity;
            int indexBase = slot * _descriptor.indexPageCapacity;
            byte[] raw = payload.raw;
            int cursor = PayloadHeaderBytes;
            if (_geometry.ResolvedVertexStride == ClusterMeshLimits.TightVertexStride)
            {
                CopyBytesToStructs(raw, cursor, _tightVertexStaging, page.vertexCount);
                if (page.vertexCount > 0) VerticesTight.SetData(_tightVertexStaging, 0, vertexBase, page.vertexCount);
            }
            else
            {
                CopyBytesToStructs(raw, cursor, _vertexStaging, page.vertexCount);
                if (page.vertexCount > 0) Vertices.SetData(_vertexStaging, 0, vertexBase, page.vertexCount);
            }
            cursor += page.vertexCount * _geometry.ResolvedVertexStride;
            if (page.indexWordCount > 0)
            {
                Buffer.BlockCopy(raw, cursor, _indexStaging, 0, page.indexWordCount * 4);
                Indices.SetData(_indexStaging, 0, indexBase, page.indexWordCount);
            }
            cursor += page.indexWordCount * 4;
            if (_descriptor.weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride)
            {
                CopyBytesToStructs(raw, cursor, _weightStaging, page.weightCount);
                if (page.weightCount > 0) Weights.SetData(_weightStaging, 0, vertexBase, page.weightCount);
            }
            else if (_descriptor.weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride8)
            {
                int count = page.weightCount * 2;
                if (count > 0)
                {
                    Buffer.BlockCopy(raw, cursor, _weight8Staging, 0, count * 4);
                    Weights8.SetData(_weight8Staging, 0, vertexBase * 2, count);
                }
            }

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
            int[] pages = _nodePages[node];
            bool all = pages.Length > 0;
            for (int i = 0; i < pages.Length; i++)
            {
                if (_pageSlots[pages[i]] < 0) { all = false; break; }
            }
            _nodeResident[node] = all ? 1u : 0u;
        }

        static int[][] BuildNodePages(int nodeCount, ClusterMeshStreamPage[] pages)
        {
            var counts = new int[nodeCount];
            for (int i = 0; i < pages.Length; i++)
                counts[pages[i].nodeIndex]++;
            var result = new int[nodeCount][];
            for (int i = 0; i < nodeCount; i++)
                result[i] = new int[counts[i]];
            Array.Clear(counts, 0, counts.Length);
            for (int page = 0; page < pages.Length; page++)
            {
                int node = pages[page].nodeIndex;
                result[node][counts[node]++] = page;
            }
            return result;
        }

        static int[][] BuildNodeChildren(ClusterMeshStreamNode[] nodes)
        {
            var counts = new int[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].parentNodeIndex >= 0) counts[nodes[i].parentNodeIndex]++;
            var result = new int[nodes.Length][];
            for (int i = 0; i < nodes.Length; i++) result[i] = new int[counts[i]];
            Array.Clear(counts, 0, counts.Length);
            for (int i = 0; i < nodes.Length; i++)
            {
                int parent = nodes[i].parentNodeIndex;
                if (parent >= 0) result[parent][counts[parent]++] = i;
            }
            return result;
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

        int CountRootPages()
        {
            int count = 0;
            for (int i = 0; i < PageCount; i++) if (IsRootPage(i)) count++;
            return count;
        }

        int CountMaxDetailNodePages()
        {
            var counts = new int[NodeCount];
            for (int page = 0; page < PageCount; page++)
            {
                int node = _descriptor.pages[page].nodeIndex;
                if (_descriptor.nodes[node].parentNodeIndex >= 0)
                    counts[node]++;
            }
            int maximum = 0;
            for (int node = 0; node < counts.Length; node++)
                maximum = Mathf.Max(maximum, counts[node]);
            return maximum;
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

        bool ValidateDescriptor()
        {
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
                bool hasPage = false;
                ClusterMeshStreamNode node = _descriptor.nodes[nodeIndex];
                for (int page = 0; page < PageCount; page++)
                    hasPage |= _descriptor.pages[page].nodeIndex == nodeIndex;
                if (!hasPage)
                    return FailInvalid("ClusterMesh streaming node " + nodeIndex + " has no pages.");
                int end = node.clusterStart + node.clusterCount;
                for (int cluster = node.clusterStart; cluster < end; cluster++)
                {
                    uint pageId = _descriptor.addresses[cluster].pageId;
                    if (_descriptor.pages[pageId].nodeIndex != nodeIndex)
                        return FailInvalid("ClusterMesh streaming cluster address crosses node ownership.");
                }
            }
            int rootPageCount = 0;
            for (int page = 0; page < PageCount; page++)
                if (IsRootPage(page)) rootPageCount++;
            for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
            {
                if (_descriptor.nodes[nodeIndex].parentNodeIndex < 0)
                    continue;
                int nodePages = 0;
                for (int page = 0; page < PageCount; page++)
                    if (_descriptor.pages[page].nodeIndex == nodeIndex) nodePages++;
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

        static async Task<Payload> ReadPageAsync(string path, ClusterMeshStreamPage page, int vertexStride, int weightStride)
        {
            byte[] packed = ArrayPool<byte>.Shared.Rent(page.compressedLength);
            byte[] raw = null;
            try
            {
                await ReadRangeIntoAsync(path, page.fileOffset, packed, page.compressedLength).ConfigureAwait(false);
                if (page.compressedLength == page.uncompressedLength)
                {
                    raw = packed;
                    packed = null;
                }
                else
                {
                    raw = ArrayPool<byte>.Shared.Rent(page.uncompressedLength);
                    using (var input = new MemoryStream(packed, 0, page.compressedLength, false, true))
                    using (var zip = new DeflateStream(input, CompressionMode.Decompress))
                    {
                        int cursor = 0;
                        while (cursor < page.uncompressedLength)
                        {
                            int read = zip.Read(raw, cursor, page.uncompressedLength - cursor);
                            if (read <= 0) break;
                            cursor += read;
                        }
                        if (cursor != page.uncompressedLength || zip.ReadByte() != -1)
                            throw new InvalidDataException("ClusterMesh stream page decompressed length is invalid.");
                    }
                }
                if (Crc32(raw, page.uncompressedLength) != page.crc32)
                    throw new InvalidDataException("ClusterMesh stream page checksum is invalid.");
                if (ReadI32(raw, 0) != page.vertexCount || ReadI32(raw, 4) != page.indexWordCount ||
                    ReadI32(raw, 8) != page.weightCount)
                    throw new InvalidDataException("ClusterMesh stream page payload counts do not match.");
                long expected = PayloadHeaderBytes + (long)page.vertexCount * vertexStride +
                    (long)page.indexWordCount * 4 + (long)page.weightCount * weightStride;
                if (expected != page.uncompressedLength)
                    throw new InvalidDataException("ClusterMesh stream page payload layout is invalid.");
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

        static async Task ReadRangeIntoAsync(string path, long offset, byte[] result, int length)
        {
            if (offset < 0 || length < 0 || result == null || result.Length < length)
                throw new IOException("ClusterMesh stream range is invalid.");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536,
                FileOptions.Asynchronous | FileOptions.RandomAccess))
            {
                if (offset > stream.Length - length) throw new EndOfStreamException("ClusterMesh stream range exceeds file.");
                stream.Seek(offset, SeekOrigin.Begin);
                int cursor = 0;
                while (cursor < length)
                {
                    int read = await stream.ReadAsync(result, cursor, length - cursor).ConfigureAwait(false);
                    if (read <= 0) throw new EndOfStreamException("ClusterMesh stream ended unexpectedly.");
                    cursor += read;
                }
            }
        }

        static async Task<byte[]> ReadRangeAsync(string path, long offset, int length)
        {
            if (offset < 0 || length < 0) throw new IOException("ClusterMesh stream range is invalid.");
            var result = new byte[length];
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536,
                FileOptions.Asynchronous | FileOptions.RandomAccess))
            {
                if (offset > stream.Length - length) throw new EndOfStreamException("ClusterMesh stream range exceeds file.");
                stream.Seek(offset, SeekOrigin.Begin);
                int cursor = 0;
                while (cursor < length)
                {
                    int read = await stream.ReadAsync(result, cursor, length - cursor).ConfigureAwait(false);
                    if (read <= 0) throw new EndOfStreamException("ClusterMesh stream ended unexpectedly.");
                    cursor += read;
                }
            }
            return result;
        }

        static void CopyBytesToStructs<T>(byte[] bytes, int sourceOffset, T[] destination, int count) where T : struct
        {
            if (count == 0) return;
            int stride = Marshal.SizeOf<T>();
            int byteCount = checked(count * stride);
            if (bytes == null || destination == null || destination.Length < count ||
                sourceOffset < 0 || sourceOffset > bytes.Length - byteCount)
                throw new InvalidDataException("ClusterMesh structured page data is invalid.");
            GCHandle h = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try { Marshal.Copy(bytes, sourceOffset, h.AddrOfPinnedObject(), byteCount); }
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
        void Fail(string text)
        {
            if (Failed) return;
            Failed = true;
            IsReady = false;
            Error = text;
            ReleasePendingPayloads();
            ReleaseGpuBuffers();
            Debug.LogError("ClusterMesh page streaming: " + text);
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsReady = false;
            _requests.Clear();
            ReleasePendingPayloads();
            ReleaseGpuBuffers();
        }
    }

    public static class ClusterMeshStreaming
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
        static readonly List<ClusterMeshSharedGpuPool> SharedPools = new List<ClusterMeshSharedGpuPool>();
        static int _lastUpdateFrame = int.MinValue;
        static int _roundRobinStart;
        static int _updateSerial;

        public static int MaxConcurrentReads { get; set; } = 8;
        public static int MaxUploadsPerUpdate { get; set; } = 4;
        public static long MaxReadBytesPerUpdate { get; set; } = 8L * 1024L * 1024L;
        public static long MaxUploadBytesPerUpdate { get; set; } = 8L * 1024L * 1024L;
        public static long MaxDecodedBytes { get; set; } = 32L * 1024L * 1024L;
        public static int ResidentGraceUpdates { get; set; } = 12;
        public static bool EnablePrefetch { get; set; } = true;
        public static int SharedGpuPoolCount => SharedPools.Count;
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

        internal static ClusterMeshSharedGpuPool AcquireSharedPool(
            ClusterMeshPageRuntime owner, int vertexStride,
            ClusterMeshStreamDescriptor descriptor, int rootPages, int detailHeadroom)
        {
            if (owner == null || descriptor == null || rootPages <= 0) return null;
            ClusterMeshSettings settings = ClusterMeshSettings.OverrideForTests ?? ClusterMeshSettings.Loaded;
            int requestedCapacity = settings != null ? settings.SharedGpuPoolPageCapacity : 256;
            int capacity = Mathf.NextPowerOfTwo(Mathf.Max(descriptor.poolPageCapacity, requestedCapacity));
            capacity = Mathf.Clamp(capacity, 8, 4096);
            var key = new ClusterMeshSharedPoolKey
            {
                vertexStride = vertexStride,
                weightStride = descriptor.weightStride,
                vertexPageCapacity = descriptor.vertexPageCapacity,
                indexPageCapacity = descriptor.indexPageCapacity
            };
            for (int i = 0; i < SharedPools.Count; i++)
            {
                ClusterMeshSharedGpuPool candidate = SharedPools[i];
                if (!candidate.Key.Equals(key) || candidate.Capacity != capacity ||
                    !candidate.CanReserve(rootPages, detailHeadroom))
                    continue;
                candidate.Acquire(owner, rootPages, detailHeadroom);
                return candidate;
            }
            var pool = new ClusterMeshSharedGpuPool(key, capacity);
            pool.Acquire(owner, rootPages, detailHeadroom);
            SharedPools.Add(pool);
            return pool;
        }

        internal static void ReleaseSharedPool(
            ClusterMeshSharedGpuPool pool, ClusterMeshPageRuntime owner)
        {
            if (pool == null) return;
            pool.Release(owner);
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
                for (int i = 0; i < Active.Count; i++)
                    Active[(start + i) % Active.Count].Update(
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
            Runtimes.Clear(); References.Clear(); Stale.Clear(); Active.Clear(); _lastUpdateFrame = int.MinValue;
            _roundRobinStart = 0;
            _updateSerial = 0;
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
