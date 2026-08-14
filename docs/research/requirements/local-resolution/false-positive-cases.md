# False-positive cases and safeguards

観測日時: `2026-08-14T10:46:58Z`

この文書は、ローカル情報があってもdependency edgeを自動確定してはいけない例を記録します。

## 1. Dependency候補ではない自然言語

- `not_applicable` と分類したbaseline行: `18`。
- `Language`、`Russian Author: ...`、`German Author: ...` は、翻訳や作者表示の可能性があります。MOD identityとして扱いません。
- `new game`、`stronger hardware` は、環境条件です。MOD packageとして扱いません。
- `to integrate ...`、`to change ...`、`to toggle ...`、`to survive ...` は、説明文の断片です。固有名として扱いません。
- 対策: `target_kind` と `resolution_status=not_applicable` を保存し、baselineのsource evidenceを削除しません。

## 2. Package nameとModInfo.xmlの不一致

- packageとModInfo名が完全一致しないpackage: `396`。
- package名はMO2表示名です。ModInfoのName / DisplayNameはゲーム内Modlet identityです。両者は同じものとは限りません。
- 例: `mods/(V3) 1-SCore Blood Moon Count` → ModInfo Name `1-SCore_BloodMoonCount`
- 例: `mods/(V3) 1-SCore Remote Crafting and Auto-Sort Drop Box` → ModInfo Name `1-SCore_RemoteCraftingAndAuto-SortDropBox`
- 例: `mods/(V3) Atomic Burger POI` → ModInfo Name `V3_AtomicBurger`
- 例: `mods/(V3) Classic Crack-a-Book HQ POI` → ModInfo Name `V3_Classic_Crack_a_Book_HQ`
- 例: `mods/(V3) Clear Bulletproof Glass` → ModInfo Name `V3_ClearBulletproofGlass`
- 例: `mods/(V3) Darks Cleanerz (NPC Mod Add On Pack)` → ModInfo Name `1_DarksCleanerz`
- 例: `mods/(V3) Darks Soldierz (NPC Mod Add On Pack)` → ModInfo Name `1_DarksSoldierz`
- 例: `mods/(V3) Game Menu Options` → ModInfo Name `V3_GameMenuOptions`

## 3. One packageに複数Modlet

- 複数Modletを含むpackage: `5`。
- package名だけで1つのtarget Modletを選ぶと、誤ったedgeになる可能性があります。
- exact package matchで複数Modletが残る場合は `ambiguous` にします。
- 例: `mods/7 Days of Insomnia (Immersive Sleeping) (server-side)` → `Byteblazar's 7 Days of Insomnia, Byteblazar's 7 Days of Insomnia - Overrides`
- 例: `mods/Deluxe Block POI Pack By ANONYMIZED_AUTHOR` → `ANONYMIZED_AUTHOR Military, Serious POI Pack by ANONYMIZED_AUTHOR`
- 例: `mods/IZY Classic - Core` → `Izayo_WeaponFixes, Alter_Soundoverride, IZY_FPV_GLOVES, IZY_MMVMV2, IZY_RMP_Miscpack, IZY_RMP_44magnum, IZY_RMP_45ACP, IZY_RMP_556, IZY_RMP_762pack, IZY_RMP_9mmVAL, IZY_RMP_Demopack, IZY_RMP_HVW, IZY_RMP_SG, IZY_RMP_Tec…`
- 例: `mods/NPCCore - Do not toggle midsave` → `No Core Human NPCs, 0-XNPCCore`
- 例: `mods/The Descent - Procedural caves - 16GB+ RAM Required - Must be enabled when you create a map!` → `ModUtils, 7D2D-cave-assets, cave-entities, cave-prefabs, TheDescent`

## 4. Archive名とpackage名の不一致

- archive名はWabbajack/Nexus配布名です。MO2 package名はインストール後の表示名です。
- archive filenameの類似だけではpackage identityを確定しません。
- archiveとpackageの対応は、exactまたは一意のprefix/token subsetを補助情報として保存します。
- `.meta`のmodID/fileIDはarchiveに付属します。packageに移すときはarchive linkageを別evidenceとして保存します。

## 5. Frameworkと通常MOD

- `Harmony`、`SCore`、`Core`を含むDLL、フォルダ、XML/configはframework候補です。
- framework markerだけではdependencyを確定しません。
- 明示的なlocal dependency metadataがない場合、DLL/config evidenceはlow扱いにします。

## 6. Fuzzy matchの扱い

- name similarityだけでresolvedにしなかった候補行: `2`。
- 候補名は `suggestions` に保存します。
- 自動edge生成では、exact ModInfo名、明示dependency metadata、またはユーザー確認を要求します。

## 7. Bundle、patch、fork、author suffix

- bundle packageは複数Modletを含む可能性があります。package-level edgeとModlet-level edgeを分けます。
- patch名は対象MODを表す場合があります。patch自身を依存先と誤認しないため、relationship typeとsource evidenceを維持します。
- renamed / forked MODは名前が一致しても同一性を保証しません。Nexus mod ID、ModInfo、archive `.meta`の組み合わせを優先します。
- author prefix / suffixは名前正規化で削除しません。raw nameとmatched fieldを両方保存します。

## 判定方針

- `resolved`: exact ModInfo Name / DisplayNameなど、local Modlet identityを観測した場合です。
- `partially_resolved`: exact package nameはあるが、Modlet identityまたはarchive linkageが不足する場合です。
- `ambiguous`: 複数package、複数Modlet、またはfuzzy matchだけがある場合です。
- `not_found_locally`: concreteな名前に対してexact local identityがありません。
- `not_applicable`: target rawがMOD identityではなく、環境条件、手順、説明文断片、translation metadataの場合です。
