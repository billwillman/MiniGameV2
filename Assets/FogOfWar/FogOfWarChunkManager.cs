using UnityEngine;
using System.Collections.Generic;

namespace FoW
{
    class FogOfWarChunk
    {
        public Vector3Int coordinate;
        public byte[] visibleData;
        public byte[] partialData;
    }

    [AddComponentMenu("FogOfWar/FogOfWarChunkManager")]
    [RequireComponent(typeof(FogOfWarTeam))]
    public class FogOfWarChunkManager : MonoBehaviour
    {
        [Tooltip("The Transform to follow that will trigger chunks to be swapped out.")]
        public Transform followTransform;
        [Tooltip("If true, chunks will be saved to memory and loaded back in when returning to that chunk.")]
        public bool rememberFog = true;
        [Tooltip("The size of an individual chunk. This will override the mapSize value on the FogOfWarTeam component.")]
        public float verticalChunkSize = 10;
        [Tooltip("The current vertical player of the fog.")]
        public float verticalChunkOffset = 0;
        [Tooltip("The offset of the map in world units. This will override the mapOffset value on the FogOfWarTeam component.")]
        public Vector2 mapOffset = Vector2.zero;
        const byte _version = 0;

        List<FogOfWarChunk> _chunks = new List<FogOfWarChunk>();
        public int loadedChunkCount { get { return _chunks.Count; } }
        Vector3Int _loadedChunk;

        public FogOfWarTeam team { get; private set; } = null;
        int _mapResolution;
        int _valuesPerMap { get { return _mapResolution * _mapResolution; } }
        int _valuesPerChunk { get { return _valuesPerMap / 4; } }
        Vector3Int _followChunk
        {
            get
            {
                Vector3 fogpos = FogOfWarConversion.WorldToFogPlane3(followTransform.position, team.plane);

                float halfchunksize = team.mapSize * 0.25f;
                fogpos.x += halfchunksize;
                fogpos.y += halfchunksize;

                Vector3 worldpos = FogOfWarConversion.FogPlaneToWorld(fogpos, team.plane);

                return WorldPositionToChunk(worldpos);
            }
        }

        void Start()
        {
            team = GetComponent<FogOfWarTeam>();
            if (team.mapResolution.x != team.mapResolution.y)
            {
                Debug.LogError("FogOfWarChunkManager requires FogOfWar Map Resolution to be square and a power of 2!");
                enabled = false;
                return;
            }

            _mapResolution = team.mapResolution.x;
            team.onRenderFogTexture.AddListener(OnRenderFog);

            ForceLoad();
        }

        /// <summary>
        /// Converts a world position to the chunk index at that point.
        /// </summary>
        public Vector3Int WorldPositionToChunk(Vector3 pos)
        {
            Vector3 fogpos = FogOfWarConversion.WorldToFogPlane3(pos, team.plane);

            float halfmapsize = team.mapSize * 0.5f;
            float halfchunksize = halfmapsize * 0.5f;
            Vector3Int chunk = new Vector3Int(
                Mathf.RoundToInt((fogpos.x - mapOffset.x + halfchunksize) / halfmapsize) - 1,
                Mathf.RoundToInt((fogpos.y - mapOffset.y + halfchunksize) / halfmapsize) - 1,
                Mathf.FloorToInt((fogpos.z - verticalChunkOffset) / verticalChunkSize)
            );
            if (fogpos.z - verticalChunkOffset < 0)
                --chunk.z;

            return chunk;
        }

        /// <summary>
        /// Returns the start corner (min point on all axes) for the specified chunk index.
        /// </summary>
        public Vector3 ChunkCornerToWorldPositionCorrect(Vector3Int pos)
        {
            float halfmapsize = team.mapSize * 0.5f;
            return new Vector3(
                halfmapsize * pos.x,
                halfmapsize * pos.y,
                pos.z * verticalChunkSize + verticalChunkOffset
                );
        }

        bool IsChunkLoaded(Vector3Int coord)
        {
            if (coord.z != _loadedChunk.z)
                return false;

            if (coord.x < _loadedChunk.x - 1 || coord.x > _loadedChunk.x + 1)
                return false;

            if (coord.y < _loadedChunk.y - 1 || coord.y > _loadedChunk.y + 1)
                return false;

            return true;
        }

        /// <summary>
        /// Same as FogOfWarTeam.GetFogValue(), but can pull value from unloaded chunk data.
        /// </summary>
        public byte GetFogValue(FogOfWarValueType type, Vector3 pos)
        {
            Vector3Int chunkid = WorldPositionToChunk(pos);

            if (IsChunkLoaded(chunkid))
                return team.GetFogValue(type, pos);

            FogOfWarChunk chunk = FindChunk(chunkid);

            if (chunk == null)
                return (byte)255;

            Vector2 chunkworldcorner = ChunkCornerToWorldPositionCorrect(chunkid);
            Vector2 localpos = new Vector2(pos.x, pos.y) - chunkworldcorner;

            Debug.DrawLine(pos, chunkworldcorner, Color.blue);

            int x = Mathf.FloorToInt(localpos.x * team.mapResolution.x / team.mapSize);
            int y = Mathf.FloorToInt(localpos.y * team.mapResolution.y / team.mapSize);

            return chunk.partialData[y * (team.mapResolution.x / 2) + x];
        }

        FogOfWarChunk FindChunk(Vector3Int id)
        {
            for (int i = 0; i < _chunks.Count; ++i)
            {
                if (_chunks[i].coordinate == id)
                    return _chunks[i];
            }
            return null;
        }

        void SaveChunk(byte[] visibledata, byte[] partialdata, int xc, int yc)
        {
            // reuse chunk if it already exists
            Vector3Int coordinate = _loadedChunk + new Vector3Int(xc, yc, 0);
            FogOfWarChunk chunk = FindChunk(coordinate);
            if (chunk == null)
            {
                chunk = new FogOfWarChunk()
                {
                    coordinate = coordinate,
                    visibleData = new byte[_valuesPerChunk],
                    partialData = new byte[_valuesPerChunk]
                };
                _chunks.Add(chunk);
            }
            else
            {
                if (chunk.visibleData == null || chunk.visibleData.Length != _valuesPerChunk)
                    chunk.visibleData = new byte[_valuesPerChunk];
                if (chunk.partialData == null || chunk.partialData.Length != _valuesPerChunk)
                    chunk.partialData = new byte[_valuesPerChunk];
            }

            int halfmapsize = _mapResolution / 2;
            int xstart = halfmapsize * xc;
            int ystart = halfmapsize * yc;

            // copy values
            for (int y = 0; y < halfmapsize; ++y)
            {
                System.Array.Copy(visibledata, (ystart + y) * _mapResolution + xstart, chunk.visibleData, y * halfmapsize, halfmapsize);
                System.Array.Copy(partialdata, (ystart + y) * _mapResolution + xstart, chunk.partialData, y * halfmapsize, halfmapsize);
            }
        }

        void SaveChunks()
        {
            // save all visible chunks
            byte[] visibledata = new byte[_valuesPerMap];
            team.GetFogValues(FogOfWarValueType.Visible, visibledata);

            byte[] partialdata = new byte[_valuesPerMap];
            team.GetFogValues(FogOfWarValueType.Partial, partialdata);

            for (int y = 0; y < 2; ++y)
            {
                for (int x = 0; x < 2; ++x)
                    SaveChunk(visibledata, partialdata, x, y);
            }
        }

        void LoadChunk(byte[] visibledata, byte[] partialdata, int xc, int yc)
        {
            // only load if the chunk exists
            Vector3Int coordinate = _loadedChunk + new Vector3Int(xc, yc, 0);
            FogOfWarChunk chunk = FindChunk(coordinate);
            if (chunk == null || chunk.partialData == null || chunk.partialData.Length != _valuesPerChunk)
                return;

            int halfmapsize = _mapResolution / 2;
            int xstart = halfmapsize * xc;
            int ystart = halfmapsize * yc;

            // copy values
            for (int y = 0; y < halfmapsize; ++y)
            {
                System.Array.Copy(chunk.visibleData, y * halfmapsize, visibledata, (ystart + y) * _mapResolution + xstart, halfmapsize);
                System.Array.Copy(chunk.partialData, y * halfmapsize, partialdata, (ystart + y) * _mapResolution + xstart, halfmapsize);
            }
        }

        void LoadChunks()
        {
            byte[] visibledata = new byte[_valuesPerMap];
            byte[] partialdata = new byte[_valuesPerMap];

            // set fog full by default
            for (int i = 0; i < visibledata.Length; ++i)
            {
                visibledata[i] = 255;
                partialdata[i] = 255;
            }

            // load each visible chunk
            for (int y = 0; y < 2; ++y)
            {
                for (int x = 0; x < 2; ++x)
                    LoadChunk(visibledata, partialdata, x, y);
            }

            // put the new map into fow
            team.SetFogValues(FogOfWarValueType.Visible, visibledata);
            team.SetFogValues(FogOfWarValueType.Partial, partialdata);

            team.ManualUpdate(team.deltaTime);
        }

        void ForceLoad()
        {
            if (followTransform == null)
                return;

            Vector3Int desiredchunk = _followChunk;

            // move fow
            float chunksize = team.mapSize * 0.5f;
            team.mapOffset = new Vector2(desiredchunk.x, desiredchunk.y) * chunksize + mapOffset;
            _loadedChunk = desiredchunk;
            team.Reinitialize();

            LoadChunks();
        }

        void OnRenderFog()
        {
            if (followTransform == null)
                return;

            // is fow in the best position?
            if (_followChunk != _loadedChunk)
            {
                // clear memory 
                if (rememberFog)
                    SaveChunks();

                ForceLoad();
            }
        }

        /// <summary>
        /// Removes all cached chunk data.
        /// </summary>
        public void Clear()
        {
            _chunks.Clear();
        }

        /// <summary>
        /// Saves all chunk data into a single byte[] that can be loaded at a later time.
        /// </summary>
        /// <returns></returns>
        public byte[] Save()
        {
            try
            {
                System.IO.MemoryStream stream = new System.IO.MemoryStream();
                System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream);

                writer.Write(_version);
                writer.Write(_valuesPerChunk);

                writer.Write(_chunks.Count);
                for (int i = 0; i < _chunks.Count; ++i)
                {
                    FogOfWarChunk chunk = _chunks[i];

                    writer.Write(chunk.coordinate.x);
                    writer.Write(chunk.coordinate.y);
                    writer.Write(chunk.coordinate.z);
                    writer.Write(chunk.partialData.Length);
                    writer.Write(chunk.partialData);
                    writer.Write(chunk.visibleData.Length);
                    writer.Write(chunk.visibleData);
                }

                return stream.ToArray();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(e);
                return null;
            }
        }

        /// <summary>
        /// Load previously saved byte[] of the chunk data.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool Load(byte[] data)
        {
            if (data == null || data.Length == 0)
                return false;

            try
            {
                System.IO.BinaryReader reader = new System.IO.BinaryReader(new System.IO.MemoryStream(data));

                byte version = reader.ReadByte();
                if (version != _version)
                {
                    Debug.LogWarningFormat(this, "Invalid FogOfWarChunkManager version (got {0}, expected {1}).", version, _version);
                    return false;
                }

                int valuesperchunk = reader.ReadInt32();
                if (_valuesPerChunk != valuesperchunk)
                {
                    Debug.LogWarning("FogOfWarChunkManager valuesPerChunk is different. This is probably due to the FogOfWarTeam having a different map resolution.", this);
                    return false;
                }

                int chunkcount = reader.ReadInt32();
                if (chunkcount < 0 || chunkcount > 99999)
                {
                    Debug.LogWarning("FogOfWarChunkManager recieved an invalid chunk count: " + chunkcount.ToString(), this);
                    return false;
                }

                _chunks.Clear();
                while (_chunks.Count < chunkcount)
                {
                    _chunks.Add(new FogOfWarChunk()
                    {
                        coordinate = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
                        partialData = reader.ReadBytes(reader.ReadInt32()),
                        visibleData = reader.ReadBytes(reader.ReadInt32())
                    });
                }

                ForceLoad();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(e);
                return false;
            }
        }
    }
}
