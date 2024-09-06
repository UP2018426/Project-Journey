using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Splines;
using UnityEngine.Profiling;

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
    [SerializeField] private AnimationCurve inverseRoadFalloff;

    [SerializeField] private float furthestPointForFalloff;
    [SerializeField] private float debugValue;

    private void Awake()
    {
        falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
        //inverseRoadFalloff = CreateInverseCurve(roadFalloff);
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
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, mapData.height , meshHeightCurve, lod);

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

    MapData GenerateMapData(Vector2 center)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize + 2, mapChunkSize + 2, seed, noiseScale, octaves, persistance, lacunarity, center + offset, normaliseMode);

        Color[] colourMap = new Color[mapChunkSize * mapChunkSize];
        
        float3 closestPosition = Vector3.zero;
        float outFloat = 0f;
        //_spline = roadManager.AllRoadSplineListSpline[0];
        float dist = 0f;

        //
        const float halfChunk = ((mapChunkSize + 1) / 2f); // TODO: THis needs to change based on chunk LOD
        const float constant = 600f / halfChunk;
        //

        float[,] height = new float[mapChunkSize+2,mapChunkSize+2];

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                /*if (useFalloff)
                {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - falloffMap[x, y]);

                }*/
                
                float currentHeight = noiseMap[x, y];
                
                /// Start
                
                // Find the closest X road subsegments
                
                /*float closestDistance = float.MaxValue;
                int closestRoadIndex = -1;
                */
                //float conversionRate = (1190f / ((float)mapChunkSize));
                //const float constant = 595f / 119.5f;
                //const float halfChunk = ((mapChunkSize + 1) / 2f);
                //const float constant = 595f / halfChunk;
                //const float constant = 600f / halfChunk;
                float3 currentWorldPosition = new Vector3(((x - halfChunk) * constant), meshHeightCurve.Evaluate(currentHeight) * meshHeightMultiplier * 5f, -((y - halfChunk) * constant)); // TODO: Add the center offset
                                                                                                                                                                                             //TODO: The above is only off by a tiny ammount (possible 1 unit of "constant" or "constant / 2") I.F.

                /*if (x > 8 && x < 12 && y > 8 && y < 12)
                {
                    Debug.Log(currentWorldPosition);
                    currentHeight = 1.01f;
                }*/

                /*if (x == 13 && y == 13)
                {
                    Debug.Log(currentWorldPosition);
                    Debug.Log(meshHeightCurve.Evaluate(currentHeight));
                }*/

                /*  
                for (int i = 0; i < roadManager.AllRoadSplineList.Count - 1; i++)
                {
                    Vector2 simplePosition = new Vector2(roadManager.AllRoadSplineListPos[i].x, roadManager.AllRoadSplineListPos[i].z);
                    
                    if (simplePosition.sqrMagnitude < closestDistance * closestDistance)
                    {
                        closestDistance = simplePosition.magnitude;
                        closestRoadIndex = i;
                    }
                }*/

                // Sample the selected road segments to find the closest point to a spline. 

                Spline _spline = roadManager.AllRoadSplineListSpline[0];
                dist = SplineUtility.GetNearestPoint(_spline, currentWorldPosition, out closestPosition, out outFloat, 4, 2);

                //Debug.Log(dist);
                /*if ((new Vector3(closestPosition.x, closestPosition.y, closestPosition.z) - new Vector3(currentWorldPosition.x, currentWorldPosition.y, currentWorldPosition.z)).magnitude < 100f)
                {
                    noiseMap[x, y] = 0;
                    //noiseMap[x,y] = Mathf.Lerp(closestPosition.y - 10f, currentHeight , roadFalloff.Evaluate(dist));
                }*/

                /*if (x == 13 && y == 13)
                {
                    Debug.Log(dist);
                }*/

                // If "distance" closer than EvaluationCurve(furthest point)
                // Use EvaluationCurve to set vertex pos to be somewhere between currentHeight and Evaluation.

                height[x+1,y+1] = meshHeightCurve.Evaluate(noiseMap[x, y]) * meshHeightMultiplier;

                if (dist < 30)
                {
                    //float heightOfRoad = (closestPosition.y / meshHeightMultiplier / 5);
                    
                    //float heightOfRoad = inverseRoadFalloff.Evaluate(closestPosition.y / (meshHeightMultiplier * 5f));

                    // // Step 1: Isolate the evaluated value
                    // float evaluatedValue = currentHeight / (meshHeightMultiplier * 5f);
                    //
                    // // Step 2: Apply the inverse curve
                    // float heightOfRoad = inverseRoadFalloff.Evaluate(evaluatedValue);
                    
                    float heightOfRoad = Mathf.InverseLerp(0, 697.5f, closestPosition.y + debugValue);

                    if (y == 10)
                    {
                        Debug.Log(closestPosition.y);
                    }

                    height[x + 1, y + 1] = (heightOfRoad) * meshHeightMultiplier ;

                    //noiseMap[x, y] = heightOfRoad;

                    //noiseMap[x, y] = Mathf.Lerp(currentHeight, heightOfRoad, -lerpValue / 5);
                    //noiseMap[x, y] = heightOfRoad;
                    currentHeight = 1.01f;
                    //currentHeight = Mathf.Lerp(closestPosition.y, currentHeight, roadFalloff.Evaluate(dist));
                    //roadManager.AllRoadSplineList[closestRoadIndex].GetComponent<SplineContainer>()[0].spli
                }

               //currentHeight = 0f;
                
                /// End
                    
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

        return new MapData(noiseMap, colourMap, height);
    }
    
    AnimationCurve CreateInverseCurve(AnimationCurve curve)
    {
        AnimationCurve invertedCurve = new AnimationCurve();
        /*Keyframe[] originalKeyframes = curve.keys;

        for (int i = 0; i < originalKeyframes.Length; i++)
        {
            invertedKeyframes[i] = new Keyframe(originalKeyframes[i].value, originalKeyframes[i].time);
        }

        AnimationCurve inverseCurve = new AnimationCurve(invertedKeyframes);

        // Optionally smooth the curve to ensure it behaves well
        for (int i = 0; i < inverseCurve.length; i++)
        {
            inverseCurve.SmoothTangents(i, 0);
        }*/
        
        for (int i = 0; i < curve.length; i++)
        {
            Keyframe inverseKey = new Keyframe(curve.keys[i].value, curve.keys[i].time);
            invertedCurve.AddKey(inverseKey);
        }

        return invertedCurve;
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
    public readonly float[,] height;

    public MapData(float[,] heightMap, Color[] colourMap, float[,] height)
    {
        this.heightMap = heightMap;
        this.colourMap = colourMap;
        this.height = height;
    }
}
