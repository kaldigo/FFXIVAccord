# Pre-game integration review — 5 November 2026

Reviewed the testing integration against the existing Wrath action/replacement/queue paths and RSR selection, execution, targeting and shutdown paths. This is a source and executable regression review, not a claim of native acceptance or comparative DPS improvement.

## Findings and corrections

| Priority | Failure | Correction and evidence |
|---|---|---|
| High | Loss of Accord ownership returned before RSR's ordinary movement cleanup. RSR could remain responsible for a movement lock after it stopped updating. | Ownership loss now uses RSR's normal `RSCommands.UpdateState(Off, role)` transition, releases its own movement lock and clears pending/next actions. The separately installed auto-attack detour also checks ownership. Production-method tests cover all six active states, repeated stops, and stopped versus active auto-attack interception. |
| High | A validated action/recipient pair could later fall back to the original target or inherit another recipient from a protected native queue. | The action hook rejects invalid strict recipients and conflicting protected queues; ordinary legacy fallback remains available when no strict decision exists. Failed submissions clear pending preparation/intent/mudra proposals, so a later manual action cannot accept an earlier rejected request. |
| High | Automatic pre-pull and prevention actions directly submitted native actions, bypassing the button path's ground placement and checked execution. A fresh combo lookup could replace or discard the selected proposal. | Automatic execution retains the publishing input's route, runs the shared ground-placement selector, and uses Wrath's existing checked-action scope around native submission. Tests execute the production method for target changes, preparation-on-self, stale/mismatched proposals, placement rejection, readiness rejection, native rejection and exception cleanup. |
| Medium | Ground-heal placement created a new decision without its prevention event, so confirmation could leave the same event available for another mitigation. | Placement now changes the existing immutable decision while retaining event, generation and preparation fields. Regression tests confirm event consumption. |
| Medium | Bell used the sheet effect radius; focus could override automated ground placement even when automation overrides were disabled. | Bell uses the explicit 20-unit effect radius also present in RSR's `ActionTargetInfo.EffectRange`. Focus follows the same automation-override setting as mouseover. |
| Medium | DRK and BLU private target helpers read hard/last targets while the new selector evaluated a different enemy. | Under a selected evaluation context they now read that context, including an explicit missing target. Outside it their existing behavior remains. |

## Integration conclusions

- Keep the existing native queue and server-confirmation hooks. Previewing an action must not commit a sequence, and a failed native submission must discard its pending proposal.
- A selected action, recipient and ground point form one decision. Later delivery may reject it, but must not silently substitute a recipient that the rotation did not evaluate.
- RSR automation owns its own choices and execution. Wrath custom buttons remain independent. Ownership must cover separate hooks and cleanup, not just the primary automatic update loop.
- The RSR examples are behavioral references. These corrections do not transplant upstream RSR rotation code or change its ordinary enabled rotation priorities.

## Validation

The paired Accord/Wrath Release build and behavior suite pass, including 474,427 assertions. Wrath's production-method and packaging/policy tests, RSR's ownership cleanup regression, and RSR's Release build are checked separately. The Python suites pass: Wrath 31 tests, Accord 11 tests and RSR 12 tests. RSR builds with no errors or warnings; the paired build has no errors and retains existing compiler warnings.

Native acceptance remains necessary for actual client queue/animation behavior, action-effect timing, ground raycasts, hook ordering with other plugins, movement-lock ownership and live target changes. These are runtime checks; they are not substitutes for the corrected source defects above. Stable Accord, Wrath and stable feeds are outside this change.
