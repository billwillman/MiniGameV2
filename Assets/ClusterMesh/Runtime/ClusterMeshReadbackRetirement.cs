using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    // Main-thread ownership of disposed contexts' in-flight request buffers.
    // Normal unload never waits for a callback. Teardown cannot rely on callbacks
    // surviving domain reload / graphics shutdown, so it drains its own requests.
    public static class ClusterMeshReadbackRetirement
    {
        public const int Capacity = 64;
        struct Pending
        {
            public GraphicsBuffer[] owner;
            public int slot;
            public AsyncGPUReadbackRequest request;
        }
        static readonly Pending[] pending = new Pending[Capacity];
        static int count;
        static bool shuttingDown;
        public static int PendingCount => count;
        public static int WaitCount { get; private set; }
        public static int CapacityWaitCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Initialize()
        {
            Drain();
            Resume();
            WaitCount = CapacityWaitCount = 0;
            Application.quitting -= BeginShutdown;
            Application.quitting += BeginShutdown;
        }

        public static void Resume() { shuttingDown = false; }

        public static void Retire(GraphicsBuffer[] owner, int slot, AsyncGPUReadbackRequest request)
        {
            if (owner[slot] == null) return;
            for (int i = 0; i < count; i++)
                if (pending[i].owner == owner && pending[i].slot == slot) return;
            if (shuttingDown || request.done || request.hasError)
            {
                Complete(new Pending { owner = owner, slot = slot, request = request });
                return;
            }
            // Collect completed requests even if their callback was suppressed.
            for (int i = count - 1; i >= 0; i--)
                if (pending[i].request.done || pending[i].request.hasError)
                    Release(pending[i].owner, pending[i].slot);
            if (count == Capacity)
            {
                // Safety back-pressure, not an unbounded GPU retirement cache.
                CapacityWaitCount++;
                Complete(pending[0]);
            }
            if (owner[slot] != null)
                pending[count++] = new Pending { owner = owner, slot = slot, request = request };
        }

        public static void Release(GraphicsBuffer[] owner, int slot)
        {
            // Clear the actual owner's slot: a late callback must not free twice.
            var buffer = owner[slot];
            owner[slot] = null;
            for (int i = count - 1; i >= 0; i--)
            {
                if (pending[i].owner != owner || pending[i].slot != slot) continue;
                pending[i] = pending[--count];
                pending[count] = default;
                break;
            }
            buffer?.Dispose();
        }

        static void Complete(Pending item)
        {
            if (item.owner[item.slot] == null) return;
            if (!item.request.done && !item.request.hasError)
            {
                WaitCount++;
                item.request.WaitForCompletion();
            }
            // WaitForCompletion may invoke the callback, or may only finish GPU
            // work. Both paths release exactly once through the shared owner slot.
            Release(item.owner, item.slot);
        }

        // Only at explicit teardown boundaries, never the regular frame pump.
        public static void Drain()
        {
            while (count != 0) Complete(pending[count - 1]);
        }

        public static void BeginShutdown()
        {
            shuttingDown = true;
            Drain();
            // Contexts disposed after this event drain immediately in Retire.
        }
    }
}
