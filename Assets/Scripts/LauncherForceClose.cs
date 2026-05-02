using UnityEngine;
using System.Diagnostics;

public class LauncherForceClose : MonoBehaviour
{
    public void QuitLauncher()
    {
        KillLauncher();
    }

    void OnApplicationQuit()
    {
        KillLauncher();
    }

    void OnDestroy()
    {
        KillLauncher();
    }

    void OnDisable()
    {
        KillLauncher();
    }

    void KillLauncher()
    {
#if UNITY_STANDALONE_WIN
        Process.GetCurrentProcess().Kill();
#else
        Application.Quit();
#endif
    }
}