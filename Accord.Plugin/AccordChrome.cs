using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Glasswork;
using Glasswork.Backend;

namespace Accord;

// Shared window chrome. Semantic glyphs use a 24-unit grid so they stay legible at UI scale.
internal static class AccordChrome
{
    private static readonly Vector4 Mint = new(.18f, .89f, .73f, 1);
    private static readonly Vector4 Violet = new(.65f, .51f, 1, 1);
    private static readonly Vector4 Lilac = new(.87f, .64f, .95f, 1);
    private static uint Ink(Vector4 color) => ImGui.GetColorU32(color);

    internal static Look Theme()
    {
        var look = Look.NeutralDark().WithAccent(new Color(.12f, .44f, .42f, 1));
        look.Background = new Color(.055f, .058f, .074f, 1);
        look.SecondaryBackground = new Color(.083f, .087f, .108f, 1);
        look.TertiaryBackground = new Color(.115f, .119f, .145f, 1);
        look.OnAccent = new Color(.97f, .98f, .99f, 1);
        look.ToggleOn = new Color(.09f, .62f, .51f, 1);
        return look;
    }

    internal static void Header(string job, string engine, Action close)
    {
        float s = Frame.Scale;
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        // Accord's three offset sliders, in the color order of its plugin icon.
        ReadOnlySpan<Vector4> colors = [new(0, .96f, .64f, 1), new(.35f, .60f, 1, 1), new(.96f, .02f, 1, 1)];
        uint left = Ink(colors[0]), middle = Ink(colors[1]), right = Ink(colors[2]);
        for (int i = 0; i < 3; i++)
        {
            float y = (9 + 7 * i) * s;
            var knob = start + new Vector2((9 + 6 * i) * s, y);
            dl.AddRectFilledMultiColor(start + new Vector2(4*s, y-s), start + new Vector2(16*s, y+s), left, middle, middle, left);
            dl.AddRectFilledMultiColor(start + new Vector2(16*s, y-s), start + new Vector2(28*s, y+s), middle, right, right, middle);
            dl.AddRectFilled(knob - new Vector2(3*s), knob + new Vector2(3*s), Ink(colors[i]), 1.5f*s);
        }
        float width = ImGui.GetContentRegionAvail().X;
        float yText = (32*s - ImGui.GetTextLineHeight()) / 2;
        var title = start + new Vector2(40*s, yText);
        dl.AddText(title, Ink(new Vector4(.96f,.97f,.99f,1)), "Accord");
        dl.AddText(title + new Vector2(ImGui.CalcTextSize("Accord").X + 14*s,0),
            Ink(new Vector4(.73f,.74f,.79f,1)), job + (engine.Length > 0 ? "  /  " + engine : ""));
        float closeWidth = 32*s;
        ImGui.InvisibleButton("Move Accord window", new Vector2(Math.Max(1, width-closeWidth-8*s),32*s));
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        ImGui.SameLine(0,8*s);
        var button = ImGui.GetCursorScreenPos();
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        if (ImGui.Button("###Close Accord", new Vector2(closeWidth,32*s))) close();
        ImGui.PopStyleColor();
        var center=button+new Vector2(16*s);
        uint ink=Ink(new Vector4(.75f,.76f,.81f,1));
        dl.AddLine(center-new Vector2(4*s),center+new Vector2(4*s),ink,1.6f*s);
        dl.AddLine(center+new Vector2(-4*s,4*s),center+new Vector2(4*s,-4*s),ink,1.6f*s);
        if (ImGui.IsItemHovered() || ImGui.IsItemFocused()) ImGui.SetTooltip("Close Accord");
        ImGui.Separator();
    }

    internal static bool RailItem(Desk desk, string name, bool active)
    {
        float s = Frame.Scale;
        var p = ImGui.GetCursorScreenPos();
        var size = new Vector2(42, 41) * s;
        // A normal named button retains ImGui's keyboard/gamepad focus and activation.
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        bool clicked = ImGui.Button("###" + name, size);
        ImGui.PopStyleColor(3);
        bool hovered = ImGui.IsItemHovered(), focused = ImGui.IsItemFocused();
        var dl = ImGui.GetWindowDrawList();
        var color = desk switch { Desk.Play => Mint, Desk.Rules => new Vector4(.42f,.79f,.99f,1), Desk.Bars => Violet, _ => Lilac };
        if (active || hovered)
        {
            var fill = color;
            fill.W = active ? .17f : .09f;
            dl.AddRectFilled(p + new Vector2(2*s), p + size - new Vector2(2*s), Ink(fill), 7*s);
        }
        if (active) dl.AddLine(p + new Vector2(2,12)*s,p + new Vector2(2,29)*s,Ink(color),3*s);
        uint stroke = Ink(active || hovered || focused ? color : new Vector4(.72f,.74f,.81f,1));
        var origin = p + (size - new Vector2(24*s)) / 2;
        Vector2 Pt(float x,float y) => origin + new Vector2(x,y)*s;
        void Line(float x,float y,float u,float v) => dl.AddLine(Pt(x,y), Pt(u,v), stroke, 1.8f*s);
        switch (desk)
        {
            case Desk.Play:
                dl.AddTriangleFilled(Pt(13,2),Pt(4,14),Pt(12,13),stroke);
                dl.AddTriangleFilled(Pt(11,10),Pt(20,9),Pt(9,23),stroke);
                break;
            case Desk.Rules:
                for (int i=0;i<3;i++) {
                    float y=5+i*7, x=7+i*4;
                    Line(2,y,22,y);
                    dl.AddRectFilled(Pt(x-2,y-2.5f),Pt(x+2,y+2.5f),stroke,1*s);
                }
                break;
            case Desk.Bars:
                for (int row=0;row<2;row++)
                    for (int col=0;col<3;col++)
                        dl.AddRect(Pt(2+col*7,5+row*8),Pt(7+col*7,11+row*8),stroke,1*s,ImDrawFlags.None,1.5f*s);
                break;
            case Desk.Tools:
                Line(1,13,6,13); Line(6,13,10,5); Line(10,5,14,19); Line(14,19,18,11); Line(18,11,23,11);
                break;
        }
        if (hovered || focused) ImGui.SetTooltip(name);
        return clicked;
    }
}
