using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AutoMap
{
    public class AutoTileMap : MonoBehaviour
    {
        public Vector2 m_AutoTileSize;
        public Vector2 m_PerTileSize;
        public Sprite m_TileAsset;
        public Material m_EditorMaterial;
        // 运行时数据
        private byte[,] m_TileMapCells = null; // 格子数据

        public Vector2Int GetTileCellCount()
        {
            if (!this.IsVaildPerTileSize())
                return new Vector2Int();
            int colCnt = Mathf.CeilToInt(this.m_AutoTileSize.x / this.m_PerTileSize.x);
            int rowCnt = Mathf.CeilToInt(this.m_AutoTileSize.y / this.m_PerTileSize.y);
            return new Vector2Int(colCnt, rowCnt);
        }

        public byte[,] GetTileMapCells()
        {
#if UNITY_EDITOR
            var colAndrowCnt = GetTileCellCount();
            if (m_TileMapCells != null)
            {
                if (colAndrowCnt.y != m_TileMapCells.GetLength(0))
                {
                    m_TileMapCells = null;
                } else if (colAndrowCnt.x != m_TileMapCells.GetLength(1))
                {
                    m_TileMapCells = null;
                }
            }
#endif
            if (m_TileMapCells != null)
                return m_TileMapCells;
            if (!this.IsVaildPerTileSize())
            {
                m_TileMapCells = null;
                return m_TileMapCells;
            }
#if !UNITY_EDITOR
            var colAndrowCnt = GetTileCellCount();
#endif
            if (colAndrowCnt.x > 0 && colAndrowCnt.y > 0)
            {
                m_TileMapCells = new byte[colAndrowCnt.y, colAndrowCnt.x];
            }
            return m_TileMapCells;
        }

        public bool IsVaildAutoTileSize()
        {
            if (this.m_AutoTileSize.x < 0 || Mathf.Abs(this.m_AutoTileSize.x) <= float.Epsilon || this.m_AutoTileSize.y < 0 || Mathf.Abs(this.m_AutoTileSize.y) <= float.Epsilon)
                return false;
            return true;
        }

        public bool IsVaildPerTileSize()
        {
            if (Mathf.Abs(this.m_PerTileSize.x) <= float.Epsilon || Mathf.Abs(this.m_PerTileSize.y) <= float.Epsilon)
                return false;
            return true;
        }

        public Vector3 GetLeftTop()
        {
            Vector3 ret = this.transform.position;
            ret.x -= this.m_AutoTileSize.x / 2.0f;
            ret.z -= this.m_AutoTileSize.y / 2.0f;
            return ret;
        }

        byte GetCellValue(int r, int c)
        {
            var cells = GetTileMapCells();
            if (cells == null)
                return 0;
            if (r < 0 || c < 0 || r >= cells.GetLength(0) || c >= cells.GetLength(0))
                return 0;
            return cells[r, c];
        }

        public void BrushTile2(RectInt brush)
        {
            Vector2Int wh = new Vector2Int(brush.max.x - brush.min.x, brush.max.y - brush.min.y);
            if (wh.x < 2 || wh.y < 2)
                return;
            var cells = GetTileMapCells();
            if (cells == null)
                return;
            Debug.Log(brush);
            Debug.LogFormat("min：{0}, max：{1}", brush.min.ToString(), brush.max.ToString());

            for (int r = 0; r <= brush.height / 2.0f; ++r)
            {
                if (r >= cells.GetLength(0))
                    break;
                for (int c = 0; c <= brush.width / 2.0f; ++c)
                {
                    if (c >= cells.GetLength(1))
                        break;
                    for (int i = 0; i < 2; ++i)
                    {
                        for (int j = 0; j < 2; ++j)
                        {
                            bool isCanBrush = false;
                            int globalR = r + i + brush.min.y;
                            int globalC = c + j + brush.min.x;
                            int targetValue = cells[globalR, globalC];
                            
                            int curValue = spriteNames[i, j];
                            targetValue += curValue;
                            targetValue = Math.Min(15, targetValue);
                            if (targetValue == 0)
                            {
                                isCanBrush = true;
                            } else if (targetValue == 4)
                            {
                                isCanBrush = (GetCellValue(globalR + 1, globalC) == 0 && GetCellValue(globalR, globalC - 1) == 0);
                            } else if (targetValue == 8)
                            {
                                isCanBrush = (GetCellValue(globalR + 1, globalC) == 0 && GetCellValue(globalR, globalC + 1) == 0);
                            } else if (targetValue == 1)
                            {
                                isCanBrush = (GetCellValue(globalR - 1, globalC) == 0 && GetCellValue(globalR, globalC - 1) == 0);
                            } else if (targetValue == 2)
                            {
                                isCanBrush = (GetCellValue(globalR - 1, globalC) == 0 && GetCellValue(globalR, globalC + 1) == 0);
                            } else if (targetValue == 3)
                            {
                                int downValue = GetCellValue(globalR - 1, globalC);
                                isCanBrush = (downValue == 0 || downValue == 4 || downValue == 12 || downValue == 8) && (GetCellValue(globalR, globalC + 1) != 0) 
                                        && GetCellValue(globalR, globalC - 1) != 0;
                            } else if (targetValue == 12)
                            {
                                int upValue = GetCellValue(globalR - 1, globalC);
                                isCanBrush = (upValue == 0 || upValue == 1 || upValue == 3 || upValue == 2) && (GetCellValue(globalR, globalC + 1) != 0)
                                        && GetCellValue(globalR, globalC - 1) != 0;
                            } else if (targetValue == 6)
                            {

                            }
                            

                            if (isCanBrush)
                            {
                                
                                cells[r + i + brush.min.y, c + j + brush.min.x] = (byte)targetValue;
                            }
                        }
                    }
                
                }
            }
        }

        // 刷子
        public void BrushTile(RectInt brush)
        {
            Vector2Int wh = new Vector2Int(brush.max.x - brush.min.x, brush.max.y - brush.min.y);
            if (wh.x < 2 || wh.y < 2)
                return;
            var cells = GetTileMapCells();
            if (cells == null)
                return;
            Debug.Log(brush);
            Debug.LogFormat("min：{0}, max：{1}", brush.min.ToString(), brush.max.ToString());

            /*
            byte[,] tempCells = new byte[brush.height + 1, brush.width + 1];

            for (int r = 0; r <= brush.height; ++r)
            {
                for (int c = 0; c <= brush.width; ++c)
                {
                    tempCells[r, c] = cells[brush.min.y + r, brush.min.x + c];
                }
            }
            */
            //bool isChanged = false;
            for (int r = 0; r <= brush.height/2.0f; ++r)
            {
                if (r >= cells.GetLength(0))
                    break;
                for (int c = 0; c <= brush.width/2.0f; ++c)
                {
                    if (c >= cells.GetLength(1))
                        break;
                    bool isCanBrush = false;
                    for (int i = 0; i < 2; ++i)
                    {
                        for (int j = 0; j < 2; ++j)
                        {
                            if (cells[r + i + brush.min.y, c + j + brush.min.x] == 0)
                            {
                                isCanBrush = true;
                                break;
                            }
                        }
                    }
                    //---------------------------------------- 新的处理 ----------------------------------
                    if (!isCanBrush)
                    {
                        for (int i = 0; i < 2; ++i)
                        {
                            for (int j = 0; j < 2; ++j)
                            {
                                int targetValue = cells[r + i + brush.min.y, c + j + brush.min.x];
                                if (targetValue == 4 || targetValue == 8 || targetValue == 1 || targetValue == 2)
                                {
                                    int curValue = spriteNames[i, j];
                                    if (targetValue != curValue)
                                    {
                                        isCanBrush = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    // ----------------------------------------------------------------
                    if (isCanBrush)
                    {
                        for (int i = 0; i < 2; ++i)
                        {
                            for (int j = 0; j < 2; ++j)
                            {
                                /*
                                int curValue = spriteNames[i, j] + tempCells[i + r, j + c];
                                curValue = Math.Min(15, curValue);
                                tempCells[i + r, j + c] = (byte)curValue;
                                */
                                // -------------------- 新的计算
                                int targetValue = cells[r + i + brush.min.y, c + j + brush.min.x];
                                int curValue = spriteNames[i, j];
                                targetValue += curValue;
                                targetValue = Math.Min(15, targetValue);
                                cells[r + i + brush.min.y, c + j + brush.min.x] = (byte)targetValue;
                                // ---------------------------
                               // isChanged = true;
                            }
                        }
                    }
                }
            }

            /*
            if (isChanged)
            {
                for (int r = 0; r <= brush.height; ++r)
                {
                    for (int c = 0; c <= brush.width; ++c)
                    {
                        cells[brush.min.y + r, brush.min.x + c] = tempCells[r, c];
                    }
                }
            }
            */
        }

        public void ClearTileCells()
        {
            m_TileMapCells = null;
        }

        public static readonly int[,] spriteNames = new int[2, 2]
                {
                    {1, 2},
                    {4, 8}
                };

    }
}
