using Accord;

internal static class UiNavigationTests
{
    internal static int Run()
    {
        int count=0;
        void Check(bool pass,string message){count++;if(!pass)throw new Exception(message);}
        TaskPage[] original=[TaskPage.Home,TaskPage.Permissions,TaskPage.Enemies,TaskPage.Healing,TaskPage.Opener,
            TaskPage.Reserved,TaskPage.JobTuning,TaskPage.Rsr,TaskPage.Jobs,TaskPage.Managed,TaskPage.Mapping,
            TaskPage.RoleActions,TaskPage.Groups,TaskPage.Overrides,TaskPage.PvP,TaskPage.PvPOverrides,
            TaskPage.Preview,TaskPage.DutyLog,TaskPage.Trace,TaskPage.Recovery];
        foreach(var task in original)Check(AccordNavigation.Pages.Count(p=>p.Id==task)==1,"Lost or duplicated original destination: "+task);
        Check(AccordNavigation.Pages.Select(p=>p.Id).Distinct().Count()==AccordNavigation.Pages.Length,"Ambiguous destination IDs");
        Check(Enum.GetValues<TaskPage>().All(t=>AccordNavigation.Pages.Any(p=>p.Id==t)),"New destination is unreachable");
        Check(AccordNavigation.Areas.SequenceEqual([Desk.Play,Desk.Rules,Desk.Bars,Desk.Setup,Desk.Tools]),"Unexpected workspace navigation");
        foreach(var desk in AccordNavigation.Areas)Check(AccordNavigation.Pages.Any(p=>p.Desk==desk),"Empty workspace: "+desk);
        Check(AccordNavigation.Pages.Single(p=>p.Id==TaskPage.Jobs).Desk==Desk.Setup,"Managed jobs lost their setup home");
        Check(AccordNavigation.Pages.Single(p=>p.Id==TaskPage.CombatOverrides).Desk==Desk.Rules,"Combat overrides are not discoverable from Combat");
        Check(AccordNavigation.Keywords(TaskPage.Mapping).Contains("joined"),"Legacy mapping search keywords were lost");
        Check(AccordNavigation.Keywords(TaskPage.Recovery).Contains("migration"),"Import recovery lost its search terms");
        return count;
    }
}
