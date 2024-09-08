using System;
using System.Collections;
using System.Collections.Generic;
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

    private void Start()
    {
        CreateRoadSegment(Vector3.zero);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            CreateRoadSegment(previousRoadSegment.GetLastSplineVector3());
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
        AllRoadSplineListPos.Add(currentRoadSegment.transform.position);
        AllRoadSplineListSpline.Add(currentRoadSegment.transform.GetComponent<SplineContainer>().Splines[0]);
        
        // Add collision to the road after generation is complete
        MeshCollider currentRoadSegmentMeshCollider = currentRoadSegment.gameObject.AddComponent<MeshCollider>();
    }
}
