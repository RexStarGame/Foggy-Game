using UnityEngine;

public class BlueCarMovement : MonoBehaviour
{
    public float movementSpeed = 1f;

    void Update()
    {
        // Move to the right
        transform.Translate(Vector3.right * movementSpeed * Time.deltaTime);

    }
}
