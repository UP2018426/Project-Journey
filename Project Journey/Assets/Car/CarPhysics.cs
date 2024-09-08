using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarPhysics : MonoBehaviour
{
    [SerializeField]
    private Vector3 COM;
    private Rigidbody rb;
    public Transform[] frontTires;
    [SerializeField]
    private float steeringValue;
    public float steeringLimit = 35f;
    public float powerValue;
    public float brakeValue;
    [SerializeField]
    private float maxPower, powerOutput;
    public float topSpeed;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        rb.centerOfMass = COM;
        
        if(Input.GetKey(KeyCode.D))
        {
            steeringValue += 35f * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.A))
        {
            steeringValue -= 35f * Time.deltaTime;
        }

        steeringValue = Mathf.Clamp(steeringValue, -steeringLimit, steeringLimit);

        for (int i = 0; i < frontTires.Length; i++)
        {
            //frontTires[i].localEulerAngles = new Vector3(frontTires[i].eulerAngles.x * 0, steeringValue, frontTires[i].eulerAngles.z * 0);
            frontTires[i].transform.localRotation = Quaternion.Euler(0f, steeringValue, 0f);
            frontTires[i].GetComponent<WheelScript>().wheelVis.transform.localRotation = Quaternion.Euler(0f, steeringValue, 0f);
        }

        if (Input.GetKey(KeyCode.W))
        {
            powerOutput += Time.deltaTime;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            powerOutput -= Time.deltaTime;
        }
        else
        {
            powerOutput = 0;
        }

        powerOutput = Mathf.Clamp(powerOutput, -1, 1);

        powerValue = powerOutput * maxPower;

        if (Input.GetKey(KeyCode.Space))
        {
            brakeValue += Time.deltaTime;
        }
        else
        {
            brakeValue = 0f;
        }

        brakeValue = Mathf.Clamp(brakeValue, 0, 1);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(COM + transform.position, 0.5f);
    }
}
