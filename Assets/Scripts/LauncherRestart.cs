using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

public class LauncherRestart : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    void Awake()
    {
    }

    public void RestartApplication()
    {
        Debug.Log("Redémarrage de l'application...");
        KillAndRestart();
    }

    void KillAndRestart()
    {
        try
        {
            string exePath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Archipelago Setup Tool.exe");
            
            if (File.Exists(exePath))
            {
                Debug.Log($"Relance : {exePath}");
                Process.Start(exePath);
            }
            else
            {
                exePath = "Archipelago Setup Tool.exe";
                if (File.Exists(exePath))
                {
                    Process.Start(exePath);
                }
                else
                {
                    Debug.LogError($"Impossible de trouver l'exécutable");
                    return;
                }
            }
            
            Process.GetCurrentProcess().Kill();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Restart failed: {ex}");
        }
    }
#else
    public void RestartApplication()
    {
    }
#endif
}
