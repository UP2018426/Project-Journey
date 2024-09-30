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
        
        float outFloat = 0f; // Not used
        //_spline = roadManager.AllRoadSplineListSpline[0];
        //float dist = float.MaxValue;

        //
        const float halfChunk = ((mapChunkSize + 1) / 2f); // TODO: THis needs to change based on chunk LOD
        const float _scale = 600f / halfChunk;
        //
        
        List<Spline> tempSplines = new List<Spline>(roadManager.AllRoadSplineListSpline);
        List<Vector3> tempPositions = new List<Vector3>(roadManager.AllRoadSplineListPos);
        List<Spline> closestSplines = new List<Spline>();
                
        Vector3 centerV3 = new Vector3(center.x * 5f, 0, center.y * 5f);
                
        // Find the nearest 2 splines to the chunk
        for (int j = 0; j < 2; j++) // How many road segments should be sampled per chunk
        {
            float closestPointSqr = float.MaxValue;
            int closestRoadIndex = 0;
                    
            for (int i = 0; i < tempSplines.Count; i++)
            {
                Vector3 offset = tempPositions[i] - centerV3;
                float sqrLen = offset.sqrMagnitude;
                if (sqrLen < closestPointSqr)
                {
                    closestPointSqr = sqrLen;
                    closestRoadIndex = i;
                }
            }
                    
            closestSplines.Add(tempSplines[closestRoadIndex]);
                    
            tempPositions.RemoveAt(closestRoadIndex);
            tempSplines.RemoveAt(closestRoadIndex);
        }
        
        /*
        // Check to see if any of the 2 splines point's overlap with this chunk
        float constantA = (-halfChunk) * _scale;
        float constantB = (mapChunkSize - halfChunk) * _scale;
        
        Vector2 posA = new Vector2(
            (constantA) + (center.x * _scale),
            -(constantA) + (center.y * _scale));
        Vector2 posB = new Vector2(
            (constantB) + (center.x * _scale),
            -(constantB) + (center.y * _scale));
        
        bool bGenerateRoadWithRoad = CheckForOverlap(closestSplines, posA, posB);*/
        
        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                /*if (useFalloff)
                {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - falloffMap[x, y]);
                }*/
                
                float currentHeight = noiseMap[x, y];
                
                // Start of road calculation

                /*if (bGenerateRoadWithRoad)
                {
                    float3 currentVertexWorldPosition = new Vector3(
                        ((x - halfChunk) * _scale) + (center.x * 5f), 
                        meshHeightCurve.Evaluate(currentHeight) * meshHeightMultiplier * 5f, 
                        -((y - halfChunk) * _scale) + (center.y * 5f));
                    
                    float dist = float.MaxValue;
                    float3 closestPosition = Vector3.zero;
                    float3 tempClosestPosition = Vector3.zero;
                    
                    // Sample both nearest splines to calculate the closest "dist" to road
                    foreach (Spline spline in closestSplines)
                    {
                        float tempDist = SplineUtility.GetNearestPoint(spline, currentVertexWorldPosition, out tempClosestPosition, out outFloat, 4, 2);

                        if (tempDist < dist)
                        {
                            dist = tempDist;
                            closestPosition = tempClosestPosition;
                        }
                    }
                   
                    if (dist < furthestPointForFalloff)
                    {
                        float heightOfRoad = Mathf.InverseLerp(0, 697.5f, closestPosition.y);

                        noiseMap[x, y] = Mathf.Lerp(currentHeight, heightOfRoad, roadFalloff.Evaluate(dist));
                        
                        currentHeight = 1.01f;
                    }
                }*/
                
                // End of road calculation
                    
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

