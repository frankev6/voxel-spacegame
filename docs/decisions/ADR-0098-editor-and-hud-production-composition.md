# ADR-0098 — Editor and HUD production composition

**Status:** Accepted (Tech lead, EDT-023/HUD-002, 2026-10-14).

## Decision

GameRootConfig holds a serialized ShipEditorConfig reference. GameComposition constructs the persistent profile-aware blueprint store, EditorHostServices and ShipEditorLauncher after the real camera, UI, content, economy and session owners. The store owns its EditorContext, borrows SaveService and resolves the active profile at each operation; when no profile exists, the first blueprint operation creates/selects a normal profile named Blueprints through SaveService. Creative Lab uses that storage without creating a campaign save.

UiComposition reuses the exact editor view/model registered by EditorHostServices, installs the actual menu and flight HUD, binds HudSyncSystem to real HudNames and live binding labels, then detaches the temporary M1 scene-navigation adapter. Input rebindings refresh HUD labels. Fleet and narrative edit requests use the same actual launcher. Direct BlueprintLab startup initializes the creative session before ready hooks.

Launcher disposal synchronously releases its active scene host's native state before persistent store/context/content disposal; destroying its GameObject remains deferred. No module references, save formats or state ownership change. Actual encounter/dialog services retain their owning tasks; no substitutes are installed.

## Consequences

The editor, menu and flight navigation use one persistent screen registry with scene-owned edit state. HUD-002/EDT-023 acceptance still requires actual root/round-trip checks; source integration alone is not completion.
