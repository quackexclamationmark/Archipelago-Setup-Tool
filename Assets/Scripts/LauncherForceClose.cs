using UnityEngine;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class LauncherForceClose : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    void Awake()
    {
        Application.wantsToQuit += OnWantsToQuit;
    }

    void OnDestroy()
    {
        Application.wantsToQuit -= OnWantsToQuit;
    }

    bool OnWantsToQuit()
    {
        KillLauncher();
        return true;
    }

    void OnApplicationQuit()
    {
        KillLauncher();
    }

    void KillLauncher()
    {
        try
        {
            Process.GetCurrentProcess().Kill();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Kill failed: {ex}. Trying Environment.Exit...");
            try
            {
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"Environment.Exit failed: {e}");
            }
        }
    }
#else
    void Awake() { }
    void OnApplicationQuit() { }
#endif
}