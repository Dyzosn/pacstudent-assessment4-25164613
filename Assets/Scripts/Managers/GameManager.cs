using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
    [SerializeField] private GameObject[] lifeIcons;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Countdown UI")]
    [SerializeField] private GameObject blockingImage;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI finalTimeText;
    [SerializeField] private TextMeshProUGUI highScoreText;

    [Header("Ghost Timer UI")]
    [SerializeField] private GameObject ghostTimerContainer;
    [SerializeField] private TextMeshProUGUI ghostTimerText;

    [Header("Ghost Management")]
    [SerializeField] private AudioController audioController;

    // PlayerPrefs key for high score
    private const string HIGH_SCORE_KEY = "HighScore";

    // Ghost state enum
    public enum GhostState
    {
        Normal = 0,
        Scared = 1,
        Recovering = 2,
        Dead = 3
    }

    private GhostState currentGhostState = GhostState.Normal;
    private bool isGhostScaredActive = false;
    private float ghostScaredTimer = 0f;
    private const float GHOST_SCARED_DURATION = 10f;
    private const float RECOVERING_THRESHOLD = 3f;

    private bool isGameActive = false;
    private GameObject[] allGhosts;
    private int deadGhostCount = 0;
    private int highScore = 0;

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
        // Find all ghosts in scene by tag
        allGhosts = GameObject.FindGameObjectsWithTag("Ghost");
        Debug.Log($"Found {allGhosts.Length} ghosts in scene");

        // Find audio controller if not assigned
        if (audioController == null)
        {
            audioController = FindFirstObjectByType<AudioController>();
        }

        // Hide ghost timer initially
        if (ghostTimerContainer != null)
        {
            ghostTimerContainer.SetActive(false);
        }

        // Hide game over panel initially
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        // Load high score from PlayerPrefs
        LoadHighScore();

        UpdateUI();

        // Start countdown sequence
        StartCoroutine(CountdownSequence());
    }

    void Update()
    {
        // Update game timer if active
        if (isGameActive)
        {
            gameTime += Time.deltaTime;
            UpdateTimerUI();
        }

        // Update ghost scared timer
        if (isGhostScaredActive)
        {
            UpdateGhostScaredTimer();
        }
    }

    IEnumerator CountdownSequence()
    {
        // Show blocking image
        if (blockingImage != null)
        {
            blockingImage.SetActive(true);
        }

        // Show countdown text
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }

        // Countdown: 3, 2, 1, GO!
        string[] countdownNumbers = { "3", "2", "1", "GO!" };

        foreach (string number in countdownNumbers)
        {
            if (countdownText != null)
            {
                countdownText.text = number;
            }

            Debug.Log($"Countdown: {number}");

            // Wait 1 second
            yield return new WaitForSeconds(1f);
        }

        // Hide countdown UI
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        if (blockingImage != null)
        {
            blockingImage.SetActive(false);
        }

        // Start game
        StartGame();

        Debug.Log("Countdown finished - game started!");
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

        // Start normal music
        if (audioController != null)
        {
            audioController.StartNormalMusic();
        }

        Debug.Log("Game active - player can now move");
    }

    public void PacStudentDeath()
    {
        // PacStudent has died - lose a life
        LoseLife();

        Debug.Log($"PacStudent died! Lives remaining: {currentLives}");

        // Death sequence will be handled by PacStudentController
        // After death animation finishes, it will call RespawnAll()
    }

    public void RespawnAll()
    {
        // Reset all ghosts to initial positions and normal state
        foreach (GameObject ghost in allGhosts)
        {
            if (ghost != null)
            {
                GhostController ghostController = ghost.GetComponent<GhostController>();
                if (ghostController != null)
                {
                    ghostController.ResetToInitialPosition();
                }
            }
        }

        // End scared mode if active
        if (isGhostScaredActive)
        {
            EndGhostScaredMode();
        }

        Debug.Log("All entities respawned to initial positions");
    }

    public void OnGhostDeath()
    {
        // Called when a ghost is eaten by PacStudent
        deadGhostCount++;

        // Add score
        AddScore(300);

        // Switch to dead music if not already playing
        if (audioController != null)
        {
            audioController.SwitchToDeadMusic();
        }

        Debug.Log($"Ghost eaten! +300 points. Dead ghosts: {deadGhostCount}");
    }

    public void OnGhostRespawn()
    {
        // Called when a dead ghost respawns
        deadGhostCount--;

        // If no more dead ghosts, return music to appropriate state
        if (deadGhostCount <= 0)
        {
            deadGhostCount = 0;

            if (audioController != null)
            {
                if (isGhostScaredActive)
                {
                    audioController.SwitchToScaredMusic();
                }
                else
                {
                    audioController.SwitchToNormalMusic();
                }
            }
        }

        Debug.Log($"Ghost respawned. Dead ghosts remaining: {deadGhostCount}");
    }

    public void StartGhostScaredMode()
    {
        // Start 10 second scared timer
        isGhostScaredActive = true;
        ghostScaredTimer = GHOST_SCARED_DURATION;

        // Show ghost timer UI
        if (ghostTimerContainer != null)
        {
            ghostTimerContainer.SetActive(true);
        }

        // Set all ghosts to scared state
        SetAllGhostsState(GhostState.Scared);

        // Change music to scared state (unless ghost is dead)
        if (audioController != null && deadGhostCount == 0)
        {
            audioController.SwitchToScaredMusic();
        }

        Debug.Log("Ghost scared mode activated! Timer: 10 seconds");
    }

    void UpdateGhostScaredTimer()
    {
        ghostScaredTimer -= Time.deltaTime;

        // Update timer UI text
        if (ghostTimerText != null)
        {
            int secondsLeft = Mathf.CeilToInt(ghostScaredTimer);
            ghostTimerText.text = secondsLeft.ToString();
        }

        // Check for recovering state (3 seconds left)
        if (ghostScaredTimer <= RECOVERING_THRESHOLD && currentGhostState == GhostState.Scared)
        {
            SetAllGhostsState(GhostState.Recovering);
            Debug.Log("Ghosts entering recovering state (3 seconds left)");
        }

        // Timer finished - return to normal
        if (ghostScaredTimer <= 0f)
        {
            EndGhostScaredMode();
        }
    }

    void EndGhostScaredMode()
    {
        isGhostScaredActive = false;
        ghostScaredTimer = 0f;

        // Hide ghost timer UI
        if (ghostTimerContainer != null)
        {
            ghostTimerContainer.SetActive(false);
        }

        // Set all non-dead ghosts back to normal
        SetAllGhostsStateExceptDead(GhostState.Normal);

        // Change music back to normal (unless ghost is dead)
        if (audioController != null && deadGhostCount == 0)
        {
            audioController.SwitchToNormalMusic();
        }

        Debug.Log("Ghost scared mode ended - back to normal");
    }

    void SetAllGhostsState(GhostState newState)
    {
        currentGhostState = newState;

        foreach (GameObject ghost in allGhosts)
        {
            if (ghost != null)
            {
                Animator animator = ghost.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetInteger("GhostState", (int)newState);
                }
            }
        }

        Debug.Log($"All ghosts set to state: {newState}");
    }

    void SetAllGhostsStateExceptDead(GhostState newState)
    {
        currentGhostState = newState;

        foreach (GameObject ghost in allGhosts)
        {
            if (ghost != null)
            {
                GhostController ghostController = ghost.GetComponent<GhostController>();
                if (ghostController != null && !ghostController.IsDead())
                {
                    Animator animator = ghost.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.SetInteger("GhostState", (int)newState);
                    }
                }
            }
        }

        Debug.Log($"All non-dead ghosts set to state: {newState}");
    }

    void GameOver()
    {
        isGameActive = false;

        // Check and save high score
        if (currentScore > highScore)
        {
            highScore = currentScore;
            SaveHighScore();
            Debug.Log($"New high score! {highScore}");
        }

        // Show game over screen
        ShowGameOverScreen();

        Debug.Log($"Game Over! Final Score: {currentScore}, Time: {gameTime:F2}s");
    }

    void ShowGameOverScreen()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // Update final score text
        if (finalScoreText != null)
        {
            finalScoreText.text = $"Final Score: {currentScore.ToString("D6")}";
        }

        // Update final time text
        if (finalTimeText != null)
        {
            int minutes = Mathf.FloorToInt(gameTime / 60f);
            int seconds = Mathf.FloorToInt(gameTime % 60f);
            int milliseconds = Mathf.FloorToInt((gameTime * 100f) % 100f);
            finalTimeText.text = $"Time: {minutes:D2}:{seconds:D2}:{milliseconds:D2}";
        }

        // Update high score text
        if (highScoreText != null)
        {
            highScoreText.text = $"High Score: {highScore.ToString("D6")}";
        }

        Debug.Log("Game Over screen displayed");
    }

    void LoadHighScore()
    {
        // Load high score from PlayerPrefs (default to 0 if not found)
        highScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        Debug.Log($"Loaded high score: {highScore}");
    }

    void SaveHighScore()
    {
        // Save high score to PlayerPrefs
        PlayerPrefs.SetInt(HIGH_SCORE_KEY, highScore);
        PlayerPrefs.Save(); // Force save to disk
        Debug.Log($"Saved high score: {highScore}");
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
            scoreText.text = currentScore.ToString("D6");
        }
    }

    void UpdateLivesUI()
    {
        // Show or hide life icons based on remaining lives
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (lifeIcons[i] != null)
            {
                lifeIcons[i].SetActive(i < currentLives);
            }
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
    public GhostState GetCurrentGhostState() => currentGhostState;
    public bool IsGhostScaredActive() => isGhostScaredActive;
    public float GetGhostScaredTimeRemaining() => ghostScaredTimer;
}