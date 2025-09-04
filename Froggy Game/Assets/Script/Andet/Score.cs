using UnityEngine;
using TMPro;

public class PlayerScore : MonoBehaviour
{
    public int score = 0;
    public TMP_Text scoreText;
    private int maxY = 0; // farthest row reached

    void Start()
    {
        UpdateScoreText();
    }

    // Call this once after a move finishes
    public void TryAddScore(Vector3 playerPosition)
    {
        int currentY = Mathf.RoundToInt(playerPosition.y);

        if (currentY > maxY)
        {
            maxY = currentY;
            score += 10;
            UpdateScoreText();
        }
    }

    private void UpdateScoreText()
    {
        scoreText.text = "Score: " + score;
    }
}
