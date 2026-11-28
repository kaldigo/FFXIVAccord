# AutoDuty starts automation without gating manual control

The Follow AutoDuty option is an automatic **start request**. It does not block the normal saved auto-rotation toggle when AutoDuty is idle, missing, stopped or temporarily disconnected.

After observing idle AutoDuty, its transition into active navigation requests the same persistent on setting as the normal toggle. Automation stays on when the run finishes, as requested. A manual off during the run remains off; repeated active polls or an IPC reconnect do not turn it back on. A later new run may request on again. Reloading Accord or enabling Follow AutoDuty during an already active run does not overwrite saved off: use the normal toggle for that existing run.

Existing engine selection, settings verification, native queue handoff, emergency backup, PvP suspension and combat-job waiting remain in force. No live configuration is edited externally and no per-user test override is installed. The normal saved preference is the only persisted automation state.

Regression coverage exercises idle/unavailable manual automation, a new duty start, completion, explicit off, repeated polls, unavailable/reconnected IPC, disabled follow and reload during an existing run. Existing both-engine handoff, PvP and combat-job resume checks remain enabled. Native AutoDuty start/stop acceptance remains for in-game testing.
