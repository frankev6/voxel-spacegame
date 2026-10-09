# ADR-0008 — Git without LFS until the first push

**Status:** Accepted (owner, 2026-10-09)

## Context
The owner wants a local repository now and GitHub later. Git LFS is not installed on the machine.

## Decision
The repository starts as plain git. Binary types are marked `binary` in `.gitattributes`, with the LFS lines prepared but commented. **Before the first push**, install Git LFS, run `git lfs migrate import --everything --include="*.png,*.jpg,*.tga,*.psd,*.wav,*.mp3,*.ogg,*.otf,*.ttf,*.fbx,*.obj"`, and uncomment the LFS lines.

## Consequences
Local history is rewritten once at migration time (safe: nothing pushed yet).

## Alternatives considered
Installing LFS now (deferred: owner asked for local-only for now).
