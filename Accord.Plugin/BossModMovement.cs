using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.DalamudServices;
using ECommons.GameHelpers;

namespace Accord;

internal sealed class BossModMovement : IDisposable
{
    private readonly ICallGateSubscriber<List<string>, bool, List<string>> read =
        Svc.PluginInterface.GetIpcSubscriber<List<string>, bool, List<string>>("BossMod.Configuration");
    private readonly ICallGateSubscriber<List<string>, List<string>> setTransient =
        Svc.PluginInterface.GetIpcSubscriber<List<string>, List<string>>("BossMod.Configuration.SetTransient");
    private readonly ICallGateSubscriber<ulong, byte> wrathPositional =
        Svc.PluginInterface.GetIpcSubscriber<ulong, byte>("WrathCombo.GetMovementPositional");
    private readonly ICallGateSubscriber<string?> activePreset =
        Svc.PluginInterface.GetIpcSubscriber<string?>("BossMod.Presets.GetActive");
    private readonly ICallGateSubscriber<string?> aiPreset =
        Svc.PluginInterface.GetIpcSubscriber<string?>("BossMod.AI.GetPreset");
    private readonly ICallGateSubscriber<string, string, string, string, bool> setStrategy =
        Svc.PluginInterface.GetIpcSubscriber<string, string, string, string, bool>("BossMod.Presets.AddTransientStrategy");
    private readonly ICallGateSubscriber<string, string, string, bool> clearStrategy =
        Svc.PluginInterface.GetIpcSubscriber<string, string, string, bool>("BossMod.Presets.ClearTransientStrategy");

    private readonly FieldOverride minimum = new("AIConfig", "MinDistance");
    private readonly FieldOverride maximum = new("AIConfig", "MaxDistanceToTarget");
    private readonly FieldOverride useRsrPositional = new("AutorotationConfig", "FollowRSRDesiredPositional");
    private readonly PresetOverride rangeStrategy = new("StayCloseToTarget", "range");
    private readonly PresetOverride positionalStrategy = new("GoToPositional", "Positional");
    private long nextUpdate;

    internal string Status { get; private set; } = "";

    internal void Tick(SetupController setup)
    {
        long now = Environment.TickCount64;
        if (now < nextUpdate) return;
        nextUpdate = now + 100;

        var preferences = setup.Settings.BossModMovement;
        if (!preferences.UseRoleDistances && !preferences.FollowWrathPositionals && !HasOverrides)
        {
            Status = "";
            return;
        }

        var plugins = Svc.PluginInterface.InstalledPlugins;
        if (!plugins.Any(p => p.InternalName == "BossModReborn" && p.IsLoaded))
        {
            minimum.Forget();
            maximum.Forget();
            useRsrPositional.Forget();
            rangeStrategy.Forget();
            positionalStrategy.Forget();
            Status = "Waiting for BossMod Reborn.";
            return;
        }

        if (plugins.Any(p => p.InternalName == "BossMod" && p.IsLoaded))
        {
            Status = "Use BossMod Reborn by itself to manage movement from Accord.";
            return;
        }

        var player = Player.Object;
        var role = JobRole.Noncombat;
        if (!Plugin.Instance.MigrationPending && Svc.ClientState.IsLoggedIn &&
            player is { IsDead: false } && !SetupController.IsPvP &&
            !Svc.Condition[ConditionFlag.BetweenAreas] && !Svc.Condition[ConditionFlag.BetweenAreas51] &&
            setup.Pack.IsActive((uint)Player.Job, setup.Settings))
            role = LayoutPreferences.Role((uint)Player.Job);

        try
        {
            if (!read.HasFunction || !setTransient.HasFunction || !setStrategy.HasFunction || !clearStrategy.HasFunction)
            {
                Status = "BossMod Reborn's temporary movement IPC is unavailable.";
                return;
            }

            string? preset = role == JobRole.Noncombat ? null : GetPreset();
            bool autoDuty = preset is "AutoDuty" or "AutoDuty Passive";
            var distances = autoDuty ? null : preferences.Distances(role);
            minimum.Apply(this, preset is null ? distances?.Minimum.ToString() : null);
            maximum.Apply(this, preset is null ? distances?.Maximum.ToString() : null);
            bool rangeApplied = rangeStrategy.Apply(this, preset,
                preset is not null ? distances?.Maximum.ToString("0.0", CultureInfo.InvariantCulture) : null);

            bool wrathAvailable = wrathPositional.HasFunction;
            string? desired = null;
            if (preferences.FollowWrathPositionals && wrathAvailable && role == JobRole.Melee && preset is not null && !autoDuty &&
                Svc.Condition[ConditionFlag.InCombat] && player is { TargetObjectId: > 0 })
            {
                desired = wrathPositional.InvokeFunc(player.TargetObjectId) switch
                {
                    1 => "Rear",
                    2 => "Flank",
                    _ => null,
                };
            }
            bool positionalApplied = positionalStrategy.Apply(this, preset, desired);
            useRsrPositional.Apply(this, desired is not null && positionalApplied ? bool.FalseString : null);

            Status = (role, autoDuty, rangeApplied, positionalApplied) switch
            {
                (JobRole.Noncombat, _, _, _) => "Movement settings apply to selected PvE combat classes.",
                (_, true, _, _) => "AutoDuty is managing this movement preset.",
                (_, _, false, _) => "Add Stay within range of target to this BossMod Reborn preset to use role distances.",
                (_, _, _, false) => "Add Goes to specified positional to this BossMod Reborn preset to follow Wrath.",
                (JobRole.Melee, _, _, _) when preferences.FollowWrathPositionals && !wrathAvailable =>
                    "Wrath's movement positional IPC is unavailable.",
                (JobRole.Melee, _, _, _) when preferences.FollowWrathPositionals && preset is null =>
                    "Activate a BossMod Reborn movement preset with Goes to specified positional to follow Wrath.",
                _ => "",
            };
        }
        catch (IpcNotReadyError)
        {
            Status = "Waiting for BossMod Reborn or Wrath IPC.";
        }
        catch (InvalidOperationException e)
        {
            Status = e.Message;
        }
    }

    private bool HasOverrides => minimum.Active || maximum.Active || useRsrPositional.Active ||
        rangeStrategy.Active || positionalStrategy.Active;

    private string? GetPreset()
    {
        string? preset = activePreset.HasFunction ? activePreset.InvokeFunc() : null;
        if (string.IsNullOrEmpty(preset) && aiPreset.HasFunction)
            preset = aiPreset.InvokeFunc();
        return string.IsNullOrWhiteSpace(preset) ? null : preset;
    }

    public void Dispose()
    {
        if (!HasOverrides || !read.HasFunction || !setTransient.HasFunction ||
            Svc.PluginInterface.InstalledPlugins.Any(p => p.InternalName == "BossMod" && p.IsLoaded))
            return;
        foreach (var field in new[] { minimum, maximum, useRsrPositional })
        {
            try { field.Apply(this, null); }
            catch (IpcNotReadyError) { }
            catch (InvalidOperationException e) { Svc.Log.Warning(e.Message); }
        }
        if (!clearStrategy.HasFunction) return;
        try
        {
            rangeStrategy.Apply(this, null, null);
            positionalStrategy.Apply(this, null, null);
        }
        catch (IpcNotReadyError) { }
    }

    private sealed class FieldOverride(string node, string field)
    {
        private string? original;
        private string? applied;
        internal bool Active => applied is not null;

        internal void Forget() => original = applied = null;

        internal void Apply(BossModMovement owner, string? value)
        {
            if (value == applied) return;

            var result = owner.read.InvokeFunc([node, field], false);
            if (result.Count != 1 || result[0].Contains(' '))
                throw new InvalidOperationException($"BossMod Reborn could not read {field}.");
            string current = result[0];
            if (value is null)
            {
                // Restore only this field and leave other plugins' temporary settings alone.
                if (current == applied && original != current)
                    Write(owner, original!);
                Forget();
                return;
            }

            string baseline = current == applied ? original! : current;
            if (current != value)
                Write(owner, value);
            original = baseline;
            applied = value;
        }

        private void Write(BossModMovement owner, string value)
        {
            if (owner.setTransient.InvokeFunc([node, field, value]).Count != 0)
                throw new InvalidOperationException($"BossMod Reborn could not set {field}.");
        }
    }

    private sealed class PresetOverride(string module, string track)
    {
        private readonly string moduleType = $"BossMod.Autorotation.MiscAI.{module}";
        private string? preset;
        private string? applied;
        internal bool Active => preset is not null;

        internal void Forget() => preset = applied = null;

        internal bool Apply(BossModMovement owner, string? name, string? value)
        {
            if (preset is not null && (preset != name || value is null))
            {
                owner.clearStrategy.InvokeFunc(preset, moduleType, track);
                Forget();
            }
            if (name is null || value is null || (preset == name && applied == value))
                return true;
            if (!owner.setStrategy.InvokeFunc(name, moduleType, track, value))
                return false;
            preset = name;
            applied = value;
            return true;
        }
    }
}
