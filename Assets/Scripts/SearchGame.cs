using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.IO;
using NUnit.Framework.Internal.Filters;

[System.Serializable]
public class GameButtonConfig
{
    public Button button;
    public string mainName;
    public List<string> aliases = new List<string>();
}

[System.Serializable]
public class ButtonSortSettings
{
    public string sortOrder = "alphabetical"; // "alphabetical" ou "custom"
}

public class SearchGame : MonoBehaviour
{
    [SerializeField] private ButtonFilterSystem filterSystem;
    [SerializeField] private Transform buttonsContainer;
    [SerializeField] private List<GameButtonConfig> games = new List<GameButtonConfig>();

    private string settingsFolder;
    private string settingsFile;

    private void Awake()
    {
        string appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        settingsFolder = Path.Combine(appDataRoaming, "Archipelago Setup Tool");
        settingsFile = Path.Combine(settingsFolder, "buttonsort.json");
    }

    private void Start()
    {
        if (games.Count == 0)
        {
            AutoFillGames();
        }

        ApplySortFromSettings();

        foreach (var gameConfig in games)
        {
            if (gameConfig.button != null)
            {
                filterSystem.AddButton(gameConfig.button, gameConfig.mainName, gameConfig.aliases,
                    () => OnGameSelected(gameConfig.mainName));
            }
        }
    }

    private void OnGameSelected(string gameName)
    {
        Debug.Log($"Jeu sélectionné: {gameName}");
        // TODO: Charger la configuration du jeu dans Archipelago
    }

    [ContextMenu("Auto-Fill Games")]
    private void AutoFillGames()
    {
        Button[] allButtons = buttonsContainer.GetComponentsInChildren<Button>();

        games.Clear();
        InitializeDefaultGames();

        for (int i = 0; i < allButtons.Length && i < games.Count; i++)
        {
            games[i].button = allButtons[i];
        }

        Debug.Log($"Rempli {games.Count} jeux avec {allButtons.Length} boutons");
    }

    private void ApplySortFromSettings()
    {
        var settings = LoadSettingsFile();

        if (settings.sortOrder == "custom")
        {
            // Le sorting personnalisé est déjà appliqué par l'ordre dans la liste games
            Debug.Log("[GameSetup] Sorting personnalisé chargé");
        }
        else
        {
            // Tri alphabétique
            SortAlphabetically();
            Debug.Log("[GameSetup] Tri alphabétique appliqué");
        }
    }

    public void SortAlphabetically()
    {
        games.Sort((a, b) => a.mainName.CompareTo(b.mainName));

        for (int i = 0; i < games.Count; i++)
        {
            if (games[i].button != null)
            {
                games[i].button.transform.SetSiblingIndex(i);
            }
        }

        SaveSortingToSettings("alphabetical");
    }

    public void SaveCustomOrder()
    {
        // Sauvegarde l'ordre actuel des boutons
        for (int i = 0; i < buttonsContainer.childCount; i++)
        {
            var child = buttonsContainer.GetChild(i);
            var button = child.GetComponent<Button>();

            if (button != null)
            {
                var gameConfig = games.Find(g => g.button == button);
                if (gameConfig != null)
                {
                    gameConfig.button.transform.SetSiblingIndex(i);
                }
            }
        }

        SaveSortingToSettings("custom");
        Debug.Log("[GameSetup] Ordre personnalisé sauvegardé");
    }

    private void SaveSortingToSettings(string sortOrder)
    {
        try
        {
            Directory.CreateDirectory(settingsFolder);
            var settings = new ButtonSortSettings { sortOrder = sortOrder };
            string json = JsonUtility.ToJson(settings, true);
            File.WriteAllText(settingsFile, json);
            Debug.Log($"[GameSetup] Sorting sauvegardé: {sortOrder}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameSetup] Erreur lors de la sauvegarde: {e}");
        }
    }

    private ButtonSortSettings LoadSettingsFile()
    {
        try
        {
            if (File.Exists(settingsFile))
            {
                string json = File.ReadAllText(settingsFile);
                var settings = JsonUtility.FromJson<ButtonSortSettings>(json);
                return settings ?? new ButtonSortSettings();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameSetup] Erreur lors du chargement: {e}");
        }
        return new ButtonSortSettings();
    }

    public void InitializeDefaultGames()
    {
        games.Clear();

        games.Add(CreateGame("A Dance of Fire and Ice", new List<string> { "Dance", "Fire", "Ice", "ADOFAI" }));
        games.Add(CreateGame("A Short Hike", new List<string> { "A", "Short", "Hike", "ASH" }));
        games.Add(CreateGame("Another Crab's Treasure", new List<string> { "Another", "Crab", "Crab's", "s", "Treasure", "ACT" }));
        games.Add(CreateGame("Balatro", new List<string> { "Balatro" }));
        games.Add(CreateGame("Bendy and the Ink Machine", new List<string> { "Bendy", "and", "the", "Ink", "Machine", "BATIM", "BIM"}));
        games.Add(CreateGame("Black Ops 3 - Zombies", new List<string> { "Call", "Of", "Duty", "Black", "Ops", "III", "3", "Zombies", "BO3", "BO 3", "COD" }));
        games.Add(CreateGame("Bloons TD 6", new List<string> { "Bloons", "TD6", "TD", "6", "BloonsTD6", "BTD6" }));
        games.Add(CreateGame("Bomb Rush Cyberfunk", new List<string> { "Bomb", "Rush", "Cyberfunk", "Cyber", "Funk", "BRC" }));
        games.Add(CreateGame("Borderlands 2", new List<string> { "Borderlands", "2", "Borderlands2" }));
        games.Add(CreateGame("Bugsnax", new List<string> { "Bug", "Snax" }));
        games.Add(CreateGame("Casualties: Unknown Demo", new List<string> { "Casualties", "Unknown", "CU", "Demo" }));
        games.Add(CreateGame("Celeste (Open World)", new List<string> { "Celeste", "Open", "World", "(Open World)" }));
        games.Add(CreateGame("Choo-Choo Charles", new List<string> { "Choo", "ChooChoo", "Choo-Choo", "Charles", "CCC" }));
        games.Add(CreateGame("Clair Obscur: Expedition 33", new List<string> { "Clair", "Obscur", "Expedition", "33", "COE33", "E33" }));
        games.Add(CreateGame("CloverPit", new List<string> { "Clover", "Pit", "CloverPit" }));
        games.Add(CreateGame("Clustertruck", new List<string> { "Cluster", "Truck", "ClusterTruck", "CT" }));
        games.Add(CreateGame("Content Warning", new List<string> { "Content", "Warning", "CW" }));
        games.Add(CreateGame("Cuphead", new List<string> { "Cup", "Head", "Cuphead" }));
        games.Add(CreateGame("DARK SOULS II", new List<string> { "Dark", "Souls", "II", "2", "Scholar", "First", "Sin", "DS", "DS2", "DS 2", "DarkSouls", "DarkSouls2" }));
        games.Add(CreateGame("DARK SOULS III", new List<string> { "Dark", "Souls", "III", "3", "DS", "DS3", "DS 3", "DarkSouls", "DarkSouls3" }));
        games.Add(CreateGame("Dead Rising Deluxe Remaster", new List<string> { "Dead", "Rising", "Deluxe", "Remaster", "DR", "DR 1", "DR1", "DRDR", "Remake" }));
        games.Add(CreateGame("DOOM (1993)", new List<string> { "Doom", "1", "I", "1993", "(1993)" }));
        games.Add(CreateGame("DOOM II", new List<string> { "Doom", "II", "2" }));
        games.Add(CreateGame("DOOM Eternal", new List<string> { "Doom", "Eternal", "DoomEternal" }));
        games.Add(CreateGame("DREDGE", new List<string> { "Dredge" }));
        games.Add(CreateGame("Easy Delivery Co.", new List<string> { "Easy", "Delivery", "Co", "Co.", "EDC" }));
        games.Add(CreateGame("ENDER MAGNOLIA: Bloom in the Mist", new List<string> { "ENDER", "MAGNOLIA", "Bloom", "In", "The", "Mist", "EMBITM" }));
        games.Add(CreateGame("Fez", new List<string> { "Fez" }));
        games.Add(CreateGame("Funi Raccoon Game", new List<string> { "Funi", "Raccoon", "Racoon", "Game", "FRG" }));
        games.Add(CreateGame("Garfield Kart - Furious Racing", new List<string> { "Garfield", "Kart", "Furious", "Racing", "GK", "FR" }));
        games.Add(CreateGame("Grand Theft Auto San Andreas", new List<string> { "Grand", "Theft", "Auto", "San", "Andreas", "GTA", "GTA SA", "GTASA" }));
        games.Add(CreateGame("Hades", new List<string> { "Hades", "1", "I" }));
        games.Add(CreateGame("Hi-Fi Rush", new List<string> { "HiFi", "Hi-Fi", "Hi", "Fi", "Rush" }));
        games.Add(CreateGame("HITMAN: World of Assassination", new List<string> { "Hitman", "World", "Assassination", "WoA" }));
        games.Add(CreateGame("Hollow Knight", new List<string> { "Hollow", "Knight", "HK" }));
        games.Add(CreateGame("Hollow Knight", new List<string> { "Hollow", "Knight", "Silksong", "HKS", "HK" }));
        games.Add(CreateGame("HuniePop", new List<string> { "Hunie", "Pop", "HuniePop 1", "HuniePop1", "Hunie Pop", "Hunie Pop 1" }));
        games.Add(CreateGame("HuniePop 2: Double Date", new List<string> { "Hunie", "Pop", "HuniePop 2", "HuniePop2", "Hunie Pop", "Hunie Pop 2", "Double Date", "HuniePop Double Date", "Double", "Date", "Hunie Pop Double Date", "HuniePop 2 Double Date", "Hunie Pop 2 Double Date" }));
        games.Add(CreateGame("I Am Your Beast", new List<string> { "I", "Am", "Your", "Beast", "IAYB", "IAMYB", "IAmYourBeast" }));
        games.Add(CreateGame("Kerbal Space Program", new List<string> { "Kerbal", "Space", "Program", "KSP", "KSP1", "KSP 1", "1" }));
        games.Add(CreateGame("Lethal Company", new List<string> { "Lethal", "Company", "LC" }));
        games.Add(CreateGame("Librarian Tidy Up the Arcane Library", new List<string> { "Librarian", "Tidy", "Up", "Arcane", "Library", "LTUTAL" }));
        games.Add(CreateGame("Lies of P", new List<string> { "Lies", "Of", "P", "Pinocchio", "LOP" }));
        games.Add(CreateGame("Luigi's Mansion", new List<string> { "Luigi", "Luigi's", "Mansion", "LM", "Mario" }));
        games.Add(CreateGame("Mario Kart Double Dash!!", new List<string> { "Mario", "Kart", "MK", "Double", "Dash", "MKDD", "M K DD", "M KDD", "DD" }));
        games.Add(CreateGame("Mario Kart Wii", new List<string> { "Mario", "Kart", "Wii", "MK", "MKWii", "M K Wii", "M KWii"}));
        games.Add(CreateGame("Metal Hellsinger", new List<string> { "Metal", "Hellsinger", "MH" }));
        games.Add(CreateGame("Metroid Fusion", new List<string> { "Metroid", "Fusion", "MetFus" }));
        games.Add(CreateGame("Minecraft", new List<string> { "Minecraft", "Mine", "Craft", "MC" }));
        games.Add(CreateGame("Neon White", new List<string> { "Neon", "White", "nw" }));
        games.Add(CreateGame("New Super Mario Bros. Wii", new List<string> { "New", "Super", "Mario", "Bros.", "Bros", "Wii", "NSMBW" }));
        games.Add(CreateGame("Nine Sols", new List<string> { "Nine", "Sols", "ns" }));
        games.Add(CreateGame("Noita", new List<string> { "Noita" }));
        games.Add(CreateGame("Okami HD", new List<string> { "Okami", "OkamiHD", "HD" }));
        games.Add(CreateGame("Ori and the Blind Forest", new List<string> { "Ori", "Blind", "Forest", "OBF", "OATBF" }));
        games.Add(CreateGame("Ori and the Will of the Wisps", new List<string> { "Ori", "Will", "Wisps", "OWW", "OATWOTW", "OWOW" }));
        games.Add(CreateGame("Outer Wilds", new List<string> { "Outer", "Wilds", "OW" }));
        games.Add(CreateGame("Overcooked! 2", new List<string> { "Overcooked", "Overcooked!", "!", "2", "Overcooked2", "Overcooked 2", "OC", "OC2", "OC 2" }));
        games.Add(CreateGame("Oxygen Not Included", new List<string> { "Oxygen", "Not", "Included", "ONI" }));
        games.Add(CreateGame("Paper Mario: The Thousand-Year Door", new List<string> { "Paper", "Mario", "The", "Thousand", "Year", "Thousand-Year", "Door", "PM", "PMTTYD", "PM TTYD", "TTYD" }));
        games.Add(CreateGame("PEAK", new List<string> { "Peak" }));
        games.Add(CreateGame("Peaks Of Yore", new List<string> { "Peaks", "Yore", "POY" }));
        games.Add(CreateGame("Persona 5 Royal", new List<string> { "Persona", "5", "Royal", "P5R", "Persona5Royal" }));
        games.Add(CreateGame("Pikmin 2", new List<string> { "Pikmin", "2", "Pikmin2" }));
        games.Add(CreateGame("Pikuniku", new List<string> { "Pikuniku", "pn" }));
        games.Add(CreateGame("Placid Plastic Duck Simulator", new List<string> { "Placid", "Plastic", "Duck", "Simulator", "Sim", "PPDS", "PlacidPlasticDuckSimulator" }));
        games.Add(CreateGame("Plants Vs. Zombies GOTY", new List<string> { "Plants", "vs", "Zombies", "GOTY", "Game", "Year", "PVZ" }));
        games.Add(CreateGame("Plants Vs. Zombies Replanted", new List<string> { "Plants", "vs", "Zombies", "Replanted", "Remake", "PVZ", "PVZR" }));
        games.Add(CreateGame("Pokémon Black and White", new List<string> { "Pokemon", "Pokémon", "Black", "And", "White", "BW", "B&W" }));
        games.Add(CreateGame("Pokémon Crystal", new List<string> { "Pokemon", "Pokémon", "Crystal" }));
        games.Add(CreateGame("Pokémon Emerald", new List<string> { "Pokemon", "Pokémon", "Emerald" }));
        games.Add(CreateGame("Pokémon FireRed and LeafGreen", new List<string> { "Pokemon", "Pokémon", "FireRed", "Fire", "Red", "LeafGreen", "Leaf", "Green", "and" }));
        games.Add(CreateGame("Pokémon Platinum", new List<string> { "Pokemon", "Pokémon", "Platinum" }));
        games.Add(CreateGame("Pokémon Snap", new List<string> { "Pokemon", "Pokémon", "Snap", "PS" }));
        games.Add(CreateGame("PokéPark Wii: Pikachu's Adventure", new List<string> { "Pokemon", "Pokémon", "Poképark", "Pokepark", "Wii", "Pikachu", "Pikachu's", "Adventure", "Park" }));
        games.Add(CreateGame("Portal 2", new List<string> { "Portal", "2", "II" }));
        games.Add(CreateGame("PowerWash Simulator", new List<string> { "PowerWash", "Wash", "Simulator", "PWS" }));
        games.Add(CreateGame("Psychonauts", new List<string> { "Psycho", "Nauts", "PN" }));
        games.Add(CreateGame("Raft", new List<string> { "Raft" }));
        games.Add(CreateGame("Refunct", new List<string> { "Refunct" }));
        games.Add(CreateGame("R.E.P.O.", new List<string> { "REPO", "R.E.P.O." }));
        games.Add(CreateGame("Resident Evil 7: Biohazard", new List<string> { "Resident", "Evil", "7", "Biohazard", "RE7", "RE" }));
        games.Add(CreateGame("Rift of the Necrodancer", new List<string> { "Rift", "Necrodancer", "ROtnD", "ROtN" }));
        games.Add(CreateGame("Risk Of Rain 2", new List<string> { "Risk", "Rain", "2", "II", "ROR2", "ROR", "ROR 2" }));
        games.Add(CreateGame("Rogue Legacy 2", new List<string> { "Rogue", "Legacy", "2", "RL", "RL2", "RL 2" }));
        games.Add(CreateGame("Sayonara Wild Hearts", new List<string> { "Sayonara", "Wild", "Hearts", "SWH" }));
        games.Add(CreateGame("Schedule I", new List<string> { "Schedule", "1", "I" }));
        games.Add(CreateGame("Scritchy Scratchy", new List<string> { "Scritchy", "Scratchy", "SS", "ScritchyScratchy" }));
        games.Add(CreateGame("Skul: The Hero Slayer", new List<string> { "Skul", "Hero", "Slayer", "STHS" }));
        games.Add(CreateGame("Slay The Spire 2", new List<string> { "Slay", "Spire", "2", "STS", "STS2" }));
        games.Add(CreateGame("Slime Rancher", new List<string> { "Slime", "Rancher", "SR" }));
        games.Add(CreateGame("Slime Rancher 2", new List<string> { "Slime", "Rancher", "2", "II", "SR", "SR2", "SR 2" }));
        games.Add(CreateGame("Sonic Adventure DX", new List<string> { "Sonic", "Adventure", "DX", "SADX" }));
        games.Add(CreateGame("Spelunky 2", new List<string> { "Spelunky", "Spel", "spel2", "Spel 2", "2" }));
        games.Add(CreateGame("Stardew Valley", new List<string> { "Stardew", "Valley", "SV" }));
        games.Add(CreateGame("Subnautica", new List<string> { "Subnautica" }));
        games.Add(CreateGame("SULFUR", new List<string> { "SULFUR" }));
        games.Add(CreateGame("Super Mario 64", new List<string> { "Super", "Mario", "64", "SM", "SM64" }));
        games.Add(CreateGame("Super Meat Boy", new List<string> { "Super", "Meat", "Boy", "SMB" }));
        games.Add(CreateGame("Super Smash Bros. Brawl - The Subspace Emissary", new List<string> { "Super", "Smash", "Bros", "Bros.", "Brawl", "Subspace", "Emissary", "The", "SSBB" }));
        games.Add(CreateGame("SUPERHOT", new List<string> { "SUPER", "HOT", "SH" }));
        games.Add(CreateGame("Supermarket Simulator", new List<string> { "Supermarket", "Simulator", "SMS", "Super", "Market" }));
        games.Add(CreateGame("TCG Card Shop Simulator", new List<string> { "TCG", "Card", "Shop", "Simulator", "Trading", "TCGCSS" }));
        games.Add(CreateGame("The Henry Stickmin Collection", new List<string> { "The", "Henry", "Stickmin", "Collection", "Stickman", "THSC", "HSC" }));
        games.Add(CreateGame("The Legend of Zelda: The Minish Cap", new List<string> { "Legend", "Zelda", "Minish", "Cap", "TLOZ", "TMC", "TLOZTMC", "TLOZ TMC", "The" }));
        games.Add(CreateGame("The Legend of Zelda: The Wind Waker", new List<string> { "Legend", "Zelda", "Wind", "Waker", "TLOZ", "WW", "TLOZWW", "TWW", "TLOZTWW", "The" }));
        games.Add(CreateGame("The Legend of Zelda: Twilight Princess", new List<string> { "Legend", "Zelda", "Twilight", "Princess", "TLOZ", "TP", "TLOZTP" }));
        games.Add(CreateGame("The Simpsons Hit & Run", new List<string> { "Simpsons", "Hit", "And", "Run", "SHAR" }));
        games.Add(CreateGame("The WereCleaner", new List<string> { "The", "WereCleaner", "Were", "Cleaner", "TWC" }));
        games.Add(CreateGame("The Witness", new List<string> { "Witness" }));
        games.Add(CreateGame("TOEM: A Photo Adventure", new List<string> { "TOEM", "A", "Photo", "Adventure", "TOEM:" }));
        games.Add(CreateGame("Totally Accurate Battle Simulator", new List<string> { "TABS", "Totally", "Accurate", "Battle", "Simulator" }));
        games.Add(CreateGame("TUNIC", new List<string> { "Tunic" }));
        games.Add(CreateGame("Ultimate Custom Night", new List<string> { "Ultimate", "Custom", "Night", "UCN", "UltimateCustom", "FNAF", "Five", "Nights", "at", "Freddy", "Freddy's", "FNAF UCN" }));
        games.Add(CreateGame("ULTRAKILL", new List<string> { "Ultra", "Kill" }));
        games.Add(CreateGame("Undertale Yellow", new List<string> { "Undertale", "Yellow" }));
        games.Add(CreateGame("Unfair Flips", new List<string> { "Unfair", "Flips" }));
        games.Add(CreateGame("Untitled Goose Game", new List<string> { "Untitled", "Goose", "Game", "UGG" }));
        games.Add(CreateGame("Vampire Survivors", new List<string> { "Vampire", "Survivors", "VS" }));
        games.Add(CreateGame("Voices Of The Void", new List<string> { "Voices", "Void", "Votv" }));
        games.Add(CreateGame("Yellow Taxi Goes Vroom", new List<string> { "Yellow", "Taxi", "Goes", "Vroom", "YTGV" }));
    }

    private GameButtonConfig CreateGame(string name, List<string> aliases)
    {
        return new GameButtonConfig
        {
            mainName = name,
            aliases = aliases
        };
    }
}