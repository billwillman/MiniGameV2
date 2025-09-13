using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;
using NsLib.ResMgr;

namespace AutoMap
{
    // 智能地表编辑器辅助显示（场景中渲染，测试用）
    [CustomEditor(typeof(AutoTileMap))]
    public class AutoTileEditorSceneRender : Editor
    {

        void DrawTileMapArea(AutoTileMap tileMap)
        {
            if (tileMap.m_AutoTileSize.x < 0 || Mathf.Abs(tileMap.m_AutoTileSize.x) <= float.Epsilon || tileMap.m_AutoTileSize.y < 0 || Mathf.Abs(tileMap.m_AutoTileSize.y) <= float.Epsilon)
                return;
            var worldPos = tileMap.transform.position;
            Handles.DrawWireCube(worldPos, new Vector3(tileMap.m_AutoTileSize.x, 1f, tileMap.m_AutoTileSize.y));
        }

        private void OnSceneGUI()
        {
            var tileMap = this.target as AutoTileMap;
            if (tileMap == null)
                return;
            if (tileMap.m_AutoTileSize.x < 0 || Mathf.Abs(tileMap.m_AutoTileSize.x) <= float.Epsilon || tileMap.m_AutoTileSize.y < 0 || Mathf.Abs(tileMap.m_AutoTileSize.y) <= float.Epsilon)
                return;
            // Graphics.ClearRandomWriteTargets();
            //GL.Clear(true, true, Color.black);
            DrawTileMapArea(tileMap);
            DrawTileWire(tileMap);
            DrawTileMouse(tileMap);
        }

        Vector3 GetLeftTop(AutoTileMap tileMap)
        {
            Vector3 ret = tileMap.transform.position;
            ret.x -= tileMap.m_AutoTileSize.x / 2.0f;
            ret.z -= tileMap.m_AutoTileSize.y / 2.0f;
            return ret;
        }

        Vector2Int GetTileCellCount(AutoTileMap tileMap)
        {
            if (!IsVaildPerTileSize(tileMap))
                return new Vector2Int();
            int colCnt = Mathf.CeilToInt(tileMap.m_AutoTileSize.x / tileMap.m_PerTileSize.x);
            int rowCnt = Mathf.CeilToInt(tileMap.m_AutoTileSize.y / tileMap.m_PerTileSize.y);
            return new Vector2Int(colCnt, rowCnt);
        }

        void DrawTileWire(AutoTileMap tileMap)
        {
            if (!IsVaildPerTileSize(tileMap))
                return;
            Vector2Int colAndrows = GetTileCellCount(tileMap);
            Vector3 leftTop = GetLeftTop(tileMap);
            var oldColor = Handles.color;
            try
            {
                Handles.color = Color.yellow;
                for (int r = 0; r < colAndrows.y; ++r)
                {
                    for (int c = 0; c < colAndrows.x; ++c)
                    {
                        Vector3 drawPos = leftTop + new Vector3(c * tileMap.m_PerTileSize.x + tileMap.m_PerTileSize.x / 2.0f, 0, r * tileMap.m_PerTileSize.y + tileMap.m_PerTileSize.y / 2.0f);
                        Handles.DrawWireCube(drawPos, new Vector3(tileMap.m_PerTileSize.x, 1.0f, tileMap.m_PerTileSize.y));
                    }
                }
            } finally
            {
                Handles.color = oldColor;
            }
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnMouseInputUpdate;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnMouseInputUpdate;
        }

        private static Vector2 m_CurrentMousePosition;
        private static Ray m_CurrentMouseRay;
        private static Vector3 m_CurrentCameraPosition;
        private static Vector3 m_TileMousePos;
        private Mesh m_MouseMesh = null;
        private Material m_MouseMaterial = null;

        void AddSubTileMesh(int row, int col, AutoTileMap tileMap, Vector3[] allVertexs, Vector2[] allTexcoords, int[] allIndexs, int startVertIndex = 0, int startIndexIndex = 0)
        {
            if (!IsVaildPerTileSize(tileMap))
                return;
            Vector3 startPos = new Vector3(-tileMap.m_PerTileSize.x, 0, -tileMap.m_PerTileSize.y);
            float x = tileMap.m_PerTileSize.x * col;
            float z = tileMap.m_PerTileSize.y * row;
            allVertexs[startVertIndex + 0] = new Vector3(x, 0, z) + startPos;
            allVertexs[startVertIndex + 1] = new Vector3(x + tileMap.m_PerTileSize.x, 0, z) + startPos;
            allVertexs[startVertIndex + 2] = new Vector3(x + tileMap.m_PerTileSize.x, 0, z + tileMap.m_PerTileSize.y) + startPos;
            allVertexs[startVertIndex + 3] = new Vector3(x, 0, z + tileMap.m_PerTileSize.y) + startPos;
            
            if (tileMap.m_TileAsset != null)
            {
                Texture2D tex = tileMap.m_TileAsset.texture;
                if (tex != null)
                {
                    int spriteIndex = spriteNames[row, col];
                    Rect[] spriteRects = GetSpriteDatas(tileMap);
                    Rect spirteRect = spriteRects[spriteIndex];
                    allTexcoords[startVertIndex + 0] = new Vector2(spirteRect.xMin / tex.width, spirteRect.yMax / tex.height);
                    allTexcoords[startVertIndex + 1] = new Vector2(spirteRect.xMax / tex.width, spirteRect.yMax / tex.height);
                    allTexcoords[startVertIndex + 2] = new Vector2(spirteRect.xMax / tex.width, spirteRect.yMin / tex.height);
                    allTexcoords[startVertIndex + 3] = new Vector2(spirteRect.xMin / tex.width, spirteRect.yMin / tex.height);
                }
            }

            allIndexs[startIndexIndex++] = startVertIndex;
            allIndexs[startIndexIndex++] = startVertIndex + 1;
            allIndexs[startIndexIndex++] = startVertIndex + 2;
            allIndexs[startIndexIndex++] = startVertIndex;
            allIndexs[startIndexIndex++] = startVertIndex + 3;
            allIndexs[startIndexIndex++] = startVertIndex + 2;
        }

        private static readonly int[,] spriteNames = new int[2, 2]
                {
                    {4, 8},
                    {1, 2}
                };

        private Mesh GetMouseMesh(AutoTileMap tileMap)
        {
            if (!IsVaildPerTileSize(tileMap))
                return null;

            if (m_MouseMesh == null)
            {
                Vector3[] vecs = new Vector3[4 * 4];
                Vector2[] texcoords = new Vector2[4 * 4];
                
                int[] indexs = new int[6 * 4];
                for (int r = 0; r <= 1; ++r)
                {
                    for (int c = 0; c <= 1; ++c)
                    {
                        int vertIndex = r * 4 * 2 + c * 4;
                        int indexIndex = r * 6 * 2 + c * 6;
                        AddSubTileMesh(r, c, tileMap, vecs, texcoords, indexs, vertIndex, indexIndex);
                    }
                }

                m_MouseMesh = new Mesh();
                m_MouseMesh.vertices = vecs;
                m_MouseMesh.triangles = indexs;
                m_MouseMesh.uv = texcoords;
                m_MouseMesh.UploadMeshData(true);
            }
            return m_MouseMesh;
        }

        void OnMouseInputUpdate(SceneView sceneView)
        {
            m_CurrentMousePosition = Event.current.mousePosition;
            var cam = sceneView.camera;
            if (cam == null)
                return;
            var autoTileMap = this.target as AutoTileMap;
            if (autoTileMap == null)
                return;
            Vector3 targetPos = autoTileMap.transform.position;
            m_CurrentCameraPosition = cam.transform.position;
            m_CurrentMousePosition.y = Screen.height - m_CurrentMousePosition.y - 50;
            m_CurrentMouseRay = cam.ScreenPointToRay(m_CurrentMousePosition);
            Vector3 dir = m_CurrentMouseRay.direction;
            if (Mathf.Abs(dir.y) <= float.Epsilon)
                return;
            float y = targetPos.y;
            float t = (targetPos.y - m_CurrentCameraPosition.y) / dir.y;
            float x = m_CurrentCameraPosition.x + dir.x * t;
            float z = m_CurrentCameraPosition.z + dir.z * t;
            var newMousePt = new Vector3(x, y, z);
            if ((m_TileMousePos - newMousePt).sqrMagnitude <= float.Epsilon)
                return;
            m_TileMousePos = newMousePt;
            sceneView.Repaint();
        }


        private Vector3 m_CurrentTileMousePos;

        bool IsVaildPerTileSize(AutoTileMap tileMap)
        {
            if (tileMap == null || Mathf.Abs(tileMap.m_PerTileSize.x) <= float.Epsilon || Mathf.Abs(tileMap.m_PerTileSize.y) <= float.Epsilon)
                return false;
            return true;
        }

        Bounds GetMousePosBounds(AutoTileMap tileMap)
        {
            Bounds ret = new Bounds(m_TileMousePos, new Vector3(tileMap.m_PerTileSize.x * 2, 1.0f, tileMap.m_PerTileSize.y * 2));
            return ret;
        }

        void DrawTileMouse(AutoTileMap tileMap)
        {
            if (!IsVaildPerTileSize(tileMap))
                return;
           // Debug.Log(m_TileMousePos);
            Handles.DrawWireCube(m_TileMousePos, new Vector3(tileMap.m_PerTileSize.x * 2, 1.0f, tileMap.m_PerTileSize.y * 2));
            Mesh mouseTile = GetMouseMesh(tileMap);
            var mat = GetMouseMeshMaterial(tileMap);
            if (mat == null)
                Graphics.DrawMeshNow(mouseTile, m_TileMousePos, Quaternion.identity);
            else
            {
                //Graphics.DrawMesh(mouseTile, m_TileMousePos, Quaternion.identity, tileMap.m_EditorMaterial, 0);
                mat.SetPass(0);
                Graphics.DrawMeshNow(mouseTile, m_TileMousePos, Quaternion.identity);
            }
        }

        private Rect[] m_SpriteDatas = null;

        private static readonly Vector2 _cDrawStartPt = new Vector2(20, 100);

        void DrawSpriteData(int index, Texture tex, int row, int col, Rect lastDrawRect)
        {
            if (m_SpriteDatas == null || m_SpriteDatas.Length <= 0 || index < 0 || index >= m_SpriteDatas.Length)
                return;
            var r = m_SpriteDatas[index];
            float left = col * r.width;
            float top = row * r.height;
            Rect drawRect = new Rect(_cDrawStartPt.x + left, _cDrawStartPt.y + top, r.width, r.height);
            Rect uvRect = new Rect(r.xMin / tex.width, r.yMin / tex.height, r.width / tex.width, r.height / tex.height);
            GUI.DrawTextureWithTexCoords(drawRect, tex, uvRect);

            var oldColor = GUI.contentColor;
            GUI.contentColor = Color.black;
            GUI.Label(drawRect, index.ToString());
            GUI.contentColor = oldColor;
        }

        Rect[] GetSpriteDatas(AutoTileMap tileMap)
        {
            if (m_SpriteDatas == null && tileMap.m_TileAsset != null)
            {
                if (tileMap.m_TileAsset != null)
                {
                    string path = AssetDatabase.GetAssetPath(tileMap.m_TileAsset);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var importer = TextureImporter.GetAtPath(path) as TextureImporter;
                        var metaDatas = importer.spritesheet;
                        if (metaDatas != null && metaDatas.Length > 0)
                        {
                            m_SpriteDatas = new Rect[metaDatas.Length];
                            foreach (var r in metaDatas)
                            {
                                int idx = int.Parse(r.name);
                                m_SpriteDatas[idx] = r.rect;
                            }
                        }
                    }
                }
            }
            return m_SpriteDatas;
        }

        void ResetSprite()
        {
            m_SpriteDatas = null;
        }

        public override void OnInspectorGUI()
        {
            // base.OnInspectorGUI();
            var tileMap = this.target as AutoTileMap;
            if (tileMap == null)
                return;
            tileMap.m_AutoTileSize = EditorGUILayout.Vector2Field("地图大小", tileMap.m_AutoTileSize);
            tileMap.m_PerTileSize = EditorGUILayout.Vector2Field("瓦片大小", tileMap.m_PerTileSize);
            tileMap.m_EditorMaterial = EditorGUILayout.ObjectField("编辑器Tile材质", tileMap.m_EditorMaterial, typeof(Material), false) as Material;
            var newSprite = EditorGUILayout.ObjectField("瓦片资源", tileMap.m_TileAsset, typeof(Sprite), false) as Sprite;
            if (tileMap.m_TileAsset != newSprite)
            {
                tileMap.m_TileAsset = newSprite;
                ResetSprite();
            }
            if (tileMap.m_TileAsset != null)
            {
                var texture = tileMap.m_TileAsset.texture;
                if (texture != null)
                {
                    var lastRect = GUILayoutUtility.GetLastRect();
                    var Rects = GetSpriteDatas(tileMap);
                    DrawSpriteData(4, texture, 0, 0, lastRect);
                    DrawSpriteData(12, texture, 0, 1, lastRect);
                    DrawSpriteData(8, texture, 0, 2, lastRect);
                    DrawSpriteData(5, texture, 1, 0, lastRect);
                    DrawSpriteData(15, texture, 1, 1, lastRect);
                    DrawSpriteData(10, texture, 1, 2, lastRect);
                    DrawSpriteData(1, texture, 2, 0, lastRect);
                    DrawSpriteData(3, texture, 2, 1, lastRect);
                    DrawSpriteData(2, texture, 2, 2, lastRect);

                    DrawSpriteData(6, texture, 0, 4, lastRect);
                    DrawSpriteData(9, texture, 0, 5, lastRect);

                    DrawSpriteData(11, texture, 0, 7, lastRect);
                    DrawSpriteData(7, texture, 0, 8, lastRect);
                    DrawSpriteData(14, texture, 1, 7, lastRect);
                    DrawSpriteData(13, texture, 1, 8, lastRect);

                    EditorGUILayout.GetControlRect(GUILayout.Height(64 * 3));
                }
            }
        }

        Material GetMouseMeshMaterial(AutoTileMap tileMap)
        {
            if (m_MouseMaterial == null && tileMap != null && tileMap.m_EditorMaterial != null)
            {
                m_MouseMaterial = GameObject.Instantiate<Material>(tileMap.m_EditorMaterial);
            }
            return m_MouseMaterial;
        }

        private void OnDestroy()
        {
            if (m_MouseMesh != null)
            {
                ResourceMgr.Instance.DestroyObject(m_MouseMesh);
                m_MouseMesh = null;
            }

            if (m_MouseMaterial != null)
            {
                ResourceMgr.Instance.DestroyObject(m_MouseMaterial);
                m_MouseMaterial = null;
            }
        }
    }
}
