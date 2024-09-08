using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelScript : MonoBehaviour
{
    private Rigidbody carRB;
    private CarPhysics car; 

    [Range(0.0f,2.0f)] 
    public float    suspensionRestDistance,
                    suspensionMaxDistance; 
    public float    springStrength, 
                    springDamper/*,
                    dragConst,
                    rollConst*/;
    [Range(0,100f)]
    public float            wheelSlip;
    public float            currentWheelSlip;

    public AnimationCurve   powerCurve,
                            slipAngleCurve,
                            speedGripCurve;

    public float brakeStrength;
    //public float brakeInput;

    [SerializeField]
    bool isPowered;

    //[SerializeField]
    public GameObject wheelVis;
    [SerializeField]
    private float wheelRadius;

    [SerializeField]
    private float groundGripScalar;

    [Header("Wheel Force Output"), SerializeField]
    private float   wheelSpringForce,
                    wheelSteerForce;

    public float minSpeedToMove = 0.5f; // Speed below which friction starts to apply
    public float stopForce = 2.0f;      // The force that tries to stop the wheel when below minSpeed

    [Header("Wheel Slip Debugs"), SerializeField]
    private float           steeringSlip,
                            speedSlip,
                            averageSlip;


    // Start is called before the first frame update
    void Start()
    {
        carRB = GetComponentInParent<Rigidbody>();
        car = carRB.GetComponentInParent<CarPhysics>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        RaycastHit hit;

        //if (Physics.Raycast(transform.position, transform.TransformDirection(-transform.up), out hit, suspensionMaxDistance))
        if (Physics.Raycast(transform.position, -transform.up, out hit, suspensionMaxDistance))
        {
            Debug.DrawRay(transform.position, -transform.up * hit.distance, Color.yellow);
            //Debug.Log("Did Hit");

            groundGripScalar = hit.transform.gameObject.GetComponent<Collider>().material.dynamicFriction;

            CalculateSpringForce(hit.distance, springStrength, springDamper);
            
            CalculateSteering(/*wheelSlip,*/ 1f);

            if (isPowered)
            {
                CalculateAcceleration(car.powerValue, car.topSpeed, powerCurve);
            }

            CalculateBraking(car.brakeValue, brakeStrength);

            wheelVis.transform.position = transform.position + (-transform.up * (hit.distance - wheelRadius));
        }

        else // Not on the floor
        {
            Debug.DrawRay(transform.position, -transform.up * suspensionMaxDistance, Color.white);
            //Debug.Log("Did not Hit");

            wheelSpringForce = 0f;

            wheelVis.transform.position = transform.position + -transform.up * (suspensionMaxDistance - wheelRadius);
        }



        float currentSpeed = carRB.velocity.magnitude;
        if (currentSpeed < minSpeedToMove && currentSpeed > 0)
        {
            // Apply a force opposite to the direction of movement
            Vector3 stopForceDirection = -carRB.velocity.normalized * stopForce;
            carRB.AddForceAtPosition(stopForceDirection, transform.position);
        }
    }

    private float CalculateWheelSlipAngle(AnimationCurve slipAngleCurve)
    {
        Vector3 wheelVelocity = carRB.GetPointVelocity(transform.position);

        Vector3 normalizedWheelVelocity = wheelVelocity.normalized;

        Vector3 wheelForward = transform.forward;

        float slipAngle = Vector3.Angle(wheelForward, normalizedWheelVelocity);

        return slipAngleCurve.Evaluate(slipAngle);
    }

    private void CalculateSpringForce(float tireRayDist, float springStrength, float springDamper)
    {
        Vector3 springDir = this.transform.up;

        Vector3 tireWorldVelocity = carRB.GetPointVelocity(this.transform.position);

        float offset = suspensionRestDistance - tireRayDist;

        float vel = Vector3.Dot(springDir, tireWorldVelocity);

        float force = (offset * springStrength) - (vel * springDamper);

        wheelSpringForce = Mathf.Clamp(force, 0, Mathf.Infinity);

        carRB.AddForceAtPosition(springDir * wheelSpringForce, this.transform.position);
    }

    private void CalculateSteering(/*float tireGripFactor,*/ float tireMass) // OLD VERSION
    {
        Vector3 steeringDir = transform.right;

        Vector3 tireWorldVelocity = carRB.GetPointVelocity(transform.position);

        float steeringVelocity = Vector3.Dot(steeringDir, tireWorldVelocity);

        //float steeringBasedGrip = CalculateWheelSlipAngle(slipAngleCurve);
        if (carRB.velocity.magnitude < 0.1f)
        {
            steeringSlip = 1f;
        }
        else
        {
            steeringSlip = CalculateWheelSlipAngle(slipAngleCurve);
        }
        //float speedBasedGrip = speedGripCurve.Evaluate(carRB.GetPointVelocity(transform.position).magnitude);
        speedSlip = speedGripCurve.Evaluate(carRB.GetPointVelocity(transform.position).magnitude);

        averageSlip = (steeringSlip + speedSlip) / 2;

        averageSlip = (averageSlip + groundGripScalar) / 2;

        if (carRB.velocity.magnitude < 0.1f)
        {
            averageSlip = 1f;
        }

        wheelSlip = averageSlip * 100;

        float desiredVelocityChange = -steeringVelocity * /*tireGripFactor*/ wheelSlip;

        float desiredAcceleration = desiredVelocityChange / Time.fixedDeltaTime;

        wheelSteerForce = tireMass * desiredAcceleration;
    
        carRB.AddForceAtPosition(steeringDir * tireMass * desiredAcceleration, transform.position);
    }

    private void CalculateAcceleration(float accelerationInput, float carTopSpeed, AnimationCurve powerCurve)
    {
        Vector3 accelerationDirection = transform.forward;

        float zSpeed = carRB.transform.InverseTransformDirection(carRB.velocity).z;
    
        if(accelerationInput != 0f)
        {
            float carSpeed = Vector3.Dot(carRB.gameObject.transform.forward, carRB.velocity);

            float normalisedSpeed = Mathf.Clamp01(Mathf.Abs(carSpeed) / carTopSpeed);

            float availableTorque = powerCurve.Evaluate(normalisedSpeed) * accelerationInput;

            //carRB.AddForceAtPosition(accelerationDirection * (availableTorque
            //+ ((-dragConst * (zSpeed * zSpeed))
            //+ (-rollConst * zSpeed))), transform.position);
            carRB.AddForceAtPosition(accelerationDirection * availableTorque, transform.position);
        }
    }

    private void CalculateBraking(float input, float brakeForce)
    {
        /*Vector3 brakingDirection = -transform.forward;

        float zSpeed = carRB.transform.InverseTransformDirection(carRB.velocity).z;

        if (input != 0f)
        {
            float carSpeed = Vector3.Dot(carRB.gameObject.transform.forward, carRB.velocity);

            //float normalisedSpeed = Mathf.Clamp01(Mathf.Abs(carSpeed) / carTopSpeed);

            //float availableTorque = powerCurve.Evaluate(normalisedSpeed) * accelerationInput;

            //carRB.AddForceAtPosition(accelerationDirection * (availableTorque
            //+ ((-dragConst * (zSpeed * zSpeed))
            //+ (-rollConst * zSpeed))), transform.position);
            carRB.AddForceAtPosition(brakingDirection * brakeForce, transform.position);
        }*/

        float currentSpeed = carRB.velocity.magnitude;
        if (input != 0 && currentSpeed > /*0.05f*/ Mathf.Epsilon)
        {
            // Apply a force opposite to the direction of movement
            Vector3 brakeForceDirection = -carRB.velocity.normalized * (input * brakeForce);
            carRB.AddForceAtPosition(brakeForceDirection, transform.position);
        }
    }

    private float GetSpeedBasedTraction(float currentSpeed, float maxSpeed)
    {
        float normalizedSpeed = currentSpeed / maxSpeed;
        float traction = Mathf.Lerp(1.0f, 0.5f, normalizedSpeed); // Linear interpolation example
        return traction;
    }

    private float GetAngleBasedTraction(float steerAngle)
    {
        float normalizedAngle = steerAngle / 45.0f; // assuming 45 is the max steer angle
        float traction = Mathf.Lerp(1.0f, 0.7f, normalizedAngle); // Linear interpolation example
        return traction;
    }

    private void OnDrawGizmos()
    {
        /*Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position, -transform.up * 10f);*/
    }
}
