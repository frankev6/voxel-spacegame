# ADR-0007 — Chunked binary save container

**Status:** Accepted

## Context
Prototype saves were ~150 B of JSON per voxel and were silently rejected after content changes.

## Decision
Saves use a chunked binary container (header, chunk table with fourCC, version, length, CRC32; unknown chunks preserved). Voxel chunks are palette + RLE + Deflate. Non-voxel chunks may embed Newtonsoft JSON. Loads never reject: per-chunk migrations, unknown content IDs map to a Missing placeholder with a warning. Writes are atomic (tmp + replace + backup). Details: SPEC-05.

## Consequences
Fast, compact, versioned saves. Every module that owns persistent state implements `ISaveParticipant`.

## Alternatives considered
JSON everywhere (rejected: size and silent schema drift).
