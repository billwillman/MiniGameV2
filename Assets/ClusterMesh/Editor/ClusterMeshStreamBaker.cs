#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    /// <summary>
    /// Builds the optional page-streamed sidecar. The legacy embedded fields are
    /// changed only after a complete, verified sidecar has been atomically written.
    /// </summary>
    public static class ClusterMeshStreamBaker
    {
        const int FileHeaderBytes = 16;
        const int DirectoryEntryBytes = 40;
        const int PayloadHeaderBytes = 12;
        const int TargetVerticesPerPage = 4096;
        const int TargetIndexWordsPerPage = 8192;

        sealed class NodeBuild
        {
            public readonly List<int> clusters = new List<int>(4);
            public int parentNode = -1;
            public int materialIndex;
            public float lodError;
            public int lodLevel;
            public Vector4 center;
            public Vector4 extents;
            public int sourceGroup = -1;
        }

        sealed class PageBuild
        {
            public byte[] compressed;
            public int uncompressedLength;
            public int vertexCount;
            public int indexWordCount;
            public int weightCount;
            public uint crc32;
            public int flags;
            public int nodeIndex;
        }

        sealed class BuildResult
        {
            public ClusterHeader[] clusters;
            public ClusterGroup[] groups;
            public ClusterMeshStreamNode[] nodes;
            public ClusterMeshStreamPage[] pages;
            public ClusterMeshStreamAddress[] addresses;
            public PageBuild[] pageData;
            public int[] oldToNew;
            public int vertexPageCapacity;
            public int indexPageCapacity;
            public int weightStride;
        }

        public static void FinalizeStatic(
            ClusterMeshAsset asset, string assetPath, ClusterMeshBakeSettings settings)
        {
            if (asset == null || settings == null || !settings.enableStreaming)
                return;
            BuildResult result = Build(asset, null, ClusterMeshStreamKind.Static);
            ClusterMeshStreamDescriptor descriptor = WriteSidecar(assetPath, settings, result, ClusterMeshStreamKind.Static);
            ApplyGeometry(asset, result, descriptor);
        }

        public static void FinalizeSkinned(
            ClusterSkinnedMeshAsset asset, string assetPath, ClusterMeshBakeSettings settings)
        {
            if (asset == null || asset.geometry == null || settings == null || !settings.enableStreaming)
                return;
            if (!asset.TryGetCullFrames(out ClusterSkinnedCullFrame[] oldCullFrames, out string cullError))
                throw new InvalidOperationException(cullError ?? "Skinned ClusterMesh cull frames are missing.");

            BuildResult result = Build(asset.geometry, asset, ClusterMeshStreamKind.Skinned);
            ClusterSkinnedCullFrame[] reorderedCull = ReorderCullFrames(
                asset, oldCullFrames, result.oldToNew, result.clusters.Length);
            ClusterMeshStreamDescriptor descriptor = WriteSidecar(assetPath, settings, result, ClusterMeshStreamKind.Skinned);

            // Nothing above this line mutates the asset. A failed write therefore
            // leaves the old embedded data fully usable.
            ApplyGeometry(asset.geometry, result, descriptor);
            asset.packedSkinWeights = Array.Empty<byte>();
            if (asset.cullFramesCompressed)
            {
                asset.cullFrames = Array.Empty<ClusterSkinnedCullFrame>();
                asset.packedCullFrames = ClusterSkinnedMeshBaker.PackCullFrames(reorderedCull);
            }
            else
            {
                asset.cullFrames = reorderedCull;
                asset.packedCullFrames = null;
            }
            EditorUtility.SetDirty(asset);
        }

        static void ApplyGeometry(
            ClusterMeshAsset asset, BuildResult result, ClusterMeshStreamDescriptor descriptor)
        {
            asset.clusters = result.clusters;
            asset.groups = result.groups;
            asset.packedVertices = Array.Empty<byte>();
            asset.packedIndices = Array.Empty<byte>();
            asset.streamDescriptor = descriptor;
            EditorUtility.SetDirty(asset);
        }

        static BuildResult Build(
            ClusterMeshAsset geometry, ClusterSkinnedMeshAsset skinned, ClusterMeshStreamKind kind)
        {
            if (geometry == null || geometry.clusters == null || geometry.clusters.Length == 0)
                throw new InvalidOperationException("ClusterMesh streaming requires baked clusters.");
            if (geometry.hierarchyVersion < ClusterMeshLod.HierarchyVersionDag ||
                geometry.groups == null || geometry.groups.Length == 0)
                throw new InvalidOperationException(
                    "Page Streaming requires a baked LOD hierarchy so a missing page can fall back to a resident parent.");

            int vertexStride = geometry.ResolvedVertexStride;
            byte[] allVertices;
            uint[] allIndexWords;
            if (vertexStride == ClusterMeshLimits.TightVertexStride)
            {
                bool verticesOk = ClusterMeshGeometry.TryReadTightVertices(
                    geometry, out ClusterPackedVertexTight[] tight, out string vertexError);
                bool indicesOk = ClusterMeshGeometry.TryReadPackedIndices(
                    geometry, out allIndexWords, out string indexError);
                if (!verticesOk || !indicesOk)
                    throw new InvalidDataException(vertexError ?? indexError ?? "Unable to decode tight ClusterMesh geometry.");
                allVertices = StructsToBytes(tight);
            }
            else
            {
                if (!ClusterMeshGeometry.TryReadGpuGeometry(
                        geometry, out ClusterPackedVertex[] vertices, out allIndexWords, out string geometryError))
                    throw new InvalidDataException(geometryError ?? "Unable to decode ClusterMesh geometry.");
                allVertices = StructsToBytes(vertices);
            }

            int weightStride = 0;
            byte[] allWeights = Array.Empty<byte>();
            if (skinned != null)
            {
                weightStride = skinned.ResolvedSkinWeightStride;
                if (weightStride == ClusterSkinnedMeshAsset.PackedSkinWeightStride8)
                {
                    if (!skinned.TryReadSkinWeights8(out ClusterPackedSkinWeight8[] weights8, out string weightError))
                        throw new InvalidDataException(weightError ?? "Unable to decode 8-byte skin weights.");
                    allWeights = StructsToBytes(weights8);
                }
                else
                {
                    if (!skinned.TryReadSkinWeights(out ClusterPackedSkinWeight[] weights, out string weightError))
                        throw new InvalidDataException(weightError ?? "Unable to decode skin weights.");
                    allWeights = StructsToBytes(weights);
                }
            }

            List<NodeBuild> nodeBuilds = BuildNodes(geometry.clusters, geometry.groups);
            var oldToNew = new int[geometry.clusters.Length];
            for (int i = 0; i < oldToNew.Length; i++)
                oldToNew[i] = -1;
            int nextCluster = 0;
            for (int node = 0; node < nodeBuilds.Count; node++)
            {
                for (int i = 0; i < nodeBuilds[node].clusters.Count; i++)
                {
                    int oldCluster = nodeBuilds[node].clusters[i];
                    if (oldCluster < 0 || oldCluster >= oldToNew.Length || oldToNew[oldCluster] >= 0)
                        throw new InvalidDataException("Cluster streaming nodes overlap or contain an invalid cluster.");
                    oldToNew[oldCluster] = nextCluster++;
                }
            }
            if (nextCluster != geometry.clusters.Length)
                throw new InvalidDataException("Cluster streaming nodes do not cover every cluster.");

            var reordered = new ClusterHeader[geometry.clusters.Length];
            var addresses = new ClusterMeshStreamAddress[geometry.clusters.Length];
            var pageBuilds = new List<PageBuild>(nodeBuilds.Count);
            int vertexPageCapacity = 0;
            int indexPageCapacity = 0;
            for (int nodeIndex = 0; nodeIndex < nodeBuilds.Count; nodeIndex++)
            {
                NodeBuild node = nodeBuilds[nodeIndex];
                var pageVertices = new MemoryStream();
                var logicalIndices = new List<ushort>();
                var pageWeights = new MemoryStream();
                int pageVertexCount = 0;
                for (int i = 0; i < node.clusters.Count; i++)
                {
                    int oldClusterIndex = node.clusters[i];
                    int newClusterIndex = oldToNew[oldClusterIndex];
                    ClusterHeader source = geometry.clusters[oldClusterIndex];
                    ValidateCluster(source, geometry.vertexCount, geometry.indexCount);

                    int sourceVertexCount = checked((int)source.vertexCount);
                    int sourceLogicalIndexCount = checked((int)source.triangleCount * 3);
                    int combinedIndexWords = checked((logicalIndices.Count + sourceLogicalIndexCount + 1) >> 1);
                    if (pageVertexCount > 0 &&
                        (pageVertexCount + sourceVertexCount > TargetVerticesPerPage ||
                         combinedIndexWords > TargetIndexWordsPerPage))
                    {
                        AddPage(pageBuilds, nodeIndex, node.parentNode < 0,
                            pageVertices, logicalIndices, pageWeights, pageVertexCount,
                            weightStride, ref vertexPageCapacity, ref indexPageCapacity);
                        pageVertices.SetLength(0);
                        pageWeights.SetLength(0);
                        logicalIndices.Clear();
                        pageVertexCount = 0;
                    }

                    addresses[newClusterIndex] = new ClusterMeshStreamAddress
                    {
                        pageId = (uint)pageBuilds.Count,
                        vertexOffset = (uint)pageVertexCount,
                        indexOffset = (uint)logicalIndices.Count,
                        reserved = 0u
                    };

                    int vertexCount = sourceVertexCount;
                    int sourceVertexByte = checked((int)source.vertexOffset * vertexStride);
                    int vertexBytes = checked(vertexCount * vertexStride);
                    pageVertices.Write(allVertices, sourceVertexByte, vertexBytes);
                    if (weightStride > 0)
                    {
                        int sourceWeightByte = checked((int)source.vertexOffset * weightStride);
                        pageWeights.Write(allWeights, sourceWeightByte, checked(vertexCount * weightStride));
                    }

                    int logicalIndexCount = sourceLogicalIndexCount;
                    for (int index = 0; index < logicalIndexCount; index++)
                    {
                        ushort local = ReadLogicalIndex(allIndexWords, checked((int)source.indexOffset + index));
                        if (local >= source.vertexCount)
                            throw new InvalidDataException("Cluster page contains a non-local vertex index.");
                        logicalIndices.Add(local);
                    }

                    ClusterHeader header = source;
                    header.vertexOffset = addresses[newClusterIndex].vertexOffset;
                    header.indexOffset = addresses[newClusterIndex].indexOffset;
                    // hierarchyVersion 2 stores a ClusterGroup index here.
                    header.parentIndex = source.parentIndex;
                    reordered[newClusterIndex] = header;
                    pageVertexCount += vertexCount;
                }
                AddPage(pageBuilds, nodeIndex, node.parentNode < 0,
                    pageVertices, logicalIndices, pageWeights, pageVertexCount,
                    weightStride, ref vertexPageCapacity, ref indexPageCapacity);
                pageVertices.Dispose();
                pageWeights.Dispose();
            }

            var groups = (ClusterGroup[])geometry.groups.Clone();
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                NodeBuild node = nodeBuilds[groupIndex];
                groups[groupIndex].clusterStart = oldToNew[node.clusters[0]];
                groups[groupIndex].clusterCount = node.clusters.Count;
            }

            var nodes = new ClusterMeshStreamNode[nodeBuilds.Count];
            for (int i = 0; i < nodeBuilds.Count; i++)
            {
                NodeBuild node = nodeBuilds[i];
                nodes[i] = new ClusterMeshStreamNode
                {
                    clusterStart = oldToNew[node.clusters[0]],
                    clusterCount = node.clusters.Count,
                    parentNodeIndex = node.parentNode,
                    materialIndex = node.materialIndex,
                    lodError = node.lodError,
                    lodLevel = node.lodLevel,
                    flags = 0u,
                    padding = 0u,
                    aabbCenter = node.center,
                    aabbExtents = node.extents
                };
            }

            return new BuildResult
            {
                clusters = reordered,
                groups = groups,
                nodes = nodes,
                pages = new ClusterMeshStreamPage[pageBuilds.Count],
                addresses = addresses,
                pageData = pageBuilds.ToArray(),
                oldToNew = oldToNew,
                vertexPageCapacity = Mathf.Max(1, vertexPageCapacity),
                indexPageCapacity = Mathf.Max(1, indexPageCapacity),
                weightStride = weightStride
            };
        }

        static void AddPage(
            List<PageBuild> pages,
            int nodeIndex,
            bool root,
            MemoryStream pageVertices,
            List<ushort> logicalIndices,
            MemoryStream pageWeights,
            int pageVertexCount,
            int weightStride,
            ref int vertexPageCapacity,
            ref int indexPageCapacity)
        {
            if (pageVertexCount <= 0)
                throw new InvalidDataException("Cluster streaming node produced an empty page.");
            uint[] pageIndexWords = PackLogicalIndices(logicalIndices);
            byte[] raw = BuildPayload(
                pageVertexCount, pageIndexWords, weightStride,
                pageVertices.ToArray(), pageWeights.ToArray());
            pages.Add(new PageBuild
            {
                compressed = Deflate(raw),
                uncompressedLength = raw.Length,
                vertexCount = pageVertexCount,
                indexWordCount = pageIndexWords.Length,
                weightCount = weightStride > 0 ? pageVertexCount : 0,
                crc32 = Crc32(raw),
                flags = root ? ClusterMeshStreamPageFlags.Root : 0,
                nodeIndex = nodeIndex
            });
            vertexPageCapacity = Mathf.Max(vertexPageCapacity, pageVertexCount);
            indexPageCapacity = Mathf.Max(indexPageCapacity, pageIndexWords.Length);
        }

        static List<NodeBuild> BuildNodes(ClusterHeader[] clusters, ClusterGroup[] groups)
        {
            var result = new List<NodeBuild>(groups.Length + clusters.Length);
            var owned = new bool[clusters.Length];
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                ClusterGroup group = groups[groupIndex];
                if (group.clusterStart < 0 || group.clusterCount <= 0 ||
                    group.clusterStart > clusters.Length - group.clusterCount)
                    throw new InvalidDataException("Cluster LOD group range is invalid.");
                int material = (int)clusters[group.clusterStart].materialIndex;
                var node = new NodeBuild
                {
                    parentNode = group.parentGroupIndex,
                    materialIndex = material,
                    lodError = group.lodError,
                    lodLevel = ClusterMeshLod.Level(clusters[group.clusterStart].flags),
                    center = group.aabbCenter,
                    extents = group.aabbExtents,
                    sourceGroup = groupIndex
                };
                for (int c = 0; c < group.clusterCount; c++)
                {
                    int cluster = group.clusterStart + c;
                    if (owned[cluster])
                        throw new InvalidDataException("Cluster LOD group ranges overlap.");
                    if ((int)clusters[cluster].materialIndex != material)
                        throw new InvalidDataException("A streamed LOD node cannot contain multiple materials.");
                    owned[cluster] = true;
                    node.clusters.Add(cluster);
                }
                result.Add(node);
            }

            var leafNodes = new Dictionary<long, NodeBuild>();
            for (int cluster = 0; cluster < clusters.Length; cluster++)
            {
                if (owned[cluster])
                    continue;
                ClusterHeader header = clusters[cluster];
                int parentNode = header.parentIndex >= 0 && header.parentIndex < groups.Length
                    ? header.parentIndex : -1;
                int material = (int)header.materialIndex;
                long key = ((long)(parentNode + 1) << 32) | (uint)material;
                if (!leafNodes.TryGetValue(key, out NodeBuild node))
                {
                    node = new NodeBuild
                    {
                        parentNode = parentNode,
                        materialIndex = material,
                        lodError = header.lodError,
                        lodLevel = ClusterMeshLod.Level(header.flags)
                    };
                    leafNodes.Add(key, node);
                    result.Add(node);
                }
                node.clusters.Add(cluster);
                Encapsulate(ref node.center, ref node.extents, header.aabbCenter, header.aabbExtents,
                    node.clusters.Count == 1);
                node.lodError = Mathf.Max(node.lodError, header.lodError);
            }

            for (int i = 0; i < result.Count; i++)
            {
                NodeBuild node = result[i];
                if (node.clusters.Count == 0 || node.parentNode < -1 || node.parentNode >= result.Count || node.parentNode == i)
                    throw new InvalidDataException("Cluster streaming node hierarchy is invalid.");
            }
            return result;
        }

        static void Encapsulate(
            ref Vector4 center, ref Vector4 extents, Vector4 addCenter, Vector4 addExtents, bool first)
        {
            Vector3 c = addCenter;
            Vector3 e = addExtents;
            if (first)
            {
                center = new Vector4(c.x, c.y, c.z, 0f);
                extents = new Vector4(e.x, e.y, e.z, 0f);
                return;
            }
            Vector3 oldC = center;
            Vector3 oldE = extents;
            Vector3 min = Vector3.Min(oldC - oldE, c - e);
            Vector3 max = Vector3.Max(oldC + oldE, c + e);
            Vector3 newC = (min + max) * 0.5f;
            Vector3 newE = (max - min) * 0.5f;
            center = new Vector4(newC.x, newC.y, newC.z, 0f);
            extents = new Vector4(newE.x, newE.y, newE.z, 0f);
        }

        static void ValidateCluster(ClusterHeader header, int vertexCount, int indexCount)
        {
            long vertexEnd = (long)header.vertexOffset + header.vertexCount;
            long logicalIndexEnd = (long)header.indexOffset + (long)header.triangleCount * 3L;
            if (header.vertexOffset > int.MaxValue || vertexEnd > vertexCount ||
                header.indexOffset > int.MaxValue || logicalIndexEnd > indexCount)
                throw new InvalidDataException("Cluster geometry range lies outside the packed source.");
        }

        static ushort ReadLogicalIndex(uint[] words, int logicalIndex)
        {
            if (words == null || logicalIndex < 0 || (logicalIndex >> 1) >= words.Length)
                throw new InvalidDataException("Packed cluster index is out of range.");
            uint word = words[logicalIndex >> 1];
            return (ushort)(((logicalIndex & 1) == 0) ? word & 0xffffu : word >> 16);
        }

        static uint[] PackLogicalIndices(List<ushort> indices)
        {
            var result = new uint[(indices.Count + 1) >> 1];
            for (int i = 0; i < indices.Count; i++)
            {
                if ((i & 1) == 0)
                    result[i >> 1] = indices[i];
                else
                    result[i >> 1] |= (uint)indices[i] << 16;
            }
            return result;
        }

        static byte[] BuildPayload(
            int vertexCount, uint[] indexWords, int weightStride, byte[] vertices, byte[] weights)
        {
            int weightCount = weightStride > 0 ? vertexCount : 0;
            using (var memory = new MemoryStream(PayloadHeaderBytes + vertices.Length + indexWords.Length * 4 + weights.Length))
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(vertexCount);
                writer.Write(indexWords.Length);
                writer.Write(weightCount);
                writer.Write(vertices);
                for (int i = 0; i < indexWords.Length; i++)
                    writer.Write(indexWords[i]);
                writer.Write(weights);
                writer.Flush();
                return memory.ToArray();
            }
        }

        static byte[] Deflate(byte[] raw)
        {
            using (var output = new MemoryStream())
            {
                using (var zip = new DeflateStream(
                    output, System.IO.Compression.CompressionLevel.Fastest, true))
                    zip.Write(raw, 0, raw.Length);
                byte[] compressed = output.ToArray();
                return compressed.Length < raw.Length ? compressed : raw;
            }
        }

        static ClusterMeshStreamDescriptor WriteSidecar(
            string assetPath, ClusterMeshBakeSettings settings, BuildResult result, ClusterMeshStreamKind kind)
        {
            string sidecarAssetPath = SidecarAssetPath(assetPath);
            string fullPath = Path.GetFullPath(sidecarAssetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("ClusterMesh stream sidecar has no output directory.");
            Directory.CreateDirectory(directory);
            string tempPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                int rootCount = 0;
                var pagesPerNode = new int[result.nodes.Length];
                for (int i = 0; i < result.pageData.Length; i++)
                {
                    PageBuild page = result.pageData[i];
                    pagesPerNode[page.nodeIndex]++;
                    if ((page.flags & ClusterMeshStreamPageFlags.Root) != 0) rootCount++;
                }
                int largestNonRootNode = 0;
                for (int node = 0; node < result.nodes.Length; node++)
                    if (result.nodes[node].parentNodeIndex >= 0)
                        largestNonRootNode = Mathf.Max(largestNonRootNode, pagesPerNode[node]);
                int requiredCapacity = Mathf.Min(
                    result.pageData.Length, checked(rootCount + largestNonRootNode));
                int poolCapacity = Mathf.Max(requiredCapacity,
                    Mathf.Min(Mathf.Clamp(settings.streamingPagePoolCapacity, 8, 512), result.pageData.Length));
                var pages = new ClusterMeshStreamPage[result.pageData.Length];
                long dataOffset = FileHeaderBytes + (long)DirectoryEntryBytes * pages.Length;
                for (int i = 0; i < pages.Length; i++)
                {
                    PageBuild source = result.pageData[i];
                    pages[i] = new ClusterMeshStreamPage
                    {
                        fileOffset = dataOffset,
                        compressedLength = source.compressed.Length,
                        uncompressedLength = source.uncompressedLength,
                        vertexCount = source.vertexCount,
                        indexWordCount = source.indexWordCount,
                        weightCount = source.weightCount,
                        crc32 = source.crc32,
                        nodeIndex = source.nodeIndex,
                        flags = source.flags
                    };
                    dataOffset = checked(dataOffset + source.compressed.Length);
                }

                using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(file))
                {
                    writer.Write(ClusterMeshStreaming.FileMagic);
                    writer.Write(ClusterMeshStreaming.FormatVersion);
                    writer.Write((int)kind);
                    writer.Write(pages.Length);
                    for (int i = 0; i < pages.Length; i++)
                    {
                        ClusterMeshStreamPage page = pages[i];
                        writer.Write(page.fileOffset);
                        writer.Write(page.compressedLength);
                        writer.Write(page.uncompressedLength);
                        writer.Write(page.vertexCount);
                        writer.Write(page.indexWordCount);
                        writer.Write(page.weightCount);
                        writer.Write(page.crc32);
                        writer.Write(page.nodeIndex);
                        writer.Write(page.flags);
                    }
                    for (int i = 0; i < result.pageData.Length; i++)
                        writer.Write(result.pageData[i].compressed);
                    writer.Flush();
                    file.Flush(true);
                }
                ReplaceAtomic(tempPath, fullPath);

                string hash;
                long fileSize;
                using (var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (SHA256 sha = SHA256.Create())
                {
                    fileSize = file.Length;
                    hash = ToHex(sha.ComputeHash(file));
                }
                result.pages = pages;
                var descriptor = new ClusterMeshStreamDescriptor
                {
                    formatVersion = ClusterMeshStreaming.FormatVersion,
                    kind = kind,
                    fileName = Path.GetFileName(fullPath),
                    editorFilePath = sidecarAssetPath.Replace('\\', '/'),
                    contentHash = hash,
                    fileSize = fileSize,
                    poolPageCapacity = poolCapacity,
                    vertexPageCapacity = result.vertexPageCapacity,
                    indexPageCapacity = result.indexPageCapacity,
                    weightStride = result.weightStride,
                    nodes = result.nodes,
                    pages = pages,
                    addresses = result.addresses
                };
                if (!descriptor.IsValid)
                    throw new InvalidDataException("Generated ClusterMesh stream descriptor is invalid.");
                AssetDatabase.ImportAsset(sidecarAssetPath, ImportAssetOptions.ForceUpdate);
                return descriptor;
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        static ClusterSkinnedCullFrame[] ReorderCullFrames(
            ClusterSkinnedMeshAsset asset, ClusterSkinnedCullFrame[] source, int[] oldToNew, int clusterCount)
        {
            if (source == null || asset.clips == null)
                return Array.Empty<ClusterSkinnedCullFrame>();
            var result = new ClusterSkinnedCullFrame[source.Length];
            for (int clipIndex = 0; clipIndex < asset.clips.Length; clipIndex++)
            {
                ClusterSkinnedClip clip = asset.clips[clipIndex];
                if (clip == null)
                    throw new InvalidDataException("Skinned stream contains a missing clip.");
                for (int segment = 0; segment < clip.segmentCount; segment++)
                {
                    int frameOffset = checked(clip.cullFrameOffset + segment * clusterCount);
                    if (frameOffset < 0 || frameOffset > source.Length - clusterCount)
                        throw new InvalidDataException("Skinned cull frame range is invalid.");
                    for (int oldCluster = 0; oldCluster < clusterCount; oldCluster++)
                        result[frameOffset + oldToNew[oldCluster]] = source[frameOffset + oldCluster];
                }
            }
            return result;
        }

        static string SidecarAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentException("Asset path is required.", nameof(assetPath));
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            string suffix = string.IsNullOrEmpty(guid) ? Guid.NewGuid().ToString("N") : guid;
            string directory = Path.GetDirectoryName(assetPath) ?? "Assets";
            string name = Path.GetFileNameWithoutExtension(assetPath);
            return (directory + "/" + name + "_" + suffix + ".cmstream").Replace('\\', '/');
        }

        static void ReplaceAtomic(string tempPath, string finalPath)
        {
            if (File.Exists(finalPath))
                File.Replace(tempPath, finalPath, null);
            else
                File.Move(tempPath, finalPath);
        }

        static byte[] StructsToBytes<T>(T[] values) where T : struct
        {
            if (values == null || values.Length == 0)
                return Array.Empty<byte>();
            int length = checked(values.Length * Marshal.SizeOf<T>());
            var bytes = new byte[length];
            GCHandle handle = GCHandle.Alloc(values, GCHandleType.Pinned);
            try { Marshal.Copy(handle.AddrOfPinnedObject(), bytes, 0, length); }
            finally { handle.Free(); }
            return bytes;
        }

        static string ToHex(byte[] bytes)
        {
            char[] chars = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = hex[bytes[i] >> 4];
                chars[i * 2 + 1] = hex[bytes[i] & 15];
            }
            return new string(chars);
        }

        static uint Crc32(byte[] data)
        {
            uint crc = 0xffffffffu;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                    crc = (crc & 1u) != 0u ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
            }
            return ~crc;
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }
}
#endif
