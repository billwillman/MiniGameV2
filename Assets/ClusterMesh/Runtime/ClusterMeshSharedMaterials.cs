using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClusterMesh
{
    // Main-thread leases. Materials are immutable while leased: per-draw data
    // belongs in an MPB. Compatible source assets are borrowed, never modified
    // or destroyed. Shader/keyword adaptation is shared only while in use, so
    // unloading a world cannot pin its AB textures through an idle cache.
    public static class ClusterMeshSharedMaterials
    {
        const string ReceiveShadowsOff="_RECEIVE_SHADOWS_OFF";
        internal sealed class Entry
        {
            internal (int,int,bool) key;
            internal Material material;
            internal bool owned;
            internal int references;
        }
        public sealed class Lease:IDisposable
        {
            Entry entry;
            internal Lease(Entry value){entry=value;value.references++;}
            public Material Material=>entry?.material;
            public void Dispose()
            {
                var value=entry;if(value==null)return;entry=null;
                if(--value.references!=0)return;
                if(entries.TryGetValue(value.key,out var current)&&ReferenceEquals(current,value))entries.Remove(value.key);
                if(value.owned&&value.material!=null)
                {
                    if(Application.isPlaying)UnityEngine.Object.Destroy(value.material);
                    else UnityEngine.Object.DestroyImmediate(value.material);
                }
                value.material=null;
            }
        }
        static readonly Dictionary<(int,int,bool),Entry> entries=new Dictionary<(int,int,bool),Entry>();
        public static int EntryCount=>entries.Count;
        public static Lease Acquire(Material source,Shader shader,bool receiveShadows)
        {
            if(shader==null)throw new ArgumentNullException(nameof(shader));
            var key=(source!=null?source.GetInstanceID():0,shader.GetInstanceID(),receiveShadows);
            if(entries.TryGetValue(key,out var current)&&current.material!=null)return new Lease(current);
            bool borrow=source!=null&&source.shader==shader&&source.enableInstancing&&
                source.IsKeywordEnabled(ReceiveShadowsOff)==!receiveShadows;
            Material material=borrow?source:ClusterMeshMaterialUtil.CreateRuntimeMaterial(source,shader);
            if(!borrow)
            {
                if(receiveShadows)material.DisableKeyword(ReceiveShadowsOff);
                else material.EnableKeyword(ReceiveShadowsOff);
            }
            var entry=new Entry{key=key,material=material,owned=!borrow};entries[key]=entry;
            return new Lease(entry);
        }
    }
}
