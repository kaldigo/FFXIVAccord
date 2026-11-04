using Newtonsoft.Json.Linq;
using WrathCombo.Engine.Selection;

internal static class DecisionReplay
{
    internal static int Run(JObject report)
    {
        int count = 0;
        foreach (var sample in report["DecisionSamples"] as JArray ?? throw new InvalidDataException("No replayable decision samples in this report."))
        {
            var input = sample["Input"] ?? throw new InvalidDataException("Missing decision input.");
            object? output = sample.Value<string>("Provider") switch
            {
                "DRG.LifeSurge" => DrgPreparation.LifeSurge(input.ToObject<DrgPreparationInput>()!),
                "MCH.Reassemble" => MchPreparation.Reassemble(input.ToObject<MchPreparationInput>()!),
                "PLD.Burst" => PldBurst.Start(input.ToObject<PldBurstInput>()!),
                "WAR.BurstDuration" => ResourceWindow.CanFinish(input.ToObject<BurstDurationInput>()!),
                "Preparation" => PreparationPolicy.Select(input.ToObject<PreparationInput>()!),
                "Target" => TargetSelection.Select(input.ToObject<TargetSelectionInput>()!),
                var unknown => throw new InvalidDataException("Unknown decision provider: " + unknown),
            };
            var actual = output is null ? JValue.CreateNull() : JToken.FromObject(output);
            if (!JToken.DeepEquals(actual, sample["Output"])) throw new InvalidDataException($"Decision sample {count + 1} differs: expected {sample["Output"]}; replayed {actual}");
            count++;
        }
        if (count == 0) throw new InvalidDataException("The capture contains no decision samples.");
        return count;
    }
}
