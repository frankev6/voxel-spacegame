# Approved direction selection

`artdir_nocturne` is the single active selection record, with the canonical `faction_ark` fallback (SPEC-33 / SPEC-80 Q8) and a reference to the existing approved live Nocturne sky material. ART-008 was not run before owner approval, so no pre-existing direction/brick definitions were available to toggle or remove.

This is **selection metadata**, not a claim that ART-008's production look data or the missing ART-007 validator exists. The remaining contract fields retain their constructor defaults; brick/prop production bindings, palette/function/faction/lighting/post data must be populated by their owning art producers. TODO(AST-006/AST-007): author/bake and bind the approved brick/palette data; TODO(AST-008/AST-015): bind the production prop/space look. Do not consume the unpopulated fields as final Nocturne values. The existing renderer config and environment retain their current bindings and all live materials are unchanged.

`faction_ark` is the canonical intended ID, but its definition is not present in this checkout; SPEC-33/MIG-012 owns it. ART-007's validator/tests are also absent. ART-014 cannot claim a zero-error ArtDatabaseValidator run until those actual producers exist. The lead must import this asset, let the existing ContentDatabasePostprocessor rebuild the catalog and validate the available content checks.

DESIGN-GUIDE wins over the old Nocturne proposal. In particular, the current ArtPostLook schema cannot encode AgX and ArtLightingLook cannot encode the guide's full key/fill/rim rig. Preserve the approved live sky brightness/density, physical planet and lighting; represent the missing look through the owning module's contract process rather than changing shared types here.
