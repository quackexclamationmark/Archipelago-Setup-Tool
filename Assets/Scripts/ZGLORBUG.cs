using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.InputSystem;

public class SpawnVideoPanel : MonoBehaviour
{
    public GameObject panelPrefab;
    public Transform parentCanvas;
    public Key startKey = Key.None;
    public Key[] sequenceKeys = new Key[0];
    public float sequenceTimeout = 3f;
    public float trimDuration = 1.137f;

    public RawImage[] targetRawImages = new RawImage[0];
    public Texture[] replacementTextures = new Texture[0];
    public Image[] targetUIImages = new Image[0];
    public Sprite[] replacementSprites = new Sprite[0];

    bool listening = false;
    int seqIndex = 0;
    float seqStartTime = 0f;

    Texture[] _originalRawTextures = new Texture[0];
    Sprite[] _originalUISprites = new Sprite[0];
    bool _replacementApplied = false;

    void Update()
    {
        if (Keyboard.current == null) return;
        if (startKey != Key.None && Keyboard.current[startKey].wasPressedThisFrame) StartSequence();
        if (!listening) return;
        if (sequenceKeys == null || sequenceKeys.Length == 0) { ResetSequence(); return; }
        if (Time.time - seqStartTime > sequenceTimeout) { ResetSequence(); return; }
        Key expected = sequenceKeys[seqIndex];
        var expectedControl = Keyboard.current[expected];
        if (expectedControl != null && expectedControl.wasPressedThisFrame)
        {
            seqIndex++;
            seqStartTime = Time.time;
            if (seqIndex >= sequenceKeys.Length) { ResetSequence(); SpawnAndPlay(); }
            return;
        }
        foreach (var k in sequenceKeys)
        {
            if (k == expected) continue;
            var c = Keyboard.current[k];
            if (c != null && c.wasPressedThisFrame) { ResetSequence(); break; }
        }
    }

    void StartSequence()
    {
        listening = true;
        seqIndex = 0;
        seqStartTime = Time.time;
    }

    void ResetSequence()
    {
        listening = false;
        seqIndex = 0;
        seqStartTime = 0f;
    }

    public void SpawnAndPlay()
    {
        if (panelPrefab == null) return;
        GameObject panel = Instantiate(panelPrefab, parentCanvas ? parentCanvas : null);
        panel.transform.SetAsLastSibling();
        VideoPlayer vp = panel.GetComponentInChildren<VideoPlayer>();
        RawImage raw = panel.GetComponentInChildren<RawImage>();
        if (vp == null) { Destroy(panel); return; }
        vp.playOnAwake = false;
        vp.Stop();
        vp.isLooping = false;
        vp.renderMode = VideoRenderMode.RenderTexture;
        bool createdRT = false;
        RenderTexture rt = vp.targetTexture;
        if (rt == null && raw != null)
        {
            int w = (vp.clip != null && vp.clip.width > 0) ? (vp.clip.width <= int.MaxValue ? (int)vp.clip.width : int.MaxValue) : Mathf.Max(256, Screen.width);
            int h = (vp.clip != null && vp.clip.height > 0) ? (vp.clip.height <= int.MaxValue ? (int)vp.clip.height : int.MaxValue) : Mathf.Max(256, Screen.height);
            rt = new RenderTexture(w, h, 0);
            createdRT = true;
            vp.targetTexture = rt;
        }
        if (raw != null && rt != null) raw.texture = rt;
        vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
        AudioSource audioSource = panel.GetComponentInChildren<AudioSource>();
        if (audioSource == null) audioSource = panel.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.Stop();
        vp.SetTargetAudioSource(0, audioSource);
        PanelVideoHandler_Monitor handler = panel.AddComponent<PanelVideoHandler_Monitor>();
        handler.Init(rt, createdRT, audioSource, vp, trimDuration, this);
        vp.prepareCompleted += handler.OnPrepareAndStartMonitoring;
        vp.loopPointReached += handler.OnVideoFinished;
        vp.errorReceived += (VideoPlayer source, string message) => { handler.CleanupAndDestroy(); };
        vp.Prepare();
    }

    public void ApplyReplacement()
    {
        if (_replacementApplied) return;
        if (targetRawImages != null && targetRawImages.Length > 0)
        {
            int len = Mathf.Min(targetRawImages.Length, replacementTextures != null ? replacementTextures.Length : 0);
            _originalRawTextures = new Texture[targetRawImages.Length];
            for (int i = 0; i < targetRawImages.Length; i++)
            {
                var r = targetRawImages[i];
                if (r == null) continue;
                try { _originalRawTextures[i] = r.texture; } catch { _originalRawTextures[i] = null; }
                if (i < len && replacementTextures[i] != null)
                {
                    try { r.texture = replacementTextures[i]; r.color = Color.white; r.enabled = true; } catch { }
                }
            }
        }
        if (targetUIImages != null && targetUIImages.Length > 0)
        {
            int len = Mathf.Min(targetUIImages.Length, replacementSprites != null ? replacementSprites.Length : 0);
            _originalUISprites = new Sprite[targetUIImages.Length];
            for (int i = 0; i < targetUIImages.Length; i++)
            {
                var im = targetUIImages[i];
                if (im == null) continue;
                try { _originalUISprites[i] = im.sprite; } catch { _originalUISprites[i] = null; }
                if (i < len && replacementSprites[i] != null)
                {
                    try { im.sprite = replacementSprites[i]; im.color = Color.white; im.enabled = true; } catch { }
                }
            }
        }
        _replacementApplied = true;
    }

    void OnApplicationQuit()
    {
        RestoreReplacement();
    }

    void OnDestroy()
    {
        RestoreReplacement();
    }

    void RestoreReplacement()
    {
        if (!_replacementApplied) return;
        if (targetRawImages != null && _originalRawTextures != null)
        {
            int len = Mathf.Min(targetRawImages.Length, _originalRawTextures.Length);
            for (int i = 0; i < len; i++)
            {
                var r = targetRawImages[i];
                if (r == null) continue;
                try { r.texture = _originalRawTextures[i]; } catch { }
            }
        }
        if (targetUIImages != null && _originalUISprites != null)
        {
            int len = Mathf.Min(targetUIImages.Length, _originalUISprites.Length);
            for (int i = 0; i < len; i++)
            {
                var im = targetUIImages[i];
                if (im == null) continue;
                try { im.sprite = _originalUISprites[i]; } catch { }
            }
        }
        _replacementApplied = false;
    }
}

public class PanelVideoHandler_Monitor : MonoBehaviour
{
    RenderTexture rt;
    bool createdRT = false;
    AudioSource audioSource;
    VideoPlayer vp;
    bool cleaned = false;
    float trimDuration = 0f;
    SpawnVideoPanel owner;

    public void Init(RenderTexture renderTexture, bool created, AudioSource aSource, VideoPlayer player, float trim, SpawnVideoPanel ownerPanel)
    {
        rt = renderTexture;
        createdRT = created;
        audioSource = aSource;
        vp = player;
        trimDuration = trim;
        owner = ownerPanel;
    }

    public void OnPrepareAndStartMonitoring(VideoPlayer source)
    {
        source.prepareCompleted -= OnPrepareAndStartMonitoring;
        source.Play();
        if (trimDuration > 0f) StartCoroutine(StopAfter(trimDuration));
        StartCoroutine(MonitorPlayback());
    }

    IEnumerator StopAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (cleaned) yield break;
        OnVideoFinished(vp);
    }

    IEnumerator MonitorPlayback()
    {
        if (vp == null) yield break;
        float waitStart = Time.time;
        while (!vp.isPlaying && Time.time - waitStart < 5f) yield return null;
        while (vp != null)
        {
            if (vp.length > 0.0)
            {
                if (vp.time >= vp.length - 0.05f) break;
            }
            if (!vp.isPlaying && vp.frame > 0) break;
            yield return null;
        }
        OnVideoFinished(vp);
    }

    public void OnVideoFinished(VideoPlayer _)
    {
        if (cleaned) return;
        cleaned = true;
        try { if (vp != null) vp.Stop(); } catch { }
        try { if (audioSource != null) audioSource.Stop(); } catch { }
        if (owner != null) owner.ApplyReplacement();
        CleanupAndDestroy();
    }

    public void CleanupAndDestroy()
    {
        if (vp != null)
        {
            try
            {
                vp.prepareCompleted -= OnPrepareAndStartMonitoring;
                vp.loopPointReached -= OnVideoFinished;
            }
            catch { }
            try { vp.enabled = false; } catch { }
            try { vp.renderMode = VideoRenderMode.APIOnly; } catch { }
            try { vp.targetTexture = null; } catch { }
            try { vp.clip = null; } catch { }
        }

        if (rt != null)
        {
            var all = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                try { if (all[i] != null && all[i].texture == rt) { all[i].texture = null; all[i].color = new Color(0f, 0f, 0f, 0f); all[i].enabled = false; all[i].gameObject.SetActive(false); } } catch { }
            }
        }

        if (createdRT && rt != null)
        {
            try { rt.Release(); } catch { }
            try { Destroy(rt); } catch { }
            rt = null;
        }
        else
        {
            rt = null;
        }

        if (audioSource != null)
        {
            try { Destroy(audioSource); } catch { }
            audioSource = null;
        }

        Canvas.ForceUpdateCanvases();

        try { Destroy(gameObject); } catch { }
    }
}