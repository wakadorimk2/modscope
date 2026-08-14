# Compatibility fixtures

このdirectoryは、Labのcompatibility-research assertionsから選んだ、8件のcompact fixtureを保持します。

これらは、将来のevidence model検証用の匿名synthetic fixtureです。
runtime testのpass、MODの有効化、7DTDの起動成功を示しません。

すべてのfixtureは、verification.runtime_verifiedをfalseにします。
source_assertionsはLab側の56 assertionsへの追跡IDです。
repoにはraw assertions、archive、MOD binary、private pathを保存しません。

| fixture | source assertions | 意味 |
| --- | --- | --- |
| 01-hard-conflict | A022 | 明示的なsame-feature conflict |
| 02-patch-required | A037 | patchを条件にしたsupport |
| 03-load-order-workaround-untested | A023 | 未検証のload-order workaround |
| 04-fixed-after-version | A045 | version-scoped conflictと後続fix |
| 05-dependency-only | A020 | dependencyでありcompatibilityではない |
| 06-conflicting-evidence | A039、A006 | 異なるdeployment contextのsource conflict |
| 07-unknown-co-presence | A053 | manifest co-presenceだけのunknown |
| 08-fallback-not-compatibility | A044 | startup fallbackとproper patchの分離 |
