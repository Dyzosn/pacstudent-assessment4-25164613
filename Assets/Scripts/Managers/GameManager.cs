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
    [SerializeField] private GameObject[] lifeIcons;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Ghost Timer UI")]
    [SerializeField] private GameObject ghostTimerContainer;
    [SerializeField] private TextMeshProUGUI ghostTimerText;

    [Header("Ghost Management")]
    [SerializeField] private AudioController audioController;

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
    private const float RECOVERING_THRESHOLD = 3f; // 3 seconds left = recovering

    private bool isGameActive = false;
    private GameObject[] allGhosts;

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

        UpdateUI();

        // TEMPORARY: Auto-start game for testing
        StartGame();
        Debug.Log("Game started automatically (temporary for testing)");
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

        // Change music to scared state
        if (audioController != null)
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

        // Change music back to normal
        if (audioController != null)
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
                Animator animator = ghost.GetComponent<Animator>();
                if (animator != null)
                {
                    // Only change state if not dead
                    int currentState = animator.GetInteger("GhostState");
                    if (currentState != (int)GhostState.Dead)
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
        Debug.Log("Game Over!");
        // Full game over implementation later
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
}