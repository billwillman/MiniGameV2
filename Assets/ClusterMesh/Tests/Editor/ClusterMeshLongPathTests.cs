using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshLongPathTests
    {
        string folder,path;
        Mesh mesh;
        ClusterMeshAsset asset;
        ClusterMeshBakeResult bake;
        ClusterMeshBakeSettings settings;
        [SetUp] public void SetUp()
        {
            folder="Assets/ClusterMeshLongPathTests-"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
            int stemLength=238-Path.GetFullPath(folder).Length-1-1-32-".cmstream".Length;
            Assert.That(stemLength,Is.InRange(8,180),"Fixture project path is outside its supported range");
            path=folder+"/Triangle"+new string('x',stemLength-8)+".asset";
            mesh=ClusterMeshTestMeshes.Grid(12,12);
            settings=new ClusterMeshBakeSettings{enableStreaming=true,buildLodHierarchy=true,preserveAssetOuterBoundary=true};
            bake=ClusterMeshBaker.Bake(mesh,new Material[1],settings);
            asset=ScriptableObject.CreateInstance<ClusterMeshAsset>();asset.CopyFrom(bake,null,settings);
            AssetDatabase.CreateAsset(asset,path);
        }
        [TearDown] public void TearDown()
        {
            if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
            if(!string.IsNullOrEmpty(folder))AssetDatabase.DeleteAsset(folder);
        }
        [Test] public void LongPath_BakeAndAtomicRebake_PreserveExactBytes()
        {
            ClusterMeshStreamBaker.FinalizeStatic(asset,path,settings);
            string sidecar=Path.GetFullPath(asset.streamDescriptor.editorFilePath);
            Assert.That(sidecar.Length,Is.EqualTo(238));
            Assert.That((sidecar+".tmp-"+Guid.NewGuid().ToString("N")).Length,Is.GreaterThanOrEqualTo(260),"Reproduce the old temporary path overflow");
            byte[] first=File.ReadAllBytes(sidecar);string hash=asset.streamDescriptor.contentHash;
            asset.CopyFrom(bake,null,settings);ClusterMeshStreamBaker.FinalizeStatic(asset,path,settings);
            Assert.That(File.ReadAllBytes(sidecar),Is.EqualTo(first));
            Assert.That(asset.streamDescriptor.contentHash,Is.EqualTo(hash));
            Assert.That(Directory.GetFiles(folder,"*.tmp"),Is.Empty);
            var shortAsset=ScriptableObject.CreateInstance<ClusterMeshAsset>();shortAsset.CopyFrom(bake,null,settings);
            string shortPath=folder+"/Short.asset";AssetDatabase.CreateAsset(shortAsset,shortPath);
            ClusterMeshStreamBaker.FinalizeStatic(shortAsset,shortPath,settings);
            Assert.That(File.ReadAllBytes(shortAsset.streamDescriptor.editorFilePath),Is.EqualTo(first),"Paths must not affect binary geometry");
        }
        [Test] public void LongPath_FailedAtomicReplace_PreservesPreviousFileAndRemovesTemporary()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor)Assert.Ignore("Windows exclusive file sharing regression");
            ClusterMeshStreamBaker.FinalizeStatic(asset,path,settings);
            string sidecar=Path.GetFullPath(asset.streamDescriptor.editorFilePath);byte[] before=File.ReadAllBytes(sidecar);
            asset.CopyFrom(bake,null,settings);
            using(var locked=new FileStream(sidecar,FileMode.Open,FileAccess.Read,FileShare.None))
                Assert.Throws<IOException>(()=>ClusterMeshStreamBaker.FinalizeStatic(asset,path,settings));
            Assert.That(File.ReadAllBytes(sidecar),Is.EqualTo(before));
            Assert.That(Directory.GetFiles(folder,"*.tmp"),Is.Empty);
            Assert.That(asset.UsesStreaming,Is.False,"A failed replacement must not install a streaming descriptor");
        }
    }
}
