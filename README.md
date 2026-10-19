# FFXIV Accord

Accord owns intent settings, selected classes, and matching PvE/PvP hotbars. It configures the compatible [Wrath Combo fork](https://github.com/kaldigo/WrathCombo) through its validated settings API; it does not include a rotation engine.

Install through the shared [Dalamud plugin repository](https://github.com/kaldigo/DalamudPlugins). Plugin identity, `/accord`, configuration, and migration behaviour are unchanged.

The current preview includes three-bar manual PvE layouts saved before normal logout, with exact normal layouts restored when compatible Wrath is available. [Fallback behaviour](docs/manual-fallback.md) · [Button tables](docs/manual-layouts.md). Native in-game acceptance remains pending.

Cleanup protects only native game-shared bars. Former utility-slot and Sprint exceptions are removed. The complete [gathering layouts](docs/gathering-layouts.md) cover Miner, Botanist and Fisher beyond the old twelve-action limit, with direct access to every distinct action and reviewed native/engine replacements.

## Build and release

Initialise pinned submodules. Build with .NET 10 and Dalamud API 15 references using `tools/build.ps1`. Regression tests use the reviewed Wrath source at `engine-validation.ref`, checked out separately as `.engine` (not packaged). See [publishing](docs/publishing.md).

Successful source updates on `main` automatically produce an immutable versioned preview using the shared DalamudPlugins publisher, then update this repository's `pluginmaster.json`. Feed-only commits do not retrigger releases. The central installation feed refreshes hourly and supports on-demand refresh.

This standalone repository was extracted from the Accord companion source at commit 4d2ce69900f30917b1c64b6634b044d8f56741e3, whose upstream history remains in the Wrath Combo fork. Inherited licenses and attribution are retained. No personal configurations, game data or build outputs are published.
