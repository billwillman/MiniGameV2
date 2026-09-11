using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterSkinnedAnimationClipFolderScan
    {
        public static AnimationClip[] Collect(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                return Array.Empty<AnimationClip>();

            var seen = new HashSet<AnimationClip>();
            var source1 = new List<AnimationClip>();
            CollectFromClipAssets(folder, seen, source1);

            var source2 = new List<AnimationClip>();
            CollectFromPrefabAssets(folder, seen, source2);

            var result = new AnimationClip[source1.Count + source2.Count];
            source1.CopyTo(result, 0);
            source2.CopyTo(result, source1.Count);
            return result;
        }

        public static int AppendUnique(IList<AnimationClip> destination, IEnumerable<AnimationClip> incoming)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");
            if (incoming == null)
                return 0;

            var have = new HashSet<AnimationClip>();
            for (int i = 0; i < destination.Count; i++)
            {
                if (destination[i] != null)
                    have.Add(destination[i]);
            }

            int added = 0;
            foreach (AnimationClip clip in incoming)
            {
                if (clip == null || !have.Add(clip))
                    continue;
                destination.Add(clip);
                added++;
            }

            return added;
        }

        static void CollectFromClipAssets(string folder, HashSet<AnimationClip> seen, List<AnimationClip> destination)
        {
            AddClipsFromSearch(AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }), seen, destination);
        }

        static void CollectFromPrefabAssets(string folder, HashSet<AnimationClip> seen, List<AnimationClip> destination)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            var paths = UniqueSortedPaths(guids);
            for (int i = 0; i < paths.Count; i++)
                AddClipsFromAssetPath(paths[i], seen, destination);
        }

        static void AddClipsFromSearch(string[] guids, HashSet<AnimationClip> seen, List<AnimationClip> destination)
        {
            var paths = UniqueSortedPaths(guids);
            for (int i = 0; i < paths.Count; i++)
                AddClipsFromAssetPath(paths[i], seen, destination);
        }

        static void AddClipsFromAssetPath(string path, HashSet<AnimationClip> seen, List<AnimationClip> destination)
        {
            UnityEngine.Object[] assets;
            try
            {
                assets = AssetDatabase.LoadAllAssetsAtPath(path);
            }
            catch
            {
                return;
            }

            if (assets == null || assets.Length == 0)
                return;

            var clips = new List<AnimationClip>();
            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview", StringComparison.Ordinal))
                    continue;
                clips.Add(clip);
            }

            clips.Sort(CompareClipName);
            for (int i = 0; i < clips.Count; i++)
            {
                if (seen.Add(clips[i]))
                    destination.Add(clips[i]);
            }
        }

        static List<string> UniqueSortedPaths(string[] guids)
        {
            var paths = new List<string>();
            var seen = new HashSet<string>();
            if (guids == null)
                return paths;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !seen.Add(path))
                    continue;
                paths.Add(path);
            }

            paths.Sort(string.CompareOrdinal);
            return paths;
        }

        static int CompareClipName(AnimationClip a, AnimationClip b)
        {
            return string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : "");
        }
    }
}
