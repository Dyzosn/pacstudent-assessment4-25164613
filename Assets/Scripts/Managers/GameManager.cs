using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    // Singleton instance
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int currentLives = 3;
    [SerializeField] private float gameTime = 0f;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private TextMeshProUGUI timerText;

    private bool isGameActive = false;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        UpdateUI();
    }

    void Update()
    {
        // Update game timer if active
        if (isGameActive)
        {
            gameTime += Time.deltaTime;
            UpdateTimerUI();
        }
    }

    public void AddScore(int points)
    {
        currentScore += points;
        UpdateScoreUI();
    }

    public void LoseLife()
    {
        currentLives--;
        UpdateLivesUI();

        if (currentLives <= 0)
        {
            GameOver();
        }
    }

    public void StartGame()
    {
        isGameActive = true;
        gameTime = 0f;
    }

    void GameOver()
    {
        isGameActive = false;
        Debug.Log("Game Over!");
        // Will implement full game over later
    }

    void UpdateUI()
    {
        UpdateScoreUI();
        UpdateLivesUI();
        UpdateTimerUI();
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString("D6"); // Format: 000000
        }
    }

    void UpdateLivesUI()
    {
        if (livesText != null)
        {
            livesText.text = currentLives.ToString();
        }
    }

    void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(gameTime / 60f);
            int seconds = Mathf.FloorToInt(gameTime % 60f);
            int milliseconds = Mathf.FloorToInt((gameTime * 100f) % 100f);

            timerText.text = string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, milliseconds);
        }
    }

    // Getters
    public int GetScore() => currentScore;
    public int GetLives() => currentLives;
    public float GetGameTime() => gameTime;
    public bool IsGameActive() => isGameActive;
}