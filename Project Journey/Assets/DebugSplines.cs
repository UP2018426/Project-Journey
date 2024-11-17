using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugSplines : MonoBehaviour
{
    [SerializeField] private SplineMeshExtrude Extrusion;
    [SerializeField] private float speed;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Extrusion.GenerateMeshAlongSpline();
        }
        
        Vector2 travelDirection = new Vector2();

        if (Input.GetKey(KeyCode.W)) { travelDirection += Vector2.up; }
        if (Input.GetKey(KeyCode.S)) { travelDirection += Vector2.down; }
        if (Input.GetKey(KeyCode.A)) { travelDirection += Vector2.left; }
        if (Input.GetKey(KeyCode.D)) { travelDirection += Vector2.right; }
        
        travelDirection = travelDirection.normalized * (speed * Time.deltaTime);
        
        transform.position = new Vector3(transform.position.x + travelDirection.x, transform.position.y, transform.position.z + travelDirection.y);

        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}
