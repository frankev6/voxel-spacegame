# Fonts after ART-014

ART-014 moved the existing Office Code Pro files from `Assets/Game/Art/_Placeholder/Fonts/` into `OfficeCodePro/`, retaining every asset GUID and every file's bytes. The original `OfficeCodePro-LICENSE.txt` is now `OfficeCodePro/OFL.txt`, with its original GUID and unmodified SIL OFL 1.1 text. The eight static TTFs and two existing SDF assets were moved; no font was downloaded, renamed as another family, regenerated or newly licensed.

These remain **compatibility placeholders**, not approved final NOCTURNE typography. The look-test fonts were never produced. Display/body/mono/body-strong temporarily use the existing Bold/Regular/Regular/Bold SDFs. TODO(AST-004): provide Michroma Regular, Inter Regular, JetBrains Mono Regular and Inter SemiBold with their own OFL texts and generated SDF assets, then replace the corresponding tokens. Preserve the existing GUID references until that producer explicitly replaces them.

The notice glyphs and existing layouts have not been regenerated. Lead validation must run the existing token-font PlayMode check after importing the moved assets. ART-014 does not certify any other carried-over font; Kontrapunkt remains outside this migration for AST-003/AST-034's provenance/removal audit.
