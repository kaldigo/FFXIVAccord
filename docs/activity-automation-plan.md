# Activity automation

Requested 2026-11-02. Status: implemented locally; final verification below. Publication requested; release verification pending. Not tested in game.

## Contract

- Supported Variant Dungeon, Bozja, Occult Crescent and Deep Dungeon automation follows the effective normal class automation settings. PvP overrides do not affect it.
- Direct activity attacks are included in normal rotation selection independently of all four automation toggles. Burst owns damage buffs; Protection owns mitigation, defensive buffs and enemy control; Support owns healing, resurrection, cleansing and resource recovery; Class mechanics owns activity utility such as movement, stealth and special activity systems.
- Activity parent presets permit their owned children, including direct attacks when all four toggles are off. Role-specific Variant/Bozja presets must not be globally rewritten when different classes have different overrides.
- Preserve saved thresholds, targeting preferences, timing restrictions and alternative-choice sub-options. Keep Wrath's territory, equipped/learned action, cooldown and resource checks. Do not add unsupported automation (Deep Dungeon pomanders are currently a stub).
- Native manual actions remain usable with automation off. No hotbar rearrangement or master auto-rotation toggle change.
- Classes without the activity-management capability and PvP retain their saved behavior. As with other Accord setup settings, deselecting a previously configured class retains its installed settings. Record limitations explicitly.

## Checks before completion

- [x] Review every activity preset as an action, parent or preserved policy. Automated coverage fails if an upstream activity preset lacks classification.
- [x] Implement per-class engine capabilities and client toggle mapping.
- [x] Verify all toggle combinations, class isolation, PvP isolation and preserved policies.
- [x] Build both plugins and run compatibility tests.
- [x] Compare implementation against this contract; record outstanding native checks.

Local verification on 2026-11-02: both plugins built successfully using the current engine source; 470,710 C# assertions passed, covering 512 PvE and 336 PvP combinations plus activity policy checks. Client Python tests passed (3); engine Python tests passed (21). The engine suite required a writable temporary directory. Both repositories passed `git diff --check`. These results do not constitute in-game validation or publication; release dependency pins now reference the matching activity implementation and tests.

Publication requested on 2026-11-02. Both plugins must pass the release build and package checks before publication is reported complete. Native acceptance remains pending.

Follow-up correction: direct attacks no longer depend on Burst. The compatibility suite was rerun after this policy change: all 470,710 assertions passed, including direct attacks and their parents with every toggle off, toggle-controlled buffs, and isolation from PvP/unmanaged classes. Packaging Python results above are from the preceding implementation pass; packaging code was unchanged by this correction.

## Ownership and supported scope

The complete executable ownership table is `Accord/WrathCombo/Engine/ActivityPresetPolicy.cs`; `ActivityAutomationTests` checks it against the actual engine preset enum, all 16 toggle combinations and every supported combat job. Parent menus remain enabled when they contain direct attacks, otherwise follow whether any automation category is enabled. Geomancer Weather is an umbrella, allowing its recovery/protection/offense/utility children independently.

Direct attacks include Variant Spirit Dart/Eagle Eye Shot, implemented Bozja Slash/Fair Trade/Assassination/Death, and Phantom attack spells and weaponskills. These retain existing readiness, resource and targeting rules. Buff initiators such as Phantom Aim, Rage and Drain Touch remain toggle-controlled; incidental damage does not make a buff initiator an unconditional attack. Oracle prediction initiation still requires Class mechanics; its damage outcomes can then be selected independently of Burst.

| Activity | Burst | Protection | Support | Class mechanics |
| --- | --- | --- | --- | --- |
| Variant | None | Rampart, Ultimatum | Cure, Raise | No separate utility action |
| Bozja (implemented Tank branches) | Focus, Font of Power, Bravery, offensive banners | Protect, Shell, Bubble, Paralyze III, Honed Acuity | Cure, Arise, Reraise | Stealth, Spellforge, Steelsting |
| Occult Crescent | Damage buffs | Mitigation, shielding, evasion, enemy damage reduction and control | Healing, raises, cleansing, HP/MP recovery | Movement, stealth/stealing utility, Dispel, Predict initiation, dance sequence, Libra, float |
| Deep Dungeons | None implemented | None implemented | Sustaining, Empyrean, Orthos and Pilgrim's recovery potions | None implemented |

Drain Touch uses Support for the existing healing/emergency-healing modes, otherwise Burst. Phantom Quick Step is Protection (evasion). Prediction and dance initiation remain Class mechanics; their existing targeting, readiness and sequencing remain unchanged.

Saved optional policies remain untouched: Phantom damage-buff restriction, Slowga waiting, Red Mage Cure retargeting/Libra refresh, Bozja banner timing, every numeric threshold and all existing alternative choices. Wrath's activity settings show an explanation when Accord controls the effective enabled state. No global EnabledActions entries are rewritten for this feature, so per-class choices cannot overwrite another class's setup.

## Explicit limitations

- Upstream Bozja's shared runner has no implementation for healer/melee/ranged/caster roles. Sixteen additional listed Tank options also lack execution branches; these are preserved, not falsely enabled as working automation.
- Deep Dungeon pomander selection returns false unconditionally upstream. This change does not invent pomander use.
- Variant Raise still requires a selected dead friendly target. Other targeting/readiness rules are unchanged.
- Manual native controls remain usable; this task does not add activity actions to hotbar mappings or equip duty actions for the player.
- Hands-free auto-rotation remains a separate Wrath setting. These toggles enable supported selection through the existing rotation paths.
- Required in-game checks: Variant action equipment/readiness, direct attacks with all four toggles off, each category switched independently, a per-class override, personal threshold retention, and no activity effect in PvP. Automated checks do not prove native combat behavior.

