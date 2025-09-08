using UnityEngine;
using TMPro;

public class DeathScreenUI : MonoBehaviour
{
    [SerializeField] private GameObject deathMenu; // Death screen panel
    [SerializeField] private TMP_Text scoreText;   // Text on the death screen
    [SerializeField] private TMP_Text highScoreText;

    void Start()
    {
        if (deathMenu != null)
            deathMenu.SetActive(false);
    }

    // Show death screen with final score
    public void ShowDeathScreen(int finalScore)
    {
        if (deathMenu != null)
            deathMenu.SetActive(true);

        if (scoreText != null)
            scoreText.text = "Score: " + finalScore;

        if (highScoreText != null)
        {
            int highScore = PlayerPrefs.GetInt("HighScore", 0);
            highScoreText.text = "High Score: " + highScore;
        }
    }

}
