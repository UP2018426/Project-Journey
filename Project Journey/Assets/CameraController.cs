using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float speed;

    // Update is called once per frame
    void Update()
    {
        Vector3 travelDirection = new Vector3();

        if (Input.GetKey(KeyCode.W)) { travelDirection += Vector3.forward; }
        if (Input.GetKey(KeyCode.S)) { travelDirection += Vector3.back; }
        
        if (Input.GetKey(KeyCode.A)) { travelDirection += Vector3.left; }
        if (Input.GetKey(KeyCode.D)) { travelDirection += Vector3.right; }

        if (Input.GetKey(KeyCode.E)) { travelDirection += Vector3.up; }
        if (Input.GetKey(KeyCode.Q)) { travelDirection += Vector3.down; }
        
        travelDirection = travelDirection.normalized * (speed * Time.deltaTime);

        transform.position += travelDirection;

        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}
