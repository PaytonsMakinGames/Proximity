using UnityEngine;

public class CatchVFX2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] ParticleSystem catchBurst;

    // Call this when a catch begins (pickup moment).
    public void PlayCatch()
    {
        if (catchBurst == null) return;

        // Ensure it plays at the ball position right now.
        catchBurst.transform.position = transform.position;
        catchBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        catchBurst.Play(true);
    }
}