using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Move : MonoBehaviour
{
    // This will be a reference to the Pellet object in the scene
    private GameObject pellet;

    // How far the Pellet moves left and right on the X axis
    public float amplitude = 2f;

    // How fast the Pellet moves back and forth
    public float speed = 2f;

    // This will store the Pellet's starting position in the scene
    private Vector3 startPos;

    // Start() runs once, at the beginning
    void Start()
    {
        // Find the object named "pellet" in the scene
        pellet = GameObject.Find("pellet");

        // If the object exists, save its starting position
        if (pellet != null)
        {
            startPos = pellet.transform.position;
        }
    }

    // Update() runs every frame (many times per second)
    void Update()
    {
        // Only run if the pellet object was found
        if (pellet != null)
        {
            // Make the pellet move left and right using a sine wave
            float offsetX = Mathf.Sin(Time.time * speed) * amplitude;

            // Set the pellet’s new position
            // X moves back and forth, Y and Z stay the same
            pellet.transform.position = new Vector3(
                startPos.x + offsetX,
                startPos.y,
                startPos.z
            );
        }
    }
}
