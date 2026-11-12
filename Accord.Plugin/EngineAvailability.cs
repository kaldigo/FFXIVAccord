using Newtonsoft.Json.Linq;

namespace Accord;

internal static class EngineAvailability
{
    internal static AutomaticRunner Resolve(AutomationEngine? preferred, bool wrath, bool rsr)
    {
        if (wrath && (!rsr || preferred == AutomationEngine.Wrath)) return AutomaticRunner.Wrath;
        return rsr ? AutomaticRunner.Rsr : AutomaticRunner.Off;
    }

    // Plugin identity alone also matches upstream releases. Require the fork-only
    // ownership protocol and actual runner-state reporting before offering it.
    internal static bool Compatible(bool loaded, JObject? reply) => loaded && reply is not null &&
        reply.Value<bool?>("Success") == true && reply.Value<int?>("Version") == 1 &&
        !string.IsNullOrEmpty(reply.Value<string>("Build")) &&
        reply["Running"]?.Type == JTokenType.Boolean && reply["Managed"]?.Type == JTokenType.Boolean;
}
