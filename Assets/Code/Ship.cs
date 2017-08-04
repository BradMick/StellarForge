using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ship : MonoBehaviour
{
    public Transform[] mainThrusters        = new Transform[2];
    public Transform[] reverseThrusters     = new Transform[2];
    public Transform[] maneuveringThrusters = new Transform[4];

    public float cruiseVelocity         = 60.0f;   //m/s
    public float translationVelocity    = 10.0f;   //m/s
    public float setVelocity            = 0.0f;    //m/s
    public float setLatTransVel         = 0.0f;
    public float setVertTransVel        = 0.0f;

    public float yawRate        = 100.0f;       //deg/s
    public float rollRate       = 100.0f;       //deg/s
    public float pitchRate      = 100.0f;       //deg/s

    float setYawRate,
          setRollRate,
          setPitchRate;

    public float mainThrust     = 120000.0f;    //N
    public float reverseThrust  = 30000.0f;     //N
    public float maneuverThrust = 5000.0f;      //N

    public Vector3 localLinearVelocity,
                   localAngularVelocity;

    public Vector3 velocityError,
                   velocityOutput;

    public Vector3 angularVelocityError,
                   angularVelocityOutput;

    private Rigidbody rb;

    void Start()
    {
        rb = gameObject.GetComponent<Rigidbody>();
        rb.useGravity = false;
        //Drag!
        rb.drag = 0.0f;
        rb.angularDrag = 0.0f;

    }

    void Update()
    {
        localLinearVelocity = this.transform.InverseTransformDirection(rb.velocity);
        localAngularVelocity = this.transform.InverseTransformDirection(rb.angularVelocity);

        velocityError.x = setLatTransVel  + localLinearVelocity.x;
        velocityError.y = setVertTransVel + localLinearVelocity.y;
        velocityError.z = setVelocity - localLinearVelocity.z;

        velocityOutput.x = PID(velocityError.x, Time.fixedDeltaTime);
        velocityOutput.y = PID(velocityError.y, Time.fixedDeltaTime);
        velocityOutput.z = PID(velocityError.z, Time.fixedDeltaTime);

        angularVelocityError.x = setPitchRate + localAngularVelocity.x;
        angularVelocityError.y = setYawRate   + localAngularVelocity.y;
        angularVelocityError.z = setRollRate  + localAngularVelocity.z;

        angularVelocityOutput.x = PID(angularVelocityError.x, Time.fixedDeltaTime);
        angularVelocityOutput.y = PID(angularVelocityError.y, Time.fixedDeltaTime);
        angularVelocityOutput.z = PID(angularVelocityError.z, Time.fixedDeltaTime);

        //Debug.Log(velocityOutput);

        GetInput();
        SetShipVelocity();
        SetShipRotationVelocity();
        SetShipTranslationVelocity();
    }

    void FixedUpdate()
    {
        ApplyMainThrust();
        ApplyManeuverThrust();
        ApplyTranslationThrust();
    }

    #region PID

    float PID(float _currentError, float _deltaTime)
    {
        float Kp = 1.0f;
        float Ki = 0.0f;
        float Kd = 0.1f;

        float P = 0.0f,
              I = 0.0f,
              D = 0.0f,
              prevError = 0.0f;

        P = _currentError;
        I += P * _deltaTime;
        D = (P - prevError) / _deltaTime;
        prevError = _currentError;

        return P * Kp + I * Ki + D * Kd;
    }

    #endregion


    #region Ship Forces

    void SetShipVelocity()
    {
        //Velocity
        if (key_1)
        {
            setVelocity = cruiseVelocity * 0.0f;
        }

        if (key_2)
        {
            setVelocity = cruiseVelocity * 0.33f;
        }

        if (key_3)
        {
            setVelocity = cruiseVelocity * 0.66f;
        }
        if (key_4)
        {
            setVelocity = cruiseVelocity * 1.0f;
        }
    }

    void SetShipRotationVelocity()
    {
        //Pitch velocities in rads/sec
        if (key_w)
            setPitchRate = -pitchRate * Mathf.Deg2Rad;

        if (key_s)
            setPitchRate = pitchRate * Mathf.Deg2Rad;

        if (!key_s && !key_w)
            setPitchRate = 0.0f;

        //Yaw velocities in rads/sec
        if (key_a)
            setYawRate = yawRate * Mathf.Deg2Rad;

        if (key_d)
            setYawRate = -yawRate * Mathf.Deg2Rad;

        if (!key_a && !key_d)
            setYawRate = 0.0f;

        //Roll velocities in rads/sec
        if (key_q)
            setRollRate = -rollRate * Mathf.Deg2Rad;

        if (key_e)
            setRollRate = rollRate * Mathf.Deg2Rad;

        if (!key_q && !key_e)
            setRollRate = 0.0f;
    }

    void SetShipTranslationVelocity()
    {
        //Left translation
        if (key_z)
            setLatTransVel = translationVelocity;

        //Right translation
        if (key_c)
            setLatTransVel = -translationVelocity;

        if (!key_z && !key_c)
            setLatTransVel = 0.0f;

        //Up translation
        if (key_r)
            setVertTransVel = -translationVelocity;

        //Down translation
        if (key_f)
            setVertTransVel = translationVelocity;

        if (!key_r && !key_f)
            setVertTransVel = 0.0f;
    }

    void ApplyMainThrust()
    {
        if (velocityOutput.z > 0.0f)
        {
            rb.AddForceAtPosition(transform.forward * mainThrust, mainThrusters[0].position);
            rb.AddForceAtPosition(transform.forward * mainThrust, mainThrusters[1].position);
        }

        if (velocityOutput.z < 0.0f)
        {
            rb.AddForceAtPosition(transform.forward * -reverseThrust, reverseThrusters[0].position);
            rb.AddForceAtPosition(transform.forward * -reverseThrust, reverseThrusters[1].position);
        }
    }

    void ApplyManeuverThrust()
    {
        //Yaw left
        if (angularVelocityOutput.y > 0.0f)
        {
            rb.AddForceAtPosition(transform.right * -maneuverThrust, maneuveringThrusters[0].position);
            rb.AddForceAtPosition(transform.right * maneuverThrust, maneuveringThrusters[3].position);
        }
        //Yaw right
        if (angularVelocityOutput.y < 0.0f)
        {
            rb.AddForceAtPosition(transform.right * maneuverThrust, maneuveringThrusters[1].position);
            rb.AddForceAtPosition(transform.right * -maneuverThrust, maneuveringThrusters[2].position);
        }
        //Pitch down
        if (angularVelocityOutput.x < 0.0f)
        {
            rb.AddForceAtPosition(transform.up * -maneuverThrust, maneuveringThrusters[0].position);
            rb.AddForceAtPosition(transform.up * -maneuverThrust, maneuveringThrusters[1].position);

            rb.AddForceAtPosition(transform.up * maneuverThrust, maneuveringThrusters[2].position);
            rb.AddForceAtPosition(transform.up * maneuverThrust, maneuveringThrusters[3].position);
        }

        //Pitch up
        if (angularVelocityOutput.x > 0.0f)
        {
            rb.AddForceAtPosition(transform.up * maneuverThrust, maneuveringThrusters[0].position);
            rb.AddForceAtPosition(transform.up * maneuverThrust, maneuveringThrusters[1].position);

            rb.AddForceAtPosition(transform.up * -maneuverThrust, maneuveringThrusters[2].position);
            rb.AddForceAtPosition(transform.up * -maneuverThrust, maneuveringThrusters[3].position);
        }

        //Roll right
        if (angularVelocityOutput.z > 0.0f)
        {
            rb.AddForceAtPosition(transform.up * -maneuverThrust, maneuveringThrusters[2].position);
            rb.AddForceAtPosition(transform.up * maneuverThrust, maneuveringThrusters[3].position);
        }

        //Roll left
        if (angularVelocityOutput.z < 0.0f)
        {
            rb.AddForceAtPosition(transform.up * maneuverThrust, maneuveringThrusters[2].position);
            rb.AddForceAtPosition(transform.up * -maneuverThrust, maneuveringThrusters[3].position);
        }
    }

    void ApplyTranslationThrust()
    {
        //Translate up
        if (velocityOutput.y < 0.0f)
        {
            rb.AddForceAtPosition(transform.up * maneuverThrust, maneuveringThrusters[0].position);
            rb.AddForceAtPosition(transform.up * maneuverThrust, maneuveringThrusters[1].position);
            rb.AddForceAtPosition(transform.up * maneuverThrust, maneuveringThrusters[2].position);
            rb.AddForceAtPosition(transform.up * maneuverThrust, maneuveringThrusters[3].position);
        }

        //Translate down
        //if (key_f)
        if (velocityOutput.y > 0.0f)
        {
            rb.AddForceAtPosition(transform.up * -maneuverThrust, maneuveringThrusters[0].position);
            rb.AddForceAtPosition(transform.up * -maneuverThrust, maneuveringThrusters[1].position);
            rb.AddForceAtPosition(transform.up * -maneuverThrust, maneuveringThrusters[2].position);
            rb.AddForceAtPosition(transform.up * -maneuverThrust, maneuveringThrusters[3].position);
        }

        //Translate left
        if (velocityOutput.x > 0.0f)
        {
            rb.AddForceAtPosition(transform.right * -maneuverThrust, maneuveringThrusters[0].position);
            rb.AddForceAtPosition(transform.right * -maneuverThrust, maneuveringThrusters[2].position);
        }
        
        //Translate right
        if (velocityOutput.x < 0.0f)
        {
            rb.AddForceAtPosition(transform.right * maneuverThrust, maneuveringThrusters[1].position);
            rb.AddForceAtPosition(transform.right * maneuverThrust, maneuveringThrusters[3].position);
        }
    }

    #endregion

    #region Input

    //Throttle
    bool key_1,
         key_2,
         key_3,
         key_4;
    //Maneuvering
    bool key_w,
         key_a,
         key_s,
         key_d,    
         key_q,
         key_e,
         key_z,
         key_c,
         key_r,
         key_f;

    void GetInput()
    {
        //Throttle Keys
        if (Input.GetKeyDown(KeyCode.Alpha1))
            key_1 = true;
        else
            key_1 = false;

        if (Input.GetKeyDown(KeyCode.Alpha2))
            key_2 = true;
        else
            key_2 = false;

        if (Input.GetKeyDown(KeyCode.Alpha3))
            key_3 = true;
        else
            key_3 = false;

        if (Input.GetKeyDown(KeyCode.Alpha4))
            key_4 = true;
        else
            key_4 = false;

        //Maneuvering Keys
        if (Input.GetKey(KeyCode.W))
            key_w = true;
        else
            key_w = false;

        if (Input.GetKey(KeyCode.A))
            key_a = true;
        else
            key_a = false;

        if (Input.GetKey(KeyCode.S))
            key_s = true;
        else
            key_s = false;

        if (Input.GetKey(KeyCode.D))
            key_d = true;
        else
            key_d = false;

        if (Input.GetKey(KeyCode.Q))
            key_q = true;
        else
            key_q = false;

        if (Input.GetKey(KeyCode.E))
            key_e = true;
        else
            key_e = false;

        //Translation Keys
        if (Input.GetKey(KeyCode.Z))
            key_z = true;
        else
            key_z = false;

        if (Input.GetKey(KeyCode.C))
            key_c = true;
        else
            key_c = false;

        if (Input.GetKey(KeyCode.R))
            key_r = true;
        else
            key_r = false;

        if (Input.GetKey(KeyCode.F))
            key_f = true;
        else
            key_f = false;
    }

    #endregion


    #region Testing Hud

    Rect textRect = new Rect(16.0f, 16.0f, 64.0f, 64.0f);
    Rect angularRect = new Rect(16.0f, 128.0f, 64.0f, 64.0f);

    void OnGUI()
    {
        GUI.Label(textRect, "Linear Velocities: " + localLinearVelocity.ToString());
        
        GUI.Label(angularRect, "Angular Velocities: " + localAngularVelocity.ToString());
    }

    #endregion
}
