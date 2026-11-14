# Caption font sources

Imported 2026-10-25 from the official Noto CJK repository. These are regional regular-weight OTF subsets, retained as Unity Font assets for dynamic TMP glyph generation.

- `NotoSansSC-Regular.otf`: https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/SubsetOTF/SC/NotoSansSC-Regular.otf
  - SHA-256: `FAA6C9DF652116DDE789D351359F3D7E5D2285A2B2A1F04A2D7244DF706D5EA9`
- `NotoSansJP-Regular.otf`: https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/SubsetOTF/JP/NotoSansJP-Regular.otf
  - SHA-256: `DFF723BA59D57D136764A04B9B2D03205544F7CD785A711442D6D2D085AC5073`

License: SIL Open Font License 1.1; the unmodified upstream license is included as `Noto-CJK-LICENSE.txt`, from https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/LICENSE.

`NotoSans-Captions-Chinese SDF.asset` and `NotoSans-Captions-Japanese SDF.asset` are static Latin TMP variants that share the existing NotoSans-Regular atlas/material. Their regional fallback ordering preserves appropriate Chinese/Japanese Han forms while retaining both character sets and Korean for mixed history. These variants do not duplicate the OTF files or Latin atlas. The underlying Chinese/Japanese SDF assets use dynamic multi-atlas population and fall back to the project's existing NanumGothic for Korean.
