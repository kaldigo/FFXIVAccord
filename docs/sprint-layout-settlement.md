# Sprint layout settlement

Saved/exported layouts can represent Sprint as General Action type 10/id 4. The mode updater uses action type 1/id 3 in PvE (29057 in PvP). Configured PvE layout planning now normalizes its desired Sprint with the same ModeActions.ForMode policy before comparing or writing slots.

This prevents the two Accord paths from repairing the same slot in opposite directions every second, which previously repeated hotbar backups and full configuration saves. The saved layout definition is preserved. Non-Sprint assignments, shared-bar protection, controller placement and rollback remain unchanged.

The production HotbarSetup/SprintHotbars regression applies an exported General Action Sprint, alternates both update paths across four polls, and checks that there are no further slot writes or backup files and no request to apply/save the layout again. Full client/engine and packaging checks remain required before publication. In-game frame-time improvement must be confirmed from subsequent captures.
