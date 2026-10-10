using System;

namespace ClusterMesh
{
    // Compressed adjacency: two arrays per table, rather than one object per node.
    // Built only from validated node/page IDs. Ranges borrow immutable table data.
    internal sealed class ClusterMeshNodeIndexTable
    {
        readonly int[] offsets;
        readonly int[] items;

        internal readonly struct Range
        {
            readonly int[] items;
            readonly int start;
            public int Length { get; }
            internal Range(int[] items, int start, int length)
            { this.items = items; this.start = start; Length = length; }
            public int this[int index] => items[start + index];
        }

        ClusterMeshNodeIndexTable(int[] offsets, int[] items)
        { this.offsets = offsets; this.items = items; }

        internal Range this[int node] => new Range(items, offsets[node], offsets[node + 1] - offsets[node]);

        // Counts are indexed by node+1, allowing in-place prefix sums and reuse
        // of the validation counts as the final offsets (no temporary cursor array).
        static void Prefix(int[] offsets)
        {
            for (int i = 1; i < offsets.Length; i++) offsets[i] += offsets[i - 1];
        }
        static void RestoreStarts(int[] offsets)
        {
            for (int i = offsets.Length - 1; i > 0; i--) offsets[i] = offsets[i - 1];
            offsets[0] = 0;
        }
        internal static ClusterMeshNodeIndexTable Pages(int[] validatedCounts, ClusterMeshStreamPage[] pages)
        {
            Prefix(validatedCounts);
            var items = new int[pages.Length];
            for (int i = 0; i < pages.Length; i++) items[validatedCounts[pages[i].nodeIndex]++] = i;
            RestoreStarts(validatedCounts);
            return new ClusterMeshNodeIndexTable(validatedCounts, items);
        }
        internal static ClusterMeshNodeIndexTable Children(ClusterMeshStreamNode[] nodes)
        {
            var offsets = new int[nodes.Length + 1];
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].parentNodeIndex >= 0) offsets[nodes[i].parentNodeIndex + 1]++;
            Prefix(offsets);
            var items = new int[offsets[nodes.Length]];
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].parentNodeIndex >= 0) items[offsets[nodes[i].parentNodeIndex]++] = i;
            RestoreStarts(offsets);
            return new ClusterMeshNodeIndexTable(offsets, items);
        }
    }
}
