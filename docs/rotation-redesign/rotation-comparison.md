# Wrath and RSR rotation logic comparison

Completed standard PvE coverage: [remaining jobs and final disposition](remaining-job-comparison.md). Proposed delivery: [Wrath suggestion redesign](wrath-improvement-plan.md). Earlier statements below about outstanding jobs record the first review and are superseded by that completion document.

4 November 2026. Local source review: Wrath fork HEAD `761b51ff9fec11ee9f7c71dcd3ef6279dcb31bae`; RSR fork HEAD `156de237f5e4231d93558c8e6fba6ee337e7b902`. This compares checked-out implementations, not latest upstream releases or measured gameplay performance. Expanded beyond Gunbreaker to all four healers and representative melee, ranged, caster and other tank paths. Coverage and remaining limits are explicit below; this is not an exhaustive audit of every job.

## Decision and assessment

Keep Wrath resolving Accord's manual hotbar controls. RSR can independently own full automation while Wrath auto rotation is disabled. Improve Wrath where specific comparisons justify changes, without importing RSR's executor or reproducing its entire architecture.

The hypothesis that RSR is more holistic is partially supported: its common selector explicitly selects a GCD and passes it to ability selection, and its action model centralises eligibility and targeting. However, both inspected rotations are ordered rule systems. Neither inspected path performs a general forward simulation to maximise the value of a complete future rotation. Wrath's distributed helpers contain substantial rotation knowledge; RSR's cleaner interfaces still contain local heuristics and fixed timing thresholds.

## Framework differences

| Concern | Wrath | RSR | Practical implication |
|---|---|---|---|
| Selection unit | GNB Invoke returns one replacement action ID from a priority list; helpers decide readiness and timing. | CustomRotation.Invoke selects GCD(), then Ability(nextGCD), coordinating which to return with GCD readiness. | RSR makes the next GCD explicit input to weaving decisions. Wrath could adopt a small shared upcoming-GCD decision without replacing hotbar controls. |
| Eligibility | GNB helpers combine presets, unlocks, resources, range, timing and job state. | Base action settings/checks hold resources, statuses, targets and thresholds; job code orders eligible actions. | Centralising invariant eligibility can reduce disagreement between Wrath paths. RSR CanUse hides substantial rules, so comparing only job file lengths is misleading. |
| Combat context | Wrath helpers access current buffs, cooldowns, gauge, combo and enemy state. | Shared combat state plus common selection stages, job rules and action checks. | Both have context. The useful question is whether each decision consumes the context it needs consistently. |
| ST/AoE | Distinct GNB ST/AoE entry points with shared helpers and separate ordering. | Combined priority path uses per-action counts and actual target geometry. | Wrath naturally fits split controls; RSR naturally fits encounter-based automatic choice. Neither structure proves better priorities. |
| Support scheduling | GNB Invoke considers interruption/content actions/mitigation before offensive choices. | Common healing/defence stages feed job-specific methods that can inspect nextGCD. | RSR can coordinate support with upcoming damage, but automatic deferral can be undesirable for deliberate protection presses. |
| Future planning | Explicit openers and local resource/cooldown/expiry predictions. | Planned next GCD plus local charge, buff and cooldown predictions. | RSR has explicit immediate coordination, not demonstrated whole-fight planning. |

Source map: Wrath `WrathCombo/Combos/PvE/GNB/GNB.cs`, `GNB_Helper.cs`; RSR `RotationSolver/Basic/Rotations/CustomRotation_Invoke.cs`, `CustomRotation_GCD.cs`, `CustomRotation_Ability.cs`, `Basic/GunbreakerRotation.cs`, `Basic/Actions/BaseAction.cs` and `ActionTargetInfo.cs`.

## Gunbreaker: concrete decision differences

### Burst entry and ability placement

Wrath ShouldUseNoMercy checks combat, target/range, cartridges, Bloodfest cooldown state and a target-HP stop threshold. It distinguishes slower/faster skill-speed paths and uses a delayed weave for the fast path. This is coordinated burst logic, but relies on indirect evidence of readiness rather than an explicit proposed next GCD.

RSR AttackAbility receives nextGCD. At lower levels it can enable No Mercy based on whether the upcoming action is Gnashing Fang, Burst Strike or a particular combo step. At higher levels it checks the Bloodfest buff (or Bloodfest disabled); its AoE branch also uses the planned GCD. This is a concrete example of richer immediate coordination, not proof that every branch has a better burst order.

Recommendation: give Wrath's buff/weave selection access to the offensive GCD it would otherwise choose. Keep its skill-speed-specific ordering until comparative traces show a better alternative. Previewing a GCD must not advance an opener or mutate an accepted sequence.

### Cooldown ranges as burst proxies

Wrath ShouldUseZone requires No Mercy cooldown between 15 and 57.5 seconds; ShouldUseBowShock requires 40 to 57.5 seconds. ShouldUseInBurst uses JustUsed(NoMercy, 20 seconds), with escape conditions for overcap/expiry. These encode an expected burst schedule through elapsed time and cooldown position.

RSR instead checks HasNoMercy for Bow Shock, then considers AoE context or Sonic Break availability/cooldown. Blasting Zone can follow Double Down during No Mercy, or fall back when No Mercy and Bloodfest are cooling down. However, its lower-level Danger Zone logic still uses 15/20-second Gnashing Fang horizons. RSR has not eliminated timing heuristics.

Assessment: the concern about Wrath's proxies is supported here. They can be less expressive after unusual interruptions or manual intervention. This is a candidate for improvement, not a demonstrated defect in every normal rotation. A buff-status check alone is also insufficient: early/late placement and weaving opportunities still matter.

### Cartridges and expiry

Wrath predicts Bloodfest capacity loss explicitly: with six cartridges spend when three GCDs remain, with five when two remain, with four when one remains. It prepares a Burst Strike before No Mercy for a particular level/skill-speed/setup, preserves basic combo time and spends before a capped generator.

RSR similarly uses OvercappedAmmo with PlayerWillStatusEndGCD, spends before an imminent No Mercy when capped, and considers Bloodfest/No Mercy horizons plus the last combo action. This is evidence of comparable forward resource reasoning in both implementations.

Recommendation: retain Wrath's good accounting. Consolidate it into shared resource forecasts used by ST/AoE and burst decisions, then test edge cases. There is no justification here for replacing it wholesale with RSR code.

### Sequence and proc preservation

Wrath prioritises Continuation at several urgency levels, including clipping near the GCD to avoid losing it. CanGF refuses to begin Gnashing Fang with Ready to Blast pending. Gnashing filler checks remaining basic combo time and reserves a charge near No Mercy. Reign has an expiry escape condition. Openers explicitly encode sequences and weaving.

RSR action checks encode gauge steps and required statuses. GeneralGCD protects Reign continuation and coordinates Gnashing/Reign selection; common and job-specific ability handling adds further scheduling. Its explicit nextGCD boundary is useful, but the inspected code still depends on distributed eligibility and priority rules.

Recommendation: compare interrupted sequences and manual overrides before changing anything. Preserve Wrath's explicit protection against losing Continuation and its combo-expiry checks. Scripted openers demonstrate planned sequences, but do not by themselves establish recovery quality after a deviation.

### Defensive coordination

Wrath GNB runs mitigation selection before the offensive list, with its own weaving/mitigation checks. RSR GNB defence/healing methods can defer job-specific choices when nextGCD is Gnashing Fang and No Mercy is ready; party mitigation also checks that No Mercy is inactive.

This demonstrates offensive coordination in RSR, but is not unconditionally better: a deliberately requested mitigation must not be postponed simply to preserve burst. Borrow the information flow while preserving Accord's distinction between automatic assistance and explicit requests.

### Fight duration and targeting

Wrath's inspected No Mercy helper uses configurable target HP percentage as a stop condition. RSR No Mercy's action configuration declares a five-second TimeToKill threshold, with enforcement delegated to common action eligibility. Duration-based policy can better express whether a buff will pay off, but estimated remaining life is uncertain and this review has not validated estimation accuracy or every gate.

RSR's target geometry and action-specific AoE counts are stronger reusable concepts for joined automatic selection. Bringing them into Wrath should be considered separately from split controls, where the player intentionally supplies an offensive preference.

## Proposed Wrath improvements, in order

1. Capture decision traces for the same job/level/settings and comparable combat situations. Record chosen action, intended next GCD, active buffs, gauge, combo expiry, cooldowns and reason. First establish which decisions are actually inferior.
2. Replace fragile burst proxies selectively with a shared burst context: confirmed buff state, initiation permission, pending follow-ups, remaining GCD opportunities and resource requirements. Keep timings where they express real weave/deadline constraints.
3. Make the intended next offensive GCD available to ability selection. Do not turn each hotbar query into a sequence-advancing evaluation.
4. Share resource/expiry calculations and invariant eligibility across ST/AoE paths. Preserve their separate player-facing preferences.
5. Improve interruption recovery and manual-initiation handling using observed action results. Hold initiation when requested while still completing accepted sequences.
6. Evaluate target-lifetime and geometry improvements where measured examples justify the added complexity.

These are proposed changes, not implementation commitments. Maintain the current manual intent contract and native passthrough behaviour throughout.

## Validation needed before claiming parity or superiority

Compare uninterrupted combat and deviations: fast/slow skill speed, relevant level-sync boundaries, missed weave, melee downtime during burst, delayed/disabled No Mercy or Bloodfest, manual initiation, capped cartridges near expiry, nearly expired combo, pending Continuation, target death, and ST/AoE changes mid-sequence.

Measure lost procs, lost resources, dropped combos, cooldown drift, buff coverage of important attacks, avoidable clipping and overall output under comparable conditions. A different choice is not automatically a worse choice. Separate engine timing from priority quality, and avoid treating target-dummy results as proof of encounter behaviour.

The expanded review below covers those additional roles. It does not constitute an all-job ranking or runtime benchmark.

## Expanded review: pre-emptive healing and encounter awareness

The GNB-only review understated the scope of the comparison. Pre-emptive healing has at least four distinct meanings: preparing before pull, reacting before an identified attack lands, preparing a multi-action healing sequence, and reserving resources for future encounter events. Finding one does not prove all four.

### Detecting future damage

Wrath `CustomCombo/Functions/Action.cs`, GroupDamageIncoming/RaidwideCasting, detects shared-damage effects with a six-second window and currently casting enemy raidwides. Cast recognition uses action geometry (large area casts) plus curated inclusions/exclusions. Therefore Wrath can act before HP falls; it is incorrect to describe it as purely HP-reactive.

RSR `Updaters/StateUpdater.cs`, ShouldAddDefenseArea/Single, similarly responds to recognised enemy casts. Additionally, `BossModUpdater.cs` reads BossModReborn timeline and damage-hint endpoints, combines the available raidwide/tankbuster estimates, and exposes downtime and vulnerability timing. The state updater can request defence when an event lies within configured anticipation windows, before the recognised cast need be available. This depends on the compatible plugin, encounter/module data and enabled configuration; it is not universal foresight built into every rotation.

Concrete difference: Wrath's inspected shared detector primarily answers “is an attack incoming now?” RSR can also supply “how soon is the next known event?” That richer signal can support preparation and resource reservation. Merely reading a timeline does not prove every healer uses it to schedule its entire toolkit.

### Wrath's existing preparation and fragmentation

`AutoRotationController.PreEmptiveHot/Shield` handles out-of-combat preparation on the focus target, with hostile proximity and status checks; Sage can prepare Eukrasia before the shield. These are pre-pull helpers, not an in-combat prediction model.

The same controller has separate tankbuster and raidwide action lists. Tankbusters attempt the first usable listed action and mark the event handled (Eukrasia is a preparation exception). Raidwides separate repeated-hit tools, track used actions, and allow one/two/three casts based on party-average HP. These lists mix protection and recovery and call the game action executor directly.

Separately, WHM/AST/SCH/SGE rotation files have their own raidwide and healing branches. SCH can prepare Recitation before Succor; AST can initiate Neutral Sect and follow with Aspected Helios; SGE has Eukrasia/shield and Zoe/Pneuma paths. This supports the concern about fragmented orchestration more strongly than the claim that Wrath lacks advanced mechanics: similar responsibilities exist in multiple execution and selection paths, with different checks.

Do not infer that controller logic necessarily fires twice or is broken: gating and runtime evidence still matter. The maintenance risk is that changes to a job helper do not automatically update the controller's separate policy.

### All four healers

| Job | Wrath evidence | RSR evidence | Assessment for Accord |
|---|---|---|---|
| Scholar | RaidwideSuccor checks shield coverage including Sage shields; optionally returns Recitation first. Separate controller has pre-pull Adloquium and raidwide lists. Configurable healing paths include Recitation preparation. | Countdown can arrange Recitation, Adloquium on a tank and Deployment. EmergencyAbility examines nextGCD for Recitation/Emergency Tactics; party needs inform the latter. Deployment has strategy-dependent status checks. | RSR offers a clearer route for coordinating preparation with a selected heal. Wrath already has useful shield-overlap checks and preparation. Preserve these while consolidating sequence ownership. |
| Sage | Raidwide Kerachole/Holos, Eukrasia/shields, Haima/Panhaima overlap handling, and a Zoe/Pneuma custom path. Generic UseZoe tests whether Pneuma is ready or not yet learned. | Tracks an Eukrasia action aim; Zoe checks the planned Pneuma or the specific shield aim. Defence considers Addersgall, multihit settings and recent Haima/Taurochole usage. Physis II may precede Panhaima. | Explicitly associating the preparation buff with its intended spell is a strong improvement candidate. Resource-aware choices exist alongside fixed cooldown spacing, so this is still heuristic scheduling. |
| Astrologian | Raidwide Collective/Neutral Sect/Aspected Helios branches; Earthly Star placement options; healing path includes Horoscope preparation and Microcosmos release based on HP. | Countdown Star preparation; nextGCD-dependent Neutral Sect/Horoscope; Synastry checks both the intended spell and matching recipient. Mature Star and Microcosmos are prioritised in area healing; MicroPrio can suppress competing heals while Macrocosmos is active. | RSR demonstrates broader interaction between the selected heal, its recipient and preparations already active. This is more meaningful than merely having a Macrocosmos button. It does not prove optimal Star timing for each encounter. |
| White Mage | Pre-pull Regen, incoming-raidwide branches, controller multihit-only Bell, Plenary/Temperance/Caress and recovery options. | Defence distinguishes multihit Bell use, Caress and Temperance; cooldown-spacing checks avoid immediately following recent major tools. Separate healing choices include target-HP thresholds and a last-Benediction check. | Both anticipate damage. RSR's spacing and common selection framework deserve comparison, but its rules are not a full encounter allocation plan. Some are fixed cooldown heuristics similar to the style criticised in Wrath. |

Important RSR limitations found in source: SCH has a comment noting Deployment can be skipped because the defence target differs from the shielded target; this is an author-identified concern, not a reproduced bug. Its Recitation helper explicitly handles cases that nextGCD cannot represent because Excogitation and Indomitability are abilities. Thus nextGCD is helpful, but does not by itself coordinate every preparation sequence.

### Reactive healing, HoTs and pending effects

RSR StatusFromHealing uses party HP distributions, counts of members needing help, Doom cases and existing HoTs, with separate spell/ability thresholds. Its ordinary healing-status path exits when HP is full; anticipation chiefly enters through defence/countdown/job-specific paths. Do not claim all RSR healing is predictive.

RSR DataCenter.GetPartyMemberHPRatio also accounts for a tracked heal during its effect window until HP reflects it, reducing the chance of selecting another heal solely because the first effect has not yet appeared. `HealPrediction.HasLanded` checks the HP delta. This is pending-effect accounting, not proof of arbitrary future damage prediction or complete coordination with another healer.

Wrath's inspected controller uses party/target HP, optional shield inclusion, and recent-use checks. A complete equivalent pending-heal model was not established by this review. This is a high-value follow-up because it affects wasted healing more directly than cosmetic priority ordering.

### What to improve in Wrath for healers

1. Share one incoming-event description across hotbar and automatic paths: event type, expected landing time, affected recipients, repeated-hit information and confidence/source. Keep cast/effect detection as fallback; timeline data is optional.
2. Select a response appropriate to the event, not just the first ready tool. Account for existing shields/mitigation, resources, recipient coverage and whether the preparation can finish before impact.
3. Associate preparation with its intended follow-up and recipient: Recitation/shield/spread, Eukrasia/shield, Zoe/Pneuma, Neutral Sect/party spell. Advance from confirmed actions and handle invalid targets or missed deadlines.
4. Distinguish prevention from recovery and release of stored healing. Avoid consuming multiple major tools merely because several independent branches see the same incoming attack.
5. Investigate pending-heal accounting and HoT-aware thresholds before broad changes to healer priorities.

For the agreed handoff, RSR owns its own automatic healer policy. These Wrath improvements primarily benefit press-driven Accord use and any retained Wrath automation. They do not require both engines to execute support simultaneously.

## Other roles: concrete comparisons

### Machinist — encounter information changes offensive decisions

Wrath MCH_Helper.UseHyperchargeST checks combo expiry, Drill/Air Anchor/Chain Saw timing, Excavator readiness, heat, Full Metal status and Wildfire timing. It already considers the opportunity cost of entering Hypercharge rather than treating heat as a simple threshold.

RSR MCH uses nextGCD to place Reassemble before eligible tools, including affected-target counts for a low-level AoE case. More significantly, it blocks Wildfire when BossMod downtime is within ten seconds and Barrel Stabilizer within two GCDs; an experimental option spends heat before impending downtime, except during its pre-burst condition.

This is concrete evidence for the user's holistic hypothesis beyond healing: encounter context changes resource spending and cooldown initiation. However, a fifteen-second heat-dump window and ten-second Wildfire block remain heuristics. Port the event input and decision rationale; validate the thresholds rather than copying them uncritically.

### Ninja — both engines maintain sequence state

Wrath's NIN entry uses MudraCasting, continues an active mudra before ordinary actions, handles Ten Chi Jin early, blocks during a lag condition, protects mudra from content actions, and spends Ninki before generators when necessary. Suiton setup uses a Trick cooldown horizon.

RSR's Ninjutsu choice retains an explicit action aim and uses eligibility of AoE attacks to choose branches; its selection can mutate that aim. The earlier resolver investigation already traced this state. Neither is merely a set of independent cooldown buttons. Improving sequence recovery and manual deviations is a more appropriate comparison than assuming RSR supplies sequence awareness absent from Wrath.

### Dancer — timing rules appear in both

Wrath distinguishes initiation permissions from active step completion, uses anti-drift windows, Finishing Move/Last Dance readiness, and explicit pre-pull steps. RSR retains Last Dance selection state, conditions choices on Esprit and Devilment, and uses Standard/Technical elapsed-cooldown thresholds of 28/118 seconds in one priority section.

RSR's structure is not evidence that it avoids the timing-based decisions criticised in Wrath. Both coordinate procs, gauge and phases; compare lost procs, Esprit overcap and dance drift under interruptions. Preserve the fork's existing ability to withhold initiation while completing an accepted dance.

### Black Mage — movement and phase state matter in both

Wrath's main path explicitly considers Polyglot overcap, Thunder, movement alternatives and elemental progression; its Ley Lines call can require standing still. RSR BLM_Default separates elemental progression from abilities, uses Triplecast while moving, and selects instant-cast resources based on ice/fire state and some nextGCD cases. It also offers different rotation variants; results must identify which variant is compared.

Both are phase-aware. A claim that one handles movement better needs scenarios involving resources before movement begins, phase deadlines and recovery after interruption. Reacting to IsMoving is not equivalent to reserving mobility for a future mechanic. This review establishes relevant paths, not a BLM performance ranking.

### Red Mage — mana and proc relationships

Wrath preserves Scorch/Resolution follow-ups, checks mana for starting/continuing melee, and avoids starting melee while certain instant-cast buffs are active. RSR's finisher selection explicitly balances mana during Embolden or a large imbalance, otherwise considers Verfire/Verstone proc preservation; its mana-pooling option is labelled experimental.

Both consider more than cooldowns. RSR provides an explicit example worth comparing, but the exact Wrath UseHolyFlare helper needs a further branch-by-branch match before calling that a missing capability. Do not label experimental pooling as established superior optimisation.

### Warrior and Dark Knight — smaller sampled scope

RSR WAR gates Inner Release on Surging Tempest surviving two GCDs and considers Infuriate with GCD-based horizons. Wrath's sampled WAR path also checks Surging Tempest, Beast Gauge, existing Nascent Chaos/Inner Release stacks and retained Infuriate charges. These are related resource/buff safeguards expressed differently; no general winner is established.

RSR DRK includes optional low-HP ally TBN outside its two-minute burst and separate defensive priorities. Wrath DRK uses shared action selection and special-mana spending paths plus a dedicated TBN control. A full MP reservation versus burst-versus-defence comparison remains open; the limited sample does not justify rewriting either.

## Revised conclusion and coverage

The strongest demonstrated RSR advantages are common information flow, preparation tied to the selected spell/recipient, pending-heal accounting, and optional encounter-timeline inputs used in actual defensive and offensive decisions. The concern that Wrath's policy is fragmented is supported by duplicate controller/job-specific responsibilities. The claim that Wrath generally lacks rotation state is contradicted by its Ninja, Dancer, Machinist and resource-management logic.

Prioritise shared encounter awareness and healer preparation coordination alongside explicit next-GCD context. Do not make the improvement programme merely “replace hardcoded timings”: some timings encode real constraints, and RSR also uses them extensively.

Coverage: detailed healer mechanism review for WHM/SCH/AST/SGE; concrete paired paths for MCH/NIN/DNC/BLM/RDM; earlier GNB review; smaller WAR/DRK samples. BRD was located but not deeply traced. PLD, MNK, DRG, SAM, RPR, VPR, SMN, PCT and limited/PvP jobs are not exhaustively compared here. No runtime or comparative output measurements were performed.

Sources for this expansion, relative to each repository: Wrath `WrathCombo/AutoRotation/AutoRotationController.cs`, `CustomCombo/Functions/Action.cs`, and `Combos/PvE/<JOB>/<JOB>.cs` / `<JOB>_Helper.cs`; RSR `RotationSolver/Updaters/StateUpdater.cs`, `BossModUpdater.cs`, `Basic/DataCenter.cs`, `Basic/Helpers/HealPrediction.cs`, and `RebornRotations/Healer/*_Reborn.cs`, `Ranged/MCH_Reborn.cs`, `Ranged/DNC_Reborn.cs`, `Melee/NIN_Reborn.cs`, `Magical/BLM_Default.cs`, `Magical/RDM_Reborn.cs`, `Tank/WAR_Reborn.cs` and `Tank/DRK_Reborn.cs`. Findings concern the named methods and branches above; comments are not treated as runtime proof.
