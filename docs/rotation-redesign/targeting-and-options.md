# Targeting adoption and player options

Planning notes, 4 November 2026. Supplements the [Wrath improvement plan](wrath-improvement-plan.md). No runtime changes. Source baseline: Wrath fork `761b51ff9fec11ee9f7c71dcd3ef6279dcb31bae`; RSR fork `156de237f5e4231d93558c8e6fba6ee337e7b902`.

## Agreed direction

Adopt RSR's targeting approach wholesale as the foundation for the improved Wrath selector, with configurable retargeting and deliberate overrides through party-list mouseover, world mouseover and focus targets. This supersedes the suggestion to adopt only selected targeting ideas. It does not change the independent RSR automation handoff or authorize implementation. Wholesale adoption means carrying over the coherent selection/eligibility/geometry pipeline; its integration must preserve Accord's controls and expose targeting policy rather than hard-code RSR's automatic behaviour.

The action recipient and the visibly selected target are separate. Healing an overridden ally must be possible while keeping the boss selected. Switching the selected target is a separate permission. Override precedence and defaults below are proposals, not settled user preferences.

## What the source actually does

Paths below are relative to the workspace root. Method names identify the reviewed paths; these findings are source analysis, not reproduction of the user's intermittent failures.

| Area | Wrath | RSR | Consequence |
|---|---|---|---|
| Enemy selection | `Accord/WrathCombo/AutoRotation/AutoRotationController.cs`, `GetSingleTarget`: separate tank/DPS selectors and configurable HP/distance/tank-target modes. `ExecuteAoE` maximizes hits around candidate enemies and can ignore the manually selected target via `AoEIgnoreManual`. | `RotationSolverReborn/RotationSolver/Basic/Actions/ActionTargetInfo.cs`, `FindTarget`: eligible direct targets and affected targets are calculated for the action, then area coverage and target-type selection are applied. Manual mode restricts ordinary hostile candidates to the selected enemy/self; friendly selection remains possible. | Adopt action-specific selection, including separate candidate and affected sets. Expose manual-target restriction separately from whether automation executes actions. RSR's Manual mode is still automatic execution. |
| Heal recipient | `SimpleTarget.cs`, `GetStack`: default order is enabled UI mouseover, enabled world mouseover, friendly soft target, friendly hard target, optional in-range focus, optional in-range missing-HP ally, self. Custom stack applies different eligibility checks and falls back to self when its length is at most three. | `ActionTargetInfo.FindHealTarget`: action-filtered candidates, healing threshold when enabled, party preference, then general healing and fallback selection. Separate target types exist for tankbusters, dispel, death, Kardia, Deployment and others. | Replace fragmented selection with the RSR foundation. Add explicit override policy ahead of automatic ranking, retaining action eligibility. Do not treat lowest HP as the recipient rule for every support action. |
| Automatic override | `SimpleTarget.Stack.OneButtonHealLogic` uses `AutorotHealTarget` before the player's heal stack. `GetSingleTarget` writes it; `ActionWatching.SendActionDetour` clears it after sending an action. | Selected recipient is stored with the action's `TargetResult`. | Wrath's automatic target can outrank mouseover by construction. Clear outgoing transient targeting state during ownership changes and remove this implicit precedence from converted providers. A stale-target failure during handoff remains a test hypothesis, not a reproduced defect. |
| Retarget delivery | `Core/ActionRetargeting.cs`: shared action-keyed registrations, checked against `LastActionInvokeFor`, consumed before invoking the resolver. `Data/ActionWatching.cs`, `UseActionDetour`: validates the replacement target, can fall back to the original recipient, and gives an existing queued target precedence. Ground placement has another path. | `BaseAction.CanUse` writes `PreviewTarget` and `Target`; `BaseAction.Use` supplies the stored object ID or ground position to native execution. | Adopt action-plus-recipient results, but make Accord results independent values: RSR action objects are mutable, so repeated previews cannot safely share their target storage. |
| Visible target changes | `ExecuteST` and `ExecuteAoE` can set `Svc.Targets.Target` under hard-target options; their manual-mode conditions differ. Ordinary recipient retargeting uses the action target argument. | `Commands/RSCommands_Actions.cs`, `SwitchTargetForAction`: changes selected targets in non-manual mode under friendly/enemy rules. `SetTargetWithDelay` avoids applying a delayed switch when the player changed target meanwhile. Other acquisition paths exist in `MajorUpdater`. | Neither engine is inherently recipient-only. Audit every acquisition/switch path; one setting must govern the visible-target side effect consistently. |
| Ground actions | Wrath's use hook starts from player position, tries recipient ground position or a nearby reachable ground point, and can execute there. | `FindTargetArea` produces a placement result, and `BaseAction.Use` submits that position. | Adopt explicit placement results. Expose placement policy, and avoid silently placing a deliberate ally-area action at the player's feet when its requested placement is unavailable. |

RSR also contains policy exceptions that should remain explicit during adoption. `FindHostile` prefers configured priority hostiles; stop-mark filtering retains the original candidate set if filtering would remove everything. A stop mark is therefore not an unconditional exclusion in this path. An explicit command object target is attempted first, but a failed match falls through to ordinary target finding. These are source behaviours, not suitable defaults for a strict manual override.

### Concrete reasons Wrath can feel inconsistent

1. Default and custom healing stacks use different filtering and fallback rules; even custom stack length changes the self fallback.
2. Automatic healing selection takes precedence over the normal override stack.
3. The queued recipient can win over a newly resolved recipient. The hook also writes `changedTargetId` to the queue on an eligible retargeted press, while its locally validated target may have fallen back to the original. This deserves a focused invalid-target/queue reproduction before changing it.
4. Automatic ST and AoE paths have different target-selection and visible-target conditions. AoE may deliberately ignore manual enemy selection according to configuration.
5. Retarget registrations are shared by action aliases and expire separately from button previews. This is a collision/lifetime risk, not evidence that a particular reported failure came from a collision.

These are stronger grounds for replacing the targeting layer than merely adding another mouseover checkbox. RSR is the better structural foundation here; neither source review proves flawless runtime behaviour.

## Proposed override and retarget contract

Selection order: explicit recipient supplied by a targeted command → enabled deliberate overrides → RSR automatic selection, if permitted. Proposed override order: party-list/UI mouseover → world mouseover → selected friendly target → focus target. Let users reorder/disable sources, and configure enemy and ally policies separately. Focus is optional so a permanently focused tank does not unintentionally consume every heal.

An override changes the recipient of an eligible action; it does not bypass range, line of sight, alive/dead requirements, target type, movement safety, or manual-only action permissions. Distinguish no override from an invalid deliberate override. For an explicit action/intent press, propose holding the action and showing why when its deliberate override is invalid; offer automatic fallback as a setting. For automatic assistance without a deliberate override, continue ordinary RSR selection. A default recommendation is to apply hover overrides to deliberate presses only; applying them continuously to automation should be an explicit option to avoid healing someone merely because the cursor rests on their party row.

For damage, expose keep selected enemy / automatically acquire when absent / automatic retargeting. Scope acquisition to configured eligible enemies, with separate permission to engage a new pack. Keep hard exclusions distinct from ranking preferences. For AoE, calculate hits from the selected action's real geometry; do not infer every action's value from one nearby-enemy count. Manual target restriction still applies to split or joined damage controls.

Once a preparation action is accepted, retain the intended follow-up recipient/placement and its validity conditions through that sequence. Revalidate before execution. If the recipient dies, leaves range, receives the necessary effect from another player, or no longer needs the sequence, cancel/replan explicitly. Do not silently transfer a prepared shield or resurrection to a different ally. Capture override recipients at press/queue time rather than continually following the mouse after a press.

## Player-facing settings proposal

Use a small main panel with advanced overrides. Preserve global → role → job inheritance; keep PvP configuration separate. The six broad intent categories organize controls, not six mandatory buttons or six identical resolvers.

| Setting | Proposed choices / purpose | Scope |
|---|---|---|
| Damage buttons | Joined adaptive button or separate ST/AoE controls. Split expresses preference, not a ban on useful actions normally associated with the other mode. | Wrath buttons |
| Include support functions in damage buttons | Per-function inclusion, grouped under existing broad categories. Dedicated buttons remain usable when excluded. | Wrath buttons |
| Healing during damage | Off; healing abilities between damage casts; full healing including replacement healing GCDs. Ability-only cannot promise enough healing for every situation. | Wrath buttons; separate RSR mapping |
| Preventive healing/protection | Separate permission for shielding/preparation before damage, plus emergency recovery policy. Missing timeline information must not be presented as known incoming damage. | Each selector/runner |
| Cleanse and resurrection | Independent inclusion. Raise permission separated from hard-cast raise permission and Swiftcast reservation. | Each selector/runner |
| Target overrides | Enabled sources, ordering, invalid-override fallback, and whether overrides affect presses only or automation too. | Ally/enemy policy, optionally per function |
| Automatic retargeting | Enemy acquisition/switch policy; ally automatic recipient selection; selected-target switching as a separate option. | Each selector/runner |
| Ground placement | Manual placement, overridden recipient, or automatic placement where supported. Failure behaviour explicit. | Relevant functions |
| Manual action ownership | Exclude from automatic selection independently of native button passthrough/interception. Emergency buttons such as Superbolide can remain direct. | Per action, advanced |
| Burst | Permit initiation separately from continuing an already accepted sequence. | Each selector/runner |
| Movement and reserves | Deliberate mobility permission; reserve charges/resources for manual use where the job supports it. | Per job, advanced |
| Existing rotation preferences | Preserve combat-only, openers, opener potions, enemy eligibility/range and activity-only untargeted AoE. Reconcile the current universal AoE count with per-action decisions. | Existing Wrath settings with reviewed migration |
| Automation owner | Off / Wrath / RSR, with visible active owner and settings applicability. External AutoDuty requests use the same ownership boundary. | Accord |
| Optional guidance | Highlight a useful non-damage intent without pressing it. Lower priority than correct targeting and decisions. | Accord display |

Do not automatically mirror Wrath button preferences into RSR. The same UI can eventually edit explicitly mapped settings for both, but must show when a setting only applies to one. Current RSR IPC exposes global scalar settings, not the complete proposed policy. A shared override contract in RSR automation will require an explicit extension where native settings do not cover it; recording this does not claim the bridge already supports it.

## Implementation order and acceptance gates

Make the RSR-derived targeting layer part of the first DRG/SGE vertical slice, not a late polish task. Separate immutable observation, selection result, press-time override capture, queue commitment and execution. Carry action, actor ID, affected actors/position, source of target choice, sequence identity and freshness together. Previewing another button must not overwrite a committed recipient.

Before broad job migration, verify: party-list versus world mouseover precedence; focused tank with injured DPS; invalid/out-of-range/obstructed override; dead ally for raise versus living ally for healing; hover changes after queuing; selected boss retained through healing; ST/AoE toggling without unintended enemy switches; no permitted enemy; AoE near an unengaged pack; ground-placement failure; Eukrasia/Zoe and Recitation/Deployment recipient retention; actor despawn; and engine switching while a request is pending. Trace original target, override source, candidate rejection, selected recipient, queue recipient and final sent recipient to distinguish policy from a delivery bug.

Run a separate coexistence gate with RSR automation enabled: Wrath suggestions stay live, RSR-issued actions bypass inappropriate Wrath retargeting, manual requests retain their own recipients, and the outgoing runner's pending target/queue state cannot leak into the new owner. Existing native game queue commitments must be handled deliberately rather than assumed to disappear when a plugin stops.
