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

    [ContextMenu("Fetch And Populate")]
    public void FetchAndPopulateContext() => StartCoroutine(FetchAndPopulateCoroutine());

    private IEnumerator Start()
    {
        if (fetchOnStart)
        {
            yield return new WaitForEndOfFrame();
            yield return StartCoroutine(FetchAndPopulateCoroutine());
        }
    }

    private IEnumerator FetchAndPopulateCoroutine()
    {
        if (sampleGameObject == null || content == null || string.IsNullOrWhiteSpace(remoteJsonUrl))
        {
            Debug.LogError("[RemoteTextPopulator] Please assign sampleGameObject, content and remoteJsonUrl.");
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
                yield break;
            }

            PopulateFromItems(items);

            if (scrollToTop != null)
            {
                scrollToTop.ScrollToTopNow();
            }
        }
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
        else
        {
            currentTMP = null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            string raw = items[i] ?? "";
            string token = raw.Trim();

            if (string.Equals(token, "clear", StringComparison.OrdinalIgnoreCase))
            {
                currentIndex++;

                if (currentIndex < textObjects.Count)
                {
                    currentTMP = textObjects[currentIndex];
                    ApplyDefaultStyle(currentTMP);
                    currentTMP.text = "";
                }
                else
                {
                    GameObject go = Instantiate(sampleGameObject);
                    go.name = sampleGameObject.name + " (from remote)";
                    go.transform.SetParent(content, false);
                    var tmp = go.GetComponent<TextMeshProUGUI>();
                    if (tmp == null)
                    {
                        Debug.LogError("[RemoteTextPopulator] sampleGameObject must have TextMeshProUGUI component.");
                        Destroy(go);
                        currentTMP = null;
                    }
                    else
                    {
                        currentTMP = tmp;
                        ApplyDefaultStyle(currentTMP);
                        currentTMP.text = "";
                        textObjects.Add(currentTMP);
                    }
                }

                continue;
            }

            if (string.Equals(token, "kill", StringComparison.OrdinalIgnoreCase))
            {
                if (i == items.Count - 1)
                {
                    break;
                }

                currentIndex++;

                if (currentIndex < textObjects.Count)
                {
                    currentTMP = textObjects[currentIndex];
                    ApplyDefaultStyle(currentTMP);
                    currentTMP.text = "";
                }
                else
                {
                    GameObject go = Instantiate(sampleGameObject);
                    go.name = sampleGameObject.name + " (from remote)";
                    go.transform.SetParent(content, false);
                    var tmp = go.GetComponent<TextMeshProUGUI>();
                    if (tmp == null)
                    {
                        Debug.LogError("[RemoteTextPopulator] sampleGameObject must have TextMeshProUGUI component.");
                        Destroy(go);
                        currentTMP = null;
                    }
                    else
                    {
                        currentTMP = tmp;
                        ApplyDefaultStyle(currentTMP);
                        currentTMP.text = "";
                        textObjects.Add(currentTMP);
                    }
                }

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
                if (currentIndex < textObjects.Count)
                {
                    currentTMP = textObjects[currentIndex];
                    ApplyDefaultStyle(currentTMP);
                    currentTMP.text = "";
                }
                else
                {
                    GameObject go = Instantiate(sampleGameObject);
                    go.name = sampleGameObject.name + " (from remote)";
                    go.transform.SetParent(content, false);
                    var tmp = go.GetComponent<TextMeshProUGUI>();
                    if (tmp == null)
                    {
                        Debug.LogError("[RemoteTextPopulator] sampleGameObject must have TextMeshProUGUI component.");
                        Destroy(go);
                        return;
                    }
                    currentTMP = tmp;
                    ApplyDefaultStyle(currentTMP);
                    currentTMP.text = "";
                    textObjects.Add(currentTMP);
                }
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