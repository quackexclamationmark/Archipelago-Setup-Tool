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
    public GameObject ds3Panel;
    public GameObject charlesPanel;
    public GameObject oc2Panel;
    public GameObject hitmanwoaPanel;
    public GameObject adofaiPanel;
    public GameObject tcgPanel;
    public GameObject doom2Panel;
    public GameObject doomPanel;
    public GameObject noitaPanel;
    public GameObject ds2Panel;
    public GameObject sm64Panel;
    public GameObject raftPanel;
    public GameObject witnessPanel;
    public GameObject neonwhitePanel;
    public GameObject garfieldkartPanel;
    public GameObject unfairflipsPanel;
    public GameObject skulPanel;
    public GameObject pikunikuPanel;
    public GameObject funiraccoonPanel;
    public GameObject fezPanel;

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
            case "ClairObscurExpedition33":
                coe33Panel.SetActive(true);
                break;
            case "LethalCompany":
                lethalcompanyPanel.SetActive(true);
                break;
            case "SlayTheSpire2":
                sts2Panel.SetActive(true);
                break;
            case "ContentWarning":
                contentwarningPanel.SetActive(true);
                break;
            case "PeaksOfYore":
                peaksofyorePanel.SetActive(true);
                break;
            case "BloonsTD6":
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
            case "LibrarianTidyUptheArcaneLibrary":
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
            case "ResidentEvil7":
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
            case "UntitledGooseGame":
                uggPanel.SetActive(true);
                break;
            case "HiFiRUSH":
                hifiPanel.SetActive(true);
                break;
            case "StardewValley":
                stardewPanel.SetActive(true);
                break;
            case "TUNIC":
                tunicPanel.SetActive(true);
                break;
            case "AnotherCrab'sTreasure":
                actPanel.SetActive(true);
                break;
            case "OriAndTheBlindForest":
                oribfPanel.SetActive(true);
                break;
            case "OriAndTheWillOfTheWisps":
                oriwotwPanel.SetActive(true);
                break;
            case "RiftOfTheNecrodancer":
                rotndPanel.SetActive(true);
                break;
            case "DREDGE":
                dredgePanel.SetActive(true);
                break;
            case "DARKSOULS3":
                ds3Panel.SetActive(true);
                break;
            case "Choo-ChooCharles":
                charlesPanel.SetActive(true);
                break;
            case "Overcooked2":
                oc2Panel.SetActive(true);
                break;
            case "HitmanWorldOfAssassination":
                hitmanwoaPanel.SetActive(true);
                break;
            case "ADanceofFireandIce":
                adofaiPanel.SetActive(true);
                break;
            case "TCGCardShopSimulator":
                tcgPanel.SetActive(true);
                break;
            case "DOOM2":
                doom2Panel.SetActive(true);
                break;
            case "DOOM":
                doomPanel.SetActive(true);
                break;
            case "Noita":
                noitaPanel.SetActive(true);
                break;
            case "DARKSOULS2":
                ds2Panel.SetActive(true);
                break;
            case "SuperMario64":
                sm64Panel.SetActive(true);
                break;
            case "Raft":
                raftPanel.SetActive(true);
                break;
            case "TheWitness":
                witnessPanel.SetActive(true);
                break;
            case "NeonWhite":
                neonwhitePanel.SetActive(true);
                break;
            case "GarfieldKartFuriousRacing":
                garfieldkartPanel.SetActive(true);
                break;
            case "UnfairFlips":
                unfairflipsPanel.SetActive(true);
                break;
            case "SkulTheHeroSlayer":
                skulPanel.SetActive(true);
                break;
            case "Pikuniku":
                pikunikuPanel.SetActive(true);
                break;
            case "FuniRaccoonGame":
                funiraccoonPanel.SetActive(true);
                break;
            case "FEZ":
                fezPanel.SetActive(true);
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
        if (ds3Panel != null) ds3Panel.SetActive(false);
        if (charlesPanel != null) charlesPanel.SetActive(false);
        if (oc2Panel != null) oc2Panel.SetActive(false);
        if (hitmanwoaPanel != null) hitmanwoaPanel.SetActive(false);
        if (adofaiPanel != null) adofaiPanel.SetActive(false);
        if (tcgPanel != null) tcgPanel.SetActive(false);
        if (doom2Panel != null) doom2Panel.SetActive(false);
        if (doomPanel != null) doomPanel.SetActive(false);
        if (noitaPanel != null) noitaPanel.SetActive(false);
        if (ds2Panel != null) ds2Panel.SetActive(false);
        if (sm64Panel != null) sm64Panel.SetActive(false);
        if (raftPanel != null) raftPanel.SetActive(false);
        if (witnessPanel != null) witnessPanel.SetActive(false);
        if (neonwhitePanel != null) neonwhitePanel.SetActive(false);
        if (garfieldkartPanel != null) garfieldkartPanel.SetActive(false);
        if (unfairflipsPanel != null) unfairflipsPanel.SetActive(false);
        if (skulPanel != null) skulPanel.SetActive(false);
        if (pikunikuPanel != null) pikunikuPanel.SetActive(false);
        if (funiraccoonPanel != null) funiraccoonPanel.SetActive(false);
        if (fezPanel != null) fezPanel.SetActive(false);
    }
}