using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Accord;

internal static class AccordChrome
{
    private static uint Ink(Vector4 color)=>ImGui.GetColorU32(color);
    internal static void Mark(Vector2 start,float size)
    {
        float s=size/30;
        var dl=ImGui.GetWindowDrawList();
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
    }
    internal static void Glyph(Desk desk,Vector2 origin,float size,uint stroke)
    {
        float s=size/24;
        var dl=ImGui.GetWindowDrawList();
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
    }
}
