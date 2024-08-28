using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class SplineDistanceTest : MonoBehaviour
{
    public GameObject curve;
    public Spline _spline;
    void Start()
    {
        _spline = curve.GetComponent<SplineContainer>().Spline;
    }

    // Update is called once per frame
    void Update()
    {
        if (/*Input.GetKeyDown(KeyCode.P)*/ true)
        {
            float outVal = 0;
            //float3 outVector3A = Vector3.zero;
            float3 outVector3 = Vector3.zero;
            float outT = 0;

            float dist = SplineUtility.GetNearestPoint(_spline, transform.position, out outVector3, out outVal, 4, 2);
            Debug.DrawLine(transform.position, outVector3, Color.red);
            Debug.Log(outVector3);
        }
    }
}
