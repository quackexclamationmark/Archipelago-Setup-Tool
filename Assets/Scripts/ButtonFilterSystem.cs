using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System;
using System.IO;

/// <summary>
/// Système de filtrage et recherche pour boutons avec plusieurs noms (alias)
/// </summary>
public class ButtonFilterSystem : MonoBehaviour
{
    [System.Serializable]
    public class FilteredButton
    {
        public Button button;
        public Text buttonText;
        public string mainName;
        public List<string> aliases = new List<string>();
        public int matchScore;
        public bool isVisible = true;

        public List<string> GetAllNames()
        {
            var allNames = new List<string> { mainName };
            allNames.AddRange(aliases);
            return allNames;
        }

        public bool ContainsSearchTerm(string searchTerm)
        {
            // Chercher dans le nom principal
            if (mainName.ToLower().Contains(searchTerm.ToLower()))
                return true;

            // Chercher dans les alias aussi
            foreach (var alias in aliases)
            {
                if (alias.ToLower().Contains(searchTerm.ToLower()))
                    return true;
            }

            return false;
        }

        public void CalculateMatchScore(string searchTerm)
        {
            matchScore = 0;
            string lowerSearchTerm = searchTerm.ToLower();

            // Chercher dans le nom principal (priorité haute)
            string lowerMainName = mainName.ToLower();
            int mainIndex = lowerMainName.IndexOf(lowerSearchTerm);
            if (mainIndex != -1)
            {
                matchScore = 1000 / (mainIndex + 1);
                return;
            }

            // Chercher dans les alias (priorité basse)
            foreach (var alias in aliases)
            {
                string lowerAlias = alias.ToLower();
                int aliasIndex = lowerAlias.IndexOf(lowerSearchTerm);
                if (aliasIndex != -1)
                {
                    int score = 100 / (aliasIndex + 1);
                    matchScore = Mathf.Max(matchScore, score);
                }
            }
        }

        public void SetActive(bool active)
        {
            isVisible = active;
            button.interactable = active;
            button.gameObject.SetActive(active);

            Color buttonColor = button.GetComponent<Image>().color;
            buttonColor.a = active ? 1f : 0.3f;
            button.GetComponent<Image>().color = buttonColor;
        }
    }

    [Header("UI References")]
    [SerializeField] private Transform buttonsContainer;
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private GameObject noResultsPanel;

    private List<FilteredButton> buttons = new List<FilteredButton>();
    private List<string> gameOrder = new List<string>(); // L'ordre de SearchGame
    private string currentSearchTerm = "";
    private bool isSearching = false;

    private void OnEnable()
    {
        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(OnSearchInputChanged);
        }
    }

    private void OnDisable()
    {
        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(OnSearchInputChanged);
        }
    }

    public void AddButton(Button button, string mainName, List<string> aliases = null, UnityEngine.Events.UnityAction onClick = null)
    {
        var filteredButton = new FilteredButton
        {
            button = button,
            buttonText = button.GetComponentInChildren<Text>(),
            mainName = mainName,
            aliases = aliases ?? new List<string>()
        };

        buttons.Add(filteredButton);
        gameOrder.Add(mainName); // Sauvegarder l'ordre de SearchGame

        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }
    }

    private void OnSearchInputChanged(string newSearchTerm)
    {
        currentSearchTerm = newSearchTerm.Trim();

        if (string.IsNullOrEmpty(currentSearchTerm) || string.IsNullOrWhiteSpace(newSearchTerm))
        {
            // Restaurer l'ordre de SearchGame
            ResetToGameOrder();
            isSearching = false;
        }
        else
        {
            if (!isSearching)
            {
                isSearching = true;
            }
            FilterAndSortButtons(currentSearchTerm);
        }
    }

    private void FilterAndSortButtons(string searchTerm)
    {
        int visibleCount = 0;
        var visibleButtons = new List<FilteredButton>();

        foreach (var filteredBtn in buttons)
        {
            if (filteredBtn.ContainsSearchTerm(searchTerm))
            {
                filteredBtn.CalculateMatchScore(searchTerm);
                filteredBtn.SetActive(true);
                visibleButtons.Add(filteredBtn);
                visibleCount++;
            }
            else
            {
                filteredBtn.SetActive(false);
            }
        }

        visibleButtons = visibleButtons.OrderByDescending(b => b.matchScore).ToList();

        for (int i = 0; i < visibleButtons.Count; i++)
        {
            visibleButtons[i].button.transform.SetSiblingIndex(i);
        }

        if (noResultsPanel != null)
        {
            noResultsPanel.SetActive(visibleCount == 0);
        }
    }

    private void ResetToGameOrder()
    {
        // Restaurer l'ordre exactement comme dans SearchGame
        for (int i = 0; i < gameOrder.Count; i++)
        {
            var button = buttons.FirstOrDefault(b => b.mainName == gameOrder[i]);
            if (button != null)
            {
                button.SetActive(true);
                button.button.transform.SetSiblingIndex(i);
            }
        }

        if (noResultsPanel != null)
        {
            noResultsPanel.SetActive(false);
        }
    }

    public void Clear()
    {
        buttons.Clear();
        gameOrder.Clear();
        if (searchInput != null)
        {
            searchInput.text = "";
        }
    }
}
