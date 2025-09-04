using UnityEngine;
using TMPro;

public class PlayerScore : MonoBehaviour
{
    public int score = 0;
    public TMP_Text scoreText;
    public TMP_Text highScoreText; // new field to display high score
    private int maxY = 0; // farthest row reached

    private int highScore = 0;

    void Start()
    {
        // Load high score from PlayerPrefs
        highScore = PlayerPrefs.GetInt("HighScore", 0);
        UpdateScoreText();
    }

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

        // Update high score if current score is higher
        if (score > PlayerPrefs.GetInt("HighScore", 0))
        {
            PlayerPrefs.SetInt("HighScore", score);
            PlayerPrefs.Save();
        }
    }

}
