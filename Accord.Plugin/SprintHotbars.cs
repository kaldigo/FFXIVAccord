using System.IO;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using HotbarTools;

namespace Accord;

internal static class SprintHotbars
{
    internal static unsafe void Refresh(bool pvp)
    {
        HotbarLayoutStore.Ready(pvp);
        uint job = (uint)Player.Job;
        if (pvp) HotbarLayoutStore.ObservePvP(job);
        var edits = ModeActions.SprintEdits(job, pvp, p => NativeHotbars.Ready(job,pvp)->IsHotbarShared((uint)p.Bar)
            ? default : NativeHotbars.Read(job, p, pvp: pvp));
        if (edits.Count == 0) return;
        var path = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "sprint-backups",
            NativeHotbars.CharacterKey(Svc.PlayerState.ContentId), pvp ? "pvp" : "pve");
        // Native shared bars are never changed, including Sprint conversion.
        NativeHotbars.Apply(job, edits, path, pvp: pvp);
    }
}
