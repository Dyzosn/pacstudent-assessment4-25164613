using UnityEngine;
using TMPro;

public class StartSceneManager : MonoBehaviour
{
    [Header("High Score Display")]
    [SerializeField] private TextMeshProUGUI level1HighScoreText;
    [SerializeField] private TextMeshProUGUI level2HighScoreText;

    // PlayerPrefs keys - must match GameManager
    private const string LEVEL1_HIGH_SCORE_KEY = "Level1_HighScore";
    private const string LEVEL1_BEST_TIME_KEY = "Level1_BestTime";
    private const string LEVEL2_HIGH_SCORE_KEY = "Level2_HighScore";
    private const string LEVEL2_BEST_TIME_KEY = "Level2_BestTime";

    void Start()
    {
        LoadAndDisplayHighScores();
    }

    void LoadAndDisplayHighScores()
    {
        // Load Level 1 high score and time
        int level1Score = PlayerPrefs.GetInt(LEVEL1_HIGH_SCORE_KEY, 0);
        float level1Time = PlayerPrefs.GetFloat(LEVEL1_BEST_TIME_KEY, 0f);

        // Load Level 2 high score and time (for future innovation scene)
        int level2Score = PlayerPrefs.GetInt(LEVEL2_HIGH_SCORE_KEY, 0);
        float level2Time = PlayerPrefs.GetFloat(LEVEL2_BEST_TIME_KEY, 0f);

        // Update Level 1 display
        if (level1HighScoreText != null)
        {
            string formattedScore = level1Score.ToString("D6");
            string formattedTime = FormatTime(level1Time);
            level1HighScoreText.text = $"LEVEL 1\nHIGH-SCORE: {formattedScore}\nTIME: {formattedTime}";
        }

        // Update Level 2 display
        if (level2HighScoreText != null)
        {
            string formattedScore = level2Score.ToString("D6");
            string formattedTime = FormatTime(level2Time);
            level2HighScoreText.text = $"LEVEL 2\nHIGH-SCORE: {formattedScore}\nTIME: {formattedTime}";
        }

        Debug.Log($"Loaded high scores - Level 1: {level1Score} ({level1Time}s), Level 2: {level2Score} ({level2Time}s)");
    }

    string FormatTime(float timeInSeconds)
    {
        // Format time as mm:ss:ms (e.g., 01:32:25)
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        int milliseconds = Mathf.FloorToInt((timeInSeconds * 100f) % 100f);

        return string.Format("{0:D2}:{1:D2}:{2:D2}", minutes, seconds, milliseconds);
    }

    // Optional: Method to reset high scores (useful for testing)
    public void ResetHighScores()
    {
        PlayerPrefs.DeleteKey(LEVEL1_HIGH_SCORE_KEY);
        PlayerPrefs.DeleteKey(LEVEL1_BEST_TIME_KEY);
        PlayerPrefs.DeleteKey(LEVEL2_HIGH_SCORE_KEY);
        PlayerPrefs.DeleteKey(LEVEL2_BEST_TIME_KEY);
        PlayerPrefs.Save();

        LoadAndDisplayHighScores();

        Debug.Log("All high scores reset to zero");
    }
}