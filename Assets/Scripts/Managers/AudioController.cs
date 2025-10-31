using UnityEngine;

public class AudioController : MonoBehaviour
{
    [Header("Music Clips")]
    public AudioClip introMusic;
    public AudioClip normalStateMusic;
    public AudioClip scaredStateMusic;
    public AudioClip deadStateMusic;

    [Header("Settings")]
    public bool letIntroFinish = false;

    private AudioSource audioSource;
    private float startTime;
    private bool switchedToNormal = false;
    private bool isIntroPhase = true;
    private bool isManuallyControlled = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        startTime = Time.time;

        // Start playing intro music
        if (introMusic != null)
        {
            audioSource.clip = introMusic;
            audioSource.loop = false;
            audioSource.Play();

            Debug.Log("Started intro music - duration: " + introMusic.length + " seconds");
        }
        else
        {
            Debug.LogError("Intro music not assigned!");
        }
    }

    void Update()
    {
        // Only handle intro transition if not manually controlled
        if (isIntroPhase && !switchedToNormal && !isManuallyControlled && audioSource != null)
        {
            float timeElapsed = Time.time - startTime;

            if (letIntroFinish)
            {
                if (!audioSource.isPlaying)
                {
                    Debug.Log("Intro finished naturally after " + timeElapsed + " seconds");
                    SwitchToNormalMusic();
                }
            }
            else
            {
                if (timeElapsed >= 3.0f)
                {
                    Debug.Log("Cutting intro at 3 seconds");
                    SwitchToNormalMusic();
                }
            }
        }
    }

    public void SwitchToNormalMusic()
    {
        if (normalStateMusic != null)
        {
            audioSource.Stop();
            audioSource.clip = normalStateMusic;
            audioSource.loop = true;
            audioSource.Play();
            switchedToNormal = true;
            isIntroPhase = false;
            isManuallyControlled = false; // Allow auto transitions again

            Debug.Log("Now playing normal state music");
        }
    }

    public void SwitchToScaredMusic()
    {
        if (scaredStateMusic != null)
        {
            audioSource.Stop();
            audioSource.clip = scaredStateMusic;
            audioSource.loop = true;
            audioSource.Play();
            isManuallyControlled = true; // Stop auto intro transitions

            Debug.Log("Switched to scared state music");
        }
    }

    public void SwitchToDeadMusic()
    {
        if (deadStateMusic != null)
        {
            audioSource.Stop();
            audioSource.clip = deadStateMusic;
            audioSource.loop = true;
            audioSource.Play();
            isManuallyControlled = true; // Stop auto intro transitions

            Debug.Log("Switched to dead state music");
        }
    }
}