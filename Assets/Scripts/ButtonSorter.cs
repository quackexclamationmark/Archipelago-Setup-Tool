/*using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class ButtonSorter : MonoBehaviour
{
    [Tooltip("Parent contenant les boutons. Si null, prend cet objet.")]
    public Transform container;

    [Tooltip("Trier automatiquement au démarrage (runtime)")]
    public bool sortOnStart = true;

    [Tooltip("Trier automatiquement en mode édition quand quelque chose change")]
    public bool sortInEditor = false;

    [Tooltip("Inclure les GameObjects inactifs")]
    public bool includeInactive = false;

    [Tooltip("Si true, ne trie que les enfants directs du container. Si false, parcourt les descendants et trie par parent.")]
    public bool onlyDirectChildren = true;

    [Tooltip("Tri sensible à la casse si coché")]
    public bool caseSensitive = false;

    [Tooltip("Ignorer uniquement l'article initial 'The ' (avec espace) ou 'The' collé (ex: 'TheWitness').")]
    public bool ignoreLeadingThe = true;

    [Tooltip("Relancer automatiquement le tri quand on change ignoreLeadingThe via les méthodes publiques.")]
    public bool sortOnToggle = true;

    [Tooltip("Si activé, loge la clé lue et la clé nettoyée pour debug dans la Console.")]
    public bool logKeys = false;

    public enum Source { GameButtonGameName, GameObjectName }
    [Tooltip("Utiliser la propriété/field 'gameName' du composant GameButton si présent, sinon le nom du GameObject")]
    public Source source = Source.GameButtonGameName;

    private string settingsFolder;
    private string settingsFile;

    void Reset()
    {
        if (container == null) container = transform;
    }

    void Awake()
    {
        // Initialiser les chemins
        string appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        settingsFolder = System.IO.Path.Combine(appDataRoaming, "Archipelago Setup Tool");
        settingsFile = System.IO.Path.Combine(settingsFolder, "buttonstate.json");

        // Charger les settings AU DEMARRAGE (même si panel inactif)
        if (Application.isPlaying && sortOnStart)
        {
            var settings = LoadSettingsFile();
            ignoreLeadingThe = settings.ignoreLeadingThe;
            Debug.Log($"[ButtonSorter] Awake: Loaded ignoreLeadingThe={ignoreLeadingThe} from settings");

            SortAlphabetically();
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying && sortInEditor) SortAlphabetically();
    }

    [ContextMenu("Sort Alphabetically")]
    public void SortAlphabetically()
    {
        if (container == null) container = transform;

        var comparer = caseSensitive ? StringComparer.CurrentCulture : StringComparer.CurrentCultureIgnoreCase;

        if (onlyDirectChildren)
        {
            SortChildrenList(GetDirectChildren(container).ToList(), container, comparer);
        }
        else
        {
            var allDesc = container.GetComponentsInChildren<Transform>(includeInactive)
                                  .Where(t => t != container).ToArray();

            var parents = allDesc.Select(t => t.parent).Distinct();
            foreach (var parent in parents)
            {
                var children = new List<Transform>();
                for (int i = 0; i < parent.childCount; i++)
                {
                    var c = parent.GetChild(i);
                    if (includeInactive || c.gameObject.activeSelf)
                        children.Add(c);
                }

                SortChildrenList(children, parent, comparer);
            }
        }
    }

    void SortChildrenList(List<Transform> originalChildren, Transform parentForUndo, StringComparer comparer)
    {
        var sortable = originalChildren.Where(t => t.GetComponent<GameButton>() != null).ToList();
        if (sortable.Count == 0) return;

        var sortedQueue = sortable.OrderBy(t =>
        {
            string key = ReadKeyFromTransform(t);
            var cleaned = CleanForSort(key);
            if (logKeys)
            {
                Debug.Log($"[SortDebug] '{t.gameObject.name}' => original:'{key}' -> cleaned:'{cleaned}'");
            }
            return cleaned;
        }, comparer).ToList();

        var empties = originalChildren.Where(t => string.Equals(t.gameObject.name, "EMPTY", StringComparison.OrdinalIgnoreCase)).ToList();

        var finalOrder = new List<Transform>();
        var sortedIndex = 0;
        foreach (var child in originalChildren)
        {
            if (empties.Contains(child)) continue;

            if (child.GetComponent<GameButton>() != null)
            {
                if (sortedIndex < sortedQueue.Count)
                {
                    finalOrder.Add(sortedQueue[sortedIndex]);
                    sortedIndex++;
                }
                else
                {
                    finalOrder.Add(child);
                }
            }
            else
            {
                finalOrder.Add(child);
            }
        }

        finalOrder.AddRange(empties);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RegisterCompleteObjectUndo(parentForUndo.gameObject, "Sort children");
            for (int i = 0; i < finalOrder.Count; i++) finalOrder[i].SetSiblingIndex(i);
            EditorUtility.SetDirty(parentForUndo.gameObject);
            return;
        }
#endif
        for (int i = 0; i < finalOrder.Count; i++) finalOrder[i].SetSiblingIndex(i);
    }

    string ReadKeyFromTransform(Transform t)
    {
        string key = null;
        if (source == Source.GameButtonGameName)
        {
            var gb = t.GetComponent<GameButton>();
            if (gb != null)
            {
                try
                {
                    var type = gb.GetType();
                    var field = type.GetField("gameName");
                    if (field != null) key = field.GetValue(gb) as string;

                    if (string.IsNullOrEmpty(key))
                    {
                        var prop = type.GetProperty("gameName");
                        if (prop != null) key = prop.GetValue(gb) as string;
                    }

                    if (string.IsNullOrEmpty(key))
                    {
                        var method = type.GetMethod("GetSortName");
                        if (method != null) key = method.Invoke(gb, null) as string;
                    }
                }
                catch
                {
                    key = null;
                }
            }
        }

        if (string.IsNullOrEmpty(key))
            key = t.gameObject.name;

        return key ?? string.Empty;
    }

    string CleanForSort(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        s = s.Normalize(System.Text.NormalizationForm.FormKC);
        s = s.Replace('\u00A0', ' ');
        s = s.Replace('\u200B', ' ');
        s = s.Replace('\uFEFF', ' ');
        s = TrimAllWhitespace(s);

        if (ignoreLeadingThe)
        {
            if (s.Length >= 4 && s.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(4).TrimStart();
            }
            else if (s.Length > 3 && s.StartsWith("the", StringComparison.OrdinalIgnoreCase))
            {
                char next = s[3];
                if (char.IsUpper(next))
                {
                    s = s.Substring(3).TrimStart();
                }
            }
        }

        return s.Trim();
    }

    IEnumerable<Transform> GetDirectChildren(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (includeInactive || c.gameObject.activeSelf) yield return c;
        }
    }

    string TrimAllWhitespace(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        int start = 0;
        int end = input.Length - 1;
        while (start <= end && char.IsWhiteSpace(input[start])) start++;
        while (end >= start && char.IsWhiteSpace(input[end])) end--;
        if (start == 0 && end == input.Length - 1) return input;
        return input.Substring(start, end - start + 1);
    }

    // ----- METHODES PUBLIQUES POUR ASSIGNATION DANS L'INSPECTEUR/UI -----

    public void SetIgnoreLeadingThe(bool value)
    {
        if (ignoreLeadingThe == value) return;
        ignoreLeadingThe = value;

        // Sauvegarder immédiatement
        var settings = LoadSettingsFile();
        settings.ignoreLeadingThe = value;
        SaveSettingsFile(settings);

        if (sortOnToggle)
        {
            SortAlphabetically();
        }
    }

    public void ToggleIgnoreLeadingThe()
    {
        SetIgnoreLeadingThe(!ignoreLeadingThe);
    }

    public void EnableIgnoreLeadingThe() => SetIgnoreLeadingThe(true);
    public void DisableIgnoreLeadingThe() => SetIgnoreLeadingThe(false);

    [ContextMenu("Toggle IgnoreLeadingThe")]
    void ContextToggleIgnoreLeadingThe()
    {
        ToggleIgnoreLeadingThe();
    }

    // ----- PERSISTENCE -----

    ButtonSettings LoadSettingsFile()
    {
        try
        {
            if (System.IO.File.Exists(settingsFile))
            {
                string json = System.IO.File.ReadAllText(settingsFile);
                var settings = JsonUtility.FromJson<ButtonSettings>(json);
                return settings ?? new ButtonSettings();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ButtonSorter] Failed to load settings file: {e}");
        }
        return new ButtonSettings();
    }

    void SaveSettingsFile(ButtonSettings settings)
    {
        try
        {
            System.IO.Directory.CreateDirectory(settingsFolder);
            string json = JsonUtility.ToJson(settings, true);
            System.IO.File.WriteAllText(settingsFile, json);
            Debug.Log($"[ButtonSorter] Saved settings: ignoreLeadingThe={settings.ignoreLeadingThe}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ButtonSorter] Failed to save settings: {e}");
        }
    }
}*/
