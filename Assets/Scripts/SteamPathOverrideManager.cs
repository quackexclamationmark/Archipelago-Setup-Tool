/*using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class SteamPathOverrideManager : MonoBehaviour
{
    [Serializable]
    public class ScriptEntry
    {
        public MonoBehaviour script;
        public string gameFolderName;
    }

    [Header("Scripts à patcher")]
    public List<ScriptEntry> targetScripts = new List<ScriptEntry>();

    [Header("Dossiers Steam à chercher (relatifs à chaque lettre de lecteur)")]
    [Tooltip("Utilise [USERNAME] pour qu'il soit remplacé automatiquement par le nom de l'utilisateur Windows courant. Ex: 'Users\\[USERNAME]\\Documents'")]
    public List<string> steamSearchDirectories = new List<string>()
    {
        "Steam\\steamapps\\common",
        "SteamLibrary\\steamapps\\common",
    };

    [Header("Options")]
    public bool verboseLogging = true;

    private Harmony harmony;
    private static Dictionary<MonoBehaviour, string> instanceToFolderName = new Dictionary<MonoBehaviour, string>();
    private static HashSet<MethodBase> patchedMethods = new HashSet<MethodBase>();
    private static SteamPathOverrideManager activeInstance;

    void Awake()
    {
        activeInstance = this;
        harmony = new Harmony("com.custom.steampathoverride." + GetInstanceID());

        foreach (var entry in targetScripts)
        {
            if (entry.script == null || string.IsNullOrEmpty(entry.gameFolderName))
            {
                Debug.LogWarning("[SteamPathOverrideManager] Entrée invalide (script ou nom de dossier manquant), ignorée.");
                continue;
            }

            instanceToFolderName[entry.script] = entry.gameFolderName;

            Type type = entry.script.GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.ReturnType == typeof(string) && m.GetParameters().Length == 0)
                .Where(m => m.Name.Contains("Get") && m.Name.Contains("Path"))
                .Where(m => !m.Name.Contains("Epic"))
                .ToList();

            if (methods.Count == 0)
            {
                Debug.LogWarning($"[SteamPathOverrideManager] Aucune méthode correspondante trouvée sur {type.Name}.");
                continue;
            }

            foreach (var method in methods)
            {
                if (patchedMethods.Contains(method))
                    continue;

                try
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(SteamPathOverrideManager), nameof(GenericPrefix)));
                    patchedMethods.Add(method);
                    if (verboseLogging)
                        Debug.Log($"[SteamPathOverrideManager] Patch appliqué sur {type.Name}.{method.Name}()");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SteamPathOverrideManager] Échec du patch sur {type.Name}.{method.Name} - {e.Message}");
                }
            }
        }
    }

    static bool GenericPrefix(object __instance, ref string __result)
    {
        if (!(__instance is MonoBehaviour mb) || !instanceToFolderName.TryGetValue(mb, out string folderName))
        {
            return true;
        }

        __result = FindGameFolder(folderName);

        if (activeInstance != null && activeInstance.verboseLogging)
            Debug.Log($"[SteamPathOverrideManager] Recherche de '{folderName}' -> résultat: '{(string.IsNullOrEmpty(__result) ? "(introuvable)" : __result)}'");

        return false;
    }

    static string FindGameFolder(string gameFolderName)
    {
        List<string> dirsToCheck = activeInstance != null
            ? activeInstance.steamSearchDirectories
            : new List<string>();

        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.DriveType != DriveType.Fixed) continue;
                if (!drive.IsReady) continue;

                foreach (var rawRelativeDir in dirsToCheck)
                {
                    if (string.IsNullOrWhiteSpace(rawRelativeDir)) continue;

                    string relativeDir = rawRelativeDir.Replace("[USERNAME]", Environment.UserName);

                    try
                    {
                        string candidate = Path.Combine(drive.Name, relativeDir, gameFolderName);
                        if (Directory.Exists(candidate))
                            return candidate;
                    }
                    catch { }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SteamPathOverrideManager] Erreur pendant la recherche des lecteurs - " + e.Message);
        }

        return "";
    }

    void OnDestroy()
    {
        if (harmony != null)
        {
            harmony.UnpatchSelf();
        }
        patchedMethods.Clear();
        instanceToFolderName.Clear();
        if (activeInstance == this) activeInstance = null;
    }
}*/