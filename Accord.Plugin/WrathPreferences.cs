using Newtonsoft.Json.Linq;
namespace Accord;

internal static class WrathPreferences
{
    internal const string DeferredWrite = "Saved changes will apply outside combat after the current action finishes.";

    internal static JObject Resolve(JObject saved)
    {
        var result = new JObject
        {
            ["Drg"] = true,
            ["Machinist"] = true,
            ["Bard"] = true,
            ["Viper"] = true,
            ["Reaper"] = true,
            ["Summoner"] = true,
            ["RedMage"] = true,
            ["Pictomancer"] = true,
            ["BlackMage"] = true,
            ["Samurai"] = true,
            ["Monk"] = true,
            ["Ninja"] = true,
            ["Dancer"] = true,
            ["BlueMage"] = true,
            ["Beastmaster"] = true,
            ["GroundTargets"] = true,
            ["SpaceMitigation"] = true,
            ["PvpMachinist"] = true,
            ["PvpScheduling"] = true,
            ["PvpTargeting"] = true,
            ["Paladin"] = true,
            ["Warrior"] = true,
            ["Gunbreaker"] = true,
            ["DarkKnight"] = true,
            ["CompareDrg"] = false,
            ["Sage"] = true,
            ["Scholar"] = true,
            ["Astrologian"] = true,
            ["WhiteMage"] = true,
            ["Forecasts"] = true,
            ["AllowPhantomKick"] = false,
            ["RaiseNearbyPlayers"] = true,
            ["IncludeRaise"] = false,
            ["IncludeCleanse"] = false,
            ["PreventiveHealing"] = true,
            ["HardCastRaise"] = true,
            ["PartyMouseover"] = true,
            ["WorldMouseover"] = true,
            ["Focus"] = false,
            ["Fallback"] = false,
            ["AutomaticAllies"] = true,
            ["EnemyTargeting"] = true,
            ["AcquireEnemy"] = true,
            ["EnemyMouseover"] = false,
            ["EnemyFocus"] = false,
            ["AutomaticEnemies"] = true,
            ["OverridesInAutomation"] = false,
            ["AcquireVisibleEnemy"] = true,
            ["SwitchEnemyTarget"] = false,
            ["SwitchAllyTarget"] = false,
            ["IncludeHealing"] = false,
            ["HealingGcds"] = false,
            ["AllyOverrideOrder"] = new JArray("PartyMouseover", "WorldMouseover", "Selected", "Focus"),
            ["EnemyOverrideOrder"] = new JArray("WorldMouseover", "Selected", "Focus"),
        };
        foreach (var item in saved.Properties()) result[item.Name] = item.Value.DeepClone();
        return result;
    }
    internal static JObject? Apply(JObject desired, System.Func<JObject> read, System.Action<JObject> write, JObject? lastVerified = null)
    {
        var before = read();
        // Older Wrath builds can still verify and apply the rest of the profile during an update.
        desired = (JObject)desired.DeepClone();
        foreach (string optional in new[] { "AcquireVisibleEnemy", "AllowPhantomKick", "RaiseNearbyPlayers" })
            if (!before.ContainsKey(optional)) desired.Remove(optional);
        var changes = new JObject();
        foreach (var item in desired.Properties())
        {
            if (before[item.Name]?.Type != item.Value.Type)
                throw new System.InvalidOperationException("Missing or incompatible Wrath setting: " + item.Name);
            if (!JToken.DeepEquals(before[item.Name], item.Value)) changes[item.Name] = item.Value.DeepClone();
        }
        if (changes.Count > 0)
        {
            try { write(changes); }
            catch (System.InvalidOperationException ex) when (ex.Message == DeferredWrite && lastVerified != null)
            {
                // Wrath rejects the batch before applying it. Confirm the running profile is still intact.
                if (JToken.DeepEquals(read(), lastVerified)) return null;
                throw;
            }
        }
        var after = read();
        foreach (var item in desired.Properties())
            if (!JToken.DeepEquals(after[item.Name], item.Value))
                throw new System.InvalidOperationException("Wrath read-back did not match Accord: " + item.Name);
        return after;
    }
}
