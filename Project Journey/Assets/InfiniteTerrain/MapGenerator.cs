using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Splines;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode
    {
        NoiseMap,
        ColourMap,
        DrawMesh,
        FalloffMap,
    }

    public DrawMode drawMode;

    public Noise.NormaliseMode normaliseMode;

    public const int mapChunkSize = 239;
    [Range(0,6)] public int editorPreviewLevelOfDetail;
    
    public float noiseScale;
    public int octaves;
    [Range(0,1)] public float persistance;
    public float lacunarity;
    public int seed;
    public Vector2 offset;

    public bool useFalloff;

    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;

    public bool autoUpdate;

    public TerrainType[] regions;

    private float[,] falloffMap;

    private Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();

    private Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    [SerializeField] public RoadManager roadManager;

    [SerializeField] private AnimationCurve roadFalloff;

    [SerializeField] private float furthestPointForFalloff;
    [SerializeField] private float debugValue;

    private void Awake()
    {
        falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
    }

    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);
        
        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.ColourMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromColourMap(mapData.colourMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.DrawMesh)
        {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, editorPreviewLevelOfDetail) , TextureGenerator.TextureFromColourMap(mapData.colourMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunkSize)));
        }
    }

    public void RequestMapData(Vector2 center, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MapDataThread(center, callback);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(Vector2 center, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(center);
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, lod, callback);
        };
        
        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, lod);

        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue((new MapThreadInfo<MeshData>(callback, meshData)));
        }
    }

    private void Update()
    {
        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    private bool RectOverlapCheck(Vector2 firstPosition, Vector2 secondPosition, Vector2 targetPosition)
    {
        var rect = Rect.MinMaxRect(firstPosition.x, firstPosition.y, secondPosition.x, secondPosition.y);

        return rect.Contains(targetPosition, true);
    }

    private bool CheckForOverlap(List<Spline> splineList, Vector2 chunkCornerA, Vector2 chunkCornerB)
    {
        for (int i = 0; i < splineList.Count; i++)
        {
            for (int j = 0; j < splineList[i].Count; j++)
            {
                if (RectOverlapCheck(chunkCornerA, chunkCornerB,
                        new Vector2(splineList[i][j].Position.x, splineList[i][j].Position.z)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    MapData GenerateMapData(Vector2 center)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize + 2, mapChunkSize + 2, seed, noiseScale, octaves, persistance, lacunarity, center + offset, normaliseMode);

        Color[] colourMap = new Color[mapChunkSize * mapChunkSize];
        
        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                /*if (useFalloff)
                {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - falloffMap[x, y]);
                }*/
                
                float currentHeight = noiseMap[x, y];
                    
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                    {
                        colourMap[y * mapChunkSize + x] = regions[i].colour;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, colourMap);
    }

    /*public float GetCurrentHeight(float targetHeight, float meshHeightMultiplier)
    {
        float targetValue = targetHeight / (meshHeightMultiplier * 5f);
        return inverseCurve.Evaluate(targetValue);
    }*/

    private void OnValidate()
    {
        /*if (mapChunkSize < 1)
        {
            mapChunkSize = 1;
        }

        if (mapChunkSize < 1)
        {
            mapChunkSize = 1;
        }*/

        if (lacunarity < 1)
        {
            lacunarity = 1;
        }

        if (octaves < 0)
        {
            octaves = 0;
        }

        falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
    }

    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }

    public float GetMapHeightAtPosition(Vector2 center, AnimationCurve heightCurve, float heightMultiplier) // I.F.
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(1, 1, seed, noiseScale, octaves, persistance, lacunarity, (center / 5) + offset, normaliseMode);
        
        float height = heightCurve.Evaluate(noiseMap[0, 0]) * heightMultiplier;
        
        return height;
    }   
}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color colour;
}

public struct MapData
{
    public readonly float[,] heightMap;
    public readonly Color[] colourMap;

    public MapData(float[,] heightMap, Color[] colourMap)
    {
        this.heightMap = heightMap;
        this.colourMap = colourMap;
    }
}

