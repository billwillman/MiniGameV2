using System;
using System.Diagnostics;

namespace ClusterMesh
{
    // Optional Editor-only wall-clock probes. Nested scopes are inclusive,
    // not GPU timestamps. No timers/callbacks run without an explicit sink;
    // calls are stripped from Players. Sink must not throw or call Unity IO.
    public static class ClusterMeshCpuTiming
    {
        public static Action<string,double> Sample;
        [Conditional("UNITY_EDITOR")]
        public static void Begin(ref long stamp)
        {if(Sample!=null)stamp=Stopwatch.GetTimestamp();}
        [Conditional("UNITY_EDITOR")]
        public static void End(string phase,long stamp)
        {if(stamp!=0)Sample?.Invoke(phase,(Stopwatch.GetTimestamp()-stamp)*1000.0/Stopwatch.Frequency);}
    }
}
