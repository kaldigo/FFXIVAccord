# Testing channel preparation

The `testing` branches retain `Accord`, `WrathCombo`, and `RotationSolver` identities. Stable source branches and published feeds are unchanged.

Accord and Wrath now have a read-only `Build testing candidate` workflow. Pushes to `testing` run validation. Manual dispatch with a four-part `testing_version` builds that exact DLL version and uploads a candidate artifact after packaging checks. It does not create a public release or commit any feed. Existing stable release workflows remain unchanged.

The packaging entry points are `tools/package_testing.py --tag testing-vA.B.C.D --stable-feed <current-stable-feed.json>` and `tools/check_testing_package.py` with the same arguments. The candidate goes under `artifacts/testing`. Its immutable tag prefix does not match stable `v*` release triggers. The source repository's `pluginmaster.json` is never written by testing packaging.

The resulting candidate feed retains every stable entry and its `AssemblyVersion`, install URL, update URL and other metadata. Only the matching entry's `TestingAssemblyVersion`, `TestingDalamudApiLevel` and `DownloadLinkTesting` are overlaid. A stable version that catches or exceeds the candidate supersedes it. Tests cover stable preservation, unchanged identities, version validation and separation of stable/testing tags and output paths.

Do not publish this snapshot as a frozen alternative stable feed. Public feed composition must refresh the stable inputs on every sync and apply the active testing overlays afterward. Otherwise a stale testing snapshot could conceal later stable updates. Central feed composition, immutable prerelease publication, RSR candidate packaging and actual Dalamud installation acceptance remain to be completed. No installation URL is available from this checkpoint.
