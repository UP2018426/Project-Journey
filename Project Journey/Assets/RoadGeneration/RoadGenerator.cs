using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Random = UnityEngine.Random;

public class RoadGenerator : MonoBehaviour
{
    
    // Pathing Wander Behaviour values
    [SerializeField] private float WanderRadius;
    [SerializeField] private float WanderDistance;
    [SerializeField] private Vector3 WanderDirection;
    // 
    
    [SerializeField] private AnimationCurve heightCurve;

    [SerializeField] private MapGenerator mapGenerator;
    public void SetMapGenerator(MapGenerator newMapGenerator)
    {
        // TODO: is this the best way to make a setter??? I.F.
        mapGenerator = newMapGenerator;
    }

    public SplineContainer spline;
    
    private Vector3 lastPosition;

    public void SetLastPosition(Vector3 newPosition)
    {
        lastPosition = newPosition;
    }
    
    private void Awake()
    {
        //lastPosition = transform.position;
        spline = GetComponent<SplineContainer>();
    }

    private void Update()
    {
        /*if (Input.GetKeyDown(KeyCode.W))
        {
            GenerateRoadSegment(lastPosition + Vector3.forward);    
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            GenerateRoadSegment(lastPosition - Vector3.forward);    
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            GenerateRoadSegment(lastPosition - Vector3.right);    
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            GenerateRoadSegment(lastPosition + Vector3.right);    
        }*/

        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateRoadSegment(DetermineNextRoadPosition());
        }
    }

    Vector3 DetermineNextRoadPosition()
    {
        // Find Wander center point
        Vector3 center = lastPosition + (WanderDistance * WanderDirection);
        
        Vector2 randomCircle = Random.insideUnitCircle;
        
        Vector3 direction = new Vector3(randomCircle.x, 0, randomCircle.y) * WanderRadius;
        
        float roadHeight = mapGenerator.GetMapHeightAtPosition(new Vector2(center.x + direction.x, center.z + direction.z),
            heightCurve, 139.5f * 5);
        
        center.y = roadHeight;

        return center + direction;
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public void GenerateRoadSegment(Vector3 targetPosition)
    {
        spline.Spline.Add(new BezierKnot(targetPosition));

        spline.Spline.SetTangentMode(TangentMode.AutoSmooth);

        lastPosition = targetPosition;
    }
    
    public void GenerateRoadSegment()
    {
        Vector3 targetPosition = DetermineNextRoadPosition();
        
        spline.Spline.Add(new BezierKnot(targetPosition));

        spline.Spline.SetTangentMode(TangentMode.AutoSmooth);

        lastPosition = targetPosition;
    }

    public Vector3 GetLastSplineVector3()
    {
        return spline.Spline[spline.Spline.Count - 1].Position;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(lastPosition, lastPosition + (WanderDistance * WanderDirection));
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(lastPosition + (WanderDistance * WanderDirection), WanderRadius);

        if (mapGenerator)
        {
            //Gizmos.DrawSphere(new Vector3(transform.position.x, mapGenerator.GetMapHeightAtPosition(new Vector2(transform.position.x, transform.position.z), heightCurve, 139.5f * 5), transform.position.z), 5f);
        }
    }
}
