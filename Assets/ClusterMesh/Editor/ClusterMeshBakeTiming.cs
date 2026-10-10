using System;
using System.Diagnostics;

namespace ClusterMesh
{
    // Optional editor-only diagnostics; no clock reads in normal baking.
    public static class ClusterMeshBakeTiming
    {
        public static Action<string,double> Observer;
        public static long Begin()=>Observer==null?0:Stopwatch.GetTimestamp();
        public static void End(string stage,long stamp)
        {if(stamp!=0)Observer?.Invoke(stage,(Stopwatch.GetTimestamp()-stamp)*1000.0/Stopwatch.Frequency);}
    }
}
