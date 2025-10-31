using UnityEngine;

public class ParticleAutoDestroy : MonoBehaviour
{
    void Start()
    {
        // Destroy particle after it finishes playing
        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps != null)
        {
            Destroy(gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
        }
    }
}