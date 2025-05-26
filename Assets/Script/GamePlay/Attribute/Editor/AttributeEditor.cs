using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using SOC.GamePlay.Attribute;
using PlasticPipe.PlasticProtocol.Messages;
using System.Text;

namespace SOC.GamePlay.Attribute
{

    [CustomEditor(typeof(AttributeComponent))]
    public class AttributeComponentEditor : Editor
    {
        static string _rootDir = "Assets/Resources/@Lua/_Attribute/";

        void DrawRuntime()
        {
            AttributeComponent target = this.target as AttributeComponent;
            if (target != null)
            {
                var GroupMetas = target.AttributeGroupMeta;
                // 绘制数据
                if (target.NetworkIntGroupVars != null)
                {
                    for (int i = 0; i < target.NetworkIntGroupVars.Count; ++i)
                    {
                        var GroupMeta = GroupMetas[i];
                        var GroupItem = target.NetworkIntGroupVars[i];
                        EditorGUILayout.BeginFoldoutHeaderGroup(true, GroupItem.Name);
                        if (GroupItem.Value != null)
                        {
                            int j = 0;
                            foreach (var item in GroupItem.Value.AttributeMap)
                            {
                                var itemMeta = GroupMeta.Attributes[j++];
                                EditorGUILayout.LabelField(itemMeta.AttributeName, item.Value.ToString());
                            }
                            EditorGUILayout.Space();
                        }
                        EditorGUILayout.EndFoldoutHeaderGroup();
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                DrawRuntime();
                return;
            }
            base.OnInspectorGUI();
            if (GUILayout.Button("生成Lua Attribute定义"))
            {
                AttributeComponent component = this.target as AttributeComponent;
                if (component != null)
                {
                    if (!Directory.Exists(_rootDir))
                        Directory.CreateDirectory(_rootDir);
                    string name = component.gameObject.name;
                    const string Clone = "(Clone)";
                    if (name.EndsWith(Clone, System.StringComparison.CurrentCultureIgnoreCase))
                        name = name.Substring(0, name.Length - Clone.Length);
                    string path = string.Format("{0}{1}_AttributeDefine.lua.bytes", _rootDir, name);
                    FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                    try
                    {
                        StringBuilder attributesBuilder = new StringBuilder();
                        int index = 0;
                        foreach (var groupMeta in component.AttributeGroupMeta)
                        {
                            StringBuilder metaBuilder = new StringBuilder();
                            metaBuilder.AppendLine();
                            int key = 0;
                            foreach (var meta in groupMeta.Attributes)
                            {
                                if (string.IsNullOrEmpty(meta.AttributeName))
                                    continue;
                                metaBuilder.Append("      ").Append(meta.AttributeName).Append(" = ").Append(key++).Append(",").AppendLine();
                            }

                            attributesBuilder.AppendFormat("    ").Append(groupMeta.AttributeGroupName).Append(" = {").AppendLine().Append("      _Index = ").Append(index++).Append(",").Append(metaBuilder.ToString()).Append("    },").AppendLine();
                        }
                        string content = "local _M = {\n" + attributesBuilder.ToString() + "}\n\nreturn _M";
                        Debug.Log(content);
                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(content);
                        stream.Write(buffer, 0, buffer.Length);
                    } finally
                    {
                        stream.Close();
                        stream.Dispose();
                    }
                    Debug.Log("生成Attribute完成");
                    AssetDatabase.Refresh();
                }
            }
        }
    }
}
