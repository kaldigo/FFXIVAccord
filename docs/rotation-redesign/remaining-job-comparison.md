# Remaining rotation comparison and final coverage

Source review, 4 November 2026. Completes the standard PvE job coverage begun in [rotation-comparison.md](rotation-comparison.md), with limited-job coverage explicitly separate. Same local baselines: Wrath `761b51ff9fec11ee9f7c71dcd3ef6279dcb31bae`, RSR `156de237f5e4231d93558c8e6fba6ee337e7b902`. Both engine checkouts were clean at this review. No gameplay benchmark was performed. Source differences establish capabilities and design candidates, not measured superiority.

## How to read the comparison

Follow-up: the previously outstanding PvP review is now recorded in [PvP comparison and compact controls](pvp-comparison.md). The PvE coverage limits above still apply; neither review is a live performance benchmark.

For each job, examine selection entry points, the relevant helpers, and the RSR action/selection boundary. Distinguish a concrete mechanism worth adopting from an unproven priority preference. Both systems distribute logic: Wrath across Invoke/helpers/action providers and RSR across job rotations, base rotations and CanUse. A shorter job file is not evidence of more complete reasoning.

This completes a decision-oriented review of all 21 standard PvE jobs, not an exhaustive proof of every preset, level, configuration or action. PvP has separate rotations and requires a separate behaviour evaluation; PvE conclusions do not certify its priorities. The earlier handoff's PvP integration constraints still apply.

## Paladin

**Wrath:** ST/AoE and simple/advanced use common TryOGCDAttacks and TryGCDAttacks helpers. The entry respects Passage of Arms channel protection and integrates content/mitigation before offence. This is already shared logic rather than four entirely independent rotations. It also has explicit opener support.

**RSR:** EmergencyAbility relates Fight or Flight to nextGCD at lower levels and to Atonement/Divine Might readiness at higher levels; Requiescat/Imperator follows Fight or Flight. GeneralGCD considers proc expiry, combo progress, medication/burst state, and Atonement-chain availability. Support includes Intervention eligibility, Oath spending, Cover/Hallowed interaction and Clemency permissions depending on surviving healers.

**Decision:** adapt the planned-GCD interface and explicit proc deadlines. Retain Wrath's channel protection and intent permissions. RSR's broad automatic rescue options are not suitable defaults for Accord: Cover/invulnerability must retain explicit ownership. Validate holding Fight or Flight, manual Imperator, expiring Atonement, ranged Holy Spirit and Passage interruption before choosing new priorities. No demonstrated need to replace every PLD action rule.

Sources: Wrath PLD.cs and PLD_Helper.TryOGCDAttacks/TryGCDAttacks; RSR PLD_Reborn.EmergencyAbility, GeneralGCD, UseOath, CanHealSingleSpell.

## Monk

**Wrath:** Perfect Balance has dedicated state-sensitive helpers, Nadi-dependent form selection, a post-burst-hold path, Opo checks, Formless Fist handling and Fire/Wind Reply rules. UsePerfectBalance refuses incompatible existing states and checks the preceding Opo GCD. It already supports player-held burst.

**RSR:** explicitly bookends Perfect Balance with Opo actions; chooses forms from Beast Chakra/Nadi, treats odd/even burst windows differently and coordinates Brotherhood/Riddle of Fire. Fire's Reply normally avoids Perfect Balance/Formless overlap, with an expiry escape. It also uses elapsed combat thresholds and a special emergency placement of early Perfect Balance; source comments describe weaving responsiveness concerns, not a runtime-proven advantage.

**Decision:** retain Wrath's accepted-phase/held-burst behaviour. Replace scattered timing decisions only with an explicit phase model that can explain Nadi goal, next form, remaining stacks and reply deadlines. RSR is a useful alternate policy, not an established better replacement. Test interrupted PB, one missing Nadi, manually held RoF, level sync and expiring Replies.

Sources: Wrath MNK.cs, MNK_Helper.DoPerfectBalanceCombo/UsePerfectBalance; RSR MNK_Reborn.EmergencyAbility, AttackAbility and GeneralGCD.

## Dragoon

**Wrath:** shared helpers coordinate Battle Litany/Lance Charge/Life Surge and jump follow-ups. It distinguishes ordinary, jump and Stardiver weave windows. UseLifeSurge infers the appropriate upcoming attack from recent actions/combo context; AoE has its own finisher checks.

**RSR:** Life Surge explicitly examines nextGCD, distinguishing large hits, alternative finishers during buffs and level-specific cases. Wyrmwind can be spent before a planned Raiden Thrust/Draconian Fury, and Lance Charge may briefly wait for Litany. It avoids ordinary abilities immediately after Stardiver and exposes dive movement/distance controls.

**Decision:** high-priority pilot for replacing the GCD/ability decision flow. Compute the intended GCD once, then choose Life Surge and gauge spending against it. Retain Wrath's jump-lock handling and manual movement rules. A planned attack can change after a player press; preparation therefore needs revalidation. Compare Life Surge landing action, Wyrmwind overcap, dive clipping and buff drift.

Sources: Wrath DRG.cs, DRG_Helper.UseLifeSurge/CanDRGWeave; RSR DRG_Reborn.AttackAbility/EmergencyAbility/GeneralGCD.

## Samurai

**Wrath:** helpers track missing Sen, buff refresh, Iaijutsu movement conditions and Kenki reservations. NeedKenkiRoomForIkishoten looks ahead five GCDs; CanDumpKenki protects Zanshin/Senei requirements. UseIaiJutsu contains explicit recovery after downtime when buffs are missing, including spending three Sen if Meikyo is unavailable. This is concrete predictive and recovery logic.

**RSR:** has an explicit ST-to-AoE choice between converting three Sen with Hagakure or spending Setsugekka. Meikyo uses charge horizons; Higanbana depends on buffs, target count and context; eligible Gekko/Kasha can be ordered by the positional currently available. It still contains fixed thresholds and job-specific exceptions.

**Decision:** preserve Wrath's Kenki reserves and downtime recovery. Introduce explicit transition policy for mixed ST/AoE state, and use actual positional availability only when choices are otherwise rotation-equivalent. RSR's default Hagakure transition is a policy option, not an automatic upgrade. Test three-Sen target-count changes, interrupted casts, lost buffs and generator overcap.

Sources: Wrath SAM.cs, SAM_Helper.DoMeikyoCombo/UseIaiJutsu/NeedKenkiRoomForIkishoten/CanDumpKenki; RSR SAM_Reborn.AttackAbility/GeneralGCD.

## Reaper

**Wrath:** retains Executioner/Soul Reaver commitments, separates Enshroud weaves from GCD steps, handles post-burst actions and prevents some content/ranged substitutions from displacing committed actions. It already has pooling and burst helpers.

**RSR:** Enshroud eligibility combines Shroud, Arcane Circle windows, Ideal Host and a dying-target exception. It protects an expiring basic combo when outside an active phase, chooses enhanced/positionally appropriate Executioner actions, and considers Death's Design coverage in area selection. These are local priorities rather than an entire future rotation simulation.

**Decision:** adapt explicit deadlines, target-lifetime exceptions and target coverage to a shared phase selector. Retain committed Soul Reaver/Executioner and Enshroud progression. A large replacement of phase-entry and spending decisions is reasonable if traces show existing burst recovery fails; replacing the whole sequence engine before that evidence is unnecessary. Test mid-Enshroud movement, manual entry, target death and basic-combo expiry.

Sources: Wrath RPR.cs, RPR_Helper.UseEnshroudComboGCD and burst helpers; RSR RPR_Reborn.AttackAbility/GeneralGCD.

## Viper

**Wrath:** prioritises tail/twin follow-ups, completes Vicewinder/Vicepit, checks buff state before Reawaken, and prevents Rattling Coil overcap ahead of Ire/charge spending. Helpers distinguish ST/AoE weave-blocked states and retain a ranged fallback.

**RSR:** has NoAbilityReady-based restrictions that protect required follow-ups, venom-dependent ordering with a timing fallback, Reawaken entry gated by remaining combo and buff duration, and explicit generation/Ouroboros progression. It can spend coils for cap pressure or a dying target. Its support restrictions can defer defence while follow-ups are pending.

**Decision:** make follow-up ownership and deadlines explicit in Wrath's shared sequence state, preserve its coil forecasts, and evaluate RSR-style duration checks for entering Reawaken. Do not import blanket support deferral for urgent manual mitigation. Test missing/delayed venom status, out-of-range follow-ups and Ire with capped coils. No source basis for wholesale VPR replacement.

Sources: Wrath VPR.cs, VPR_Helper.UseReawaken/OvercapUncoiledFuryProtection/UseViceTwinWeaves; RSR VPR_Reborn.EmergencyAbility/GeneralGCD.

## Bard

**Wrath:** has shared ST/AoE helpers, explicit song transitions, Pitch Perfect spending before a transition, anticipation of Empyreal Arrow generating repertoire, pooled Bloodletter-family charges and coordinated buffs. It includes Accord's manual burst follow-through and song-initiation permission.

**RSR:** configurable song order/durations, transition checks that protect repertoire, Iron Jaws refresh near the end of active buff windows, and Soul Voice spending/holding based on song and buff timing. Its Apex helper contains several fixed windows. Flexibility is not proof the selected default song cycle is better.

**Decision:** compare song transitions and DoT refresh value directly, keep Wrath's manual initiation/continuation semantics, and replace the shared song/buff/DoT decision block only if needed. Adopt explicit candidate reasons such as “refresh under expiring buff” rather than separate unexplained cooldown tests. Validate song drift, no-target transitions, repertoire loss and DoT refresh without sufficient remaining target life.

Sources: Wrath BRD_Helper.TryOGCDAttacks/PitchPerfected and song local functions; RSR BRD_Reborn song helpers, GeneralGCD and CanUseApexArrow.

## Summoner

**Wrath:** common helpers select abilities, mitigation and summon spells. Simple mode uses a fixed Titan/Garuda/Ifrit preference; advanced controls and opener logic are separate. Gauge timers and demi identity already represent phase state.

**RSR:** relates Swiftcast to the planned resurrection/Ruby/Slipstream spell, has configurable summon order, coordinates Searing Light with demi state and opener delay, and releases certain resources when the target is dying or a generator is nearly ready. Rekindle/Lux have expiry fallbacks and healing-category targeting.

**Decision:** adopt purpose-bound Swiftcast and expiry-aware support selection, with a shared reservation between resurrection and damage. Preserve user choices over movement-producing Crimson Cyclone. A fixed summon-order difference is not enough to establish superiority; future movement-aware summon ordering is a proposed enhancement, not a demonstrated RSR planner. Test phase truncation, manual resurrection, pet absence and movement during Ruby/Garuda.

Sources: Wrath SMN.cs and SMN_Helper phase/selection helpers; RSR SMN_Reborn.EmergencyAbility/AttackAbility/GeneralAbility/GeneralGCD/RaiseGCD.

## Pictomancer

**Wrath:** explicit movement alternatives, motif readiness, pre-pull/no-target drawing, burst-preparation windows and muse-charge forecasts. It avoids drawing motifs during Starry Muse and guards against immediate buff-propagation delays. Hammer use includes movement, burst and expiry cases.

**RSR:** separates muse abilities and spell priorities, prioritises Rainbow/Comet/Star Prism/Hammer phases, prepares motifs out of combat, and has burst-dependent mitigation rules. It also uses elapsed combat and cooldown thresholds. The inspected rules do not demonstrate a superior encounter-aware motif schedule.

**Decision:** retain Wrath's useful preparation and movement choices, but consolidate motif/paint/hammer decisions into an explicit preparation budget. Add reliable downtime information to judge when drawing can finish; this is a new adaptation inspired by RSR's shared encounter context, not a direct proven PCT feature to copy. Test short downtime, movement, capped paint, expiring Hammer and manual Starry initiation.

Sources: Wrath PCT.cs, PCT_Helper.TryMovementOption/TryDrawMotif/TryGCDSpells; RSR PCT_Reborn.AttackAbility/GeneralAbility/GeneralGCD.

## Closing earlier partial comparisons

**Dark Knight:** Wrath DRK_ActionLogic.Spender already reserves configurable mana, including 3,000 for TBN in simple mitigation mode and removing the reserve below its unlock. It considers imminent burst, Darkside expiry and blood/combos. RSR CheckDarkSide also reserves TBN MP in one burst-spending branch, handles Dark Arts and uses an 8,500 MP fallback. Retain Wrath's configurable reserve; improve it with a specific incoming-event reservation if warranted. RSR is not uniquely aware of the damage-versus-defence resource tradeoff.

**Warrior:** Wrath WAR_Helper shares ST/AoE attack handling and protects Nascent Chaos, IR stacks, gauge and charge retention; it refreshes Surging Tempest through combo policy. RSR's GCD-based buff/charge horizons are useful for expressing how long a buff must survive. Recommend duration-aware entry checks and common spending forecasts, retaining existing defensive and movement ownership. Compare high-gauge Infuriate, proc expiry, level sync and manually held IR.

**Red Mage:** inspecting Wrath UseHolyFlare closes the earlier open question: it already selects finishers using mana imbalance and existing Verstone/Verfire procs. RSR additionally forces balancing in certain burst/large-imbalance conditions. This is a policy difference to measure, not a missing Wrath capability. Reserve dualcast/Swiftcast deliberately when resurrection is requested; do not let a broad rewrite discard proc preservation.

## Limited jobs present in these checkouts

These findings describe repository code only; no claim is made about current live-game support outside the inspected builds.

**Blue Mage:** Wrath contains loadout/mimicry-aware controls, dedicated Final Sting and Moon Flute sequences and role-specific paths. RSR similarly has slot-aware checks, Moon Flute and Bristle/Whistle preparation, and separate role/utility selection. Neither can be evaluated as one fixed rotation independent of the equipped spell list. Retain explicit loadout and self-sacrifice controls; use separate loadout-specific scenarios before replacing priorities. Shared eligibility and preparation architecture can serve BLU without importing a generic damage ranking.

**Beastmaster:** both checkouts contain implementation. Wrath coordinates pets, capture, intentional/instinctual actions and finishers; RSR exposes horn order, per-horn One with Nature choices and optional familiar/player HP-based tank swapping, alongside TP accounting. These are real policy differences in source, not evidence that one delivers a better complete rotation. Retain Wrath's capture/control behaviour and treat pet/horn/TP planning as a dedicated adapter. Do not silently enable RSR's pet sacrifice/tank-swapping preference in Accord.

Sources: Wrath BLU.cs/BLU_Helper.cs and BST.cs/BST_Helper.cs; RSR Limited Jobs/BLU_Reborn.cs and BST_Reborn.cs.

## Final disposition for every standard PvE job

“Adapt” means bring the decision into the proposed shared selector while retaining useful rules; “replace block” means substantial new selection is justified as a candidate, subject to validation. Neither means importing RSR execution into Wrath.

| Job | Proposed scope | Most valuable comparison/adaptation |
|---|---|---|
| PLD | Adapt burst/proc block | Planned GCD, proc deadlines, manual buff continuation |
| WAR | Adapt | Duration-aware IR entry, charge/gauge forecast |
| DRK | Adapt | Event-specific MP reserve; retain existing TBN pooling |
| GNB | Replace burst/weave block candidate | Confirmed burst state and planned GCD instead of independent proxies |
| WHM | Replace shared healing/protection orchestration | Event timing, pending heals, major-tool spacing |
| SCH | Replace preparation/target orchestration | Recitation/shield/Deployment recipient and deadline |
| AST | Replace preparation/release orchestration | Planned heal, matching recipient, stored-heal coordination |
| SGE | Replace preparation/resource orchestration | Eukrasia aim, Zoe follow-up, Addersgall and overlapping protection |
| MNK | Adapt phase block | Explicit Nadi goal and accepted PB sequence |
| DRG | Replace buff/weave block candidate | Life Surge on the selected GCD and gauge-before-generator |
| NIN | Adapt sequence interface | Preserve mudra state; recover from manual deviations |
| SAM | Adapt | Preserve Kenki/recovery; explicit ST/AoE transition |
| RPR | Adapt, replace phase-entry block if justified | Deadlines, pooling, target lifetime, committed follow-ups |
| VPR | Adapt | Follow-up ownership, buff-duration entry, coil forecast |
| BRD | Adapt song/buff/DoT block | Song transitions, snapshot refresh, resource deadlines |
| MCH | Replace scheduling block candidate | Planned tool preparation and downtime-sensitive commitments |
| DNC | Adapt phase block | Proc/gauge deadlines and dance initiation/continuation |
| BLM | Adapt phase/movement block | Select variant explicitly; preserve phase and instant resources |
| SMN | Adapt | Purpose-bound Swiftcast, demi/support expiry |
| RDM | Adapt | Retain proc balancing; explicit damage/raise resource ownership |
| PCT | Adapt preparation/movement block | Motif budget, paint/hammer deadlines, downtime opportunity |

## What the full comparison establishes

The strongest basis for a sizeable change is a shared decision system that carries encounter information, intended GCD, preparation/recipient commitments and resource deadlines consistently. That addresses recurring structural limitations across jobs. It is not evidence that every RSR priority list should replace its Wrath counterpart.

Some proposals are direct adaptations of observed RSR mechanisms (nextGCD preparation, pending-heal accounting, timeline consumption). Others are design improvements motivated by gaps in both engines (deadline-aware preparation chains, consistent immutable previews, event-wide prevention of redundant mitigation). The implementation plan labels that distinction.

The decision can now be made with complete standard PvE coverage: pursue a staged replacement of Wrath's suggestion architecture, prioritising healer coordination and GCD/ability planning, while preserving the hotbar contract and independent RSR automation handoff. Confirm gains through traces and gameplay before making each new job selector the default.
