using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ToggleOnStart : MonoBehaviour
{
    [SerializeField] private Button activeButton;
    [SerializeField] private Button inactiveButton;
    [Tooltip("Référence au ButtonSorter (ton script)")]
    [SerializeField] private ButtonSorter buttonSorter;
    [SerializeField] private IntEvent selectionChanged;

    private ColorBlock activeButtonOriginalColors;
    private ColorBlock inactiveButtonOriginalColors;

    private string settingsFolder;
    private string settingsFile;

    private void Awake()
    {
        string appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        settingsFolder = System.IO.Path.Combine(appDataRoaming, "Archipelago Setup Tool");
        settingsFile = System.IO.Path.Combine(settingsFolder, "buttonstate.json");
    }

    private void Start()
    {
        if (activeButton == null || inactiveButton == null)
        {
            Debug.LogError("[ToggleOnStart] Buttons not assigned in inspector.");
            return;
        }

        activeButtonOriginalColors = activeButton.colors;
        inactiveButtonOriginalColors = inactiveButton.colors;

        // ATTENTION : on n'appelle pas RemoveAllListeners() pour ne pas écraser les callbacks existants.
        activeButton.onClick.AddListener(() => OnButtonClicked(0));
        inactiveButton.onClick.AddListener(() => OnButtonClicked(1));

        // Charger l'état et appliquer visuels
        var settings = LoadSettings();
        Debug.Log($"[ToggleOnStart] Start: Loaded settings: activeButtonIndex={settings.activeButtonIndex}, ignoreLeadingThe={settings.ignoreLeadingThe}");

        SetSelection(settings.activeButtonIndex, invokeEvent: false);

        Debug.Log($"[ToggleOnStart] Selection applied visually: index={settings.activeButtonIndex}");
    }

    private void OnButtonClicked(int index)
    {
        Debug.Log($"[ToggleOnStart] OnButtonClicked index={index}");
        SetSelection(index, invokeEvent: true);

        var settings = LoadSettings();
        settings.activeButtonIndex = index;

        // Sauvegarder le bouton cliqué et la configuration correspondante
        if (index == 0)
        {
            settings.ignoreLeadingThe = false;
        }
        else
        {
            settings.ignoreLeadingThe = true;
        }

        SaveSettings(settings);
        Debug.Log($"[ToggleOnStart] Saved settings: activeButtonIndex={index}, ignoreLeadingThe={settings.ignoreLeadingThe}");
    }

    public void SetSelection(int index, bool invokeEvent)
    {
        if (index == 0)
        {
            ApplyActiveVisual(activeButton);
            ApplyInactiveVisual(inactiveButton);
        }
        else
        {
            ApplyActiveVisual(inactiveButton);
            ApplyInactiveVisual(activeButton);
        }

        if (invokeEvent && selectionChanged != null)
            selectionChanged.Invoke(index);

        Debug.Log($"[ToggleOnStart] Selection applied visually: index={index}");
    }

    private void ApplyActiveVisual(Button button)
    {
        if (button == activeButton) button.colors = activeButtonOriginalColors;
        else if (button == inactiveButton) button.colors = inactiveButtonOriginalColors;
        button.interactable = true;
    }

    private void ApplyInactiveVisual(Button button)
    {
        ColorBlock original = (button == activeButton) ? activeButtonOriginalColors : inactiveButtonOriginalColors;
        Color disabled = original.disabledColor;
        ColorBlock cb = original;
        cb.normalColor = disabled;
        cb.highlightedColor = disabled;
        cb.pressedColor = disabled;
        cb.selectedColor = disabled;
        button.colors = cb;
        button.interactable = true;
    }

    private void SaveSettings(ButtonSettings settings)
    {
        try
        {
            Directory.CreateDirectory(settingsFolder);
            string json = JsonUtility.ToJson(settings, true);
            File.WriteAllText(settingsFile, json);
            Debug.Log($"[ToggleOnStart] Saved settings to {settingsFile}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ToggleOnStart] Failed to save settings: {e}");
        }
    }

    private ButtonSettings LoadSettings()
    {
        try
        {
            if (File.Exists(settingsFile))
            {
                string json = File.ReadAllText(settingsFile);
                var settings = JsonUtility.FromJson<ButtonSettings>(json);
                return settings ?? new ButtonSettings();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ToggleOnStart] Failed to load settings: {e}");
        }
        return new ButtonSettings();
    }
}
