using UnityEngine;

public class PlatformSelection : MonoBehaviour
{
    [Header("Platform Selection (choisir dans l'inspecteur)")]
    [SerializeField] private bool selectWindows = true;

    [Header("State (accessible depuis d'autres scripts)")]
    public bool isWindows = false;
    public bool isLinux = false;

    void Start()
    {
        if (selectWindows)
        {
            isWindows = true;
            isLinux = false;
            Debug.Log("[PlatformSelection] Windows selected");
        }
        else
        {
            isWindows = false;
            isLinux = true;
            Debug.Log("[PlatformSelection] Linux selected");
        }
    }
}