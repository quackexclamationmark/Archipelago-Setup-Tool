using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        [NonSerialized] public HashSet<string> tokens;

        public List<string> GetAllNames()
        {
            var allNames = new List<string> { mainName };
            allNames.AddRange(aliases);
            return allNames;
        }

        public bool ContainsSearchTerm(string searchTerm)
        {
            return SearchUtils.Matches(mainName, aliases, tokens, searchTerm);
        }

        public void CalculateMatchScore(string searchTerm)
        {
            matchScore = SearchUtils.GetScore(mainName, aliases, tokens, searchTerm);
        }

        public void SetActive(bool active)
        {
            isVisible = active;
            if (button == null) return;

            button.interactable = active;
            button.gameObject.SetActive(active);

            var img = button.GetComponent<Image>();
            if (img != null)
            {
                Color buttonColor = img.color;
                buttonColor.a = active ? 1f : 0.3f;
                img.color = buttonColor;
            }
        }
    }

    [Header("UI References")]
    [SerializeField] private Transform buttonsContainer;
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private GameObject noResultsPanel;

    [Header("Matching Options")]
    [SerializeField] private bool strictMatching = false;

    private List<FilteredButton> buttons = new List<FilteredButton>();
    private List<string> gameOrder = new List<string>();
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
        if (button == null) return;

        var filteredButton = new FilteredButton
        {
            button = button,
            buttonText = button.GetComponentInChildren<Text>(),
            mainName = mainName ?? string.Empty,
            aliases = aliases ?? new List<string>()
        };

        filteredButton.tokens = SearchUtils.BuildTokenSet(filteredButton.mainName, filteredButton.aliases);

        buttons.Add(filteredButton);
        gameOrder.Add(mainName ?? string.Empty);

        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        filteredButton.SetActive(true);
    }

    private void OnSearchInputChanged(string newSearchTerm)
    {
        currentSearchTerm = newSearchTerm?.Trim() ?? "";

        if (string.IsNullOrEmpty(currentSearchTerm) || string.IsNullOrWhiteSpace(newSearchTerm))
        {
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
            if (filteredBtn == null || filteredBtn.button == null) continue;

            if (filteredBtn.tokens == null)
                filteredBtn.tokens = SearchUtils.BuildTokenSet(filteredBtn.mainName, filteredBtn.aliases);

            bool contains = SearchUtils.Matches(filteredBtn.mainName, filteredBtn.aliases, filteredBtn.tokens, searchTerm, strictMatching);
            if (contains)
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

        visibleButtons = visibleButtons
            .OrderByDescending(b => b.matchScore)
            .ThenBy(b => b.mainName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < visibleButtons.Count; i++)
        {
            visibleButtons[i].button.transform.SetSiblingIndex(i);
        }

        int nextIndex = visibleButtons.Count;
        var hiddenButtons = buttons.Where(b => b != null && b.button != null && !b.isVisible).ToList();
        foreach (var hb in hiddenButtons)
        {
            hb.button.transform.SetSiblingIndex(nextIndex);
            nextIndex++;
        }

        if (noResultsPanel != null)
        {
            noResultsPanel.SetActive(visibleCount == 0);
        }
    }

    private void ResetToGameOrder()
    {
        for (int i = 0; i < gameOrder.Count; i++)
        {
            var main = gameOrder[i];
            var button = buttons.FirstOrDefault(b => string.Equals(b.mainName, main, StringComparison.OrdinalIgnoreCase));
            if (button != null && button.button != null)
            {
                button.SetActive(true);
                button.button.transform.SetSiblingIndex(i);
            }
        }

        int startIndex = gameOrder.Count;
        var extras = buttons.Where(b => !gameOrder.Any(n => string.Equals(n, b.mainName, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var ex in extras)
        {
            if (ex.button != null)
            {
                ex.SetActive(true);
                ex.button.transform.SetSiblingIndex(startIndex++);
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

    public void RecomputeAllTokens()
    {
        foreach (var b in buttons)
        {
            if (b != null)
                b.tokens = SearchUtils.BuildTokenSet(b.mainName, b.aliases);
        }
    }

    private static class SearchUtils
    {
        public static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.ToLowerInvariant();
            s = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in s)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            s = sb.ToString().Normalize(NormalizationForm.FormC);
            s = Regex.Replace(s, @"[^0-9a-z]+", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        public static IEnumerable<string> Tokenize(string s)
        {
            var norm = Normalize(s);
            if (string.IsNullOrEmpty(norm)) yield break;
            foreach (var part in norm.Split(' '))
                if (!string.IsNullOrWhiteSpace(part))
                    yield return part;
        }

        public static HashSet<string> BuildTokenSet(string mainName, IEnumerable<string> aliases)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(mainName))
            {
                foreach (var t in Tokenize(mainName))
                    set.Add(t);
            }
            if (aliases != null)
            {
                foreach (var a in aliases)
                {
                    foreach (var t in Tokenize(a))
                        set.Add(t);
                }
            }
            return set;
        }

        public static bool Matches(string mainName, IEnumerable<string> aliases, HashSet<string> precomputedTokens, string query, bool strictMatching = false)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            var normQuery = Normalize(query);
            if (string.IsNullOrEmpty(normQuery)) return true;

            var normMain = Normalize(mainName ?? string.Empty);
            if (!string.IsNullOrEmpty(normMain) && normMain.Contains(normQuery)) return true;

            if (aliases != null)
            {
                foreach (var a in aliases)
                {
                    var na = Normalize(a ?? string.Empty);
                    if (!string.IsNullOrEmpty(na) && na.Contains(normQuery)) return true;
                }
            }

            var words = normQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return false;

            var tokens = precomputedTokens ?? BuildTokenSet(mainName ?? string.Empty, aliases ?? new List<string>());

            foreach (var w in words)
            {
                bool ok;
                if (strictMatching)
                    ok = tokens.Any(t => string.Equals(t, w, StringComparison.OrdinalIgnoreCase));
                else
                    ok = tokens.Any(t => t.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!ok) return false;
            }
            return true;
        }

        public static int GetScore(string mainName, IEnumerable<string> aliases, HashSet<string> precomputedTokens, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return int.MaxValue / 2;
            var normQuery = Normalize(query);
            if (string.IsNullOrEmpty(normQuery)) return int.MaxValue / 2;

            var normMain = Normalize(mainName ?? string.Empty);
            if (!string.IsNullOrEmpty(normMain))
            {
                int idx = normMain.IndexOf(normQuery, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return 10000 - idx;
                }
            }

            var words = normQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return 0;

            var tokens = precomputedTokens ?? BuildTokenSet(mainName ?? string.Empty, aliases ?? new List<string>());
            var mainTokens = new HashSet<string>(Tokenize(mainName ?? string.Empty), StringComparer.OrdinalIgnoreCase);

            int matchedWords = 0;
            int score = 0;

            foreach (var w in words)
            {
                bool anyContains = tokens.Any(t => t.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
                if (anyContains)
                {
                    matchedWords++;
                    score += 1000;
                    bool exact = tokens.Any(t => string.Equals(t, w, StringComparison.OrdinalIgnoreCase));
                    if (exact) score += 500;
                    bool inMain = mainTokens.Any(t => string.Equals(t, w, StringComparison.OrdinalIgnoreCase));
                    if (inMain) score += 200;
                }
            }

            score += matchedWords * 10;

            return score;
        }
    }
}