using System.Collections.Frozen;

namespace DlssSwapper.Shared;

// ProductCode identifies a product page; LauncherId is the distinct, case-sensitive --exec launch identifier.
// No reliable on-disk mapping for LauncherId is known. Preserve explicit values rather than guessing from Uid.
public sealed record BattleNetGameDefinition(string Uid, string ProductCode, string LauncherId, string Name);

public static class BattleNetGameCatalog
{
    // Definitions come from multiple places:
    // - https://github.com/dafzor/bnetlauncher/blob/master/bnetlauncher/Resources/gamesdb.ini
    // - https://github.com/lutris/lutris/blob/master/lutris/util/battlenet/definitions.py
    // - BattleNet agent log on launch
    //
    // Key is Uid, not ProductCode
    public static FrozenDictionary<string, BattleNetGameDefinition> Games { get; } = new Dictionary<string, BattleNetGameDefinition>()
    {
        { "rtro", new BattleNetGameDefinition("rtro", "rtro", "RTRO", "Blizzard Arcade Collection") },
        { "auks", new BattleNetGameDefinition("auks", "auks", "AUKS", "Call of Duty") },
        { "wlby", new BattleNetGameDefinition("wlby", "wlby", "WLBY", "Crash Bandicoot 4: It's About Time") },
        { "w1r", new BattleNetGameDefinition("w1r", "w1r", "W1R", "Warcraft I: Remastered") },
        { "diablo3", new BattleNetGameDefinition("diablo3", "d3", "D3", "Diablo III") },
        { "aris", new BattleNetGameDefinition("aris", "aris", "ARIS", "Doom: The Dark Ages") },
        { "heroes", new BattleNetGameDefinition("heroes", "hero", "Hero", "Heroes of the Storm") },
        { "d3cn", new BattleNetGameDefinition("d3cn", "d3cn", "D3CN", "暗黑破壞神III") }, // to verify
        { "aqua", new BattleNetGameDefinition("aqua", "aqua", "AQUA", "Avowed") },
        { "s2", new BattleNetGameDefinition("s2", "s2", "S2", "StarCraft II") },
        { "w2", new BattleNetGameDefinition("w2", "w2bn", "W2", "Warcraft II: Battle.net Edition") },
        { "fenris", new BattleNetGameDefinition("fenris", "fenris", "Fen", "Diablo IV") },
        { "d1", new BattleNetGameDefinition("d1", "drtl", "D1", "Diablo") },
        { "scor", new BattleNetGameDefinition("scor", "scor", "SCOR", "Sea of Thieves") },
        { "w3", new BattleNetGameDefinition("w3", "w3", "W3", "Warcraft III: Reforged") },
        { "fore", new BattleNetGameDefinition("fore", "fore", "FORE", "Call of Duty: Vanguard") }, // to verify
        { "s1", new BattleNetGameDefinition("s1", "s1", "S1", "StarCraft") },
        { "wow", new BattleNetGameDefinition("wow", "wow", "WoW", "World of Warcraft") },
        { "osi", new BattleNetGameDefinition("osi", "osi", "OSI", "Diablo II: Resurrected") },
        { "lazarus", new BattleNetGameDefinition("lazarus", "lazr", "LAZR", "Call of Duty: MW2 Campaign Remastered") }, // to verify
        { "odin", new BattleNetGameDefinition("odin", "odin", "ODIN", "Call of Duty: Modern Warfare") }, // to verify
        { "pinta", new BattleNetGameDefinition("pinta", "pinta", "PNTA", "Call of Duty: Modern Warfare III") }, // to verify
        { "prometheus", new BattleNetGameDefinition("prometheus", "pro", "Pro", "Overwatch") },
        { "viper", new BattleNetGameDefinition("viper", "viper", "VIPR", "Call of Duty: Black Ops 4") }, // to verify
        { "zeus", new BattleNetGameDefinition("zeus", "zeus", "ZEUS", "Call of Duty: Black Ops Cold War") }, // to verify
        { "w1", new BattleNetGameDefinition("w1", "war1", "W1", "Warcraft: Orcs & Humans") },
        { "w2r", new BattleNetGameDefinition("w2r", "w2r", "W2R", "Warcraft II Remastered") },
        { "hs_beta", new BattleNetGameDefinition("hs_beta", "hsb", "WTCG", "Hearthstone") },
        
        // The launcher is not working, but that is acceptable because WoW is hidden.
        { "wow_classic", new BattleNetGameDefinition("wow_classic", "wow_classic", "Wow_wow_classic", "World of Warcraft Classic") }, // to verify

        // Does not appear in aggregate.json so they have no cover photos.
        { "lbra", new BattleNetGameDefinition("lbra", "lbra", "LBRA", "Tony Hawk's Pro Skater 3+4") },
        { "ark", new BattleNetGameDefinition("ark", "ark", "ARK", "The Outer Worlds 2") },
        { "nina", new BattleNetGameDefinition("nina", "nina", "NINA", "Call of Duty: Modern Warfare II") },


        // Mobile-only titles such as Diablo Immortal and Warcraft Rumble are intentionally excluded.
    }.ToFrozenDictionary(StringComparer.Ordinal);

}
