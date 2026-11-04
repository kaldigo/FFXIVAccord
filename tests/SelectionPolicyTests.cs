using System.Collections.Immutable;
using System.Numerics;
using WrathCombo.Engine.Selection;

internal static class SelectionPolicyTests
{
    internal static int Run()
    {
        int checks=0;
        void Check(bool value,string message) { checks++; if(!value)throw new Exception(message); }
        var tank=new TargetActor(1,new(0,0,5),1,true,true,.9f);
        var injured=new TargetActor(2,new(0,0,6),1,true,true,.2f);
        var distant=new TargetActor(3,new(0,0,100),1,true,true,.1f);
        ImmutableArray<TargetActor> party=[tank,injured,distant];
        var heal=new TargetAction(30,0);
        var policy=new TargetPolicy(true,false,false,true,false);
        TargetDecision Choose(ImmutableArray<TargetOverride> overrides) => TargetSelection.Select(Vector3.Zero,1,heal,policy,party,party,overrides);
        Check(Choose([]).Recipient==2,"Automatic healing must rank eligible injured party members");
        Check(Choose([new(TargetSource.PartyMouseover,1),new(TargetSource.Focus,2)]).Recipient==1,"Deliberate recipient wins before automatic ranking");
        Check(Choose([new(TargetSource.PartyMouseover,3)]).Recipient is null,"Invalid deliberate recipient must hold");
        Check(TargetSelection.Select(Vector3.Zero,1,heal,policy with{FallbackOnInvalidOverride=true},party,party,[new(TargetSource.PartyMouseover,3)]).Recipient==2,"Explicit fallback must be honored");
        Check(TargetSelection.Select(Vector3.Zero,1,heal,policy with{FallbackOnInvalidOverride=true},party,party,[new(TargetSource.Explicit,3)]).Recipient is null,"Explicit command recipient cannot silently change");
        Check(TargetSelection.Select(Vector3.Zero,1,heal,policy with{Automatic=false},party,party,[]).Recipient is null,"Automatic ally selection can be disabled");
        Check(Choose([]).Affected.SequenceEqual(new ulong[]{2}),"Single target must not affect colocated allies");
        var pack=party.SetItem(2,distant with{Position=new(0,0,7),MayAffect=false});
        Check(TargetSelection.Select(Vector3.Zero,1,new(30,5,EffectShape.Circle),policy with{AvoidUnengaged=true},pack,pack,[]).Recipient is null,"Coverage must reject collateral unengaged targets");
        Check(TargetSelection.Covers(Vector3.Zero,new(0,0,10),new(25,25,EffectShape.Line),injured),"Line should include forward target");
        Check(!TargetSelection.Covers(Vector3.Zero,new(0,0,10),new(25,25,EffectShape.Line),injured with{Position=new(8,0,6)}),"Line width must be respected");
        Check(!TargetSelection.Covers(Vector3.Zero,new(0,0,10),new(25,25,EffectShape.Cone),injured with{Position=new(0,0,-6)}),"Cone must reject behind caster");
        Check(!TargetSelection.Covers(Vector3.Zero,Vector3.Zero,new(0,20,EffectShape.Donut,InnerRadius:8),tank),"Donut hole must be respected");
        Check(!TargetSelection.Covers(Vector3.Zero,Vector3.Zero,new(0,20,EffectShape.Circle),tank with{Position=new(0,100,0)}),"Different floors must not inflate area coverage");
        Check(!TargetSelection.Covers(Vector3.Zero,Vector3.Zero,new(0,20,EffectShape.Circle),tank with{Position=new(float.NaN,0,0)}),"Invalid actor positions must not count as affected");
        var input=new PreparationInput(10,1,20,true,true,true,true,true,false,true,3,5,100,1);
        var proposal=PreparationPolicy.Select(input)!;
        var sequence=new PreparationSequence();
        for(int i=0;i<100;i++)Check(PreparationPolicy.Select(input)==proposal && sequence.Current(100,1) is null,"Repeated previews must not commit a sequence");
        sequence.Press(proposal);
        Check(sequence.Current(101,1) is null,"Press alone is not acceptance");
        sequence.Confirm(99,102,1,false);
        Check(sequence.Current(102,1) is null,"Unrelated action must not accept preparation");
        sequence.Confirm(20,103,1,false);
        Check(sequence.Current(104,1)?.Recipient==1,"Confirmed preparation must retain intended recipient");
        Check(sequence.Current(104,2) is null,"Generation changes invalidate commitments");
        Check(sequence.Current(10103,1) is null,"Preparation must expire");
        sequence.Confirm(10,105,1,true);
        Check(sequence.Current(106,1) is null,"Confirmed follow-up consumes commitment");
        Check(PreparationPolicy.Select(input with{RecipientValid=false}) is null,"Invalid recipient cancels preparation");
        Check(PreparationPolicy.Select(input with{FollowupPermitted=false}) is null,"Excluded follow-up cannot prepare");
        Check(PreparationPolicy.Select(input with{DeadlineSeconds=2}) is null,"Insufficient lead time cannot start preparation");
        Check(PreparationPolicy.Select(input with{Prepared=true})?.Action==10,"Existing preparation selects follow-up");
        Check(PreparationPolicy.Select(input with{Needed=false}) is null,"Co-healer coverage cancels redundant preparation");
        Check(EncounterForecast.Normalize(float.MaxValue) is null && EncounterForecast.Normalize(float.NaN) is null && EncounterForecast.Normalize(-1) is null,"Invalid forecast must remain unknown");
        var forecast=new EncounterForecast(1000,1,"boss",5,null,null,null);
        Check(forecast.Remaining(forecast.Raidwide,1500,1)==4.5f,"Forecast counts down with monotonic time");
        Check(forecast.Remaining(forecast.Raidwide,3000,1) is null && forecast.Remaining(5,1500,2) is null,"Stale or foreign forecasts cannot drive actions");
        var drg = new DrgPreparationInput(25771,25771,36954,36955,25772,25770,true,true,true,true,false,true,false,true,false);
        Check(DrgPreparation.LifeSurge(drg),"Life Surge should prepare a planned Heavens Thrust");
        Check(!DrgPreparation.LifeSurge(drg with { PlannedGcd=75 }),"Last action must not substitute for actual next hit");
        Check(!DrgPreparation.LifeSurge(drg with { InRange=false }),"Target loss must not waste Life Surge");
        Check(!DrgPreparation.LifeSurge(drg with { AlreadyPrepared=true }),"Existing Life Surge must not be overwritten");
        Check(DrgPreparation.LifeSurge(drg with { Aoe=true,PlannedGcd=16477,Buffed=true }),"AoE finisher must retain buffed preparation");
        Check(!DrgPreparation.LifeSurge(drg with { Aoe=true,PlannedGcd=16477,Buffed=false }),"AoE hold must remain explicit");
        Check(DrgPreparation.LifeSurge(drg with { FullThrustLearned=false,PlannedGcd=36954 }),"Level-sync fallback must use the available combo step");
        var budget = new PreventionBudget();
        budget.Observe("raidwide",true,1000,1,0,5);
        var firstWindow = budget.Available("raidwide")!;
        budget.Observe("raidwide",true,1250,1,0,4.75f);
        Check(budget.Available("raidwide")==firstWindow,"Countdown polling must not fabricate a second damage event");
        for(int i=0;i<10;i++)Check(budget.Available("raidwide") is not null,"Previewing must not spend prevention budget");
        budget.Confirm(firstWindow.Id);
        Check(budget.Available("raidwide") is null,"Confirmed prevention must prevent another independent cooldown for the same event");
        budget.Observe("raidwide",true,2000,1,0,12);
        Check(budget.Available("raidwide") is not null,"A distinct forecast may receive a new prevention budget");
        budget.Observe("tankbuster",true,2000,1,1,null);
        var buster=budget.Available("tankbuster")!;
        budget.Confirm(buster.Id);
        budget.Observe("tankbuster",true,2100,1,2,null);
        Check(budget.Available("tankbuster")?.Recipient==2,"Changed victim must invalidate the old event context");
        budget.Observe("raidwide",false,2200,1,0,null);
        Check(budget.Available("raidwide") is null,"Ended damage window must not remain available");
        budget.Reset();
        Check(budget.Available("tankbuster") is null,"Context reset must discard prevention state");
        Check(RetargetDelivery.Select(1,2,true,true,true)==2,"Dequeuing must retain the committed recipient even after hover changes");
        Check(RetargetDelivery.Select(1,2,false,false,true)==2,"A protected existing queue must retain its recipient");
        Check(RetargetDelivery.Select(1,2,false,true,true)==1,"An explicit queue replacement must use the newly validated recipient");
        Check(RetargetDelivery.Select(1,0,false,true,true)==1,"New queue must capture the validated recipient");
        var targetInput = new TargetSelectionInput(Vector3.Zero,1,heal,policy,party,party,[new(TargetSource.Focus,1)]);
        var capture = new Newtonsoft.Json.Linq.JObject { ["DecisionSamples"] = new Newtonsoft.Json.Linq.JArray(
            new Newtonsoft.Json.Linq.JObject { ["Provider"]="DRG.LifeSurge",["Input"]=Newtonsoft.Json.Linq.JToken.FromObject(drg),["Output"]=DrgPreparation.LifeSurge(drg) },
            new Newtonsoft.Json.Linq.JObject { ["Provider"]="Preparation",["Input"]=Newtonsoft.Json.Linq.JToken.FromObject(input),["Output"]=Newtonsoft.Json.Linq.JToken.FromObject(PreparationPolicy.Select(input)!) },
            new Newtonsoft.Json.Linq.JObject { ["Provider"]="Target",["Input"]=Newtonsoft.Json.Linq.JToken.FromObject(targetInput),["Output"]=Newtonsoft.Json.Linq.JToken.FromObject(TargetSelection.Select(targetInput)) }) };
        Check(DecisionReplay.Run(Newtonsoft.Json.Linq.JObject.Parse(capture.ToString()))==3,"Exported immutable decisions must replay through JSON without live selectors");
        capture["DecisionSamples"]![0]!["Output"] = false;
        bool mismatch=false;
        try { DecisionReplay.Run(capture); } catch(InvalidDataException) { mismatch=true; }
        Check(mismatch,"Replay must detect a changed recorded result");
        return checks;
    }
}
