global using Dalamud.Bindings.ImGui;
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
    private readonly string[] names = ["Single target (ST)", "Area damage (AoE)", "Healing protection"];
    private readonly bool[] keep = [true, false, false];
    private readonly bool[] permissions = [false,true,true,false];
    private readonly WrathFork.Contract.RotationPreferences rotation = new();
    private protected void StartTask(TaskPage page)=>shell.OpenTask(page);
    private protected void StartIn(Desk area) => shell.OpenArea(area);
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
    public Look Look => Look.NeutralDark();
    public virtual ReadOnlySpan<PreviewInput> Script => [];
    public void Draw()
    {
        shell.Draw("BRD", engine == 0 ? "Wrath" : "RSR", task =>
    {
        if (task == TaskPage.Home)
        {
            ImGui.TextWrapped(enabled ? "Automatic execution active." : "Auto rotation disabled.");
            AccordControls.Runner(() => Gw.ComboBox(ref engine, ["Wrath", "RSR"], "Automation engine"),
                follow, "Follow AutoDuty navigation while auto rotation is enabled.", value => follow = value);
            AccordControls.Section("Action permissions", () =>
            {
                string[] labels = ["Auto burst", "Auto protection", "Auto support", "Auto class mechanics"];
                for (int i=0;i<labels.Length;i++) AccordControls.Toggle(labels[i], ref permissions[i], "Global");
            });
        }
        else if (task == TaskPage.Mapping) DrawMapping();
        else if (task == TaskPage.Healing)
        {
            Gw.Heading("Damage buttons · save changes");
            LayoutEditor.DrawRotation(rotation, new(), false, LayoutEditor.RotationSection.Healing);
            Gw.Heading("Wrath preferences · saved automatically");
            AccordControls.HealingRules(key => ruleset.TryGetValue(key, out bool value) && value, (key, value) => ruleset[key] = value);
        }
        else if (task == TaskPage.Enemies) AccordControls.EnemyRules(key => ruleset.TryGetValue(key, out bool value) && value, (key, value) => ruleset[key] = value);
        }, () => { }, () => AccordControls.Dock(enabled, value => enabled = value), () => { });
    }
    private void DrawMapping()
    {
        ImGui.TextUnformatted("Bar 1 · Left edge");
        AccordControls.MappingTable(stacked =>
        {
            for (int i = 0; i < names.Length; i++)
            {
                ImGui.PushID(i);
                AccordControls.MappingRow(stacked, $"{i + 1}. {names[i]}", false, ref keep[i]);
                foreach (string label in new[] { "Up", "Down", "Move", "Options" })
                {
                    if (label != "Up") ImGui.SameLine();
                    ImGui.SmallButton(label);
                }
                ImGui.PopID();
            }
        });
    }

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
public sealed class Targeting : Screen
{
    public Targeting() : base(true) => StartTask(TaskPage.Enemies);

    public override string Name => "accord-targeting";
    public override Vector2 WindowSize => new(960, 800);
}
public sealed class Hotbars : Screen
{
    public Hotbars() : base(false) => StartIn(Desk.Bars);
    public override string Name => "accord-hotbars";
    public override Vector2 WindowSize => new(960, 770);
}
public sealed class Tools : Screen
{
    public Tools() : base(false) => StartIn(Desk.Tools);
    public override string Name => "accord-tools";
    public override Vector2 WindowSize => new(410, 650);
}
public sealed class Healing : Screen
{
    public Healing() : base(true) => StartTask(TaskPage.Healing);

    public override string Name => "accord-healing";
    public override Vector2 WindowSize => new(960, 760);
}

public sealed class Mapping : Screen
{
    public Mapping() : base(false) => StartTask(TaskPage.Mapping);
    public override string Name => "accord-mapping-compact";
    public override Vector2 WindowSize => new(360, 660);

}

public sealed class RailHover() : Screen(false)
{
    public override string Name => "accord-rail-hover";
    public override Vector2 WindowSize => new(960, 710);
    private static readonly PreviewInput[] Hover = [new(2, PreviewInputKind.Move, new(34, 128)), new(65, PreviewInputKind.Move, new(34, 128))];
    public override ReadOnlySpan<PreviewInput> Script => Hover;
}
