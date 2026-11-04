# Testing automation handoff

Implementation checkpoint, 4 November 2026. This is opt-in work on the three `testing` branches. It has not been accepted in game or published.

The Rotation testing page selects Wrath, RSR, Off, or release to native controls. `Accord.AutomationRunner.v1` accepts `wrath`, `rsr`, `off`, `release`, and `status` on the framework thread. It returns a human-readable status; this is a testing interface, not a promise of a stable external contract. AutoDuty/BossMod integrations still need to use the coordinator for owner changes. Their direct requests cannot enable the blocked runner.

## Transfer contract

Every transfer first blocks both engines with a new session token. It attempts both stops even if one fails. Both must acknowledge the token and report blocked before proceeding. Plugin action requests and target/preparation caches are cleared; an already queued native action or active cast is allowed to finish. If both engines settle, only the chosen engine receives permission and its native start command. Both sides are checked again after the change. A 15-second unsettled-action timeout leaves automation off.

The controller renews authority every 500 ms. Authority expires after three seconds. Missing peers, changed build IDs, rejected starts and unexpected ownership changes stop both sides. An expired lease cannot be renewed back into execution. Ordinary native pause/auto-off is not reversed by heartbeats. The selected native runner may still be paused by its normal controls.

Opting in persists only the fact that automation is coordinated. No live permission or token is persisted. An engine reload therefore starts blocked and requires an explicit new selection. Releasing control first stops both engines, then removes coordination. Native controls and third-party leases apply again after release. The controller requires both compatible forks for this initial implementation; it does not attempt a partially managed setup with a stock or missing peer.

## Execution boundaries

Wrath gates its main runner, its effective enabled state above external leases, and background stance/partner execution. Manual button resolution remains enabled. RSR gates its complete updater (including targeting, auto-enable, macros, movement and cast cancellation), native requested state, and direct action/item execution. RSR native action interception is suppressed while coordination is enabled so that Wrath's manual button presses do not become intercepted RSR requests.

RSR obtains native adjusted action IDs through a narrow Wrath endpoint that bypasses Wrath's replacement hook. Its update/action execution uses a synchronous token-checked scope; Wrath passes those calls to its original action hook. Accepted queued external actions retain their native action and recipient when processed later. Failed external attempts do not claim an existing manual queue. Ordinary manual calls outside these scopes continue through Wrath. Ground-targeted calls already pass through Wrath's location hook unchanged.

Wrath endpoints: `WrathCombo.AutomationOwnership.v1` (JSON command/status), `WrathCombo.AutomationExternal.Begin.v1`/`End.v1` (token-scoped hook bypass), and `WrathCombo.AutomationNativeAction.v1` (native adjustment). RSR endpoint: `RotationSolverReborn.AccordAutomationOwnership` (JSON command/status). Ownership commands are `block`, `renew`, `start`, `status`, `release`. These additions preserve all existing identities and IPC endpoints.

## Evidence and remaining checks

Deterministic tests cover stop-before-start, both switch directions, busy native action, heartbeat expiry, disconnected peer, start rejection, build change, transfer timeout, release, wrong/stale token, queue overwrite/expiry and failed external attempts. Both plugin codebases compile; this does not prove native hook ordering.

Required native runs: enable/disable each runner; switch while casting and in the queue window; press Wrath buttons during RSR execution; use ground actions and items; pause the selected runner; send competing native external enable requests; unload/reload each plugin and Accord; interrupt the framework; enter/leave PvP and observe match/death state changes; release coordination. Confirm action IDs, recipients and no duplicate automatic sends in captures. RSR action/job permission mapping and integration-specific AutoDuty/BossMod control remain pending. No all-job rotation-quality or seamless handoff claim is made.

## Provenance

All new coordination policy and bridge code is locally authored. RSR base: `156de237f5e4231d93558c8e6fba6ee337e7b902`; reviewed boundaries include `MajorUpdater.OnUpdate`, `RSCommands.ApplyState`/`DoAction`, `DataCenter.State`/scheduled actions, `BaseAction.Use`, `BaseItem.Use`, `Service.GetAdjustedActionId`, and `ActionQueueManager.UseActionDetour`. Wrath base: `761b51ff9fec11ee9f7c71dcd3ef6279dcb31bae`; reviewed boundaries include `AutoRotationController.Run`, `AutoRotationConfigIPCWrapper.Enabled`, `StancePartner.Cast`, `ActionReplacer.GetAdjustedActionDetour`, and `ActionWatching.UseActionDetour`. Upstream licenses and attribution remain in place. The small `AutomationLease` source is kept identical in the two forks to keep them independently buildable.
