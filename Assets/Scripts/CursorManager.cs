using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager instance;

    public Texture2D defaultCursor;
    public Texture2D clickableCursor; // Main/pointeur
    public Vector2 hotSpot = new Vector2(16, 16);

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    void Start()
    {
        SetDefaultCursor();
    }

    public void SetDefaultCursor()
    {
        Cursor.SetCursor(defaultCursor, hotSpot, CursorMode.ForceSoftware);
    }

    public void SetClickableCursor()
    {
        Cursor.SetCursor(clickableCursor, hotSpot, CursorMode.ForceSoftware);
    }
}