using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LavaController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float MaxMoveSpeed = 2.0f;
    [SerializeField] private float startDelay = 10f;
    private Rigidbody2D rb2D;
    private bool isRising = false;

    [SerializeField] private Transform playerPos;
    [SerializeField] private bool isPlayerClose = false;
    [SerializeField] private float distance = 10f;

    [SerializeField] private FrogMovement menu;


    private void Start()
    {
        rb2D = GetComponent<Rigidbody2D>();
        StartCoroutine(StartLavaRise());
    }

    private IEnumerator StartLavaRise()
    {
        yield return new WaitForSeconds(startDelay);
        isRising = true;
    }

    private void Update()
    {
        if (isRising && playerPos != null)
        {
            float yDifference = playerPos.position.y - rb2D.position.y;

            if (Mathf.Abs(yDifference) >= distance)
            {
                rb2D.linearVelocity = new Vector2(0f, Mathf.Sign(yDifference) * MaxMoveSpeed);
            }
            else
            {
                rb2D.linearVelocity = new Vector2(0f, Mathf.Sign(yDifference) * moveSpeed);
            }
        }
        else
        {
            rb2D.linearVelocity = Vector2.zero;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            //Destroy(other.gameObject);
           
            FrogMovement gameOverMenu = GetComponent<FrogMovement>();
            menu.GameOver();
           
        }
    }
}
