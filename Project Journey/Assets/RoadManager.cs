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

    private const int chunkSize = 238;

    [SerializeField] private MeshFilter selectedMeshFilter; // The chunk to be used for road carving.
    
    private Dictionary<Vector2, GameObject> roadChunkDictionary = new Dictionary<Vector2, GameObject>();
    private static List<GameObject> roadChunksVisibleLastUpdate = new List<GameObject>();

    [SerializeField] private float roadViewDistance, maxRoadViewDistance;
    
    [SerializeField] private Transform viewer;
    
    private Vector2 viewerPosition, viewerPositionOld;
    
    private const float viewerMoveThresholdForChunkUpdate = 25f;
    private const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;
    
    private int chunksVisibleInViewDst;

    private const float distanceToSpawnNewRoadSegment = 2500f;

    private const float distanceToSpawnNewRoadSegmentSqr = distanceToSpawnNewRoadSegment * distanceToSpawnNewRoadSegment;

    private struct ChunkJobsStruct
    {
        public JobHandle ChunkJobHandle;
        public IJob ChunkJob;
        public MeshFilter ChunkMeshFilter;
    }
    
    private Queue<ChunkJobsStruct> queuedJobs = new Queue<ChunkJobsStruct>();
    private List<ChunkJobsStruct> inProgressJobs = new List<ChunkJobsStruct>();

    [SerializeField] private int maxJobs;

#if UNITY_EDITOR
    [SerializeField] private int currentTotalJobs;
#endif

    private void Start()
    {
        chunksVisibleInViewDst = Mathf.RoundToInt(maxRoadViewDistance / chunkSize);
        
        CreateRoadSegment(Vector3.zero);
        CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());
        CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());
        CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());
        CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());

        UpdateVisibleRoads();
    }

    void Update()
    {
        viewerPosition = new Vector2 (viewer.position.x, viewer.position.z);

        Vector2 lastRoadPosition = new Vector2(AllRoadSplineListPos[^1].x, AllRoadSplineListPos[^1].z);
        if ((lastRoadPosition - viewerPosition).sqrMagnitude < distanceToSpawnNewRoadSegmentSqr)
        {
            CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());
        }
        
        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate) 
        {
            viewerPositionOld = viewerPosition;
            
            UpdateVisibleRoads();
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Q)) // Debugging tool. TODO: remove
        {
            viewerPositionOld = viewerPosition;
            UpdateVisibleRoads();
            Debug.Log("Q Pressed");
        }

        // Testing the performance manually in here. Will eventually be automatic when I've hooked in the dictionary of mesh's
        if (Input.GetKeyDown(KeyCode.E))
        {
            Vector2 viewerPosition = new Vector2(endlessTerrain.viewer.position.x, endlessTerrain.viewer.position.z);
            viewerPosition /= 5f; 
            int currentChunkCoordX = Mathf.RoundToInt (viewerPosition.x / chunkSize);
            int currentChunkCoordY = Mathf.RoundToInt (viewerPosition.y / chunkSize);
            
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
                
                int splinesToSample = 3;
                
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
                
                CarveChunkMeshFilter(selectedMeshFilter, closestSplines);
                
            }
        }
#endif

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
                    
                    inProgressJobs.RemoveAt(i);

                    tempJob.vertices.Dispose();
                    //meshVertices.Dispose();
                    
                    for (int j = 0; j < tempJob.splines.Length; j++)
                    {
                        tempJob.splines[j].Dispose();
                    }
                    tempJob.splines.Dispose();
                }
            }
        }
        
        // Add jobs from the queue if needed
        if (inProgressJobs.Count < maxJobs && queuedJobs.Count > 0)
        {
            int numberOfJobsToAdd = Mathf.Min(maxJobs - inProgressJobs.Count, queuedJobs.Count);

            for (int i = 0; i < numberOfJobsToAdd; i++)
            {
                ChunkJobsStruct jobToStart = queuedJobs.Dequeue();

                GetNearestJob job = (GetNearestJob)jobToStart.ChunkJob;
                jobToStart.ChunkJobHandle = job.Schedule();
                
                inProgressJobs.Add(jobToStart);
            }
        }

#if UNITY_EDITOR
        currentTotalJobs = inProgressJobs.Count + queuedJobs.Count;
#endif
    }
    
    void UpdateVisibleRoads() 
    {
        for (int i = 0; i < roadChunksVisibleLastUpdate.Count; i++) 
        {
            roadChunksVisibleLastUpdate[i].SetActive(false);
        }
        roadChunksVisibleLastUpdate.Clear();
			
        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++) 
        {
            for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++) 
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (roadChunkDictionary.ContainsKey(viewedChunkCoord)) 
                {
                    if ((viewedChunkCoord - (viewerPosition / chunkSize)).magnitude < roadViewDistance)
                    {
                        roadChunksVisibleLastUpdate.Add(roadChunkDictionary[viewedChunkCoord]);
                        roadChunkDictionary[viewedChunkCoord].SetActive(true);
                    }
                }
            }
        }
    }

    void CarveChunkMeshFilter(MeshFilter chunkMeshFilter, Spline[] splines)
    {
        Matrix4x4 chunkMatrix = chunkMeshFilter.gameObject.GetComponent<Transform>().localToWorldMatrix;
        
        // Convert chunkMeshFilter to a NativeArray<float3>
        NativeArray<float3> meshVertices = new NativeArray<float3>(chunkMeshFilter.sharedMesh.vertices.Length, Allocator.Persistent);
        MeshFilterToFloat3Array(chunkMeshFilter, meshVertices);

        if (splines.Length > 0)
        {
            NativeArray<NativeSpline> nativeSplines = new NativeArray<NativeSpline>(splines.Length, Allocator.Persistent);
            
            for (int i = 0; i < splines.Length; i++)
            {
                nativeSplines[i] = new NativeSpline(splines[i], Allocator.Persistent);
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
                ChunkJob = job,
                ChunkMeshFilter = chunkMeshFilter,
            };
            queuedJobs.Enqueue(newJob);
        }
    }

    public void CarveByCoord(Vector2 viewedChunkCoord)
    {
        if (endlessTerrain.terrainChunkDictionary.ContainsKey(viewedChunkCoord))
        {
            EndlessTerrain.TerrainChunk terrainChunk = endlessTerrain.terrainChunkDictionary[viewedChunkCoord];

            MeshFilter terrainChunkMeshFilter = terrainChunk.GetMeshFilter();
            if (terrainChunkMeshFilter.sharedMesh != null)
            {
                selectedMeshFilter = terrainChunkMeshFilter;
            }
        }
        
        if (selectedMeshFilter != null)
        {
            if (selectedMeshFilter.sharedMesh.vertexCount > 0)
            {
                // This finds the nearest couple of splines to the chunk. This affects performance way more than I'd expect :/
                // TODO: Investigate performance impact of finding closest splines
                // Note: Burst may not be appropriate here as there'd have to be a conversion to NativeSpline and back again. This may not be worth it.
                
                int splinesToSample = 3;
                
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
                
                CarveChunkMeshFilter(selectedMeshFilter, closestSplines);
            }
        }
    }
    
    static void MeshFilterToFloat3Array(MeshFilter meshFilter, NativeArray<float3> rv)
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

                    if (closestDistance < distanceCutoff)
                    {
                        float normalizedDistance = Mathf.Clamp01(closestDistance / distanceCutoff);
                        float outValue = 1f / (1f + Mathf.Exp(10f * (normalizedDistance - 0.5f)));
                        
                        worldSpaceVertex.y = Mathf.Lerp(worldSpaceVertex.y, closestPoint.y, outValue);

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
        GameObject currentRoadSegmentGameObject = GameObject.Instantiate(RoadGeneratorPrefab, Vector3.zero, quaternion.identity, this.transform);
        currentRoadSegment = currentRoadSegmentGameObject.GetComponent<RoadGenerator>();
        currentRoadSegment.MapGenerator = mapGenerator;
        
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
        
        currentRoadSegment.SetLastPosition(startPosition);
        
        for (int i = 0; i < roadSegmentSubSegments; i++)
        {
            currentRoadSegment.GenerateRoadSegment();
        }
        
        currentRoadSegment.transform.gameObject.GetComponent<SplineMeshExtrude>().GenerateOnFly();

        previousRoadSegment = currentRoadSegment;

        AllRoadSplineList.Add(currentRoadSegment.transform);
        AllRoadSplineListPos.Add(currentRoadSegment.GetLastSplineVector3());
        AllRoadSplineListSpline.Add(currentRoadSegment.transform.GetComponent<SplineContainer>().Splines[0]);
        
        // Add collision to the road after generation is complete // TODO: Look into this
        MeshCollider currentRoadSegmentMeshCollider = currentRoadSegment.gameObject.AddComponent<MeshCollider>();
        
        int currentChunkCoordX = Mathf.RoundToInt(startPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(startPosition.z / chunkSize);
        
        roadChunkDictionary.Add(new Vector2(currentChunkCoordX,currentChunkCoordY), currentRoadSegmentGameObject);
        roadChunksVisibleLastUpdate.Add(currentRoadSegmentGameObject);
    }
}
