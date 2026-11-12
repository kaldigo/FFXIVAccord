using System;
using System.Numerics;
using Accord;
using Glasswork;
using Glasswork.Backend;
using Glasswork.Backend.Preview;

namespace AccordPreview;

public abstract class Screen : IPreviewScreen
{
    private readonly AccordShell shell = new();
    private bool enabled = true, follow;
    private int engine;
    private readonly System.Collections.Generic.Dictionary<string, bool> ruleset = new()
    {
        ["AcquireVisibleEnemy"] = true, ["EnemyTargeting"] = true, ["AcquireEnemy"] = true, ["Fallback"] = true
    };
    protected Screen(bool rules)
    {
        if (rules) shell.OpenArea(Desk.Rules);
    }
    public abstract string Name { get; }
    public abstract Vector2 WindowSize { get; }
    public Look Look => AccordShell.Theme();
    public virtual ReadOnlySpan<PreviewInput> Script => [];
    public void Draw() => shell.Draw("BRD", task =>
    {
        if (task == TaskPage.Home)
            AccordControls.Runner(enabled, enabled ? $"{(engine == 0 ? "Wrath" : "RSR")} owns automatic execution." : "Automation stopped.",
                value => enabled = value, () => Gw.ComboBox(ref engine, ["Wrath", "RSR"], "Automation engine"),
                follow, "Follow AutoDuty navigation while auto rotation is enabled.", value => follow = value);
        else if (task == TaskPage.Enemies) AccordControls.EnemyRules(key => ruleset.TryGetValue(key, out bool value) && value, (key, value) => ruleset[key] = value);
    }, () => { });
}
public sealed class Play() : Screen(false)
{
    public override string Name => "accord-play";
    public override Vector2 WindowSize => new(960, 650);
}
public sealed class Compact() : Screen(false)
{
    public override string Name => "accord-compact";
    public override Vector2 WindowSize => new(410, 650);
}
public sealed class Rules() : Screen(true)
{
    public override string Name => "accord-rules";
    public override Vector2 WindowSize => new(960, 720);
}
public sealed class Targeting() : Screen(true)
{
    private static readonly PreviewInput[] Navigate =
    [
        new(3, PreviewInputKind.Press, new(290, 220)),
        new(4, PreviewInputKind.Release, new(290, 220)),
        new(8, PreviewInputKind.Move, new(180, 90))
    ];
    public override ReadOnlySpan<PreviewInput> Script => Navigate;
    public override string Name => "accord-targeting";
    public override Vector2 WindowSize => new(960, 800);
}
