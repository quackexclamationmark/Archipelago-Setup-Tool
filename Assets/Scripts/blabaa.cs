using UnityEngine;

public class OpenSteamPage : MonoBehaviour
{
    [SerializeField]
    private string steamProtocolUrl = "steam://url/CommunityFilePage/3640861678"; //TEST TRUC OUAIS CA OUVRE BIEN LA C'EST BIEN CE TRUC LA YOUPI

    public void OpenSteamWorkshopPage()
    {
        Application.OpenURL(steamProtocolUrl);
    }
}