/*using UnityEditor.Rendering.LookDev;*/
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
    public GameObject rl2Panel;
    public GameObject pvzrPanel;
    public GameObject pvzgotyPanel;
    public GameObject bo3Panel;
    public GameObject simpsonsPanel;
    public GameObject pokemonemeraldPanel;
    public GameObject windwakerPanel;
    public GameObject ashorthikePanel;
    public GameObject twilightprincessPanel;
    public GameObject refunctPanel;
    public GameObject spel2Panel;
    public GameObject edcPanel;
    public GameObject pokemonfrlgPanel;
    public GameObject ssbbPanel;
    public GameObject pikmin2Panel;
    public GameObject mkwiiPanel;
    public GameObject okamihdPanel;
    public GameObject doometernalPanel;
    public GameObject fnafucnPanel;
    public GameObject ppdsPanel;
    public GameObject toemPanel;
    public GameObject gtasaPanel;
    public GameObject huniepopPanel;
    public GameObject huniepop2Panel;
    public GameObject pokeparkwiiPanel;
    public GameObject pokemoncrystalPanel;
    public GameObject bendyinkPanel;
    public GameObject pmttydPanel;
    public GameObject luigismansionPanel;
    public GameObject tloztmcPanel;
    public GameObject scritchyPanel;
    public GameObject iamyourbeastPanel;
    public GameObject ksp1Panel;
    public GameObject oniPanel;
    public GameObject sulfurPanel;
    public GameObject psychonautsPanel;
    public GameObject sadxPanel;
    public GameObject nsmbwPanel;
    public GameObject pokemonsnapPanel;
    public GameObject pokemonplatinumPanel;
    public GameObject werecleanerPanel;
    public GameObject drdrPanel;
    public GameObject pokemonbwPanel;
    public GameObject borderlands2Panel;
    public GameObject supermarketsimPanel;
    public GameObject bugsnaxPanel;

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
            case "A DanceofFireandIce":
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
            case "RogueLegacy2":
                rl2Panel.SetActive(true);
                break;
            case "PlantsVsZombiesReplanted":
                pvzrPanel.SetActive(true);
                break;
            case "PlantsVsZombiesGOTY":
                pvzgotyPanel.SetActive(true);
                break;
            case "BlackOps3":
                bo3Panel.SetActive(true);
                break;
            case "TheSimpsonsHit&Run":
                simpsonsPanel.SetActive(true);
                break;
            case "PokémonEmerald":
                pokemonemeraldPanel.SetActive(true);
                break;
            case "TheLegendOfZeldaTheWindWaker":
                windwakerPanel.SetActive(true);
                break;
            case "A ShortHike":
                ashorthikePanel.SetActive(true);
                break;
            case "TheLegendOfZeldaTwilightPrincess":
                twilightprincessPanel.SetActive(true);
                break;
            case "Refunct":
                refunctPanel.SetActive(true);
                break;
            case "Spelunky2":
                spel2Panel.SetActive(true);
                break;
            case "EasyDeliveryCo":
                edcPanel.SetActive(true);
                break;
            case "PokemonFireRedandLeafGreen":
                pokemonfrlgPanel.SetActive(true);
                break;
            case "SuperSmashBrosBrawlTheSubspaceEmissary":
                ssbbPanel.SetActive(true);
                break;
            case "Pikmin2":
                pikmin2Panel.SetActive(true);
                break;
            case "MarioKartWii":
                mkwiiPanel.SetActive(true);
                break;
            case "OkamiHD":
                okamihdPanel.SetActive(true);
                break;
            case "DOOMEternal":
                doometernalPanel.SetActive(true);
                break;
            case "UltimateCustomNight":
                fnafucnPanel.SetActive(true);
                break;
            case "PlacidPlasticDuckSimulator":
                ppdsPanel.SetActive(true);
                break;
            case "TOEMAPhotoAdventure":
                toemPanel.SetActive(true);
                break;
            case "GrandTheftAutoSanAndreas":
                gtasaPanel.SetActive(true);
                break;
            case "HuniePop":
                huniepopPanel.SetActive(true);
                break;
            case "HuniePop2":
                huniepop2Panel.SetActive(true);
                break;
            case "PokéParkWii":
                pokeparkwiiPanel.SetActive(true);
                break;
            case "PokémonCrystal":
                pokemoncrystalPanel.SetActive(true);
                break;
            case "BendyAndTheInkMachine":
                bendyinkPanel.SetActive(true);
                break;
            case "PaperMarioTheThousandYearDoor":
                pmttydPanel.SetActive(true);
                break;
            case "LuigisMansion":
                luigismansionPanel.SetActive(true);
                break;
            case "TheLegendOfZeldaTheMinishCap":
                tloztmcPanel.SetActive(true);
                break;
            case "ScritchyScratchy":
                scritchyPanel.SetActive(true);
                break;
            case "IAmYourBeast":
                iamyourbeastPanel.SetActive(true);
                break;
            case "KerbalSpaceProgram":
                ksp1Panel.SetActive(true);
                break;
            case "OxygenNotIncluded":
                oniPanel.SetActive(true);
                break;
            case "SULFUR":
                sulfurPanel.SetActive(true);
                break;
            case "Psychonauts":
                psychonautsPanel.SetActive(true);
                break;
            case "SonicAdventureDX":
                sadxPanel.SetActive(true);
                break;
            case "NewSuperMarioBrosWii":
                nsmbwPanel.SetActive(true);
                break;
            case "PokémonSnap":
                pokemonsnapPanel.SetActive(true);
                break;
            case "PokémonPlatinum":
                pokemonplatinumPanel.SetActive(true);
                break;
            case "TheWereCleaner":
                werecleanerPanel.SetActive(true);
                break;
            case "DeadRisingDeluxeRemaster":
                drdrPanel.SetActive(true);
                break;
            case "PokémonBlackAndWhite":
                pokemonbwPanel.SetActive(true);
                break;
            case "Borderlands2":
                borderlands2Panel.SetActive(true);
                break;
            case "SupermarketSimulator":
                supermarketsimPanel.SetActive(true);
                break;
            case "Bugsnax":
                bugsnaxPanel.SetActive(true);
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
        if (rl2Panel != null) rl2Panel.SetActive(false);
        if (pvzrPanel != null) pvzrPanel.SetActive(false);
        if (pvzgotyPanel != null) pvzgotyPanel.SetActive(false);
        if (bo3Panel != null) bo3Panel.SetActive(false);
        if (simpsonsPanel != null) simpsonsPanel.SetActive(false);
        if (pokemonemeraldPanel != null) pokemonemeraldPanel.SetActive(false);
        if (windwakerPanel != null) windwakerPanel.SetActive(false);
        if (ashorthikePanel != null) ashorthikePanel.SetActive(false);
        if (twilightprincessPanel != null) twilightprincessPanel.SetActive(false);
        if (refunctPanel != null) refunctPanel.SetActive(false);
        if (spel2Panel != null) spel2Panel.SetActive(false);
        if (edcPanel != null) edcPanel.SetActive(false);
        if (pokemonfrlgPanel != null) pokemonfrlgPanel.SetActive(false);
        if (ssbbPanel != null) ssbbPanel.SetActive(false);
        if (pikmin2Panel != null) pikmin2Panel.SetActive(false);
        if (mkwiiPanel != null) mkwiiPanel.SetActive(false);
        if (okamihdPanel != null) okamihdPanel.SetActive(false);
        if (doometernalPanel != null) doometernalPanel.SetActive(false);
        if (fnafucnPanel != null) fnafucnPanel.SetActive(false);
        if (ppdsPanel != null) ppdsPanel.SetActive(false);
        if (toemPanel != null) toemPanel.SetActive(false);
        if (gtasaPanel != null) gtasaPanel.SetActive(false);
        if (huniepopPanel != null) huniepopPanel.SetActive(false);
        if (huniepop2Panel != null) huniepop2Panel.SetActive(false);
        if (pokeparkwiiPanel != null) pokeparkwiiPanel.SetActive(false);
        if (pokemoncrystalPanel != null) pokemoncrystalPanel.SetActive(false);
        if (bendyinkPanel != null) bendyinkPanel.SetActive(false);
        if (pmttydPanel != null) pmttydPanel.SetActive(false);
        if (luigismansionPanel != null) luigismansionPanel.SetActive(false);
        if (tloztmcPanel != null) tloztmcPanel.SetActive(false);
        if (scritchyPanel != null) scritchyPanel.SetActive(false);
        if (iamyourbeastPanel != null) iamyourbeastPanel.SetActive(false);
        if (ksp1Panel != null) ksp1Panel.SetActive(false);
        if (oniPanel != null) oniPanel.SetActive(false);
        if (sulfurPanel != null) sulfurPanel.SetActive(false);
        if (psychonautsPanel != null) psychonautsPanel.SetActive(false);
        if (sadxPanel != null) sadxPanel.SetActive(false);
        if (nsmbwPanel != null) nsmbwPanel.SetActive(false);
        if (pokemonsnapPanel != null) pokemonsnapPanel.SetActive(false);
        if (pokemonplatinumPanel != null) pokemonplatinumPanel.SetActive(false);
        if (werecleanerPanel != null) werecleanerPanel.SetActive(false);
        if (drdrPanel != null) drdrPanel.SetActive(false);
        if (pokemonbwPanel != null) pokemonbwPanel.SetActive(false);
        if (borderlands2Panel != null) borderlands2Panel.SetActive(false);
        if (supermarketsimPanel != null) supermarketsimPanel.SetActive(false);
        if (bugsnaxPanel != null) bugsnaxPanel.SetActive(false);
    }
}