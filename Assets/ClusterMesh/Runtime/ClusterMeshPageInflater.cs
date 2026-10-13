// Experimental opt-in only. Lower GC, but the Mono corpus CPU time regressed.
// See ClusterMeshSettings.ReuseDecompressionWarning before enabling.
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ClusterMesh
{
    // Raw DEFLATE (RFC 1951), for a complete, independently compressed page.
    // The caller already owns the final output array: back-references can read
    // it directly, so no additional 32 KiB sliding window is needed. All trees
    // and scratch arrays are bounded and reused, including dynamic blocks.
    internal sealed class ClusterMeshPageInflater
    {
        static readonly int[] LengthBase = { 3,4,5,6,7,8,9,10,11,13,15,17,19,23,27,31,35,43,51,59,67,83,99,115,131,163,195,227,258 };
        static readonly int[] LengthExtra = { 0,0,0,0,0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3,4,4,4,4,5,5,5,5,0 };
        static readonly int[] DistanceBase = { 1,2,3,4,5,7,9,13,17,25,33,49,65,97,129,193,257,385,513,769,1025,1537,2049,3073,4097,6145,8193,12289,16385,24577 };
        static readonly int[] DistanceExtra = { 0,0,0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11,12,12,13,13 };
        static readonly int[] CodeOrder = { 16,17,18,0,8,7,9,6,10,5,11,4,12,3,13,2,14,1,15 };
        static readonly Huffman FixedLiteral, FixedDistance;
        readonly Huffman literal = new Huffman(288, 9), distance = new Huffman(32, 6), codes = new Huffman(19, 7);
        readonly byte[] lengths = new byte[320];
        byte[] input, output;
        int at, end, bitCount, written, outputLength;
        uint bits;

        // Pool linkage is intrusive: acquiring/returning a warmed context does
        // not allocate a lease, list node, delegate or grow a collection.
        internal ClusterMeshPageInflater Next;
        internal int LeaseGeneration;
        internal bool Leased;
        internal const int WorkspacePayloadBytes = 320 + (288 + 32 + 19 + 512 + 64 + 128) * 2 + 3 * 4 * 16 * 4;

        static ClusterMeshPageInflater()
        {
            var size = new byte[288];
            for (int i = 0; i < size.Length; i++) size[i] = (byte)(i < 144 ? 8 : i < 256 ? 9 : i < 280 ? 7 : 8);
            FixedLiteral = new Huffman(288, 9); FixedLiteral.Build(size, 0, 288, false, false);
            for (int i = 0; i < 32; i++) size[i] = 5;
            FixedDistance = new Huffman(32, 6); FixedDistance.Build(size, 0, 32, false, false);
        }

        internal void Inflate(byte[] source, int offset, int length, byte[] destination, int expectedLength)
        {
            if (source == null || destination == null || offset < 0 || length < 0 || offset > source.Length - length ||
                expectedLength < 0 || expectedLength > destination.Length || ReferenceEquals(source, destination))
                throw new ArgumentException("Invalid ClusterMesh inflate range.");
            input = source; output = destination; at = offset; end = offset + length;
            outputLength = expectedLength; bits = 0; bitCount = written = 0;
            try
            {
                bool final;
                do
                {
                    final = ReadBits(1) != 0;
                    int type = ReadBits(2);
                    if (type == 0) StoredBlock();
                    else if (type == 1) CompressedBlock(FixedLiteral, FixedDistance);
                    else if (type == 2) { DynamicTrees(); CompressedBlock(literal, distance); }
                    else throw Invalid();
                } while (!final);
                if (written != expectedLength || end - at + bitCount / 8 != 0) throw Invalid();
            }
            finally
            {
                // A retained decoder must never retain borrowed page arrays.
                input = output = null; bits = 0; bitCount = at = end = written = outputLength = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FillBits(int count)
        {
            while (bitCount < count && at < end) { bits |= (uint)input[at++] << bitCount; bitCount += 8; }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ReadBits(int count)
        {
            if (count == 0) return 0;
            FillBits(count);
            if (bitCount < count) throw Invalid();
            int result = (int)(bits & ((1u << count) - 1));
            bits >>= count; bitCount -= count; return result;
        }
        void StoredBlock()
        {
            int padding = bitCount & 7; bits >>= padding; bitCount -= padding;
            int count = ReadBits(16), inverse = ReadBits(16);
            if ((count ^ inverse) != 65535 || count > outputLength - written) throw Invalid();
            while (count > 0 && bitCount >= 8) { output[written++] = (byte)ReadBits(8); count--; }
            if (count > end - at) throw Invalid();
            Buffer.BlockCopy(input, at, output, written, count); at += count; written += count;
        }
        void DynamicTrees()
        {
            int literals = ReadBits(5) + 257, distances = ReadBits(5) + 1, codeCount = ReadBits(4) + 4;
            if (literals > 286) throw Invalid();
            Array.Clear(lengths, 0, 19);
            for (int i = 0; i < codeCount; i++) lengths[CodeOrder[i]] = (byte)ReadBits(3);
            codes.Build(lengths, 0, 19, false, false);
            int count = literals + distances, cursor = 0;
            while (cursor < count)
            {
                int symbol = codes.Decode(this);
                if (symbol < 16) { lengths[cursor++] = (byte)symbol; continue; }
                int repeat, value;
                if (symbol == 16)
                {
                    if (cursor == 0) throw Invalid();
                    repeat = ReadBits(2) + 3; value = lengths[cursor - 1];
                }
                else if (symbol == 17) { repeat = ReadBits(3) + 3; value = 0; }
                else if (symbol == 18) { repeat = ReadBits(7) + 11; value = 0; }
                else throw Invalid();
                if (repeat > count - cursor) throw Invalid();
                for (int i = 0; i < repeat; i++) lengths[cursor++] = (byte)value;
            }
            if (lengths[256] == 0) throw Invalid();
            literal.Build(lengths, 0, literals, true, false);
            distance.Build(lengths, literals, distances, true, true);
        }
        void CompressedBlock(Huffman literals, Huffman distances)
        {
            for (;;)
            {
                int symbol = literals.Decode(this);
                if (symbol < 256)
                {
                    if (written == outputLength) throw Invalid();
                    output[written++] = (byte)symbol; continue;
                }
                if (symbol == 256) return;
                if (symbol > 285) throw Invalid();
                int length = LengthBase[symbol - 257] + ReadBits(LengthExtra[symbol - 257]);
                int distanceCode = distances.Decode(this);
                if (distanceCode > 29) throw Invalid();
                int back = DistanceBase[distanceCode] + ReadBits(DistanceExtra[distanceCode]);
                if (back > written || length > outputLength - written) throw Invalid();
                if (back >= length)
                {
                    Buffer.BlockCopy(output, written - back, output, written, length); written += length;
                }
                else
                {
                    // LZ matches can overlap; each new byte extends the history.
                    int stop = written + length;
                    while (written < stop) { output[written] = output[written - back]; written++; }
                }
            }
        }
        static InvalidDataException Invalid() => new InvalidDataException("Invalid ClusterMesh DEFLATE page.");

        sealed class Huffman
        {
            readonly int rootBits;
            readonly ushort[] fast, symbols;
            readonly int[] counts = new int[16], firstCodes = new int[16], firstSymbols = new int[16], offsets = new int[16];
            int maximum;
            internal Huffman(int symbolCount, int rootBits)
            { this.rootBits = rootBits; fast = new ushort[1 << rootBits]; symbols = new ushort[symbolCount]; }
            internal void Build(byte[] lengths, int start, int count, bool allowSingle, bool allowEmpty)
            {
                Array.Clear(counts, 0, counts.Length); Array.Clear(fast, 0, fast.Length); maximum = 0;
                for (int i = 0; i < count; i++)
                {
                    int length = lengths[start + i];
                    if (length > 15) throw Invalid();
                    if (length != 0) { counts[length]++; maximum = Math.Max(maximum, length); }
                }
                if (maximum == 0) { if (!allowEmpty) throw Invalid(); return; }
                int left = 1, code = 0, rank = 0;
                for (int length = 1; length <= 15; length++)
                {
                    left = (left << 1) - counts[length];
                    if (left < 0) throw Invalid();
                    code = (code + counts[length - 1]) << 1;
                    firstCodes[length] = code; firstSymbols[length] = offsets[length] = rank; rank += counts[length];
                }
                if (left != 0 && !(allowSingle && maximum == 1 && counts[1] == 1)) throw Invalid();
                for (int symbol = 0; symbol < count; symbol++)
                {
                    int length = lengths[start + symbol];
                    if (length == 0) continue;
                    int index = offsets[length]++; symbols[index] = (ushort)symbol;
                    if (length > rootBits) continue;
                    int value = firstCodes[length] + index - firstSymbols[length], reversed = 0;
                    for (int bit = 0; bit < length; bit++) { reversed = (reversed << 1) | (value & 1); value >>= 1; }
                    ushort entry = (ushort)(((symbol + 1) << 4) | length);
                    for (int i = reversed; i < fast.Length; i += 1 << length) fast[i] = entry;
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal int Decode(ClusterMeshPageInflater reader)
            {
                reader.FillBits(rootBits);
                int entry = fast[(int)reader.bits & (fast.Length - 1)], length = entry & 15;
                if (length != 0 && length <= reader.bitCount)
                { reader.bits >>= length; reader.bitCount -= length; return (entry >> 4) - 1; }
                int code = 0;
                for (int bits = 1; bits <= maximum; bits++)
                {
                    code = (code << 1) | reader.ReadBits(1);
                    int index = code - firstCodes[bits];
                    if (index >= 0 && index < counts[bits]) return symbols[firstSymbols[bits] + index];
                }
                throw Invalid();
            }
        }
    }

    internal static class ClusterMeshInflatePool
    {
        static readonly object Gate = new object();
        static ClusterMeshPageInflater idle;
        static int limit, active, idleCount, generation; // Disabled until explicitly configured.
        internal static int Active { get { lock (Gate) return active; } }
        internal static int Retained { get { lock (Gate) return active + idleCount; } }
        internal static long WorkspacePayloadBytes => (long)Retained * ClusterMeshPageInflater.WorkspacePayloadBytes;
        internal static void SetLimit(int value)
        {
            lock (Gate)
            {
                limit = Math.Max(0, value);
                while (idle != null && active + idleCount > limit)
                { var next = idle.Next; idle.Next = null; idle = next; idleCount--; }
            }
        }
        internal static bool TryRent(out ClusterMeshPageInflater result)
        {
            lock (Gate)
            {
                result = null;
                if (active >= limit) return false;
                result = idle;
                if (result == null) result = new ClusterMeshPageInflater();
                else { idle = result.Next; result.Next = null; idleCount--; }
                result.Leased = true; result.LeaseGeneration = generation; active++; return true;
            }
        }
        internal static void Return(ClusterMeshPageInflater value)
        {
            if (value == null) return;
            lock (Gate)
            {
                if (!value.Leased) throw new InvalidOperationException("ClusterMesh inflater returned twice.");
                value.Leased = false; active--;
                if (value.LeaseGeneration != generation || active + idleCount >= limit) return;
                value.Next = idle; idle = value; idleCount++;
            }
        }
        internal static void Clear()
        {
            lock (Gate)
            {
                // In-flight tasks keep their own context until their finally.
                // They still count against admission and cannot repopulate idle.
                generation++;
                while (idle != null) { var next = idle.Next; idle.Next = null; idle = next; }
                idleCount = 0;
            }
        }
    }
}
