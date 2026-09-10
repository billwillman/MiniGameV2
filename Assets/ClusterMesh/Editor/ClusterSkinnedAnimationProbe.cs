using System.Text;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    [InitializeOnLoad]
    static class ClusterSkinnedAnimationProbe
    {
        const string Marker = "[ClusterSkinnedAnimationProbe]";

        static ClusterSkinnedAnimationProbe()
        {
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            string[] guids = AssetDatabase.FindAssets("t:ClusterSkinnedMeshAsset");
            var text = new StringBuilder(Marker).Append(" assets=").Append(guids.Length);
            for (int a = 0; a < guids.Length; a++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[a]);
                ClusterSkinnedMeshAsset asset = AssetDatabase.LoadAssetAtPath<ClusterSkinnedMeshAsset>(path);
                text.Append(" | ").Append(path);
                if (asset == null || asset.clips == null)
                {
                    text.Append(" invalid");
                    continue;
                }
                text.Append(" clips=").Append(asset.clips.Length).Append(" bones=").Append(asset.bindPoses != null ? asset.bindPoses.Length : 0);
                for (int c = 0; c < asset.clips.Length; c++)
                {
                    ClusterSkinnedClip clip = asset.clips[c];
                    int curveKeys = 0;
                    float curveRange = 0f;
                    if (clip != null && clip.boneCurves != null)
                    {
                        for (int b = 0; b < clip.boneCurves.Length; b++)
                        {
                            ClusterSkinnedBoneCurves curves = clip.boneCurves[b];
                            if (curves == null) continue;
                            AnimationCurve[] all =
                            {
                                curves.positionX, curves.positionY, curves.positionZ,
                                curves.rotationX, curves.rotationY, curves.rotationZ, curves.rotationW,
                                curves.scaleX, curves.scaleY, curves.scaleZ
                            };
                            for (int i = 0; i < all.Length; i++)
                            {
                                AnimationCurve curve = all[i];
                                if (curve == null) continue;
                                curveKeys += curve.length;
                                if (curve.length > 1)
                                    curveRange = Mathf.Max(curveRange, Mathf.Abs(curve.keys[curve.length - 1].value - curve.keys[0].value));
                            }
                        }
                    }
                    float paletteDelta = 0f;
                    int bones = asset.bindPoses != null ? asset.bindPoses.Length : 0;
                    var first = new Matrix4x4[bones];
                    var middle = new Matrix4x4[bones];
                    if (ClusterSkinnedAnimation.EvaluatePalette(asset, c, 0f, first) &&
                        ClusterSkinnedAnimation.EvaluatePalette(asset, c, 0.5f, middle))
                    {
                        for (int b = 0; b < bones; b++)
                            for (int i = 0; i < 16; i++)
                                paletteDelta = Mathf.Max(paletteDelta, Mathf.Abs(first[b][i] - middle[b][i]));
                    }
                    text.Append(" clip[").Append(c).Append("]=").Append(clip != null ? clip.name : "null")
                        .Append(" duration=").Append(clip != null ? clip.duration : 0f)
                        .Append(" keys=").Append(curveKeys)
                        .Append(" range=").Append(curveRange)
                        .Append(" paletteDelta=").Append(paletteDelta);
                }
            }
            Debug.Log(text.ToString());
        }
    }
}
