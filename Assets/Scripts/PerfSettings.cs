using UnityEngine;

public class PerfSettings : MonoBehaviour
{
    void Awake()
    {
        Application.targetFrameRate = 120; // S24 Ultra can do 120
        Time.fixedDeltaTime = 1f / 120f;      // match physics to 120
        QualitySettings.vSyncCount = 0;
    }
}