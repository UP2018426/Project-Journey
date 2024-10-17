using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Splines;

public class RoadManager : MonoBehaviour
{
    [SerializeField] private GameObject RoadGeneratorPrefab;

    [SerializeField] private RoadGenerator currentRoadSegment, previousRoadSegment;
    
    public List<Transform> AllRoadSplineList; // This array is used to store all road splines and is sampled for terrain deformation
    public List<Vector3> AllRoadSplineListPos; // This array is used to store all road splines and is sampled for terrain deformation
    public List<Spline> AllRoadSplineListSpline; // This array is used to store all road splines and is sampled for terrain deformation
    
    [SerializeField] private int roadSegmentSubSegments = 50;

    [SerializeField] private MapGenerator mapGenerator; // Used to provide the newly created road segment with the mapGenerator.
    
    [SerializeField] private EndlessTerrain endlessTerrain; // Assigned manually

    private static int chunkSize;

    [SerializeField] private MeshFilter selectedMeshFilter; // The chunk to be used for road carving.

    public float startTime, endTime; // Debugging variables

    private struct ChunkJobsStruct
    {
        public JobHandle ChunkJobHandle;
        public IJob ChunkJob;
        public MeshFilter ChunkMeshFilter;
        public NativeArray<float3> ChunkMeshVerts;
    }
    
    private List<ChunkJobsStruct> inProgressJobs = new List<ChunkJobsStruct>();
    
    int chunksVisibleInViewDst = Mathf.RoundToInt(400f / chunkSize);

    private void Start()
    {
        CreateRoadSegment(Vector3.zero);
        CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());
        CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());
        CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());
        CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());

        //chunkSize = endlessTerrain.GetChunkSize();
        chunkSize = 238;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());
        }

        // Testing the performance manually in here. Will eventually be automatic when I've hooked in the dictionary of mesh's
        if (Input.GetKeyDown(KeyCode.E))
        {
            startTime = Time.realtimeSinceStartup;

            Vector2 viewerPosition = new Vector2(endlessTerrain.viewer.position.x, endlessTerrain.viewer.position.z);
            viewerPosition /= 5f; 
            int currentChunkCoordX = Mathf.RoundToInt (viewerPosition.x / chunkSize);
            int currentChunkCoordY = Mathf.RoundToInt (viewerPosition.y / chunkSize);

            //currentChunkCoordX -= chunksVisibleInViewDst;
            //currentChunkCoordY -= chunksVisibleInViewDst;
            
            Vector2 viewedChunkCoord = new Vector2 (currentChunkCoordX, currentChunkCoordY);

            if (endlessTerrain.terrainChunkDictionary.ContainsKey(viewedChunkCoord))
            {
                EndlessTerrain.TerrainChunk terrainChunk = endlessTerrain.terrainChunkDictionary[viewedChunkCoord];
                Debug.Log("chunk found in dict.!");

                MeshFilter terrainChunkMeshFilter = terrainChunk.GetMeshFilter();
                if (terrainChunkMeshFilter)
                {
                    selectedMeshFilter = terrainChunkMeshFilter;
                }
            }
            
            if (selectedMeshFilter != null)
            {
                // This finds the nearest couple of splines to the chunk. This affects performance way more than I'd expect :/
                // TODO: Investigate performance impact of finding closest splines
                // Note: Burst may not be appropriate here as there'd have to be a conversion to NativeSpline and back again. This may not be worth it.
                
                
                int splinesToSample = 2;
                
                List<Spline> tempSplines = new List<Spline>(AllRoadSplineListSpline);
                List<Vector3> tempPositions = new List<Vector3>(AllRoadSplineListPos);
                Spline[] closestSplines = new Spline[splinesToSample];

                Vector3 centerV3 = selectedMeshFilter.transform.position;
                
                
                // Find the nearest "splinesToSample" of splines to the chunk
                for (int j = 0; j < splinesToSample; j++) // How many road segments should be sampled per chunk
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
                    
                    closestSplines[j] = tempSplines[closestRoadIndex];
                    
                    tempPositions.RemoveAt(closestRoadIndex);
                    tempSplines.RemoveAt(closestRoadIndex);
                }
                
                Convert(selectedMeshFilter, closestSplines);
                
            }
        }

        // Run through all active jobs and check if any are ready to finish up (there may be a better way to do this but I havnt found it yet...) 
        if (inProgressJobs.Count > 0)
        {
            for (int i = inProgressJobs.Count - 1; i >= 0; i--) // Count down to not skip a job
            {
                if (inProgressJobs[i].ChunkJobHandle.IsCompleted)
                {
                    GetNearestJob tempJob = (GetNearestJob)inProgressJobs[i].ChunkJob;
                    
                    inProgressJobs[i].ChunkJobHandle.Complete();
                    
                    inProgressJobs[i].ChunkMeshFilter.sharedMesh.vertices = Float3ArrayToVector3Array(tempJob.vertices);
                    
                    Debug.Log("job is done!");
                    
                    inProgressJobs.RemoveAt(i);
                    
                    // TODO: Remove this debugging segment
                    endTime = Time.realtimeSinceStartup;
                    float resultTime = endTime - startTime;
                    Debug.Log("Time to edit terrain: " + resultTime);
                    /*meshVertices.Dispose();
                    for (int j = 0; j < nativeSplines.Length; j++)
                    {
                        nativeSplines[j].Dispose();
                    }
                    nativeSplines.Dispose();*/
                }
            }
        }
    }

    void Convert(MeshFilter chunkMeshFilter, Spline[] splines) // TODO: This is a shitty name
    {
        Matrix4x4 chunkMatrix = chunkMeshFilter.gameObject.GetComponent<Transform>().localToWorldMatrix;
        
        Debug.Log("0 " + Time.realtimeSinceStartup);
        
        // TODO: One of the "Allocator.TempJob"'s is kicking up a fuss over a memory leak. Unity is catching and dealing with it but investigate an fix
        
        // Convert chunkMeshFilter to a NativeArray<float3>
        NativeArray<float3> meshVertices = new NativeArray<float3>(chunkMeshFilter.sharedMesh.vertices.Length, Allocator.TempJob);
        Debug.Log("1 " + Time.realtimeSinceStartup);
        MeshFilterToFloat3Array(chunkMeshFilter, meshVertices); // TODO: its this
        
        Debug.Log("2 " + Time.realtimeSinceStartup);

        if (splines.Length > 0)
        {
            NativeArray<NativeSpline> nativeSplines = new NativeArray<NativeSpline>(splines.Length, Allocator.TempJob);
            
            for (int i = 0; i < splines.Length; i++)
            {
                nativeSplines[i] = new NativeSpline(splines[i], Allocator.TempJob);
            }
            
            var job = new GetNearestJob
            {
                vertices = meshVertices,
                splines = nativeSplines,
                distanceCutoff = 30f,
                ChunkMatrix4X4 = chunkMatrix
            };
            
            ChunkJobsStruct newJob = new ChunkJobsStruct
            {
                ChunkJobHandle = job.Schedule(),
                ChunkJob = job,
                ChunkMeshFilter = chunkMeshFilter,
                ChunkMeshVerts = meshVertices
            };
            inProgressJobs.Add(newJob);
        }
    }

    /*void MeshFilterToFloat3Array(MeshFilter meshFilter, NativeArray<float3> rv)
    {
        float startTime = Time.realtimeSinceStartup;
        int length = meshFilter.sharedMesh.vertices.Length;
        for (int i = 0; i < length; i++)
        {
            rv[i] = meshFilter.sharedMesh.vertices[i];
        }
        Debug.Log("Time Taken: " + (Time.realtimeSinceStartup - startTime));
    }*/
    
    void MeshFilterToFloat3Array(MeshFilter meshFilter, NativeArray<float3> rv)
    {
        NativeArray<Vector3> vertices = new NativeArray<Vector3>(meshFilter.sharedMesh.vertices, Allocator.TempJob);
    
        ConvertVerticesToFloat3Job job = new ConvertVerticesToFloat3Job
        {
            vertices = vertices,
            rv = rv
        };
    
        JobHandle handle = job.Schedule(vertices.Length, 64); // Schedule the job with batch size of 64. TODO: Investigate modifying "innerLoopBatchCount" form 64
        handle.Complete();
    
        vertices.Dispose();
    }
    
    [BurstCompile]
    struct ConvertVerticesToFloat3Job : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> vertices;
        public NativeArray<float3> rv;

        public void Execute(int i)
        {
            Vector3 vertex = vertices[i];
            rv[i] = new float3(vertex.x, vertex.y, vertex.z);
        }
    }
    
    Vector3[] Float3ArrayToVector3Array(NativeArray<float3> floatArray)
    {
        int length = floatArray.Length;
        Vector3[] rv = new Vector3[length];
        for (int i = 0; i < length; i++)
        {
            rv[i] = floatArray[i];
        }
        return rv;
    }
    
    [BurstCompile]
    struct GetNearestJob : IJob
    {
        public NativeArray<float3> vertices;
        
        // Yes this is unsafe. Yes it can be a problem. It is, sadly, the only way to have a nested Native :( I.F. 
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<NativeSpline> splines;
        
        public float distanceCutoff;
        
        public Matrix4x4 ChunkMatrix4X4;

        public void Execute()
        {
            // Get the inverse of the ChunkMatrix4X4 to convert vertices back to local space later
            Matrix4x4 inverseMatrix = ChunkMatrix4X4.inverse;
            
            for (int i = 0; i < vertices.Length; i++)
            {
                float closestDistance = float.MaxValue;
                float3 closestPoint = new();

                // Convert the vertex to world space
                float3 worldSpaceVertex = ChunkMatrix4X4.MultiplyPoint3x4(vertices[i]);

                for (int j = 0; j < splines.Length; j++)
                {
                    float tempClosestDistance = SplineUtility.GetNearestPoint(splines[j], worldSpaceVertex, out float3 tempClosestPoint, out float _);

                    if (tempClosestDistance < closestDistance)
                    {
                        closestPoint = tempClosestPoint;
                        closestDistance = tempClosestDistance;
                    }

                    // TODO: Pass the correct distance cutoff for this
                    if (closestDistance < distanceCutoff)
                    {
                        // TODO: Bring in AnimationCurve to evaluate road curve
                        worldSpaceVertex.y = closestPoint.y;

                        // Convert modified vertex back to localSpace
                        float3 localSpaceVertex = inverseMatrix.MultiplyPoint3x4(worldSpaceVertex);
                        
                        vertices[i] = localSpaceVertex;
                    }
                }
            }
        }
    }


    void CreateRoadSegment(Vector3 startPosition)
    {
        //currentRoadSegment = GameObject.Instantiate(RoadGeneratorPrefab, startPosition, quaternion.identity).GetComponent<RoadGenerator>();
        currentRoadSegment = GameObject.Instantiate(RoadGeneratorPrefab, Vector3.zero, quaternion.identity).GetComponent<RoadGenerator>();
        currentRoadSegment.SetMapGenerator(mapGenerator);
        
        if (previousRoadSegment != null)
        {
            Vector3 lastPos = previousRoadSegment.spline.Spline[previousRoadSegment.spline.Spline.Count - 1].Position;
            Vector3 secondToLastPos = previousRoadSegment.spline.Spline[previousRoadSegment.spline.Spline.Count - 2].Position;
            Vector3 thirdToLastPos = previousRoadSegment.spline.Spline[previousRoadSegment.spline.Spline.Count - 3].Position;
            Vector3 fourthToLastPos = previousRoadSegment.spline.Spline[previousRoadSegment.spline.Spline.Count - 4].Position;
            
            currentRoadSegment.spline.Spline.Add(new BezierKnot(fourthToLastPos));
            currentRoadSegment.spline.Spline.Add(new BezierKnot(thirdToLastPos));
            currentRoadSegment.spline.Spline.Add(new BezierKnot(secondToLastPos));
            currentRoadSegment.spline.Spline.Add(new BezierKnot(lastPos));
        }
        
        
        //currentRoadSegment.SetLastPosition(Vector3.zero);
        currentRoadSegment.SetLastPosition(startPosition);
        
        for (int i = 0; i < roadSegmentSubSegments; i++)
        {
            currentRoadSegment.GenerateRoadSegment();
        }
        
        currentRoadSegment.transform.gameObject.GetComponent<SplineMeshExtrude>().GenerateOnFly();

        previousRoadSegment = currentRoadSegment;

        AllRoadSplineList.Add(currentRoadSegment.transform);
        //AllRoadSplineListPos.Add(currentRoadSegment.transform.position);
        AllRoadSplineListPos.Add(currentRoadSegment.GetLastSplineVector3());
        AllRoadSplineListSpline.Add(currentRoadSegment.transform.GetComponent<SplineContainer>().Splines[0]);
        
        // Add collision to the road after generation is complete
        MeshCollider currentRoadSegmentMeshCollider = currentRoadSegment.gameObject.AddComponent<MeshCollider>();
    }
}
