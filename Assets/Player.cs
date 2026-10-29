using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class PlayerController : MonoBehaviour
{
    public float moveForce;     // Force applied when we move left or right.
    public Rigidbody rig;       // A referene to our Rigidbody component.
    // Called 60 times a second.
    // Similar to the Update function, but used for physics calculations.
    void FixedUpdate()
    {
        // Get the horizontal input.
        // 0 = nothing
        // 1 = right
        // -1 = left
        float xInput = Input.GetAxis("Horizontal");
        // Add force based on our input.
        rig.AddForce(Vector3.right * xInput * moveForce);
    }
}