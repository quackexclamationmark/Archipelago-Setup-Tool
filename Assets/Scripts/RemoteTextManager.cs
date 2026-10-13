using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class RemoteTextPopulator : MonoBehaviour
{
    [Header("Assign a sample GameObject that has a TextMeshProUGUI component")]
    public GameObject sampleGameObject;

    [Header("Parent (Content) where text GameObjects live or will be created")]
    public Transform content;

    [Header("Remote JSON URL (must be raw GitHub URL, e.g. raw.githubusercontent.com/...)")]
    public string remoteJsonUrl;

    [Header("Options")]
    public bool fetchOnStart = true;
    public float defaultFontSize = 24f;
    public float headerFontSize = 34f;
    public float linkFontSize = 26f;
    public int image1EmptyLines = 18;
    public bool image1AppendTab = true;

    [Header("Optional UI helpers")]
    public ScrollToTop scrollToTop;

    private bool loaded = false;
    private bool isLoading = false;

    [ContextMenu("Fetch And Populate")]
    public void FetchAndPopulateContext()
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("[RemoteTextPopulator] Le composant est inactif, impossible de lancer la coroutine.");
            return;
        }
        StartCoroutine(FetchAndPopulateCoroutine());
    }

    private void OnEnable()
    {
        if (fetchOnStart && !loaded && !isLoading)
            StartCoroutine(FetchAndPopulateCoroutine());
    }

    private void OnDisable()
    {
        isLoading = false;
    }

    private IEnumerator FetchAndPopulateCoroutine()
    {
        isLoading = true;

        yield return null;

        if (sampleGameObject == null || content == null || string.IsNullOrWhiteSpace(remoteJsonUrl))
        {
            Debug.LogError("[RemoteTextPopulator] Please assign sampleGameObject, content and remoteJsonUrl.");
            isLoading = false;
            yield break;
        }

        string url = remoteJsonUrl.Trim();

        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError($"[RemoteTextPopulator] Failed to download JSON from '{url}': {req.error}");
                isLoading = false;
                yield break;
            }

            string jsonText = req.downloadHandler.text;
            List<string> items;
            try
            {
                items = ParseJsonStringArray(jsonText);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RemoteTextPopulator] Failed to parse JSON array: {e.Message}");
                isLoading = false;
                yield break;
            }

            PopulateFromItems(items);
            loaded = true;
            isLoading = false;

            if (scrollToTop != null)
            {
                scrollToTop.ScrollToTopNow();
            }
        }
    }

    private TextMeshProUGUI GetOrCreateTMP(List<TextMeshProUGUI> textObjects, int index)
    {
        if (index < textObjects.Count)
        {
            var existing = textObjects[index];
            ApplyDefaultStyle(existing);
            existing.text = "";
            return existing;
        }

        GameObject go = Instantiate(sampleGameObject);
        go.name = sampleGameObject.name + " (from remote)";
        go.transform.SetParent(content, false);
        go.SetActive(true);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            Debug.LogError("[RemoteTextPopulator] sampleGameObject must have TextMeshProUGUI component on its root.");
            Destroy(go);
            return null;
        }

        ApplyDefaultStyle(tmp);
        tmp.text = "";
        textObjects.Add(tmp);
        return tmp;
    }

    private void PopulateFromItems(List<string> items)
    {
        if (items == null || items.Count == 0)
        {
            Debug.LogWarning("[RemoteTextPopulator] No items found in JSON.");
            return;
        }

        List<TextMeshProUGUI> textObjects = new List<TextMeshProUGUI>();
        for (int i = 0; i < content.childCount; i++)
        {
            var child = content.GetChild(i);
            var tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp != null) textObjects.Add(tmp);
        }

        int currentIndex = 0;
        TextMeshProUGUI currentTMP = null;
        if (textObjects.Count > 0)
        {
            currentTMP = textObjects[0];
            ApplyDefaultStyle(currentTMP);
            currentTMP.text = "";
        }

        for (int i = 0; i < items.Count; i++)
        {
            string raw = items[i] ?? "";
            string token = raw.Trim();

            if (string.Equals(token, "clear", StringComparison.OrdinalIgnoreCase))
            {
                currentIndex++;
                currentTMP = GetOrCreateTMP(textObjects, currentIndex);
                continue;
            }

            if (string.Equals(token, "kill", StringComparison.OrdinalIgnoreCase))
            {
                if (i == items.Count - 1)
                {
                    break;
                }

                currentIndex++;
                currentTMP = GetOrCreateTMP(textObjects, currentIndex);
                continue;
            }

            bool isHeader = false;
            bool isLink = false;
            bool isImage1 = false;
            string contentText = raw;

            string trimmedStart = raw.TrimStart();

            if (trimmedStart.StartsWith("[IMAGE1]", StringComparison.OrdinalIgnoreCase))
            {
                isImage1 = true;
                int idx = raw.IndexOf("[IMAGE1]", StringComparison.OrdinalIgnoreCase);
                contentText = raw.Substring(idx + "[IMAGE1]".Length).TrimStart();
            }
            else if (trimmedStart.StartsWith("[LINK]", StringComparison.OrdinalIgnoreCase))
            {
                isLink = true;
                int idx = raw.IndexOf("[LINK]", StringComparison.OrdinalIgnoreCase);
                contentText = raw.Substring(idx + "[LINK]".Length).TrimStart();
            }
            else if (trimmedStart.StartsWith("[HEADER]", StringComparison.OrdinalIgnoreCase))
            {
                isHeader = true;
                int idx = raw.IndexOf("[HEADER]", StringComparison.OrdinalIgnoreCase);
                contentText = raw.Substring(idx + "[HEADER]".Length).TrimStart();
            }

            if (currentTMP == null)
            {
                currentTMP = GetOrCreateTMP(textObjects, currentIndex);
                if (currentTMP == null) return;
            }

            if (isImage1)
            {
                string nlBlock = "";
                if (image1EmptyLines > 0)
                {
                    var sb = new StringBuilder();
                    for (int k = 0; k < image1EmptyLines; k++) sb.Append('\n');
                    nlBlock = sb.ToString();
                }

                if (!string.IsNullOrEmpty(nlBlock))
                {
                    if (string.IsNullOrEmpty(currentTMP.text))
                        currentTMP.text = nlBlock + (image1AppendTab ? "\t" : "");
                    else
                        currentTMP.text += nlBlock + (image1AppendTab ? "\t" : "");
                }
                else if (image1AppendTab)
                {
                    if (string.IsNullOrEmpty(currentTMP.text))
                        currentTMP.text = "\t";
                    else
                        currentTMP.text += "\t";
                }

                if (!string.IsNullOrEmpty(contentText))
                {
                    if (string.IsNullOrEmpty(currentTMP.text))
                        currentTMP.text = contentText;
                    else
                        currentTMP.text += "\n" + contentText;
                }

                continue;
            }

            if (string.IsNullOrEmpty(currentTMP.text))
                currentTMP.text = contentText;
            else
                currentTMP.text += "\n" + contentText;

            if (isLink)
            {
                currentTMP.fontSize = linkFontSize;
                currentTMP.fontStyle = FontStyles.Bold;
            }
            else if (isHeader)
            {
                currentTMP.fontSize = headerFontSize;
                currentTMP.fontStyle = FontStyles.Bold;
            }
            else
            {
                currentTMP.fontSize = defaultFontSize;
                currentTMP.fontStyle = FontStyles.Normal;
            }
        }

        Debug.Log("[RemoteTextPopulator] Populate complete. Items processed: " + items.Count);
    }

    private void ApplyDefaultStyle(TextMeshProUGUI tmp)
    {
        tmp.fontSize = defaultFontSize;
        tmp.fontStyle = FontStyles.Normal;
    }

    private List<string> ParseJsonStringArray(string json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        int i = 0;
        int n = json.Length;
        SkipWhitespace();
        if (i >= n || json[i] != '[') throw new FormatException("JSON must start with '[' for an array.");
        i++; // skip '['
        var list = new List<string>();
        while (true)
        {
            SkipWhitespace();
            if (i >= n) throw new FormatException("Unexpected end of JSON array.");
            if (json[i] == ']')
            {
                i++;
                break;
            }

            if (json[i] == ',')
            {
                i++;
                SkipWhitespace();
                continue;
            }

            if (json[i] != '"') throw new FormatException($"Expected '\"' at position {i}.");
            i++;
            StringBuilder sb = new StringBuilder();
            while (i < n)
            {
                char c = json[i++];
                if (c == '\\')
                {
                    if (i >= n) throw new FormatException("Invalid escape at end of string.");
                    char esc = json[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > n) throw new FormatException("Invalid \\u escape.");
                            string hex = json.Substring(i, 4);
                            if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                                throw new FormatException("Invalid \\uXXXX value.");
                            sb.Append((char)codePoint);
                            i += 4;
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else if (c == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(c);
                }
            }

            list.Add(sb.ToString());
            SkipWhitespace();
            if (i < n && json[i] == ',') i++;
            SkipWhitespace();
        }

        return list;

        void SkipWhitespace()
        {
            while (i < n && char.IsWhiteSpace(json[i])) i++;
        }
    }
}