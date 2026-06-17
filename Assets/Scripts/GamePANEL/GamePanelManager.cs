using UnityEngine;

public class GamePanelManager : MonoBehaviour
{
    public GameObject subnauticaPanel;
    public GameObject repoPanel;
    public GameObject powerwashPanel;
    public GameObject coe33Panel;
    public GameObject lethalcompanyPanel;
    public GameObject sts2Panel;
    public GameObject contentwarningPanel;
    public GameObject peaksofyorePanel;
    public GameObject btd6Panel;
    public GameObject votvPanel;
    public GameObject balatroPanel;
    public GameObject ninesolsPanel;
    public GameObject peakPanel;
    public GameObject outerwildsPanel;
    public GameObject cupheadPanel;
    public GameObject ror2Panel;
    public GameObject brcPanel;
    public GameObject portal2Panel;
    public GameObject ltutalPanel;
    public GameObject minecraftPanel;
    public GameObject celesteOWPanel;
    public GameObject slimerancherPanel;
    public GameObject slimerancher2Panel;
    public GameObject clustertruckPanel;
    public GameObject hollowknightPanel;
    public GameObject cloverpitPanel;
    public GameObject re7Panel;
    public GameObject schedulePanel;
    public GameObject hadesPanel;
    public GameObject ultrakillPanel;
    public GameObject uggPanel;
    public GameObject hifiPanel;
    public GameObject stardewPanel;
    public GameObject tunicPanel;
    public GameObject actPanel;
    public GameObject oribfPanel;
    public GameObject oriwotwPanel;
    public GameObject rotndPanel;
    public GameObject dredgePanel;

    void Start()
    {
        HideAllPanels();
    }

    public void ShowPanel(string gameName)
    {
        HideAllPanels();

        switch (gameName)
        {
            case "Subnautica":
                subnauticaPanel.SetActive(true);
                break;
            case "REPO":
                repoPanel.SetActive(true);
                break;
            case "PowerwashSimulator":
                powerwashPanel.SetActive(true);
                break;
            case "COE33":
                coe33Panel.SetActive(true);
                break;
            case "LethalCompany":
                lethalcompanyPanel.SetActive(true);
                break;
            case "Slay The Spire 2":
                sts2Panel.SetActive(true);
                break;
            case "ContentWarning":
                contentwarningPanel.SetActive(true);
                break;
            case "PeaksOfYore":
                peaksofyorePanel.SetActive(true);
                break;
            case "BTD6":
                btd6Panel.SetActive(true);
                break;
            case "VoicesOfTheVoid":
                votvPanel.SetActive(true);
                break;
            case "Balatro":
                balatroPanel.SetActive(true);
                break;
            case "NineSols":
                ninesolsPanel.SetActive(true);
                break;
            case "PEAK":
                peakPanel.SetActive(true);
                break;
            case "OuterWilds":
                outerwildsPanel.SetActive(true);
                break;
            case "Cuphead":
                cupheadPanel.SetActive(true);
                break;
            case "RiskOfRain2":
                ror2Panel.SetActive(true);
                break;
            case "BombRushCyberfunk":
                brcPanel.SetActive(true);
                break;
            case "Portal2":
                portal2Panel.SetActive(true);
                break;
            case "LTUTAL":
                ltutalPanel.SetActive(true);
                break;
            case "Minecraft":
                minecraftPanel.SetActive(true);
                break;
            case "CelesteOW":
                celesteOWPanel.SetActive(true);
                break;
            case "SlimeRancher":
                slimerancherPanel.SetActive(true);
                break;
            case "SlimeRancher2":
                slimerancher2Panel.SetActive(true);
                break;
            case "Clustertruck":
                clustertruckPanel.SetActive(true);
                break;
            case "HollowKnight":
                hollowknightPanel.SetActive(true);
                break;
            case "CloverPit":
                cloverpitPanel.SetActive(true);
                break;
            case "RE7":
                re7Panel.SetActive(true);
                break;
            case "ScheduleI":
                schedulePanel.SetActive(true);
                break;
            case "Hades":
                hadesPanel.SetActive(true);
                break;
            case "ULTRAKILL":
                ultrakillPanel.SetActive(true);
                break;
            case "UGG":
                uggPanel.SetActive(true);
                break;
            case "HiFiRUSH":
                hifiPanel.SetActive(true);
                break;
            case "Stardew":
                stardewPanel.SetActive(true);
                break;
            case "TUNIC":
                tunicPanel.SetActive(true);
                break;
            case "ACT":
                actPanel.SetActive(true);
                break;
            case "OriBF":
                oribfPanel.SetActive(true);
                break;
            case "OriWotW":
                oriwotwPanel.SetActive(true);
                break;
            case "RotND":
                rotndPanel.SetActive(true);
                break;
            case "DREDGE":
                dredgePanel.SetActive(true);
                break;
        }
    }

    void HideAllPanels()
    {
        if (subnauticaPanel != null) subnauticaPanel.SetActive(false);
        if (repoPanel != null) repoPanel.SetActive(false);
        if (powerwashPanel != null) powerwashPanel.SetActive(false);
        if (coe33Panel != null) coe33Panel.SetActive(false);
        if (lethalcompanyPanel != null) lethalcompanyPanel.SetActive(false);
        if (sts2Panel != null) sts2Panel.SetActive(false);
        if (contentwarningPanel != null) contentwarningPanel.SetActive(false);
        if (peaksofyorePanel != null) peaksofyorePanel.SetActive(false);
        if (btd6Panel != null) btd6Panel.SetActive(false);
        if (votvPanel != null) votvPanel.SetActive(false);
        if (balatroPanel != null) balatroPanel.SetActive(false);
        if (ninesolsPanel != null) ninesolsPanel.SetActive(false);
        if (peakPanel != null) peakPanel.SetActive(false);
        if (outerwildsPanel != null) outerwildsPanel.SetActive(false);
        if (cupheadPanel != null) cupheadPanel.SetActive(false);
        if (ror2Panel != null) ror2Panel.SetActive(false);
        if (brcPanel != null) brcPanel.SetActive(false);
        if (portal2Panel != null) portal2Panel.SetActive(false);
        if (ltutalPanel != null) ltutalPanel.SetActive(false);
        if (minecraftPanel != null) minecraftPanel.SetActive(false);
        if (celesteOWPanel != null) celesteOWPanel.SetActive(false);
        if (slimerancherPanel != null) slimerancherPanel.SetActive(false);
        if (slimerancher2Panel != null) slimerancher2Panel.SetActive(false);
        if (clustertruckPanel != null) clustertruckPanel.SetActive(false);
        if (hollowknightPanel != null) hollowknightPanel.SetActive(false);
        if (cloverpitPanel != null) cloverpitPanel.SetActive(false);
        if (re7Panel != null) re7Panel.SetActive(false);
        if (schedulePanel != null) schedulePanel.SetActive(false);
        if (hadesPanel != null) hadesPanel.SetActive(false);
        if (ultrakillPanel != null) ultrakillPanel.SetActive(false);
        if (uggPanel != null) uggPanel.SetActive(false);
        if (hifiPanel != null) hifiPanel.SetActive(false);
        if (stardewPanel != null) stardewPanel.SetActive(false);
        if (tunicPanel != null) tunicPanel.SetActive(false);
        if (actPanel != null) actPanel.SetActive(false);
        if (oribfPanel != null) oribfPanel.SetActive(false);
        if (oriwotwPanel != null) oriwotwPanel.SetActive(false);
        if (rotndPanel != null) rotndPanel.SetActive(false);
        if (dredgePanel != null) dredgePanel.SetActive(false);
    }
}