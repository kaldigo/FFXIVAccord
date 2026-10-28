using System;

namespace Accord;

internal static class CombinedUpdate
{
    // The hotbar writer owns its slot rollback. This boundary restores engine
    // settings if either stage fails, including a lost acknowledgement.
    internal static void Apply(Action settings, Action layout, Action restoreSettings)
    {
        try { settings(); layout(); }
        catch (Exception failure)
        {
            try { restoreSettings(); }
            catch (Exception rollback) { throw new AggregateException("Combined update failed and settings recovery requires attention.", failure, rollback); }
            throw new InvalidOperationException("Update failed; engine settings restored. Hotbar recovery details: " + failure.Message, failure);
        }
    }
}
