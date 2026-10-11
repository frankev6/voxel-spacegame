# ADR-0033 — Collider overlap memory budget

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction.

## Context

PHY-019 measured the reference station through four consecutive damage buckets, completing snapshot-only construction outside the fixed step and sampling every blob owner before deferred disposal. The peak is **3,275,424 B**, above SPEC-12's 2.5 MiB allocation (**2,621,440 B**) and 26.6% above VOX-002/003's **2,587,904 B** replacement model.

At the measured peak: installed **670,096 B**, cached bricks **590,016 B**, pending compound **671,296 B**, retired blobs **669,024 B**, and completed batch results **674,992 B**. One installed version, a future pending version, a retired version, and a newly built version can coexist. The old model counted only three compound versions. This is required lifetime overlap, not duplicate ownership or leaked memory. The damaged steady state returns to **1,263,456 B**.

## Decision

Allocate **3.5 MiB (3,670,016 B)** to the reference station's live collider blobs, and retain the test that walks all owners, including in-flight results and the deferred-disposal queue. Record the original 2.5 MiB gate as **no-go**. The new allocation has 394,592 B (12%) headroom over the measured four-bucket workload; it is not an unlimited-damage guarantee.

Keep deterministic 7–10 tick installation and the minimum three-frame/two-tick retirement. Neither delay is shortened to make the memory measurement pass. Keep the two-level collider representation; there is no new public API, package, or ownership contract.

Correct SPEC-10's retained-copy reference model to **32,227,328 B** by replacing only its old collider peak with the measured live peak. This remains below both the **36 MB** working headroom gate and **40 MB** whole-voxel gate. Those whole-voxel limits do not change. Pool capacity growth, renderer allocations and whole-process integration remain TST-004's responsibility; blob MemorySize alone does not prove that exit gate.

## Alternatives

1. **Reallocate the collider share within existing total limits (chosen).** Matches measured lifetime overlap with no simulation or disposal change.
2. Reduce installation or retirement delays. Saves overlap but violates the catch-up determinism or borrowed-blob lifetime contracts; rejected.
3. Redesign collider sharing/region assembly now. Much larger representation/ownership work with unproven savings; unnecessary while the reference total has headroom. PHY-021 remains the conditional assembly-cost fallback.

## Consequences

SPEC-12 §4.8 and §8.1 use the corrected model and 3.5 MiB blob gate; its test name follows the new limit. Q24 in SPEC-12 and Q103 in the ledger record this explicit technical budget change. Future substantially more fragmented geometry and changes to install/bucket/retirement settings require renewed measurement. Thin-shell historical estimates remain stress-only and are not claimed to cover live overlapping updates.
