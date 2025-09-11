using UnityEngine;

public class LogMovement : MonoBehaviour
{
    public float speed = 2f;

    [Tooltip("Set to 1 for right, -1 for left")]
    public int horizontalDirection = 1; // 1 = right, -1 = left

    [Header("Loop Settings")]
    public float leftBound = -18f;
    public float rightBound = 18f;

    void Update()
    {
        // Move horizontally based on the direction sign
        transform.position += Vector3.right * horizontalDirection * speed * Time.deltaTime;

        // Wrap around based on direction
        if (horizontalDirection > 0 && transform.position.x > rightBound)
        {
            transform.position = new Vector3(leftBound, transform.position.y, transform.position.z);
        }
        else if (horizontalDirection < 0 && transform.position.x < leftBound)
        {
            transform.position = new Vector3(rightBound, transform.position.y, transform.position.z);
        }
    }
}
