using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherglassUI;
using Dalamud.Bindings.ImGui;

namespace Accord;

internal sealed class AccordShell
{
    private readonly NavigationShell window=new();
    private readonly Dictionary<TaskPage,Topic[]> rows=[];
    private readonly List<NavigationPage> routes=[];
    private readonly Dictionary<TaskPage,Action> custom=[];
    private Action<TaskPage>? draw;
    private bool initialized;
    private void Initialize()
    {
        if(initialized)return;
        foreach(var desk in AccordNavigation.Areas)
        {
            var area=desk;
            if(area==Desk.Play)
                routes.Add(new(TaskPage.Home.ToString(),"Play",()=>DrawTask(TaskPage.Home)){DrawIcon=(p,s,c)=>AccordChrome.Glyph(area,p,s,c)});
            else
            {
                routes.Add(new(area.ToString(),AccordNavigation.Area(area),()=>{}){DrawIcon=(p,s,c)=>AccordChrome.Glyph(area,p,s,c)});
                foreach(var page in AccordNavigation.Pages.Where(p=>p.Desk==area))
                {
                    var task=page.Id;
                    routes.Add(new(task.ToString(),page.Name,()=>DrawTask(task)){ParentId=area.ToString(),Description=page.Detail});
                }
            }
        }
        initialized=true;
    }
    internal void OpenArea(Desk desk){Initialize();window.Navigate(desk==Desk.Play?TaskPage.Home.ToString():desk.ToString(),routes);}
    internal void OpenTask(TaskPage task){Initialize();window.Navigate(task.ToString(),routes);}
    internal void SetCustom(TaskPage task,Action render)=>custom[task]=render;
    internal string? ConsumeReveal(TaskPage task)
    {var state=window.State.Page(task.ToString());string? value=state.RevealSetting;state.RevealSetting=null;return value;}
    internal void Draw(string job,string engine,Action<TaskPage> render,Action status,Action dock,Action close,
        Func<TaskPage,Topic[]?>? topics=null,Action<Vector2,float>? drawMark=null,ChangeSummary? changes=null)
    {
        Initialize();draw=render;rows.Clear();
        var links=new List<SettingLink>();
        foreach(var page in AccordNavigation.Pages)
        {
            var sections=topics?.Invoke(page.Id);
            if(sections!=null)rows[page.Id]=sections;
            links.Add(new("page",page.Id.ToString(),page.Name,page.Detail+" "+AccordNavigation.Keywords(page.Id)));
            if(sections==null)continue;
            foreach(var section in sections)
                foreach(var row in section.Rows)
                    links.Add(new(section.Name+"/"+row.Id,page.Id.ToString(),row.Label,row.Keywords+" "+row.Detail,section.Name){Visible=row.Visible});
        }
        window.Close=close;window.Dock=dock;window.Changes=changes;window.SearchTrailing=status;
        window.Draw("Accord",string.IsNullOrEmpty(engine)?job:job+" / "+engine,routes,drawMark??AccordChrome.Mark,links);
    }
    private void DrawTask(TaskPage task)
    {
        var page=AccordNavigation.Pages.Single(p=>p.Id==task);
        PageControls.Heading(page.Name,breadcrumb:AccordNavigation.Area(page.Desk));
        if(custom.TryGetValue(task,out var body)){body();return;}
        if(!rows.TryGetValue(task,out var sections)){draw?.Invoke(task);return;}
        foreach(var section in sections)
            PageControls.Section(section.Name,section.Name,()=>DrawRows(task,section));
    }
    private void DrawRows(TaskPage task,Topic section)
    {
        var visible=section.Rows.Where(r=>r.Visible?.Invoke()??true).ToArray();
        for(int i=0;i<visible.Length;i++)
        {
            var row=visible[i];
            string id=section.Name+"/"+row.Id;
            bool reveal=window.State.Page(task.ToString()).RevealSetting==id;
            string? reason=row.DisabledReason?.Invoke();
            void Field()
            {
                ImGui.BeginDisabled(!string.IsNullOrEmpty(reason));
                try{if(reveal)ImGui.SetKeyboardFocusHere();row.Draw();}finally{ImGui.EndDisabled();}
            }
            if(row.Layout==FieldLayout.FullWidth){ImGui.PushID(id);try{Field();}finally{ImGui.PopID();}}
            else Controls.Row(id,row.Label,string.Join(" ",new[]{row.Detail,reason}.Where(s=>!string.IsNullOrEmpty(s))),Field,row.Layout,i==0,i==visible.Length-1);
            if(row.ExpandedContent!=null)
            {
                ImGui.PushID(id);ImGui.BeginDisabled(!string.IsNullOrEmpty(reason));
                try{row.ExpandedContent();}finally{ImGui.EndDisabled();ImGui.PopID();}
            }
            if(reveal){ImGui.SetScrollHereY(.2f);window.State.Page(task.ToString()).RevealSetting=null;}
        }
    }
}
