# Accord-owned engine controls (testing)

Accord owns the automation server-bar entry. Wrath and RSR no longer register clickable server-bar entries in these testing builds. The existing Accord support/protection/burst/class-mechanics entries remain separate permissions. There is no engine preference: Wrath handles the buttons, and the server-bar entry starts or stops RSR automatic execution through the verified handoff. Wrath automatic execution remains blocked. Reloading does not start automation.

Before starting, Accord reapplies settings for its selected classes to Wrath through the transaction API. It applies its saved RSR preferences and reads them back before starting RSR. Missing settings, rejected writes, mismatched read-back, missing engines or lost ownership leave automation stopped. RSR settings are checked every two seconds while RSR owns automation. Teaching mode is disabled for automatic execution. Engine replies report actual runner state as well as ownership permission.

Global rotation preferences no longer defer to saved Wrath preferences. Sparse old configurations resolve to Accord defaults: party-combat targeting, range 30, AoE threshold 3, combat only, openers enabled, opener potions and activity untargeted AoE disabled. Role/class overrides inherit from Accord globals. Existing explicit Accord choices remain intact.

Healer rotation settings now contain saved options for healing abilities in damage buttons and separately for healing GCDs. Auto support is required. These select the existing WHM/SCH/AST/SGE healing providers and their target/readiness checks; dedicated healing buttons remain available. Damage buttons still belong to Wrath when RSR owns unattended execution. RSR preferences configure its independent automatic healing behavior, not Wrath's manual button suggestions.

RSR keeps its plugin identity and commands. Its installer icon matches the Accord family; automatic first-start tutorial and update popups are suppressed. Manual tutorial access remains. During Accord ownership, RSR does not acquire a second upstream Wrath control lease.

Native verification: confirm the installed versions and enable diagnostic capture; exercise automatic RSR versus manual Wrath buttons, stop/start, reload, class changes, support disabled/enabled, ability-only healing and GCD healing. Ownership, settings verification and isolation failures are recorded. Build and mock tests cannot establish that an in-game action was executed successfully.
