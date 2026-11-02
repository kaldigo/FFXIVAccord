# Rotation behaviour and hands-free automation

Agreed 2026-11-02. Status: implementation and local verification complete; native validation pending. Publication requested; release verification pending.

## Required behaviour

- Support owns hands-free resurrection (including supported damage jobs), Esuna, Kardia management and pre-emptive healing. Protection owns preventive shielding and mitigation for raidwides/tankbusters; healing responses follow Support.
- Resolve existing global, role and class automation overrides. Preserve thresholds, target priorities, Swiftcast requirements and recipient restrictions. Keep Wrath's master auto-rotation switch independent.
- Add a compact Rotation behaviour page: enemy targeting (manual / enemies fighting party / any eligible enemy), automatic selection range, AoE enemy count, untargeted AoE in activities, combat-only operation, job openers, and damage potions in supported openers.
- Preserve existing behaviour until a new preference is explicitly configured. Use sparse role/class overrides with Apply checkboxes and live global inheritance. PvP must retain its existing behaviour.
- Untargeted AoE may select a suitable self-centred attack against even one nearby eligible enemy only in activities, never open world. Do not acquire an enemy target merely to implement this option. Retain readiness, range, exclusion, cooldown and resource checks. Target-requiring attacks must not be fired on an invented target.
- UI descriptions must distinguish automatic target acquisition from untargeted AoE, explain activity scope, and show inherited values accurately. Settings take effect without death/scene changes.
- Preserve existing hotbar mappings and manual action access. Do not rewrite personal Wrath configuration files or reset unrelated preferences.

## Implementation and verification checklist

- [x] Trace runner support/protection paths and document actual ownership.
- [x] Implement engine-owned effective settings with validated client transport.
- [x] Implement sparse global/role/class preference resolution and migration defaults.
- [x] Add simple global page and one-at-a-time override editing.
- [x] Implement activity-only untargeted AoE with open-world/PvP isolation.
- [x] Integrate opener and potion preferences without replacing opener logic.
- [x] Test all toggle combinations, override precedence/inheritance, serialization, switching classes and settings, and activity eligibility/targetless selection.
- [x] Build both plugins, run relevant compatibility and packaging checks.
- [x] Review every requirement against final changes; record gaps and required in-game checks before completion.

## Native acceptance (pending)

Check toggles and overrides while Wrath auto-rotation is enabled; support and protection independently; global edits inherited unless Apply is set; class switching; manual/party-combat/any-enemy targeting; targetless AoE against one enemy in a supported activity and no such action in open world; openers and optional potions; unchanged PvP and manual buttons.

## Implementation review

- `RotationPreferences` is a shared, validated per-job engine setting. Missing/null choices preserve Wrath configuration. Each supplied job replaces its previous complete preference set, so removing Apply clears stale values without changing other jobs. No saved rotation thresholds, recipient lists or Swiftcast policies are overwritten.
- Global preferences live on Rotation behaviour; role/class choices live in the existing one-at-a-time override editor. Each preference has its own Apply checkbox. Rotation-only overrides survive pruning. Normal role automation now resolves below class automation; server-status changes update the highest applied layer. Open-editor rebase preserves concurrent role/class automation changes.
- Wrath's effective config wrapper reads current class settings each time. PvP and unmanaged classes use their existing Wrath preferences; the master auto-rotation switch is untouched. Ordinary auto-healing rotation selection is not redesigned by this task.
- Support enables auto-rez (including RDM/SMN), auto-cleanse, Kardia management and pre-emptive WHM/AST healing. Protection enables pre-emptive SCH/SGE shields and the existing tankbuster response list. Those tankbuster responses include shields, mitigation and healing-amplification tools, rather than standalone emergency heals.
- Raidwide recovery actions follow Support: Bell, Plenary Indulgence, Asylum, Medica II/III, Seraphism, Celestial Opposition, Aspected Helios/Helios Conjunction and Physis I/II. The remaining existing raidwide entries are preventive mitigation/shields and follow Protection. Mixed healing/mitigation actions such as Holos and Kerachole retain Protection ownership. Existing sequence/readiness/recipient logic is retained.
- Automatic targeting uses nearest eligible enemies; party-combat mode additionally requires an enemy in combat targeting the player/party, or a fighting party member targeting it. A configured range also disables the larger boss-range exception. Manual targeting does not silently enable Wrath's automatic AoE retargeting.
- Activity eligibility reads the current content type directly (not a cached field-activity flag): Dungeon, Deep Dungeon, Variant, Criterion, Trial, Field Operations, Field Raid, Alliance Raid and Raid. Open world, FATEs/hunts, PvP, housing and unclassified/quest content do not enable the new untargeted AoE path.
- Untargeted AoE requires at least one nearby eligible enemy inside the action's actual circle, respects ignored enemies, target range/line of sight, cooldowns, resources, movement and action penalties, and does not create an enemy target. Target-requiring replacements fall back to the checked native AoE; classes without a usable self-centred AoE cannot use this mode. Native execution retains the checked action through the replacement hook. Ninja mudra sequences are not replaced by this path.
- Openers use the reviewed per-job opener preset gates and existing FullOpener logic. Their original level/content/readiness restrictions remain. Potion preference only changes inclusion of existing opener item steps; it does not create potion use outside those openers. Disabling openers resets an active opener on settings refresh.
- No hotbar definitions or action mappings changed. Native manual controls remain usable. Detailed Wrath settings remain stored and resume when the corresponding new preference is returned to existing Wrath behaviour.

## Verification evidence

The expanded C# suite currently passes 471,920 assertions, including all job/toggle combinations, per-class engine-schema round trips for every new preference, sparse inheritance and explicit false, override removal, class isolation, server-status role/class precedence, open-editor rebase, transport rejection of invalid settings, and all 128 untargeted AoE eligibility combinations. Geometry checks reject no-enemy, targeted-circle, cone, line and ground-placement cases. These are model/policy checks; actual combat execution, native UI rendering and live territory transitions still require in-game acceptance.

Final verification on 2026-11-02: both plugins built successfully with the current local engine source, with 471,920 C# assertions passing. Client Python suite: 3 passed. Engine Python suite: 22 passed, including the production Dancer dispatch across 480 scenarios and the production action hook against a stale cached replacement. The checked action guard precedes the icon cache and does not overwrite unrelated cached actions. Both final local archives passed package/identity/dependency/checksum validation; local source feeds were restored unchanged. Shared contracts match and both repositories passed whitespace checks.

The plan review found and corrected the missing normal role-automation resolution, stale role changes in an open editor, misleading fallback values in the override UI, Dancer's no-target dispatch boundary, and the cached-replacement bypass. There are no remaining known implementation checklist items. In-game acceptance above is explicitly outstanding. Release dependency pins now reference the matching engine implementation and client compatibility tests; both publication workflows must pass before release completion is reported. Existing deselection semantics are retained: deselecting an already configured class does not erase its installed engine settings.
