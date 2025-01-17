using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using UnityEditor;
//using Unity.Netcode;

namespace SOC.GamePlay
{
    [CustomEditor(typeof(MoeCharacterController))]
    public class MoeCharacterControllerEditor : Editor
    {

        private static void GetBonePath(Transform bone, ref StringBuilder builder, Transform root = null) {
            if (bone == null)
                return;
            if (root != null && bone == root)
                return;
            if (builder == null)
                builder = new StringBuilder();
            if (builder.Length <= 0)
                builder.Append(bone.name);
            else {
                builder.Insert(0, bone.name + "/");
            }
            bone = bone.parent;
            GetBonePath(bone, ref builder, root);
        }

        void BuildNewBodyMesh(SkinnedMeshRenderer body, List<Transform> boneList, List<Matrix4x4> bindPoseList) {
            if (body == null || boneList == null || bindPoseList == null)
                return;
            Mesh OrignMesh = body.sharedMesh;
            if (OrignMesh != null) {
                string meshFilePath = AssetDatabase.GetAssetPath(OrignMesh);
                if (!string.IsNullOrEmpty(meshFilePath)) {
                    string ext = Path.GetExtension(meshFilePath);
                    if (string.Compare(ext, ".fbx", true) == 0) {
                        Mesh targetMesh = Instantiate<Mesh>(OrignMesh);
                        meshFilePath = Path.GetDirectoryName(meshFilePath) + "/" + OrignMesh.name + ".asset";
                        meshFilePath = meshFilePath.Replace("\\", "/");
                        AssetDatabase.DeleteAsset(meshFilePath);
                        AssetDatabase.CreateAsset(targetMesh, meshFilePath);
                        SetAssetMeshReadable(meshFilePath, true);
                        // 存储过后的
                        OrignMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshFilePath);
                        body.bones = boneList.ToArray();
                        OrignMesh.bindposes = bindPoseList.ToArray();
                        AssetDatabase.SaveAssets();
                        SetAssetMeshReadable(OrignMesh, false);
                        this.SetDirty();
                        this.SaveChanges();
                        AssetDatabase.Refresh();
                        // 设置到body的skinned上
                        body.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshFilePath);
                        this.SetDirty();
                        this.SaveChanges();
                        AssetDatabase.Refresh();
                    }
                }
            }
        }

        private void ProcessSkinnedMesh(SkinnedMeshRenderer body, SkinnedMeshRenderer other) {
            if (body == null || other == null)
                return;
            var otherBone = other.rootBone;
            if (otherBone == null)
                return;
            // 1.处理rootBone
            StringBuilder builder = new StringBuilder();
            Transform otherRootTrans = other.transform.parent;
            GetBonePath(otherBone, ref builder, otherRootTrans);
            if (builder != null) {
                string path = builder.ToString();
                if (!string.IsNullOrEmpty(path)) {
                    Transform bodyRootTrans = body.transform.parent;
                    if (bodyRootTrans != null) {
                        Transform newRootBone = bodyRootTrans.Find(path);
                        if (newRootBone != null) {
                            other.rootBone = newRootBone;
                        }
                    }
                }
            }
            // 合并骨骼，并且需要生成一个Mesh的VertexIndex, 最后要自己生成一个Mesh的资产
            // 处理最大骨骼数量，然后处理bindPoses
            //other.bones = body.bones;
            // 2.处理bones
            // 不需要other.bones和body.bones相同，因为传入Shader里的骨骼对象，只需要传入使用的
            var otherBones = other.bones;
            if (otherBones != null && otherBones != null && otherBones.Length > 0 /*&& other.bones.Length != body.bones.Length*/) {
               // Dictionary<int, int> oldBoneToNewBoneMap = new Dictionary<int, int>();
                var bodyBones = body.bones;
                for (int i = 0; i < otherBones.Length; ++i) {
                    var bone = otherBones[i];
                    builder.Clear();
                    GetBonePath(bone, ref builder, otherRootTrans);
                    string path = builder.ToString();
                    if (!string.IsNullOrEmpty(path)) {
                        Transform bodyRootTrans = body.transform.parent;
                        if (bodyRootTrans != null) {
                            Transform newBone = bodyRootTrans.Find(path);
                            if (newBone != null) {
                                /*
                                int index = -1;
                                for (int j = 0; j < bodyBones.Length; ++j) {
                                    if (bodyBones[j] == newBone) {
                                        index = j;
                                        break;
                                    }
                                }
                                if (index >= 0)
                                    oldBoneToNewBoneMap[i] = index;
                                else {
                                    Debug.LogError("[OtherBone] not found:" + path + "\n=== " + other.name);
                                }*/
                                otherBones[i] = newBone;
                            } else {
                                Debug.LogError("[OtherBone] not found:" + path + "\n=== " + other.name);
                            }
                        }
                    }
                }
                other.bones = otherBones;
                /*
                if (oldBoneToNewBoneMap.Count > 0) {
                    // Clone Mesh
                    Mesh OrignMesh = other.sharedMesh;
                    if (OrignMesh != null) {
                        string meshFilePath = AssetDatabase.GetAssetPath(OrignMesh);
                        if (!string.IsNullOrEmpty(meshFilePath)) {
                            string ext = Path.GetExtension(meshFilePath);
                            if (string.Compare(ext, ".fbx", true) == 0) {
                                Mesh targetMesh = Instantiate<Mesh>(OrignMesh);
                                meshFilePath = Path.GetDirectoryName(meshFilePath) + "/" + OrignMesh.name + ".asset";
                                meshFilePath = meshFilePath.Replace("\\", "/");
                                AssetDatabase.DeleteAsset(meshFilePath);
                                AssetDatabase.CreateAsset(targetMesh, meshFilePath);
                                SetAssetMeshReadable(meshFilePath, true);
                                // 存储过后的
                               OrignMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshFilePath);
                                
                             BoneWeight[] otherBoneWeights = OrignMesh.boneWeights;
                             if (otherBoneWeights != null) {
                                 bool isDirty = false;
                                 for (int i = 0; i < otherBoneWeights.Length; ++i) {
                                     BoneWeight boneWeight = otherBoneWeights[i];
                                     if (TransOldBoneIndexToNewBoneIndex(oldBoneToNewBoneMap, ref boneWeight)) {
                                         otherBoneWeights[i] = boneWeight;
                                         isDirty = true;
                                     }
                                 }
                                 if (isDirty) {
                                     OrignMesh.boneWeights = otherBoneWeights;
                                 }
                             }
                             // 写bindPose
                             var otherBindPoses = OrignMesh.bindposes;
                             var bodyBindPoses = body.sharedMesh.bindposes;
                             if (otherBindPoses != null) {
                                 for (int i = 0; i < OrignMesh.bindposes.Length; ++i) {
                                     int newIdx;
                                     if (oldBoneToNewBoneMap.TryGetValue(i, out newIdx)) {
                                         Debug.Log("[Origin] " + otherBindPoses[i].ToString() + "\n[New] " + bodyBindPoses[newIdx].ToString());
                                     } else {
                                         Debug.LogError("not found bindPose: " + i.ToString() + "\n=== " + other.name);
                                     }
                                 }
                                 OrignMesh.bindposes = bodyBindPoses;
                                 other.bones = body.bones;
                             }
                             AssetDatabase.SaveAssets();
                          
                                SetAssetMeshReadable(OrignMesh, false);
                               // 写入到SkinnedMesh里
                               other.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshFilePath);
                           }
                       }
                   }
               }
                */
                //--
            }
            // other.sharedMesh.bindposes
            if (otherRootTrans != null) {
               Animator otherAnim = otherRootTrans.GetComponent<Animator>();
               if (otherAnim != null) {
                   otherAnim.enabled = false;
                   GameObject.DestroyImmediate(otherAnim);
               }
               Animancer.AnimancerComponent a1 = otherRootTrans.GetComponent<Animancer.AnimancerComponent>();
               if (a1 != null) {
                   GameObject.DestroyImmediate(a1);
               }
               Animancer.NamedAnimancerComponent a2 = otherRootTrans.GetComponent<Animancer.NamedAnimancerComponent>();
               if (a2 != null) {
                   GameObject.DestroyImmediate(a2);
               }
           }
       }

       static bool TransOldBoneIndexToNewBoneIndex(Dictionary<int, int> map, ref BoneWeight weight) {
           if (map == null)
               return false;
           bool ret = false;
           if (weight.weight0 > 0) {
               int idx;
               if (map.TryGetValue(weight.boneIndex0, out idx)) {
                   weight.boneIndex0 = idx;
                   ret = true;
               }
           }
           if (weight.weight1 > 0) {
               int idx;
               if (map.TryGetValue(weight.boneIndex1, out idx)) {
                   weight.boneIndex1 = idx;
                   ret = true;
               }
           }
           if (weight.weight2 > 0) {
               int idx;
               if (map.TryGetValue(weight.boneIndex2, out idx)) {
                   weight.boneIndex2 = idx;
                   ret = true;
               }
           }
           if (weight.weight3 > 0) {
               int idx;
               if (map.TryGetValue(weight.boneIndex3, out idx)) {
                   weight.boneIndex3 = idx;
                   ret = true;
               }
           }
           return ret;
       }

       static void SetAssetMeshReadable(UnityEngine.Object assetObj, bool isReadWrite) {
           if (assetObj == null)
               return;
           string path = AssetDatabase.GetAssetPath(assetObj);
           SetAssetMeshReadable(path, isReadWrite);
       }

       static void SetAssetMeshReadable(string meshFilePath, bool isReadWrite) {
           if (string.IsNullOrEmpty(meshFilePath))
               return;
           if (File.Exists(meshFilePath)) {
               FileStream metaStream = new FileStream(meshFilePath, FileMode.Open, FileAccess.ReadWrite);
               try {
                   byte[] buffer = new byte[metaStream.Length];
                   metaStream.Read(buffer, 0, buffer.Length);
                   string metaStr = System.Text.Encoding.UTF8.GetString(buffer);
                   const string readAblaStr = "m_IsReadable: ";
                   int startIndex = metaStr.IndexOf(readAblaStr);
                   if (startIndex < 0)
                       return;
                   int endIndex = metaStr.IndexOf("\n", startIndex);
                   if (endIndex < 0)
                       endIndex = metaStr.Length - 1;
                   string leftStr = metaStr.Substring(0, startIndex);
                   string rightStr = metaStr.Substring(endIndex);
                   metaStr = leftStr + readAblaStr + (isReadWrite ? "1" : "0") + rightStr;
                   metaStream.Seek(0, SeekOrigin.Begin);

                   buffer = System.Text.Encoding.UTF8.GetBytes(metaStr);
                   metaStream.Write(buffer, 0, buffer.Length);
               } finally {
                   metaStream.Close();
                   metaStream.Dispose();
               }
               AssetDatabase.Refresh(); // 刷新
           }
       }


        static void ProcessBodySkinnedMesh(MoeCharacterController controller, SkinnedMeshRenderer body, bool isCombineBone) {
            if (body == null)
                return;
            Transform parent = body.transform.parent;
            if (parent == null)
                return;
            Animator animator = parent.GetComponent<Animator>();
            if (animator == null)
                return;
            animator.enabled = true;
            Animancer.AnimancerComponent animancerComp = parent.GetComponent<Animancer.AnimancerComponent>();
            if (animancerComp == null) {
                animancerComp = parent.gameObject.AddComponent<Animancer.AnimancerComponent>();
            }
            // animancerComp.enabled = true;
            if (controller.m_Animancer == null || isCombineBone)
                controller.m_Animancer = new Animancer.AnimancerComponent[1];
            controller.m_Animancer[0] = animancerComp;
            if (isCombineBone) {
                // 把Prefab关联去掉
                PrefabUtility.UnpackPrefabInstance(controller.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                // 删除骨骼冗余节点
                HashSet<GameObject> deleteObjs = null;
                RemoveSkinnedMeshBoneRoot(controller.m_Body, controller.m_Hair, ref deleteObjs);
                RemoveSkinnedMeshBoneRoot(controller.m_Body, controller.m_Head, ref deleteObjs);
                RemoveSkinnedMeshBoneRoot(controller.m_Body, controller.m_Weapon, ref deleteObjs);
                if (controller.m_OtherSkinedMeshList != null) {
                    for (int i = 0; i < controller.m_OtherSkinedMeshList.Count; ++i) {
                        RemoveSkinnedMeshBoneRoot(controller.m_Body, controller.m_OtherSkinedMeshList[i], ref deleteObjs);
                    }
                }
                // 删除不用的骨骼
                if (deleteObjs != null) {
                    var iter = deleteObjs.GetEnumerator();
                    while (iter.MoveNext()) {
                        if (iter.Current != null) {
                            GameObject.DestroyImmediate(iter.Current);
                        }
                    }
                    iter.Dispose();
                    deleteObjs.Clear();
                }
            }
        }

        static void RemoveSkinnedMeshBoneRoot(SkinnedMeshRenderer body, SkinnedMeshRenderer skin, ref HashSet<GameObject> deleteObj) {
            if (body == null || skin == null)
                return;
            var bodyParent = body.transform.parent;
            if (bodyParent == null)
                return;
            var skinParent = skin.transform.parent;
            skin.transform.SetParent(bodyParent, true);
            if (deleteObj == null)
                deleteObj = new HashSet<GameObject>();
            if (!deleteObj.Contains(skinParent.gameObject))
                deleteObj.Add(skinParent.gameObject);
        }

        static void ProcessControllerRoot(MoeCharacterController controller) {
            if (controller == null)
                return;
            /*
            NetworkObject netObject = controller.GetComponent<NetworkObject>();
            if (netObject != null) {
                netObject.enabled = true;
                netObject.AlwaysReplicateAsRoot = true;
                netObject.SynchronizeTransform = true;
            }
            */
        }

        static void _AddBodyToListFunc(SkinnedMeshRenderer body, ref List<Transform> bodyBoneList,
                ref List<Matrix4x4> bodyBoneBindPoseList,
                Transform[] otherBones, Matrix4x4[] otherBindPoses, Transform cloneChildRoot, Transform childRoot) {
            int oldIdx = -1;
            for (int i = 0; i < otherBones.Length; ++i) {
                if (otherBones[i] == childRoot) {
                    oldIdx = i;
                    break;
                }
            }
            if (oldIdx >= 0) {
                if (bodyBoneList == null)
                    bodyBoneList = new List<Transform>(body.bones);
                // 不需要更改
                //bodyBoneList.Add(cloneChildRoot);
                if (bodyBoneBindPoseList == null)
                    bodyBoneBindPoseList = new List<Matrix4x4>(body.sharedMesh.bindposes);
                // 不需要更改
                //bodyBoneBindPoseList.Add(otherBindPoses[oldIdx]);
            }
            for (int i = 0; i < childRoot.childCount; ++i) {
                _AddBodyToListFunc(body, ref bodyBoneList, ref bodyBoneBindPoseList, otherBones, otherBindPoses, cloneChildRoot.GetChild(i), childRoot.GetChild(i));
            }
        }

        static void CheckAddChildNode(Transform targetRoot, Transform sourceRoot) {
            if (targetRoot != null && sourceRoot != null) {
                for (int i = 0; i < sourceRoot.childCount; ++i) {
                    var sourceChild = sourceRoot.GetChild(i);
                    string name = sourceChild.name;
                    var targetChild = targetRoot.Find(name);
                    if (targetChild != null) {
                        CheckAddChildNode(targetChild, sourceChild);
                    } else {
                        var newGameObj = GameObject.Instantiate<GameObject>(sourceChild.gameObject, targetRoot, false);
                        newGameObj.name = sourceChild.gameObject.name;
                        //CheckAddChildNode(newGameObj.transform, sourceChild);
                    }
                }
            }
        }

        // 这个函数有问题，bindposes和bones的数量跟骨骼节点数量是不一样的，需要全合并，再根据bindposes和bones处理
        static void ProcessOhterBonesAddToBodySkinnedMesh(SkinnedMeshRenderer body, SkinnedMeshRenderer other, ref List<Transform> bodyBoneList,
                ref List<Matrix4x4> bodyBoneBindPoseList) {
            if (body == null || other == null || body.sharedMesh == null || other.sharedMesh == null)
                return;
            Transform bodyRootTrans = body.transform.parent;
            Transform otherRootTrans = other.transform.parent;
            Transform[] otherBones = other.bones;
            Matrix4x4[] otherBindPoses = other.sharedMesh.bindposes;
            if (otherBones != null) {
                List<string> newBoneIndexs = new List<string>();
                StringBuilder builder = new StringBuilder();
                // 1.增加没有的bone,放到列表里
                string boneRootName = null;
                for (int i = 0; i < otherBones.Length; ++i) {
                    builder.Clear();
                    Transform bone = otherBones[i];
                    GetBonePath(bone, ref builder, otherRootTrans);
                    string path = builder.ToString();
                    Transform bodyBone = bodyRootTrans.Find(path);
                    if (bodyBone != null) {
                        // 判断bodyBone是不是在列表里
                        if (bodyBoneList == null)
                            bodyBoneList = new List<Transform>(body.bones);
                        if (bodyBoneList.IndexOf(bodyBone) >= 0)
                            continue;
                    }
                    if (string.IsNullOrEmpty(boneRootName))
                        boneRootName = path.Split("/")[0];
                    newBoneIndexs.Add(path);
                }
                if (!string.IsNullOrEmpty(boneRootName) && newBoneIndexs.Count > 0) {
                    Transform bodyBoneRoot = bodyRootTrans.Find(boneRootName);
                    Transform otherBoneRoot = otherRootTrans.Find(boneRootName);
                    CheckAddChildNode(bodyBoneRoot, otherBoneRoot);
                    for (int i = 0; i < newBoneIndexs.Count; ++i) {
                        string path = newBoneIndexs[i];
                        Transform cloneChildRoot = bodyRootTrans.Find(path);
                        Transform childRoot = otherRootTrans.Find(path);
                        _AddBodyToListFunc(body, ref bodyBoneList, ref bodyBoneBindPoseList, otherBones, otherBindPoses, cloneChildRoot, childRoot);
                    }
                }
            }
        }

        static void SavePrefab(GameObject target) {
            var prefabInstance = PrefabUtility.GetCorrespondingObjectFromSource(target);
            if (prefabInstance) {
                var path = AssetDatabase.GetAssetPath(prefabInstance);
                PrefabUtility.UnpackPrefabInstance(target, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                PrefabUtility.SaveAsPrefabAssetAndConnect(target, path, InteractionMode.AutomatedAction);
                PrefabUtility.SaveAsPrefabAsset(target, path);
            } else {
                
            }
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            /*
            if (GUILayout.Button("角色骨骼合并")) {
                MoeCharacterController controller = this.target as MoeCharacterController;
                if (controller != null) {
                    // 骨骼增加到Body上
                    List<Transform> boneList = null;
                    List<Matrix4x4> bindPoseList = null;
                    ProcessOhterBonesAddToBodySkinnedMesh(controller.m_Body, controller.m_Head, ref boneList, ref bindPoseList);
                    ProcessOhterBonesAddToBodySkinnedMesh(controller.m_Body, controller.m_Hair, ref boneList, ref bindPoseList);
                    ProcessOhterBonesAddToBodySkinnedMesh(controller.m_Body, controller.m_Weapon, ref boneList, ref bindPoseList);
                    if (controller.m_OtherSkinedMeshList != null) {
                        for (int i = 0; i < controller.m_OtherSkinedMeshList.Count; ++i) {
                            ProcessOhterBonesAddToBodySkinnedMesh(controller.m_Body, controller.m_OtherSkinedMeshList[i], ref boneList, ref bindPoseList);
                        }
                    }
                    // 生成新的body数据
                    BuildNewBodyMesh(controller.m_Body, boneList, bindPoseList);
                    // 存储数据到prefab
                    SavePrefab(controller.gameObject);
                    AssetDatabase.Refresh();
                    //--
                }
            }
            */
            if (GUILayout.Button("角色标准化")) {
                MoeCharacterController controller = this.target as MoeCharacterController;
                if (controller != null) {
                    // 骨骼增加到Body上
                    List<Transform> boneList = null;
                    List<Matrix4x4> bindPoseList = null;
                    ProcessOhterBonesAddToBodySkinnedMesh(controller.m_Body, controller.m_Head, ref boneList, ref bindPoseList);
                    ProcessOhterBonesAddToBodySkinnedMesh(controller.m_Body, controller.m_Hair, ref boneList, ref bindPoseList);
                    ProcessOhterBonesAddToBodySkinnedMesh(controller.m_Body, controller.m_Weapon, ref boneList, ref bindPoseList);
                    if (controller.m_OtherSkinedMeshList != null) {
                        for (int i = 0; i < controller.m_OtherSkinedMeshList.Count; ++i) {
                            ProcessOhterBonesAddToBodySkinnedMesh(controller.m_Body, controller.m_OtherSkinedMeshList[i], ref boneList, ref bindPoseList);
                        }
                    }
                    // 生成新的body数据
                    //BuildNewBodyMesh(controller.m_Body, boneList, bindPoseList);
                    AssetDatabase.Refresh();

                    ProcessSkinnedMesh(controller.m_Body, controller.m_Head);
                    ProcessSkinnedMesh(controller.m_Body, controller.m_Hair);
                    ProcessSkinnedMesh(controller.m_Body, controller.m_Weapon);
                    if (controller.m_OtherSkinedMeshList != null) {
                        for (int i = 0; i < controller.m_OtherSkinedMeshList.Count; ++i) {
                            ProcessSkinnedMesh(controller.m_Body, controller.m_OtherSkinedMeshList[i]);
                        }
                    }
                    ProcessBodySkinnedMesh(controller, controller.m_Body, true);
                    ProcessControllerRoot(controller);
                    this.SetDirty();
                    this.SaveChanges();
                }
            }
        }
    }
}
