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
            Extrusion.GenerateOnFly();
        }

        if (Input.GetKey(KeyCode.W))
        {
            transform.position = new Vector3(transform.position.x,transform.position.y, transform.position.z + speed * Time.deltaTime);
        }

        
    }
}
