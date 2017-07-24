using UnityEngine;
using System.Collections;

public class SystemForgeAlt : MonoBehaviour
{
    Gizmos sphere;

    void Init()
    {

    }


    void OnDrawGizmos()
    {
        DrawOrbit(10.0f);

        Gizmos.DrawSphere(new Vector3(15.0f, 0.0f, 0.0f), 1.0f);
    }

    void DrawOrbit(float _r)
    {
        float numVerts = 32;
        float x, z;
        float x2, z2;
        float angleIncrement = (360.0f / numVerts) * Mathf.Deg2Rad;

        for (int i = 0; i < numVerts; i++)
        {
            x = _r * Mathf.Cos(angleIncrement * i) + transform.position.x;
            z = _r * Mathf.Sin(angleIncrement * i) + transform.position.z;

            x2 = _r * Mathf.Cos(angleIncrement * (i + 1)) + transform.position.x;
            z2 = _r * Mathf.Sin(angleIncrement * (i + 1)) + transform.position.z;

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(new Vector3(x, transform.position.y, z), new Vector3(x2, transform.position.y, z2));
        }
    }
}
