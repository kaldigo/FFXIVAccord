# Common Global mapping correction

Reviewed 2026-10-28. This supersedes the class-template design shipped in 0.2.15.5.

## Intended model and implementation

- Global contains one editable ordered intent mapping for all 21 combat classes. It includes resurrection, healing, class mechanics and all utility intents. Unsupported content disappears within an area without leaving gaps.
- Global has no class selector. Preview is a separate, read-only page with its own class selection and an option to include explicit overrides.
- Mapping overrides has an explicit Add/list/edit/remove flow. Only one override is edited at a time. New configurations contain no overrides. Apply is per setting, with Mapping treated as one complete setting.
- Global edits continue to reach a role/class unless that setting has Apply enabled. Personal-group role/class contents remain separate reusable definitions.
- The runtime resolves only the common mapping or an explicitly applied replacement. There is no per-class-template fallback.
- All four automation categories are off for capacity validation, after inheritance, availability filtering and priority deduplication. Split and unsplit mechanics are both checked.

## Historical layout audit

The audit covers `SetupPack.Resolve`, including controls appended by its variant logic, rather than only the original preset JSON.

| Historical difference | Common rule / decision |
| --- | --- |
| Healers use the damage-adjacent slots for single/area healing; other jobs put protection/burst there | Bar 1 order: ST, AoE, single healing, area healing, protection, burst. Unsupported groups collapse. |
| Healer movement was at the far end of bar 2 | All dedicated movement starts at bar 2's left edge. The healer exception is removed. Dragoon's two movement controls stay together. |
| Tanks have stance, while healers and raising casters have a resurrection selector | Both intents occupy bar 2 slot 12; they are mutually exclusive across supported jobs. Shirk occupies slot 11. |
| Summoner Astral Flow occupied a movement position, but changes function by summon, including Rekindle | Treat it as class mechanics. Sprint is independently available to every class. |
| Dancer Technical Step was placed with burst | Keep it with class mechanics, alongside Standard Step. Split mode still supplies both direct controls. |
| Targeted protection, personal shields, party protection, Rescue and preparation tools varied by old slot | Give these explicit intents instead of deriving their placement from class-specific coordinates. The existing primary Protection selector remains distinct from additional protection utilities. |
| Tank/healer controls are appended or swapped by variant resolution | Classify the complete resolved controls by function: e.g. Bulwark/Holos as protection, Oblation/Deployment Tactics as targeted protection, Equilibrium as recovery and Aurora as support. |
| Useful role actions were missing from some reviewed base layouts | Supply applicable interrupts, Esuna, recovery and knockback controls. Important role groups win duplicates by priority. Native class/level availability still filters them. |

No default role or class mapping override is needed. All original all-off controls remain represented. Controller, gatherer and PvP layouts retain their existing paths.

## Upgrade behavior

Before changing an existing layout configuration, Accord saves the original settings to a uniquely named `layout-before-global-*.json` file in its configuration directory.

- Old Global class templates become the common intent mapping; they do not generate overrides.
- An already-authored common Global mapping is preserved, including an intentionally empty initialized mapping.
- Explicit class template overrides are converted to concrete entries for that class. Explicit role template mappings are normalized to the common default; their former templates remain in the backup. Other applied settings remain intact.
- Empty records from the previous automatic scope creation are pruned during migration. Explicit empty overrides added after migration persist.
- Migration is versioned and does not repeat on each launch.

## Verification

- Paired client/engine build passes, with 247,031 assertions across the existing 512 PvE and 336 PvP combinations and the new common-mapping checks. Package vulnerability lookup emitted network warnings; compilation and tests completed successfully.
- New coverage checks all 21 combat jobs, split/unsplit mechanics, all 16 automation combinations, complete original control coverage, no unclassified original controls, common healer movement, resurrection position, and one Global movement edit reaching every applicable class without overrides.
- Migration, serialization, explicit empty override persistence, and conversion of an existing class template are checked. Existing Bard, inheritance, priority, exclusion, rollback and controller tests remain passing.
- Installed game data resolves all 332 original preset assignments plus the split and added role controls. Three packaging tests pass.
- Source review confirms class selection is used only by Preview, legacy templates only by migration, and saving no longer prunes explicitly added empty overrides.

## Native acceptance remains open

Compilation and simulated calls do not establish visual or in-game correctness. Verify the new pages at normal display scale, a Global edit on Bard and a healer, explicit role/class Apply behavior, and immediate automation changes without a death/scene change. The broader native checklist remains in `layout-customization-plan.md`.
