using System;
using Newtonsoft.Json.Linq;

namespace Accord;

internal static class VerifiedSettings
{
    internal static JObject Apply(JObject desired, Func<JObject> read, Action<string, JToken> write)
    {
        var before = read();
        // Validate the entire surface before the first mutation.
        foreach (var item in desired.Properties())
            if (before[item.Name]?.Type != item.Value.Type)
                throw new InvalidOperationException("Missing or incompatible engine setting: " + item.Name);
        foreach (var item in desired.Properties())
            if (!JToken.DeepEquals(before[item.Name], item.Value)) write(item.Name, item.Value);
        var after = read();
        foreach (var item in desired.Properties())
            if (!JToken.DeepEquals(after[item.Name], item.Value))
                throw new InvalidOperationException("Engine read-back did not match Accord: " + item.Name);
        return after;
    }
}
