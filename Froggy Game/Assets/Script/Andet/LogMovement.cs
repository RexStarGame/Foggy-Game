using UnityEngine;

public class LogMovement : MonoBehaviour
{
    public float speed = 2f;

    [Tooltip("Set to 1 for right, -1 for left")]
    public int horizontalDirection = 1; // 1 = right, -1 = left

    [Header("Loop Settings")]
    public float leftBound = -18f;
    public float rightBound = 18f;

    [Header("Safety")]
    [Tooltip("If true, kill the frog if this log wraps while the frog is on it.")]
    public bool killPassengerOnWrap = true;

    void Update()
    {
        // Move horizontally based on the direction sign
        transform.position += Vector3.right * horizontalDirection * speed * Time.deltaTime;

        // Wrap + optionally kill passenger riding this log
        if (horizontalDirection > 0 && transform.position.x > rightBound)
        {
            if (killPassengerOnWrap) KillFrogRidingThisLog();
            transform.position = new Vector3(leftBound, transform.position.y, transform.position.z);
        }
        else if (horizontalDirection < 0 && transform.position.x < leftBound)
        {
            if (killPassengerOnWrap) KillFrogRidingThisLog();
            transform.position = new Vector3(rightBound, transform.position.y, transform.position.z);
        }
    }

    // If the frog is parented under this log (your FrogMovement does SetParent on enter),
    // call its existing GameOver().
    private void KillFrogRidingThisLog()
    {
        // Look for FrogMovement in this log's children (active or inactive)
        var frog = GetComponentInChildren<FrogMovement>(includeInactive: true);
        if (frog != null)
        {
            frog.GameOver(); // uses your existing death flow
        }
    }
}
