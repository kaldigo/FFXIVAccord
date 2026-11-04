# Accord RSR intent planning

Implementation update: see [implementation status](implementation-status.md) for the authorised first delivery on testing branches. Earlier authorisation wording below is historical for Stage 0.

Working plan and notes, started 2 November 2026. This document records agreed design decisions and remaining questions. Implementation is not yet authorised by this document.

## Current direction

The pre-implementation review now also covers [all 21 PvP jobs and compact-bar proposals](pvp-comparison.md), plus [BossModReborn forecasts and cooldown plans for Wrath](bossmod-encounter-integration.md). These add implementation candidates and validation requirements; no runtime work is started by these notes.

Targeting clarification, 4 November: adopt the RSR targeting approach wholesale, exposing retargeting settings and overrides through party-list mouseover, world mouseover and focus targets. See [targeting and options](targeting-and-options.md). This applies to the targeting foundation of the improved Wrath selector; the independent RSR automation plan below remains in force.

Updated 4 November 2026: retain the existing Wrath-based Accord controls. Wrath continues resolving the hotbar buttons. Switching to RSR automation disables Wrath's automatic execution and enables RSR's independent rotation. Bar assignments, displayed suggestions and Wrath intent selection do not supply RSR's rotation decisions. Actual manual actions naturally change the combat state RSR observes.

Investigate hook/interception coexistence, reliable exclusive automatic execution, external automation requests and manual presses during RSR sequences. Wrath controls remain usable during RSR automation; do not disable them as a design shortcut. RSR retains its original identity. Its automation permissions need explicit configuration; do not assume Wrath toggles already govern RSR. Improving Wrath's rotation logic is a separate, evidence-led workstream informed by comparison with RSR.

This supersedes the proposed replacement of Wrath hotbar resolution with custom RSR intent resolvers as the active implementation direction. Earlier resolver investigations and proposals below are retained as research history, not current implementation requirements. Stable/test publishing remains independent; implementation is not authorised by recording this decision.

See [rotation comparison](rotation-comparison.md) for the first detailed source comparison and recommended improvements.

The standard PvE job review is completed by [remaining jobs and final disposition](remaining-job-comparison.md). The [Wrath improvement plan](wrath-improvement-plan.md) proposes a substantial staged replacement of the suggestion pipeline, including large job-block replacements where justified. The user explicitly allows that scope for planning; implementation has not started.

The agreed shared intents are Damage, Healing, Protection, Empowerment, Movement and Utility. They need not match RSR's existing internal selector categories. Exact controls, resolver scopes and job mappings remain to be decided.

## Agreed button and execution model

Clarified 4 November 2026.

### Dedicated custom intent buttons

Use dedicated custom hotbar buttons in the same user-facing manner as Wrath's custom buttons. Each is its own hotbar assignment. It must not take over a native action button's assignment or change the meaning of that native button.

Each custom button expresses an intent. Its implementation can be a direct action, a simple resolver or a fuller RSR-derived selector, according to the function. The custom button may display the selected action while retaining its own identity on the bar. Native actions can coexist on the same layout for deliberate access.

### Native manual buttons

Keep native action buttons available. Their press handling can be configured for manual passthrough or interception. Passthrough attempts the native action on the press; interception requests scheduled execution through RSR. Native readiness and animation locks still apply.

This preference must be distinct from whether an action is allowed in automatic selection. Superbolide is the explicit example requiring deliberate immediate access. Existing RSR interception preferences are action-based; whether duplicate copies of an action can have different per-slot policies remains an implementation question.

### Automatic rotation and damage presses

| Automatic rotation | Damage controls |
|---|---|
| Off | Pressing the custom ST/AoE or joined rotation button requests the next eligible action under that button's policy. The rotation does not advance unattended. |
| On | The runner advances the rotation without requiring damage-button presses, using the same inclusion settings and job rules. Native manual buttons and explicit requests remain available. |

Automatic rotation is a separate master choice from the inclusion toggles below. Its targeting policy and the runner's ST/AoE selection policy in split-button layouts still need definition. Keeping two buttons does not itself decide how the unattended runner selects between ST and AoE.

### Inclusion toggles

Provide a setting for an intent's functions to be included in the rotation buttons or kept for deliberate use. Inclusion controls eligibility; it does not mean an eligible action must be selected ahead of everything else. The automatic runner applies the same inclusion policy when advancing the rotation. This supersedes treating the old support/class-actions/mitigation/burst quartet as the fixed new settings model.

With automatic rotation off, enabling an inclusion toggle allows the relevant action to be selected when a rotation button is pressed. It does not by itself authorise an independent background support/mitigation runner. This clarifies the earlier play-style discussion about automatic assistance alongside manually pressed damage.

Turning inclusion off must retain deliberate access through the appropriate custom intent or native action controls. Exact action ownership, sequence initiation versus continuation, resource reserves and per-job mappings remain to be specified; the old Wrath mappings are not automatically adopted.

Broad classification does not require every control in it to have one indivisible inclusion choice. Resurrection must have the requested option to be incorporated in rotation buttons while retaining its own deliberate control. Do not infer permission to automate other Utility actions such as Shirk merely from enabling resurrection. Further setting hierarchy and granularity remain to be specified.

### Damage layout choice

Retain the setting for either separate ST and AoE rotation buttons or one joined rotation button. This layout choice is independent of automatic rotation and the inclusion toggles.

## ST and AoE controls

Provide a setting to choose between separate ST/AoE damage buttons and one joined damage button.

| Layout choice | Intended distinction | Questions to resolve |
|---|---|---|
| Split | Evaluate the rotation for the best current ST choice and the best current AoE choice, supplying each button its own result | How should each job's resolver express the preference without changing actual world state? |
| Joined | One control covers ST and AoE | When does selection switch between them, and how are changes communicated? |

Both evaluations retain access to the full action set. Neither button excludes actions merely because they are usually associated with the other path. ST may choose a splash attack, and AoE may choose a single-target cooldown or follow-up when that is the best choice at that moment. The two buttons can legitimately show the same action.

The selected direction is a pair of custom resolvers, ST and AoE, derived from RSR's original resolver. Share action eligibility, targeting and job knowledge where possible; give each resolver its own selection priorities. This supersedes using a fake enemy count as the primary mechanism. Real enemy positions, affected-target geometry, range, resources, cooldowns and recipient validity must remain accurate.

RSR's existing combined selection uses job priorities, action-specific target thresholds and actual hit geometry, rather than only a single count of nearby enemies. The unattended runner should use that real encounter context to choose its next action, independently of whether the displayed layout is split or joined. Explicit ST/AoE button evaluations apply their respective preference to that same real encounter.

Both evaluations must leave persistent settings, queued requests and sequence state unchanged. Capture each result independently because a second evaluation can mutate shared action objects. Revalidate the chosen result on its actual button press.

The setting's scope is undecided: global, per-job, or a global preference with job overrides. Layout placement and what changes when the setting is switched are also undecided.

## Resolver investigation findings

Investigated 4 November 2026 against fork commit `156de237f5e4231d93558c8e6fba6ee337e7b902`, based on upstream `ec34ec9433fd04012fb5fa059db8b775c932161f`. Findings are source-traced; no runtime prototype or gameplay benchmark was performed.

### Where native ST and AoE selection happens

RSR is a first-eligible priority system, not a generic potency optimiser that can return the two highest-scoring actions. `CustomRotation.TryInvoke` returns a next action and a next GCD. `Invoke` selects the GCD first and then considers abilities in relation to that GCD. Those two outputs are not ST and AoE alternatives.

ST/AoE decisions are distributed across several layers:

| Layer | Evidence | Consequence |
|---|---|---|
| Action-specific thresholds | Paladin Total Eclipse and Holy Circle default to `AoeCount = 2`; Confiteor and several splash follow-ups default to 1 | One global enemy threshold cannot model all actions. Splash shape alone does not determine the preferred resolver. |
| Actual hit geometry | `ActionTargetInfo.GetMostCanTargetObjects` counts affected enemies per candidate target, using circle/cone/line/donut checks, range and hitboxes | ST/AoE preference must not fabricate targets, range or legal affected enemies. |
| Native AoE policy | Off rejects offensive area actions; Cleave rejects paths requiring more than one target; Full permits threshold-based selection | Switching Off/Full or Cleave/Full gives different restrictions, not the required pair of best-choice resolvers. Cleave may help compare native behaviour during research, but is not the finished ST resolver. |
| Job priorities | Paladin considers area-combo candidates before its basic ST combo; other important cooldowns and procs appear earlier | Raising or lowering a threshold changes eligibility but does not automatically produce the right priority order for both goals. |
| Direct job branches | Samurai branches on enemy counts for AoE and finishers; Dark Knight checks counts for Abyssal Drain and early rotation; Paladin has low-level count branches | A shared target-check override alone cannot cover every job. Each job's direct selection branches need review. |
| Indirect job branches | Ninja tests whether Death Blossom/Hakke can be selected to decide which Ninjutsu to aim for | A change to action-selection eligibility can change a later sequence, not merely the displayed attack. |

There is no native call that supplies independent ST/AoE damage results. Nor is there a single enemy-count field to spoof: DataCenter range-count properties recompute counts from the real hostile list, and action geometry calculates separate affected-target counts.

### Agreed resolver architecture

Use intent-specific action selection based on RSR where needed. The ST/AoE pair needs custom resolvers; other controls may use a direct action or a simple resolver rather than a full rotation-style selector. Existing RSR category methods are reusable components, not necessarily complete implementations of our intent definitions.

- **ST resolver:** selects the best current action for the ST damage intent, retaining useful splash actions, cooldowns and sequence follow-ups.
- **AoE resolver:** selects the best current action for the AoE damage intent using actual eligible enemies and geometry, retaining useful ST actions, cooldowns and follow-ups. It need not force a multi-target action when that is a worse choice or unavailable.
- **Mitigation/support resolvers:** adapt native defence/healing/utility routines to the eventual intent definitions, targets and resource policies. Keep explicit player requests distinct from automatic inclusion conditions.
- **Other intent resolvers:** follow the same model as their boundaries are agreed. The exact resolver/button inventory remains open.

Share native action availability, target validity, geometry, cooldown, resource, buff and combo checks. Adapt job-specific preference and ordering where needed. Maintaining full copies of every original rotation for every intent is not required by this decision and would increase upstream maintenance costs. The concrete code boundaries remain to be designed.

“Best” means the reviewed job priority for the requested intent; native RSR does not provide a general damage-score comparator. A new mathematical optimiser is not implied. The resolvers can legitimately agree on one action, and neither ST nor AoE should exclude an action simply because of its usual category.

### Why two calls to the existing resolver are insufficient

Selection is not currently free of side effects:

- `BaseAction.CanUse` writes `Target` and `PreviewTarget` into a shared action object. A later query can overwrite the first query's recipient.
- `BaseAction.Config` can initialise or synchronise stored action settings while being read. Initialise such configuration outside speculative queries.
- `DataCenter.CommandNextAction` removes expired or completed requests when read. Merely inspecting it twice is not a read-only snapshot of the queue.
- Ninja's selection updates `_ninActionAim` through `SetNinjutsu`/`ClearNinjutsu`; Dancer updates `shouldUseLastDance` while choosing attacks.
- The common GCD selector updates a downtime-healing timer and can consume random choices. `TryInvoke` also updates next-GCD tracking and diagnostics.
- `IBaseAction` overrides are shared static state. Existing cleanup resets flags rather than restoring an enclosing evaluation context.
- The existing `ActionPreview` flag relaxes casting checks; it is not a general read-only or execution-ready ST/AoE preview mode.

Creating two rotation instances would not isolate shared static action flags, queues or configuration. Likewise, temporarily editing `AoEType`/`AoeCount` and restoring them would not restore job-private sequence state or action targets. Neither is a complete production solution.

### Proposed evaluation and execution flow

1. Capture common frame inputs and non-consuming views of active requests and accepted sequence state once.
2. Query each requested intent resolver sequentially on the framework thread against that common state. Keep speculative changes local to the evaluation. Do not use the normal automatic-update publication path for every button preview.
3. Preserve native GCD/ability coordination within each damage resolver. Each result needs its own planned GCD as well as the next executable action; the ability choice may depend on that planned GCD.
4. Return an independent decision containing the requested intent, root/adjusted action ID, exact recipient or ground position, evaluation freshness, readiness and any proposed continuation information. Copy needed affected-target data instead of returning only the mutable action reference.
5. On a button press, evaluate/revalidate that intent against current conditions, apply its immediate-or-scheduled execution policy, and commit only the selected execution's state. Update accepted continuation after confirmed action effects; previews must not establish continuation. Already committed sequences must be respected by all resolvers.
6. Keep ordinary automatic selection unchanged as a reference until the custom runner policy is defined and approved. The future runner and optional next-intent suggestion need an explicit policy for choosing between resolver results; the resolver family alone does not establish that ordering.

Keep automatic/manual permissions, inclusion toggles and explicit requests separate from ST/AoE preference. Decide how stored native AoE restrictions and encounter-specific restrictions interact with explicit intent selection before implementation; do not silently overwrite settings. Scoped evaluation state must be restored on errors and must not leak across resolvers.

### Work needed before a runtime prototype

- Identify the shared selection checks and the job-specific branch adaptations for a first job. Paladin is a useful ST/AoE example with level-dependent rules and splash follow-ups.
- Separate speculative job-state changes from committed continuation. Ninja is an important early stress case, because a second damage query must not change a mudra plan already selected or begun by the player.
- Define the initial mitigation/support intents and audit their native routines for mixed recovery, protection, targeting and automatic timing conditions.
- Specify the independent decision format and execution handoff. Our current settings-only IPC cannot return or execute these decisions; new integration would be required after implementation is requested.
- Measure the cost of multiple queries and decide which previews need refreshing. No performance claim is established by this investigation.

Future verification should cover: one versus clustered/spread enemies; genuine range and line-of-sight failures; both buttons choosing the same valuable action; native ST actions remaining available on AoE; splash follow-ups remaining available on ST; resources/proc expiry; existing manual-only exclusions; explicit queued requests; accepted dance/mudra/combo continuation; target switching between preview and press; unchanged settings/queue/job state after unpressed previews; evaluation-order independence; error cleanup; and normal automatic-selection regression. Mitigation/support testing also needs the same recipient from selection through execution and immediate-request timing independent of automatic scheduling preferences.

### Source references

All links below identify the inspected fork commit.

- [Resolver entry and GCD/ability coordination](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/Basic/Rotations/CustomRotation_Invoke.cs)
- [Target thresholds, candidate targeting and actual geometry](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/Basic/Actions/ActionTargetInfo.cs)
- [Action configuration synchronisation, selection and execution](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/Basic/Actions/BaseAction.cs)
- [Shared selection overrides](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/Basic/Actions/IBaseAction.cs)
- [Enemy-count helpers and command queue](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/Basic/DataCenter.cs)
- [Paladin priorities](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/RebornRotations/Tank/PLD_Reborn.cs) and [action thresholds](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/Basic/Rotations/Basic/PaladinRotation.cs)
- [Ninja's mutable Ninjutsu aim](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/RebornRotations/Melee/NIN_Reborn.cs)
- [Dancer's selection state](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/RebornRotations/Ranged/DNC_Reborn.cs)
- [Samurai's direct count branches](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/RebornRotations/Melee/SAM_Reborn.cs)
- [Common GCD selection and downtime-healing state](https://github.com/kaldigo/RotationSolverReborn/blob/156de237f5e4231d93558c8e6fba6ee337e7b902/RotationSolver/Basic/Rotations/CustomRotation_GCD.cs)

## Intent selection in RSR

### Use the current default hotbars as the starting point

User clarification, 4 November 2026: the current default hotbar layouts are mostly right. The problem is the size and specificity of the intent classification list. Investigate consolidating classifications around the existing controls; do not substitute a newly invented minimal bar or treat fewer intent names as permission to remove useful buttons.

The stable source's ControlIntent list mixes player purposes, targeting scope, primary/auxiliary placement and direct utility controls. Current defaults in ConfiguredLayout.CreateDefaults distinguish main-bar PrimaryMechanics from auxiliary Mechanics, and main/auxiliary burst or healing-protection placements. Those layout distinctions do not necessarily require different intent categories in the redesign.

The following six shared intents are now agreed. They classify the existing controls; they do not impose a six-button layout.

| Intent | Shared purpose | Examples and retained distinctions |
|---|---|---|
| Damage | Attacks and normal rotation progression | Joined or split ST/AoE; valuable splash actions and ST follow-ups remain eligible on either path. |
| Healing | Restore health or remove harmful conditions | ST/party healing, Equilibrium and cleansing; preserve meaningful control scopes. |
| Protection | Prevent damage or protect against a mechanic | General mitigation, shields, targeted/party protection and knockback immunity; retain precise native manual controls such as Superbolide. |
| Empowerment | Start a buff, job phase or preparation enabling subsequent actions | Burst, AdditionalBurst, Mechanics, PrimaryMechanics and Preparation can classify together; exact job/action membership still needs review. |
| Movement | Deliberate repositioning or travel improvement | Gap closers, disengages, Rescue and Peloton; controls can remain distinct. |
| Utility | Remaining deliberate job/role operations | Provoke, Shirk, stance and resurrection; grouping does not authorise blanket automatic use. |

Support is no longer a shared intent name: classify its actions into Healing, Protection or Empowerment according to purpose. Primary versus auxiliary placement is layout information, not a separate intent. Existing action mappings still need review rather than automatic conversion.

Native manual actions such as stance, Shirk and invulnerability do not each require a new custom resolver merely because the old mapping enum gives them individual labels. They still need their appropriate deliberate controls and locations. Role-action groups, exclusions and miscellaneous mapping containers should also be distinguished from combat decision intents.

A broad classification may supply multiple controls. Keep classification, resolver scope, direct-action access and layout placement as separate concepts. This permits fewer categories without merging every protection action into one button or shifting the established layout. The six category names are agreed; how control scope is presented and the exact job mappings remain open.

### Resolver complexity and resurrection

Not every intent control needs a complex resolver. Use the smallest selection mechanism that fulfils the requested function:

- A fixed manual control can execute its native action directly.
- A simple resolver can select an appropriate action/target or short preparation step.
- A fuller intent resolver can select between several job actions using current combat conditions.

Resurrection is the agreed simple-resolver example, similar in purpose to Wrath's existing resurrection button. Keep a deliberate resurrection control and separately allow resurrection to be incorporated into the rotation buttons. With inclusion off, the deliberate button still works; with inclusion on, rotation presses or the automatic runner may select resurrection when its conditions permit.

Recipient selection, Swiftcast versus hardcast policy, how preparation advances on button presses, supported raising jobs and resource reserves remain to be specified. Do not import old Wrath behaviour wholesale or assume the player's manual timing should use every automatic scheduling condition.

The user also identified a possible future efficiency approach: shared combat analysis feeding the intent selectors rather than repeating all checks per resolver. Record it as a future investigation; do not design or implement the optimisation yet.

Source basis: stable FFXIVAccord/Accord.Plugin/LayoutPreferences.cs (ControlIntent), ConfiguredLayout.cs (CreateDefaults, Intent and BuildContents), and docs/agreed-layout-requirements.md. The inspected default is the current version-three global mapping; presets.json alone contains historical slot positions and is insufficient to describe the final normalized default layout.

Investigate how existing job routines, healing/defence/movement selectors and action eligibility checks can serve each intent. Add distinct selection paths only where the required behaviour needs them.

For each proposed intent, record:

- What the player is requesting and what the button is allowed to choose.
- How the recipient or enemy target is chosen.
- Which resources, charges or cooldowns should be reserved.
- Whether the press initiates a sequence, continues one, or performs a single action.
- What happens when nothing is eligible.
- Whether the same logic can be used safely for a suggestion and for execution.

Selection must keep the chosen action and its intended recipient/ground position together. RSR's action objects can be mutated by subsequent evaluations, so an icon or cached action reference alone is not a complete execution decision. Previewing alternatives must not start or accept sequences.

## Suggesting the next intent

Exploratory idea: show which intent button the player should press next when it is something other than the ST/AoE damage button.

This would recommend a player-facing function, rather than just displaying the next underlying ability. Presentation and whether the feature is optional are not decided.

Questions to investigate:

- Can RSR's ordinary next-action decision be mapped back to an intent, or is comparison between intent-specific results needed?
- If multiple intents can choose the same action, which button should be suggested?
- Should recommendations reflect the player's reserves and manually controlled responsibilities?
- How should urgency, timing and target availability affect a recommendation?
- How do we avoid suggestions flickering between buttons?
- Does the display highlight the existing button, show a separate indicator, or use another approach?
- How does this work for keyboard and controller, and for split versus joined damage buttons?
- Should the indicator disappear when ordinary damage is the recommended next step?

A suggestion is guidance only. This idea does not imply automatic execution, a fixed priority hierarchy, or changing the player's target.

## Practical integration review — proposal, 4 November 2026

This section challenges the accumulated design and proposes a coherent first implementation. It is not an agreed specification or permission to implement. Earlier requirements remain recorded; unresolved conflicts below must not be silently resolved by implementation.

### What the six categories do not solve

The six names are useful organisation, but are insufficient as executable requests or automation permissions. Protection still needs a recipient/scope; Movement cannot guess whether the player wants to approach or retreat; Utility cannot choose freely between raising, Shirk and stance. Keep concrete controls such as self protection, party protection, resurrection and approach beneath the categories. Retain the current default layout as the starting point.

One checkbox per broad category would reproduce ambiguity. Recommend concrete function settings, grouped under those six headings, with category-wide bulk changes only where appropriate. A manual-only action exclusion must apply across every automatic path, including emergency routines. Explicit native presses remain available.

### Recommended division of responsibility

Accord owns custom hotbar assignments, layout, preferences and player requests. The modified RSR owns combat selection, action/target validation, timing, accepted sequences and coordinated execution. Prefer one engine-side execution coordinator for Accord-driven play; do not have Accord cast one decision while RSR's independent runner casts another. Native passthrough presses remain direct game attempts and must be observed before the next engine decision. Reuse RSR's existing execution machinery where suitable; a second unrelated executor is not implied.

Keep RSR's identity and ordinary standalone mode. The Accord-driven path would be an explicit opt-in extension. The current settings-only bridge cannot provide it: future integration needs query, deliberate request/press and execution-mode operations. Native RSR special commands are timed priority requests, so mapping every custom button straight to a special command would fail the immediate-press requirement.

For an Accord-driven decision:

1. Identify its cause: preview, rotation-button press, deliberate function press, or unattended execution.
2. Apply the corresponding permissions and target/scope. A deliberate healing press can bypass automatic urgency thresholds without bypassing legal targets, resources or manual-only exclusions accidentally.
3. Select through RSR-derived job priorities with the relevant ST/AoE preference and allowed functions. Preserve GCD/ability coordination; do not collect six unrelated winners and execute the first non-null result.
4. For a press, attempt one selected action through the common handoff, or register an explicitly scheduled request if that control is configured that way. Preview never authorises execution.
5. Observe the actual result before committing a continuation or making another execution decision. An unavailable immediate action must not silently become a five-second request.

Do not implement full rotation copies per category. Extend common selection with context and scoped function selectors, adapting individual job branches where necessary. This still requires substantial fork work: existing selection mutates action targets and job state. Shared read-only analysis can follow later; correct isolation cannot.

### Settings need two separate questions

Proposed per-function controls:

| Setting | Meaning |
|---|---|
| Include in rotation | A rotation-button press, or full automatic rotation, may choose this function when its automatic conditions permit. |
| Allow background assistance | In press-driven play, the function may execute without a damage-button press. Only expose for reviewed assist functions; default off. |

These are independent of immediate-versus-scheduled handling on deliberate controls and native passthrough-versus-interception. Avoid exposing a generic matrix for every action initially; use useful function-level defaults plus explicit manual-only exceptions.

There is a real unresolved requirement conflict: inclusion-on-press alone does not reproduce the friend's existing background mitigation/support. If that behaviour is retained, recommend a restricted assistance mode that runs only approved healing/protection functions, without initiating damage, movement, burst or resurrection by implication. Full automatic rotation uses the normal inclusion policy; the background-assistance permission applies specifically to press-driven play. If assistance mode is omitted, document the lost behaviour rather than calling inclusion equivalent.

Recommendation: provide press-driven, press-driven with assistance, and full-rotation execution modes. These are execution choices, not three different rotation implementations. AutoDuty adds external navigation to full rotation. External mode commands must enter this same coordinator rather than accidentally enabling a competing native runner; existing AutoDuty identity compatibility alone does not prove that routing works.

### Concrete behaviour to prove

| Situation | Proposed result |
|---|---|
| GNB, rotation off, protection included, press ST | One currently eligible action: automatic protection if its reviewed priority/conditions warrant it, otherwise the ST damage decision. |
| Same setup, no press | Nothing executes unless restricted background assistance is explicitly enabled. |
| GNB presses self-protection control | Resolve eligible self protection for deliberate use; do not defer merely because an automatic routine avoids the No Mercy window. Immediate mode attempts now, scheduled mode waits according to its visible request policy. |
| GNB native Superbolide, marked manual-only and passthrough | Native attempt now; every unattended and rotation-button selection path excludes it. Normal game locks still apply. |
| Resurrection excluded from rotation | Damage presses and full auto do not initiate it; the dedicated resurrection control remains usable. |
| Resurrection included | Rotation presses/full auto may initiate it under the raise policy; damage selection must preserve a confirmed raise preparation long enough to finish or cancel it deliberately. |
| Immediate resurrection, Swiftcast available | Recommend first press attempts Swiftcast, next resurrection press attempts Raise at the retained eligible recipient. This is two actions, not an instant combined cast. No unattended second step in strictly press-driven mode. |
| Swiftcast succeeded, then player presses damage | Recommend showing/performing the pending Raise on that rotation press while the short preparation remains valid. This is continuation of an explicit request, even if routine resurrection inclusion is off. This exception needs agreement and visible indication/cancellation. |
| Full auto and a deliberate party-protection request | Common coordinator handles the request, preventing a separately selected offensive action from racing it. Immediate requests can require clipping; scheduled requests may wait for a suitable window. |

The resurrection example is simple in action selection, but not trivial in orchestration: validate the recipient after preparation, avoid spending Swiftcast on damage before the intended Raise, handle another player raising the recipient, and define expiry/cancellation. Do not hold damage indefinitely if resurrection becomes impossible. Default automatic hardcasting should be a separate reviewed choice, not an automatic consequence of inclusion.

Empowerment also needs a narrower executable meaning. Recommend function-level controls such as burst initiation or specific job preparation, grouped under Empowerment. Disabling initiation should not disable every follow-up attack after the player starts the phase. Native GNB No Mercy/Bloodfest and GCD priorities are interdependent; categorising the buttons does not by itself make the rotation tolerate withholding these actions optimally. Review and validate the altered job priorities rather than claiming upstream rotation quality is preserved.

For split damage, recommend ST/AoE preferences affect offensive selection; included healing/protection still competes under the same inclusion and urgency policy on both buttons. Both can therefore show the same support action. That is consistent with inclusion, but it means these are rotation buttons, not guaranteed attack buttons. Joined/full-auto offensive selection should start from RSR's real encounter policy. Optional next-intent guidance can come later, after deliberate and automatic decisions are coherent.

### A useful first proof, rather than a whole redesign

If implementation is later authorised, recommend one vertical slice on GNB: custom joined/ST/AoE controls, direct native passthrough, one scoped protection control, one inclusion switch, explicit manual-only Superbolide, and full-auto versus press-driven execution through the same coordinator. GNB reflects the user's actual needs; its existing burst coupling makes it a useful test of whether the fork remains manageable. Use a raising job for a separate Swiftcast/raise continuation proof and Ninja for speculative-state isolation before generalising to all jobs.

Judge this by behaviour in game: no unsolicited actions in press-driven mode; no duplicate casts/races in full auto; no Superbolide leakage; deliberate protection not blocked by automatic damage optimisation; stable target/sequence handling; plausible independent ST/AoE results. Then evaluate restricted assistance against the friend's needs. Defer PvP, recommendations, all-job mappings, migration and performance optimisation.

The main feasibility risk is preserving RSR's job coordination while changing who may initiate actions and when. This cannot be settled by the intent taxonomy or source inspection alone. A limited prototype and gameplay comparison should precede committing to RSR as the replacement foundation.

Source basis rechecked locally: CustomRotation_Invoke.cs, CustomRotation_GCD.cs (TryRaise/RaiseSpell), CustomRotation_Ability.cs (command priority, healing branches and Swiftcast), ActionQueueManager.cs, GNB_Reborn.cs and Basic/GunbreakerRotation.cs. These findings support the constraints; the proposed modes, settings and continuation policies above remain design recommendations, not existing RSR features.

## Investigation order

1. Understand and document native RSR selection and hotbar behaviour.
2. Define a first draft of the intent list and its boundaries.
3. Specify split and joined ST/AoE behaviour with concrete job examples.
4. Map each draft intent to existing RSR logic and identify actual gaps.
5. Investigate how to recommend an intent without changing selection state.
6. Review the findings before choosing implementation work.

## Play styles and control requirements

Recorded 4 November 2026. These describe the two users' needs; they do not select a final interface or authorise implementation.

| Player and situation | Desired experience |
|---|---|
| Primary user, complicated/Savage content | Manually engage and press intent buttons, with selected automation responsibilities enabled or disabled. |
| Primary user, casual content | Automatic rotation with manual movement; retain the ability to request particular actions. |
| Primary user, AutoDuty | External duty navigation and encounter movement alongside automatic rotation. |
| Other user | Manually press combat buttons with automatic mitigation/support; sometimes disable that assistance in complex content. Presses should attempt execution immediately. |

The primary user wants a per-button choice between an immediate action attempt and scheduled intent execution. Superbolide is the explicit example that must not deliberately wait for RSR's rotation scheduler. Normal game readiness and animation locks still apply. The other user prefers immediate presses throughout.

Split ST/AoE controls are required for the other user. The primary user is comfortable with joined damage selection if its decisions are good. These preferences must coexist rather than forcing one user's play style on the other.

PvP needs its own investigation. The primary user wants a smaller, simpler bar than the present Accord setup. No button count or list of actions to automate has been agreed.

### Native support and custom work

| Capability | Source finding |
|---|---|
| Direct presses of native actions | Native: per-action interception can be disabled. Excluding an action from normal rotation selection also prevents ordinary automatic use, including CanUse-based emergency branches. |
| Scheduled requests | Native: interception and explicit action requests feed RSR's command queue. Native interception is PvE only; ordinary intercepted presses do not capture the original recipient. |
| Per-button immediate/scheduled intent behaviour | Requires Accord button handling and selection context. Native interception preference is per underlying action, not per custom intent button. |
| Joined automatic ST/AoE choice | Native priority lists, per-action thresholds and AoE policy provide a starting point; decisions vary by job. |
| Separate press-driven ST/AoE selectors | Custom: native next-action selection produces one shared damage decision, not two parallel button results. |
| Full automatic rotation with manual movement | Native RSR operation, configurable targeting, healing/defence and action exclusions. |
| Press-driven damage with independent automatic support/protection | Requires a deliberate execution policy. Native RSR's normal runner includes damage; Teaching mode with rotation Off does not independently execute support. Disabling every damage action is not an established solution. |
| BossMod movement alongside RSR | External integration. The fork has BossModReborn movement/cast-safety and timeline/planned-action readers; RSR itself is not a general duty pathfinder. Exact installed BossMod variant and encounter coverage need verification. |
| AutoDuty | Native integration exists in upstream AutoDuty: it recognises RotationSolver and calls RSR's AutoDuty operating-mode endpoints. Both sides retain the original identity/contracts. End-to-end testing is still needed. |
| PvP rotations | Native: dedicated job rotations and common recovery/Guard/Purify handling exist. This does not itself provide compressed press-driven Accord hotbars. |
| PvP intent/hotbar integration | Custom: existing public RSR operating-mode, special-state and action-command IPC methods have PvP guards; PvE interception cannot be assumed to work there. |

Broad settings do not establish complete action ownership. For example, Gunbreaker has an emergency Superbolide path separate from its ordinary defence selector. Manual-only invulnerability needs an action-level exclusion as well as interception policy. Existing job conditions may also defer defence during damage windows, so immediate intent requests must not blindly inherit all automatic timing restrictions.

The agreed button model defines damage triggering and inclusion in the rotation buttons. The six-intent classification and per-intent/control inclusion direction supersede the earlier fixed four-category settings proposal. Exact action mappings, settings scope, movement integration and optional profiles remain open. Earlier references to automatic support alongside manual damage should be read in light of the clarification: inclusion on a rotation press is required; independent background execution is not established by those settings.

Sources inspected: fork baseline ec34ec9433fd04012fb5fa059db8b775c932161f; upstream RSR a560469db6f67d26cedee4373b38a0af7f3b3944 was checked for intervening changes on 4 November 2026 without merging it. Relevant files include ActionConfig.cs, ActionQueueManager.cs, StateUpdater.cs, CustomRotation_Invoke.cs, GunbreakerRotation.cs, IPCProvider.cs, BossModUpdater.cs, and RebornRotations/PVPRotations. Upstream AutoDuty's IPCSubscriber.cs confirms the RSR integration. These are source findings, not completed gameplay validation.

## Decisions and notes

| Date | Status | Note |
|---|---|---|
| 2 November 2026 | Direction | Define intents and investigate RSR selection paths for the best current action for each intent. Exact paths remain undecided. |
| 2 November 2026 | Requirement | Offer split or joined ST/AoE controls. Setting scope and detailed selection behaviour remain open. |
| 2 November 2026 | Exploring | Suggest a non-damage intent button when it should be pressed next. No presentation or recommendation policy selected. |
| 4 November 2026 | Requirement | Dedicated custom intent buttons coexist with native action buttons. Native controls offer passthrough or interception. |
| 4 November 2026 | Requirement | Automatic rotation controls whether damage-button presses are needed to advance the rotation. Inclusion toggles apply to the same selection policy in either mode. |
| 4 November 2026 | Superseded | The initial fixed support/class-actions/mitigation/burst inclusion quartet is replaced by inclusion settings for the new intents/controls. |
| 4 November 2026 | Direction | Build ST and AoE as custom resolvers derived from native RSR, keeping the full action set available to both. Apply that resolver model to mitigation, support and other intents as their definitions are agreed. |

### Intent inventory

The six intent categories above are agreed. Exact buttons and job mappings are to be drafted; old Wrath action mappings are not inherited automatically. No fixed button count is agreed.

### Job examples and source findings

#### Native RSR action exclusions

RSR has per-action Enabled and interception controls in its Actions settings. Disabling an action excludes it from ordinary CanUse-based rotation selection, without disabling the native game action. With interception enabled for that action, a manual press can queue an explicit request; the command-selection path temporarily permits disabled actions. With interception disabled for that action, the native press passes through to the game.

This is action-level exclusion, not an intent-specific permission. Ordinary healing/defence selectors also use CanUse, so a disabled action is not automatically available to a future manual-intent selector. Investigate a distinction between routine selection and explicitly requested intent selection. Also check shared cooldowns, upgraded action IDs and follow-up eligibility job by job; disabling an initiator does not prove that its dependent scheduling remains suitable.

Source basis: upstream RSR ec34ec9433fd04012fb5fa059db8b775c932161f, ActionConfig.cs, ActionBasicInfo.cs, ActionQueueManager.cs and CustomRotation_GCD.cs/CustomRotation_Ability.cs. Source-traced; native gameplay verification remains pending. No settings or engine behaviour changed for this finding.

### Additional notes

Add observations, alternatives and decisions here. Mark proposals separately from agreed requirements.

## Boundaries

Stable Accord remains independently updateable. Migration is deferred. RSR retains its original identity and behaviour; its current runtime addition is settings access only. This plan does not itself change that implementation scope.
