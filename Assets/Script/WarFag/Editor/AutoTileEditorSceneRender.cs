using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AutoMap
{
    // 智能地表编辑器辅助显示（场景中渲染，测试用）
    [CustomEditor(typeof(AutoTileMap))]
    public class AutoTileEditorSceneRender : Editor
    {
        private void OnSceneGUI()
        {
            var tileMap = this.target as AutoTileMap;
            if (tileMap == null)
                return;
            if (tileMap.m_AutoTileSize.x < 0 || Mathf.Abs(tileMap.m_AutoTileSize.x) <= float.Epsilon || tileMap.m_AutoTileSize.y < 0 || Mathf.Abs(tileMap.m_AutoTileSize.y) <= float.Epsilon)
                return;
            var worldPos = tileMap.transform.position;
            Handles.DrawWireCube(worldPos, new Vector3(tileMap.m_AutoTileSize.x, 1f, tileMap.m_AutoTileSize.y));
        }
    }
}
