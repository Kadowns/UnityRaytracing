using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    Rigidbody rb;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.UpArrow))
            rb.AddForce(Vector3.forward);
        if (Input.GetKey(KeyCode.LeftArrow))
            rb.AddForce(Vector3.left);
        if (Input.GetKey(KeyCode.RightArrow))
            rb.AddForce(Vector3.right);
        if (Input.GetKey(KeyCode.DownArrow))
            rb.AddForce(Vector3.back);
    }
}
