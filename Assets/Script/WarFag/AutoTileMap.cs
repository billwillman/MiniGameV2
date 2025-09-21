using Autodesk.Fbx;
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

        byte GetCellValue(int r, int c, byte[,] customCells = null)
        {
            if (customCells == null)
                customCells = GetTileMapCells();
            if (customCells == null)
                return 0;
            if (r < 0 || c < 0 || r >= customCells.GetLength(0) || c >= customCells.GetLength(0))
                return 0;
            return customCells[r, c];
        }

        bool CompareCellValue(int r, int c, byte[,] customCells, params int[] values)
        {
            if (values == null)
                return false;
            byte value = GetCellValue(r, c, customCells);
            foreach (var v in values)
            {
                if (v == value)
                    return true;
            }
            return false;
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

            byte[,] tempCells = new byte[brush.height + 1, brush.width + 1];

            Func<int, int, bool> isVaild_15 = (globalR, globalC) =>
            {
                bool ret = CompareCellValue(globalR, globalC - 1, null, 5, 7, 13, 15) && CompareCellValue(globalR + 1, globalC, null, 3, 12, 13, 14, 15) &&
                    CompareCellValue(globalR, globalC + 1, null, 10, 11, 14, 15) && CompareCellValue(globalR - 1, globalC, null, 3, 7, 11, 15) &&
                    CompareCellValue(globalR - 1, globalC - 1, null, 1, 3, 5, 7, 9, 11, 13, 15) && CompareCellValue(globalR + 1, globalC - 1, null, 4, 5, 6, 7, 12, 13, 14, 15) &&
                    CompareCellValue(globalR - 1, globalC + 1, null, 2, 3, 6, 7, 10, 11, 14, 15) && CompareCellValue(globalR + 1, globalC + 1, null, 8, 9, 10, 11, 12, 13, 14, 15);
                return ret;
            };

            for (int r = 0; r <= brush.height / 2.0f; ++r)
            {
                if (r >= cells.GetLength(0))
                    break;
                for (int c = 0; c <= brush.width / 2.0f; ++c)
                {
                    if (c >= cells.GetLength(1))
                        break;
                    // 1. 缓存之前的值
                    for (int i = 0; i < 2; ++i)
                    {
                        for (int j = 0; j < 2; ++j)
                        {
                            int globalR = r + i + brush.min.y;
                            int globalC = c + j + brush.min.x;

                            tempCells[i, j] = cells[globalR, globalC]; ;


                        }
                    }

                    // 设置值
                    for (int i = 0; i < 2; ++i)
                    {
                        for (int j = 0; j < 2; ++j)
                        {
                            int globalR = r + i + brush.min.y;
                            int globalC = c + j + brush.min.x;
                            int targetValue = cells[globalR, globalC];

                            int curValue = spriteNames[i, j];
                            targetValue += curValue;
                            targetValue = Math.Min(15, targetValue);
                            cells[r + i + brush.min.y, c + j + brush.min.x] = (byte)targetValue;
                        }
                    }

                    // 3.检查值有效性
                    for (int i = 0; i < 2; ++i)
                    {
                        for (int j = 0; j < 2; ++j)
                        {
                            int globalR = r + i + brush.min.y;
                            int globalC = c + j + brush.min.x;
                            int targetValue = cells[globalR, globalC];
                            bool isVaild = false;

                            if (targetValue == 0)
                                isVaild = true;
                            else
                            {
                                /*
                                if (GetCellValue(globalR, globalC - 1) != 0 && GetCellValue(globalR + 1, globalC) != 0 &&
                                        GetCellValue(globalR, globalC + 1) != 0 && GetCellValue(globalR - 1, globalC) != 0 &&
                                        GetCellValue(globalR - 1, globalC - 1) != 0 && GetCellValue(globalR + 1, globalC - 1) != 0 &&
                                        GetCellValue(globalR - 1, globalC + 1) != 0 && GetCellValue(globalR + 1, globalC + 1) != 0)
                                    targetValue = 15;
                                */

                                if (targetValue == 4)
                                {
                                    if (CompareCellValue(globalR, globalC - 1, null, 0, 8, 2, 10) && CompareCellValue(globalR + 1, globalC, null, 0, 1, 2, 3) &&
                                        CompareCellValue(globalR, globalC + 1, null, 8, 9, 12, 13) && CompareCellValue(globalR - 1, globalC, null, 1, 5, 9, 13) &&
                                        CompareCellValue(globalR - 1, globalC + 1, null, 2, 6, 3, 10, 14, 15))
                                        isVaild = true;
                                } else if (targetValue == 8)
                                {
                                    if (CompareCellValue(globalR + 1, globalC, null, 0, 1, 2, 3) && CompareCellValue(globalR, globalC + 1, null, 0, 1, 4, 5) &&
                                        CompareCellValue(globalR, globalC - 1, null, 4, 6, 12, 14) && CompareCellValue(globalR - 1, globalC, null, 2, 6, 10, 14) &&
                                        CompareCellValue(globalR - 1, globalC - 1, null, 1, 3, 5, 7, 9, 15))
                                        isVaild = true;
                                } else if (targetValue == 1)
                                {
                                    if (CompareCellValue(globalR, globalC - 1, null, 0, 8, 2, 10) && CompareCellValue(globalR - 1, globalC, null, 0, 4, 8, 12) &&
                                        CompareCellValue(globalR, globalC + 1, null, 2, 3, 6, 7) && CompareCellValue(globalR + 1, globalC, null, 4, 5, 6, 7) &&
                                        CompareCellValue(globalR + 1, globalC + 1, null, 8, 9, 10, 12, 13, 15))
                                        isVaild = true;
                                } else if (targetValue == 2)
                                {
                                    if (CompareCellValue(globalR, globalC + 1, null, 0, 4, 1, 5) && CompareCellValue(globalR - 1, globalC, null, 0, 4, 8, 12) &&
                                        CompareCellValue(globalR, globalC - 1, null, 1, 3, 9, 11) && CompareCellValue(globalR + 1, globalC, null, 8, 9, 10, 11) &&
                                        CompareCellValue(globalR + 1, globalC - 1, null, 4, 5, 6, 12, 14, 15))
                                        isVaild = true;
                                } else if (targetValue == 12)
                                {
                                    if (CompareCellValue(globalR + 1, globalC, null, 0, 1, 2, 3) &&
                                        CompareCellValue(globalR, globalC - 1, null, 4, 6, 12, 14) && CompareCellValue(globalR, globalC + 1, null, 8, 9, 12, 13) &&
                                        CompareCellValue(globalR - 1, globalC, null, 3, 7, 11, 15) &&
                                        CompareCellValue(globalR - 1, globalC - 1, null, 1, 3, 5, 7, 9, 11, 13, 15) && CompareCellValue(globalR - 1, globalC + 1, null, 2, 3, 6, 7, 10, 11, 14, 15))
                                        isVaild = true;
                                } else if (targetValue == 3)
                                {
                                    if (CompareCellValue(globalR - 1, globalC, null, 0, 4, 8, 12) &&
                                        CompareCellValue(globalR, globalC - 1, null, 1, 3, 9, 11) && CompareCellValue(globalR, globalC + 1, null, 2, 3, 6, 7) &&
                                        CompareCellValue(globalR + 1, globalC, null, 12, 13, 14, 15) &&
                                        CompareCellValue(globalR + 1, globalC + 1, null, 8, 9, 10, 11, 12, 13, 14, 15) && CompareCellValue(globalR + 1, globalC - 1, null, 4, 5, 6, 7, 12, 13, 14, 15))
                                        isVaild = true;
                                } else if (targetValue == 5)
                                {
                                    if (CompareCellValue(globalR, globalC - 1, null, 0, 2, 8, 10) &&
                                        CompareCellValue(globalR - 1, globalC, null, 1, 3, 5, 13) && CompareCellValue(globalR + 1, globalC, null, 4, 5, 6, 7) &&
                                        CompareCellValue(globalR, globalC + 1, null, 10, 11, 14, 15) &&
                                        CompareCellValue(globalR + 1, globalC + 1, null, 8, 9, 10, 11, 12, 13, 14, 15) && CompareCellValue(globalR - 1, globalC + 1, null, 2, 3, 6, 7, 10, 11, 14, 15))
                                        isVaild = true;
                                } else if (targetValue == 10)
                                {
                                    if (CompareCellValue(globalR, globalC + 1, null, 0, 1, 4, 5) &&
                                        CompareCellValue(globalR - 1, globalC, null, 2, 6, 10, 14) && CompareCellValue(globalR + 1, globalC, null, 8, 9, 10, 11) &&
                                        CompareCellValue(globalR, globalC - 1, null, 5, 7, 13, 15) &&
                                        CompareCellValue(globalR - 1, globalC - 1, null, 1, 3, 5, 7, 9, 11, 13, 15) && CompareCellValue(globalR + 1, globalC - 1, null, 4, 5, 6, 7, 12, 13, 14, 15))
                                        isVaild = true;
                                } else if (targetValue == 15)
                                {
                                    if (isVaild_15(globalR, globalC))
                                        isVaild = true;
                                } else if (targetValue == 14)
                                {
                                    if (CompareCellValue(globalR + 1, globalC, null, 8, 9, 10, 11) && CompareCellValue(globalR - 1, globalC, null, 3, 7, 11, 15) && 
                                        CompareCellValue(globalR, globalC - 1, null, 5, 7, 13, 15) && CompareCellValue(globalR, globalC + 1, null, 8, 9, 12, 13) &&
                                        CompareCellValue(globalR - 1, globalC - 1, null, 1, 3, 5, 7, 9, 11, 13, 15) && CompareCellValue(globalR + 1, globalC - 1, null, 4, 5, 6, 7, 12, 13, 14, 15) && CompareCellValue(globalR - 1, globalC + 1, null, 2, 3, 6, 7, 10, 11, 14, 15))
                                        isVaild = true;
                                }
                                /*else if (targetValue == 6)
                                {
                                    if (CompareCellValue(globalR - 1, globalC - 1, null, 0, 2, 4, 8) && CompareCellValue(globalR + 1, globalC + 1, null, 0, 1, 2, 4) &&
                                        GetCellValue(globalR + 1, globalC - 1) != 0 && GetCellValue(globalR - 1, globalC + 1) != 0)
                                        isVaild = true;
                                } else if (targetValue == 9)
                                {
                                    if (CompareCellValue(globalR + 1, globalC - 1, null, 0, 1, 2, 8) && CompareCellValue(globalR - 1, globalC + 1, null, 0, 4) &&
                                        GetCellValue(globalR + 1, globalC + 1) != 0 && GetCellValue(globalR - 1, globalC - 1) != 0)
                                        isVaild = true;
                                } else if (targetValue == 11)
                                {
                                    if (CompareCellValue(globalR - 1, globalC + 1, null, 0, 4) &&
                                        GetCellValue(globalR + 1, globalC) != 0 && GetCellValue(globalR, globalC - 1) != 0 &&
                                        GetCellValue(globalR - 1, globalC) != 0 && GetCellValue(globalR + 1, globalC - 1) != 0)
                                        isVaild = true;
                                } else if (targetValue == 7)
                                {
                                    if (CompareCellValue(globalR - 1, globalC - 1, null, 0, 8) &&
                                        GetCellValue(globalR + 1, globalC) != 0 && GetCellValue(globalR, globalC + 1) != 0 &&
                                        GetCellValue(globalR - 1, globalC) != 0 && GetCellValue(globalR + 1, globalC + 1) != 0)
                                        isVaild = true;
                                }  else if (targetValue == 13)
                                {
                                    if (CompareCellValue(globalR + 1, globalC - 1, null, 0, 2) &&
                                        GetCellValue(globalR + 1, globalC) != 0 && GetCellValue(globalR, globalC + 1) != 0 &&
                                        GetCellValue(globalR - 1, globalC) != 0 && GetCellValue(globalR - 1, globalC + 1) != 0)
                                        isVaild = true;
                                }*/
                                else
                                    isVaild = true;
                            }

                            if (!isVaild)
                                cells[globalR, globalC] = tempCells[i, j];
                        }
                    }
                }
            }

            for (int r = 0; r <= brush.height / 2.0f; ++r)
            {
                if (r >= cells.GetLength(0))
                    break;
                for (int c = 0; c <= brush.width / 2.0f; ++c)
                {
                    if (c >= cells.GetLength(1))
                        break;
                    // 如果有0则填充对应的值
                    for (int i = 0; i < 2; ++i)
                    {
                        for (int j = 0; j < 2; ++j)
                        {
                            int globalR = r + i + brush.min.y;
                            int globalC = c + j + brush.min.x;
                            int targetValue = cells[globalR, globalC];
                            if (targetValue == 0)
                            {
                                int curValue = spriteNames[i, j];
                                cells[globalR, globalC] = (byte)curValue;
                            } else if (isVaild_15(globalR, globalC))
                            {
                                cells[globalR, globalC] = 15;
                            }
                        }
                    }
                    //---
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
