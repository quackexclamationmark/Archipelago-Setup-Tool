using UnityEngine;
using System.Diagnostics;
using System.IO;

public class ShortcutManager : MonoBehaviour
{
    public static void CreateOrUpdateShortcut()
    {
#if UNITY_STANDALONE_WIN
        try
        {
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string exeDir = Path.GetDirectoryName(exePath);
            string createShortcutExe = Path.Combine(exeDir, "CreateShortcut.exe");

            if (!File.Exists(createShortcutExe))
            {
                UnityEngine.Debug.LogError("CreateShortcut.exe introuvable !");
                return;
            }

            ProcessStartInfo psi = new ProcessStartInfo()
            {
                FileName = createShortcutExe,
                Arguments = $"\"{exePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
            }

            UnityEngine.Debug.Log("✓ Raccourci créé/mis à jour");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Erreur : {e.Message}");
        }
#endif
    }
}