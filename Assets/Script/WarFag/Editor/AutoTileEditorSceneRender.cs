using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;
using NsLib.ResMgr;
using System;
using Unity.VisualScripting.Dependencies.NCalc;
using System.Linq;

namespace AutoMap
{
    // 智能地表编辑器辅助显示（场景中渲染，测试用）
    [CustomEditor(typeof(AutoTileMap))]
    public class AutoTileEditorSceneRender : Editor
    {

        void DrawTileMapArea(AutoTileMap tileMap)
        {
            if (!tileMap.IsVaildAutoTileSize())
                return;
            var worldPos = tileMap.transform.position;
            Handles.DrawWireCube(worldPos, new Vector3(tileMap.m_AutoTileSize.x, 1f, tileMap.m_AutoTileSize.y));
        }

        private void OnSceneGUI()
        {
            var tileMap = this.target as AutoTileMap;
            if (tileMap == null || !tileMap.IsVaildAutoTileSize())
                return;
            // Graphics.ClearRandomWriteTargets();
            //GL.Clear(true, true, Color.black);
            DrawTileMapArea(tileMap);
            DrawTileWire(tileMap);
            DrawTileMapCellsTex(tileMap);
            DrawTileMouse(tileMap);
        }

        void DoBurshTile(AutoTileMap tileMap)
        {
            if (tileMap != null && m_IsWaitBrushTile)
            {
                m_IsWaitBrushTile = false;
                if (!tileMap.IsVaildPerTileSize())
                    return;
                tileMap.BrushTile2(m_MouseBrushColAndRolRect);
            }
        }

        void DrawTileWire(AutoTileMap tileMap)
        {
            if (!tileMap.IsVaildPerTileSize())
                return;
            Vector2Int colAndrows = tileMap.GetTileCellCount();
            Vector3 leftTop = tileMap.GetLeftTop();
            var oldColor = Handles.color;
            try
            {
                m_MouseBrushRect.xMin = float.MaxValue;
                m_MouseBrushRect.yMin = float.MaxValue;
                m_MouseBrushRect.xMax = float.MinValue;
                m_MouseBrushRect.yMax = float.MinValue;

                int minR = int.MaxValue;
                int minC = int.MaxValue;
                int maxR = int.MinValue;
                int maxC = int.MinValue;
                var mouseBounds = GetMousePosBounds(tileMap);
                for (int r = 0; r < colAndrows.y; ++r)
                {
                    for (int c = 0; c < colAndrows.x; ++c)
                    {
                        Vector3 drawPos = leftTop + new Vector3(c * tileMap.m_PerTileSize.x + tileMap.m_PerTileSize.x / 2.0f, 0, r * tileMap.m_PerTileSize.y + tileMap.m_PerTileSize.y / 2.0f);
                        Vector3 drawSize = new Vector3(tileMap.m_PerTileSize.x, 1.0f, tileMap.m_PerTileSize.y);
                        Bounds drawBounds = new Bounds(drawPos, drawSize);
                        if (drawBounds.Intersects(mouseBounds))
                        {
                            Handles.color = Color.red;
                            Handles.DrawWireCube(drawPos, drawSize);
                            float halfW = drawSize.x / 2.0f;
                            float halfH = drawSize.z / 2.0f;
                            m_MouseBrushRect.min = Vector2.Min(m_MouseBrushRect.min, new Vector2(drawPos.x - halfW, drawPos.z - halfH));
                            m_MouseBrushRect.max = Vector2.Max(m_MouseBrushRect.max, new Vector2(drawPos.x + halfW, drawPos.z + halfH));

                            minR = Math.Min(minR, r);
                            minC = Math.Min(minC, c);
                            maxR = Math.Max(maxR, r);
                            maxC = Math.Max(maxC, c);
                        }
                    }
                }
                // Debug.Log(m_MouseBrushRect);
                if (Event.current.alt)
                {
                    double currentTime = EditorApplication.timeSinceStartup;
                    if (currentTime - m_BrushTileTime > 1f)
                    {
                        m_IsWaitBrushTile = true;
                        m_MouseBrushColAndRolRect = new RectInt(new Vector2Int(minC, minR), new Vector2Int(maxC - minC, maxR - minR));
                    }
                    //m_BrushTileTime = currentTime;
                }
                DoBurshTile(tileMap);
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
        private Mesh m_TileMesh = null;
        private Material m_MouseMaterial = null;
        private Rect m_MouseBrushRect;
        private RectInt m_MouseBrushColAndRolRect;
        

        void DrawTileMapCellsTex(AutoTileMap tileMap)
        {
            if (!tileMap.IsVaildPerTileSize())
                return;
            var tileMapCells = tileMap.GetTileMapCells();
            if (tileMapCells == null)
                return;
            Vector3 startPos = tileMap.GetLeftTop();
            var oldColor = Handles.color;
            Mesh tileMesh = GetTileMesh(tileMap);
            var mat = GetMouseMeshMaterial(tileMap);
            var spriteDatas = GetSpriteDatas(tileMap);
            float halfW = tileMap.m_PerTileSize.x / 2.0f;
            float halfH = tileMap.m_PerTileSize.y / 2.0f;
            var tex = tileMap.m_TileAsset != null ? tileMap.m_TileAsset.texture : null;
           
            RenderTexture rtTex = tileMap.m_RtTexture;
            var colorBuffer = Graphics.activeColorBuffer;
            var depthBuffer = Graphics.activeDepthBuffer;

            bool hasRtTex = rtTex != null;
            if (hasRtTex)
            {
                for (int r = 0; r < tileMapCells.GetLength(0); ++r)
                {
                    for (int c = 0; c < tileMapCells.GetLength(1); ++c)
                    {
                        byte id = tileMapCells[r, c];
                        Vector3 drawPos = new Vector3(c * tileMap.m_PerTileSize.x + halfW, 0, r * tileMap.m_PerTileSize.y + halfH);
                        if (id > 0 && tex != null && tileMesh != null && tileMap != null && mat != null && spriteDatas != null && spriteDatas.Length > 0)
                        {
                            var sprite = spriteDatas[id];
                            if (sprite != null)
                            {
                                mat.mainTextureOffset = new Vector2(sprite.xMin / tex.width, sprite.yMin / tex.height);
                                Graphics.Blit(tex, rtTex, mat, 0);
                            }
                        }
                    }
                }
            }


            Graphics.SetRenderTarget(colorBuffer, depthBuffer);


            for (int r = 0; r < tileMapCells.GetLength(0); ++r)
            {
                for (int c = 0; c < tileMapCells.GetLength(1); ++c)
                {
                    byte id = tileMapCells[r, c];
                    
                    Vector3 drawPos = new Vector3(c * tileMap.m_PerTileSize.x + halfW, 0, r * tileMap.m_PerTileSize.y + halfH) + startPos;

                    if (id > 0 && tex != null && tileMesh != null && tileMap != null && mat != null && spriteDatas != null && spriteDatas.Length > 0)
                    {
                        var sprite = spriteDatas[id];
                        if (sprite != null)
                        {
                            mat.mainTextureOffset = new Vector2(sprite.xMin / tex.width, sprite.yMin / tex.height);
                            mat.SetPass(0);
                            Graphics.DrawMeshNow(tileMesh, drawPos + new Vector3(halfW, 0, halfH), Quaternion.identity);
                        }
                    }

                    switch (id)
                    {
                        case 4:
                        case 8:
                        case 1:
                        case 2:
                            {
                                Handles.color = Color.yellow;
                                break;
                            }
                        case 6:
                        case 9:
                            {
                                Handles.color = Color.green;
                                break;
                            }
                        case 7:
                        case 11:
                        case 13:
                        case 14:
                            {
                                Handles.color = Color.blue;
                                break;
                            }
                        default:
                            Handles.color = Color.black;
                            break;
                    }
                    Handles.Label(drawPos, id.ToString());
                }
            }
            Handles.color = oldColor;
        }

        public static readonly int[,] spriteNames = new int[2, 2]
               {
                    {4, 8},
                    {1, 2},
               };

        private int GetSpriteZeroIndex(AutoTileMap tileMap)
        {
            Rect[] spriteRects = GetSpriteDatas(tileMap);
            if (spriteRects != null)
            {
                for (int i = 0; i < spriteRects.Length; ++i)
                {
                    var r = spriteRects[i];
                    if (Mathf.Abs(r.xMin) <= float.Epsilon && Mathf.Abs(r.yMin) <= float.Epsilon)
                        return i;
                }
            }
            return -1;
        }

        void AddSubTileMesh(int row, int col, AutoTileMap tileMap, Vector3[] allVertexs, Vector2[] allTexcoords, int[] allIndexs, int startVertIndex = 0, int startIndexIndex = 0, int customSpriteIndex = -1, bool isFlipY = true)
        {
            if (!tileMap.IsVaildPerTileSize())
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
                    if (customSpriteIndex >= 0)
                        spriteIndex = customSpriteIndex;
                    Rect[] spriteRects = GetSpriteDatas(tileMap);
                    Rect spirteRect = spriteRects[spriteIndex];
                    float topY = isFlipY ? spirteRect.yMax : spirteRect.yMin;
                    float bottomY = isFlipY ? spirteRect.yMin : spirteRect.yMax;
                    allTexcoords[startVertIndex + 0] = new Vector2(spirteRect.xMin / tex.width, topY / tex.height);
                    allTexcoords[startVertIndex + 1] = new Vector2(spirteRect.xMax / tex.width, topY / tex.height);
                    allTexcoords[startVertIndex + 2] = new Vector2(spirteRect.xMax / tex.width, bottomY / tex.height);
                    allTexcoords[startVertIndex + 3] = new Vector2(spirteRect.xMin / tex.width, bottomY / tex.height);
                }
            }

            allIndexs[startIndexIndex++] = startVertIndex;
            allIndexs[startIndexIndex++] = startVertIndex + 1;
            allIndexs[startIndexIndex++] = startVertIndex + 2;
            allIndexs[startIndexIndex++] = startVertIndex;
            allIndexs[startIndexIndex++] = startVertIndex + 3;
            allIndexs[startIndexIndex++] = startVertIndex + 2;
        }

        private Mesh GetTileMesh(AutoTileMap tileMap)
        {
            if (!tileMap.IsVaildPerTileSize())
                return null;
            if (m_TileMesh == null)
            {
                Vector3[] vecs = new Vector3[4];
                Vector2[] texcoords = new Vector2[4];
                int[] indexs = new int[6];
                int index = GetSpriteZeroIndex(tileMap);
                if (index < 0)
                    index = 0;
                AddSubTileMesh(0, 0, tileMap, vecs, texcoords, indexs, 0, 0, index, false);
                m_TileMesh = new Mesh();
                m_TileMesh.vertices = vecs;
                m_TileMesh.triangles = indexs;
                m_TileMesh.uv = texcoords;
                m_TileMesh.UploadMeshData(true);
            }
            return m_TileMesh;
        }

        private Mesh GetMouseMesh(AutoTileMap tileMap)
        {
            if (!tileMap.IsVaildPerTileSize())
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

        private bool m_IsWaitBrushTile = false;
        private double m_BrushTileTime = 0;

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

        Bounds GetMousePosBounds(AutoTileMap tileMap)
        {
            Bounds ret = new Bounds(m_TileMousePos, new Vector3(tileMap.m_PerTileSize.x * 2, 1.0f, tileMap.m_PerTileSize.y * 2));
            return ret;
        }

        void DrawTileMouse(AutoTileMap tileMap)
        {
            if (!tileMap.IsVaildPerTileSize())
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
                mat.mainTextureOffset = Vector2.zero;
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

            var oldColor = GUI.color;
            GUI.color = Color.black;
            GUI.Label(drawRect, index.ToString());
            GUI.color = oldColor;
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
            tileMap.m_RtTexture = EditorGUILayout.ObjectField("RT输出图", tileMap.m_RtTexture, typeof(RenderTexture), false) as RenderTexture;
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

            if (GUILayout.Button("清理Cell数据"))
            {
                tileMap.ClearTileCells();
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
