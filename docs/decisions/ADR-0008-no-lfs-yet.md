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

## Amendment (owner, 2026-10-16): LFS from now on, no history rewrite

The first push to GitHub happened on 2026-10-16 without the planned `git lfs migrate`. The owner then chose to keep history as it is and use LFS for everything from that point on ("LFS from now on", then "lets use lfs"), because a second developer will clone and work in the repo.

- Git LFS 3.8.0 was installed and `.gitattributes` now stores images (`png jpg jpeg tga psd tif tiff exr hdr webp kra`), audio and video (`wav mp3 ogg aif aiff flac mp4 mov`), fonts (`otf ttf`) and models and art sources (`fbx obj blend mmg`) through LFS. Every existing file of those types was converted to an LFS pointer in one ordinary commit (`git add --renormalize`).
- History before that commit is unchanged, so its binaries (about 130 MB compressed) stay in every clone. Only versions committed after it go to LFS. A clone downloads the old history once plus the current LFS files.
- The six generated placeholder texture arrays (`Assets/Game/Art/_Placeholder/Textures/*.asset`, 10.7 MB each) are Unity YAML and stay in plain git; real art replaces them (SPEC-81).
- Rejected: `git lfs migrate import --everything`. It would make clones about 100 MB smaller, but it rewrites every commit hash on the public `main` and on 561 local branches and six worktrees, and needs a force push.
