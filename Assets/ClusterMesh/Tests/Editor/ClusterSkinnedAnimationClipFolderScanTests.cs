using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterSkinnedAnimationClipFolderScanTests
    {
        string _folder;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_folder) && AssetDatabase.IsValidFolder(_folder))
                AssetDatabase.DeleteAsset(_folder);
            _folder = null;
            AssetDatabase.Refresh();
        }

        [Test]
        public void Collect_InvalidFolder_ReturnsEmpty()
        {
            AnimationClip[] found = ClusterSkinnedAnimationClipFolderScan.Collect(null);
            Assert.That(found, Is.Empty);
            found = ClusterSkinnedAnimationClipFolderScan.Collect("Assets/ClusterMesh/DoesNotExist_ClipScan");
            Assert.That(found, Is.Empty);
        }

        [Test]
        public void Collect_EmptyFolder_ReturnsEmpty()
        {
            _folder = CreateTempFolder();
            Assert.That(ClusterSkinnedAnimationClipFolderScan.Collect(_folder), Is.Empty);
        }

        [Test]
        public void Collect_LooseClips_AreSortedByPath()
        {
            _folder = CreateTempFolder();
            CreateLooseClip(_folder, "B_Walk");
            CreateLooseClip(_folder, "A_Idle");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AnimationClip[] found = ClusterSkinnedAnimationClipFolderScan.Collect(_folder);
            Assert.That(ClipNames(found), Is.EqualTo(new[] { "A_Idle", "B_Walk" }));
            Assert.That(found[0], Is.Not.Null);
            Assert.That(found[1], Is.Not.Null);
        }

        [Test]
        public void Collect_NestedFolderClip_IsIncluded()
        {
            _folder = CreateTempFolder();
            string nestedName = "nested";
            AssetDatabase.CreateFolder(_folder, nestedName);
            CreateLooseClip(_folder + "/" + nestedName, "C_Jump");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AnimationClip[] found = ClusterSkinnedAnimationClipFolderScan.Collect(_folder);
            Assert.That(ClipNames(found), Is.EqualTo(new[] { "C_Jump" }));
        }

        [Test]
        public void Collect_PrefabEmbeddedClips_AreAllReturned()
        {
            _folder = CreateTempFolder();
            CreatePrefabWithClips(_folder, "Embed", "Idle", "Run");
            AssetDatabase.Refresh();

            AnimationClip[] found = ClusterSkinnedAnimationClipFolderScan.Collect(_folder);
            Assert.That(ClipNames(found), Is.EquivalentTo(new[] { "Idle", "Run" }));
        }

        [Test]
        public void Collect_SameClipFromBothSources_AppearsOnce()
        {
            _folder = CreateTempFolder();
            CreatePrefabWithClips(_folder, "Shared", "Only");
            AssetDatabase.Refresh();

            AnimationClip[] found = ClusterSkinnedAnimationClipFolderScan.Collect(_folder);
            Assert.That(ClipNames(found), Is.EqualTo(new[] { "Only" }));
        }

        [Test]
        public void AppendUnique_SkipsExistingAndNulls_PreservesOrder()
        {
            var already = new AnimationClip { name = "Already" };
            var extra = new AnimationClip { name = "Extra" };
            var list = new List<AnimationClip> { null, already };

            int added = ClusterSkinnedAnimationClipFolderScan.AppendUnique(
                list, new[] { already, extra, null });

            Assert.That(added, Is.EqualTo(1));
            Assert.That(list, Is.EqualTo(new[] { null, already, extra }));
        }

        [Test]
        public void AppendUnique_EmptyIncoming_DoesNotChangeList()
        {
            var already = new AnimationClip { name = "Keep" };
            var list = new List<AnimationClip> { already };
            int added = ClusterSkinnedAnimationClipFolderScan.AppendUnique(list, new AnimationClip[0]);
            Assert.That(added, Is.EqualTo(0));
            Assert.That(list, Is.EqualTo(new[] { already }));
        }

        static string[] ClipNames(AnimationClip[] clips)
        {
            var names = new string[clips.Length];
            for (int i = 0; i < clips.Length; i++)
                names[i] = clips[i] != null ? clips[i].name : "";
            return names;
        }

        static string CreateTempFolder()
        {
            string name = "_TmpClipScan_" + GUID.Generate();
            string parent = "Assets/ClusterMesh/Tests/Editor";
            Assert.That(AssetDatabase.IsValidFolder(parent), Is.True);
            string guid = AssetDatabase.CreateFolder(parent, name);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.That(AssetDatabase.IsValidFolder(path), Is.True);
            return path;
        }

        static AnimationClip CreateLooseClip(string folder, string fileName)
        {
            var clip = new AnimationClip { name = fileName };
            string path = folder + "/" + fileName + ".anim";
            AssetDatabase.CreateAsset(clip, path);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        static AnimationClip[] CreatePrefabWithClips(string folder, string prefabName, params string[] clipNames)
        {
            var go = new GameObject(prefabName);
            string prefabPath = folder + "/" + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            Object prefab = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            for (int i = 0; i < clipNames.Length; i++)
            {
                var clip = new AnimationClip { name = clipNames[i] };
                AssetDatabase.AddObjectToAsset(clip, prefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(prefabPath);

            var loaded = new List<AnimationClip>();
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(prefabPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip)
                    loaded.Add(clip);
            }

            loaded.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return loaded.ToArray();
        }
    }
}
