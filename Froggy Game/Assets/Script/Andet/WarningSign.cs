using UnityEngine;
using System.Collections;

public class WarningSign : MonoBehaviour
{
    public GameObject warningSign;    // Assign in Inspector
    public GameObject lava;           // Assign in Inspector
    public Transform player;          // Assign in Inspector
    public float warningVerticalDistance = 100f; // Adjust as needed
    public float blinkInterval = 0.5f; // How fast it blinks in seconds

    private Coroutine blinkCoroutine;

    void Update()
    {
        if (lava == null || warningSign == null || player == null)
            return;

        Collider2D lavaCollider = lava.GetComponent<Collider2D>();
        if (lavaCollider == null)
            return;

        float lavaTopY = lavaCollider.bounds.max.y;
        float playerY = player.position.y;
        float verticalDistance = Mathf.Abs(playerY - lavaTopY);

        bool isLavaClose = verticalDistance <= warningVerticalDistance;

        // Start blinking if lava is close and not already blinking
        if (isLavaClose && blinkCoroutine == null)
        {
            blinkCoroutine = StartCoroutine(BlinkWarning());
        }
        // Stop blinking if lava is not close
        else if (!isLavaClose && blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
            warningSign.SetActive(false); // Make sure it's hidden when not blinking
        }
    }

    private IEnumerator BlinkWarning()
    {
        while (true)
        {
            warningSign.SetActive(!warningSign.activeSelf);
            yield return new WaitForSeconds(blinkInterval);
        }
    }
}
