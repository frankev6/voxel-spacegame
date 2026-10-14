# ADR-0101 — Use the specified dialog fallback while the package gate is pending

**Status:** Accepted (Tech lead, 2026-10-14).

## Decision

Frank's standing direction is to use documented defaults at owner gates and continue implementation. SPEC-41 question1 explicitly specifies that the fallback format and runner allow work to continue while DLG-005 is pending. Implement DLG-007 with the specified DialogProgramCompiler and GraphDialogRunner against completed DLG-003/006. Keep the same Yarn-subset inputs, IDialogRunner boundary and full runner conformance requirements.

DLG-004's package spike and DLG-005's owner decision remain open; this is neither a fabricated spike result nor owner rejection of Yarn. No package, assembly-reference or third-party dependency change is made. If the owner later adopts Yarn after the spike, replace the runner behind the existing interface and run the same conformance suite. The program asset is the documented default implementation pending that choice.

## Consequences

Dialogue, beats and real story interactions can be implemented now. The owner gate remains visible and does not prevent DLG-007's functional acceptance or subsequent work. Change DLG-007's prerequisites to the completed DLG-003 and DLG-006; preserve all eleven required conformance cases. No test or runtime behavior is waived.

The existing NarrativeLogicAdapter gains an additive BindDialog(DialogService) composition hook for its two already-specified dialog queries (visited_count and roll); other QST-006 methods remain explicitly pending. This resolves the context/service construction cycle without service-location during expressions or changing ILogicNarrative.
