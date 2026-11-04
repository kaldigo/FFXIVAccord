using System;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using Newtonsoft.Json.Linq;

namespace Accord;

internal sealed class AutomationControl : IDisposable
{
    private sealed class Endpoint(string name) : IRunnerEndpoint
    {
        public RunnerStatus Send(string command, Guid token)
        {
            var request = new JObject { ["Command"] = command, ["Token"] = token.ToString() };
            var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string>(name).InvokeFunc(request.ToString()));
            return new(response.Value<bool>("Success") && response.Value<int>("Version") == 1,
                response.Value<bool>("Owned"), response.Value<bool>("Allowed"), response.Value<bool>("Busy"), response.Value<string>("Build") ?? "");
        }
    }

    private readonly AutomationHandoff handoff = new(new Endpoint("WrathCombo.AutomationOwnership.v1"), new Endpoint("RotationSolverReborn.AccordAutomationOwnership"));
    private readonly ICallGateProvider<string, string> api = Svc.PluginInterface.GetIpcProvider<string, string>("Accord.AutomationRunner.v1");
    private bool used;
    internal AutomationControl() => api.RegisterFunc(Request);
    internal void Tick() => handoff.Tick(Environment.TickCount64);
    internal string Request(string command)
    {
        if (!Svc.Framework.IsInFrameworkUpdateThread) return "Use the framework thread.";
        switch (command.ToLowerInvariant())
        {
            case "wrath": used = true; handoff.Request(AutomaticRunner.Wrath, Environment.TickCount64); break;
            case "rsr": used = true; handoff.Request(AutomaticRunner.Rsr, Environment.TickCount64); break;
            case "off": used = true; handoff.Request(AutomaticRunner.Off, Environment.TickCount64); break;
            case "release": handoff.Release(Environment.TickCount64); used = false; break;
            case "status": break;
            default: return "Choose wrath, rsr, off, release or status.";
        }
        return handoff.Status;
    }
    internal void Draw()
    {
        ImGui.TextWrapped("Testing automation handoff. Requires the matching Wrath and RSR testing builds. Manual Wrath buttons remain available. An engine reload or lost connection stops automation; select a runner again to restart.");
        if (ImGui.Button("Wrath automation")) Queue("wrath");
        ImGui.SameLine(); if (ImGui.Button("RSR automation")) Queue("rsr");
        ImGui.SameLine(); if (ImGui.Button("Stop automation")) Queue("off");
        if (ImGui.Button("Return control to each engine")) Queue("release");
        ImGui.TextWrapped(handoff.Status);
        ImGui.Separator();
    }
    private void Queue(string command) => _ = Svc.Framework.RunOnTick(() => Request(command));
    public void Dispose()
    {
        if (used) handoff.Stop();
        api.UnregisterFunc();
    }
}
