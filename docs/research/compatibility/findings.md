# Compatibility調査 findings

## Snapshotと範囲

この文書は、Labのcompatibility-research snapshotで実施した調査の、repo向け要約です。
raw dataset、archive、実行ログはrepoへコピーしません。

- 観測日：2026-08-14
- 対象list：Smorgasbord
- list version：3.1.1.39
- game：SevenDaysToDie
- assertions：56
- distinct labels：63
- 公開source：13
- manifest SHA-256：E74A9D3D37DAD299C4D869F526DD5E2F1EA5EF3EB568D5AA8EC87E0B998DBE27
- runtime verification：未実施

confirmed_compatibleとconfirmed_incompatibleは、保存したsource claimの極性を確認した状態です。
7DTD runtimeでの動作成功を意味しません。

## Assertionの状態

| status | 件数 |
| --- | ---: |
| conditional | 24 |
| confirmed_compatible | 14 |
| confirmed_incompatible | 13 |
| conflicting_evidence | 1 |
| unknown | 4 |

調査では、archive同居、file overlap、未解析のWabbajack directive、名前の類似だけをcompatibilityの証拠にしません。
未観測のenabled state、exact package identity、runtime resultは推測しません。

## 代表的な findings

### Oakraven Ammo Press：patchとload orderが条件になる

Ammo Press Add-On Patch for EFT and Izy v2は、Oakraven Ammo Pressへ特定packのsupportを追加すると記述します。
これは無条件のcompatibilityではありません。
patchの有無、patch version、対象pack、load orderを分けて保持します。

- A037：IZY-All in One Gun Packにはpatchが必要です。
- A043：patchはrequired modの後にloadする条件を持ちます。
- A039とA006：patch sourceはEFT Overhaulのsupportを記述しますが、Smorgasbordのsourceはmain EFT Overhaulを拒否します。

短いsource claimは次のとおりです。

- “Adds support to IZY-All in One Gun Pack”
- “folder name to load after requirement mod”
- “Not compatible with the main EFT Overhaul”

EFT Overhaulは、deployment contextが異なるためconflicting_evidenceとして保持します。

Source：
[Ammo Press patch](https://www.nexusmods.com/7daystodie/mods/6993)、
[Smorgasbord MOD_NOTES](https://github.com/ANONYMIZED_AUTHOR/Smorgasbord/blob/main/MOD_NOTES.md)、
[Smorgasbord manifest](https://raw.githubusercontent.com/ANONYMIZED_AUTHOR/Smorgasbord/main/modlists.json)

### Quest Revamp：hard conflictと未検証load-lastを分ける

Quest Revamp-Gears Editionは、SMXを含むUI MODがmodを壊すと記述します。
同じsourceは、Quest Revampをlastに置く回避策を示します。
ただし、その回避策は未検証です。

- A022：SMXとの通常条件はsame_feature_conflictです。
- A023：Quest Revampをlastに置く案はload_order_sensitiveです。
- A020、A021：Quest Revamp → Gears → Quartzはdependency chainです。

requiresをcompatible_withへ変換しません。
dependencyを満たしても、同じfeatureのconflictが消えたとは表示しません。

Source：
[Quest Revamp](https://www.nexusmods.com/7daystodie/mods/7210?tab=description)、
[Smorgasbord MOD_NOTES](https://github.com/ANONYMIZED_AUTHOR/Smorgasbord/blob/main/MOD_NOTES.md)

### WitosRoot：version scopeを失わない

WitosRootとWitos Root Osprey v2には、古いreleaseで同じloot list nameを定義したという報告があります。
maintainerは、後続のWitos/OspreyとSmorgasbord updateで修正済みと記述します。

- A045：旧versionのconflictと後続versionのfixを、別のversion-scoped evidenceとして保持します。
- A056：current manifestで両方が同居する事実だけを保持します。

exact old version、exact new version、enabled state、runtime resultは未確定です。
current co-presenceをruntime successへ昇格させません。

Source：
[Smorgasbord posts](https://www.nexusmods.com/7daystodie/mods/6764?tab=posts)、
[Smorgasbord manifest](https://raw.githubusercontent.com/ANONYMIZED_AUTHOR/Smorgasbord/main/modlists.json)

### Better Mod Compatibility：fallbackはproper patchではない

Better Mod Compatibilityは、invalid XML entryをskipしてstartupを継続させるfallback utilityです。
意図したfeatureの動作を修復したproper compatibility patchとは分けます。

- A044：startup fallbackはconditionalです。
- feature correctnessは未確認です。
- proper author patchは観測していません。

source claimは“does not magically make mods work”です。
起動できたことだけで、MODが正しく動作したとは表示しません。

Source：
[Better Mod Compatibility](https://www.nexusmods.com/7daystodie/mods/6097?tab=description)

### Static observation：co-presenceとdirectiveを昇格させない

- A053：Oakraven Ammo PressとIZY Classicがmanifestに同居します。しかし、IZY Classicがpatch対象のIZY-All in One Gun Packと同一とは確認できません。
- A054：WabbajackのPatchedFromArchive directiveが存在します。しかし、patch blobとauthor intentを解析していないため、compatibility patchとは判定しません。

この2件はunknownです。
manifest membershipとlist build transformationは、runtime compatibilityの証拠ではありません。

## この調査から固定する境界

- source claim、static observation、runtime observationを分けます。
- relationとconditionを分けます。
- confidenceはsource claimへの信頼度です。
- verificationは実際に確認した対象を示します。
- unknownには理由を付けます。
- conflicting evidenceは勝者を自動選択しません。

## Repoへ持ち込まないもの

- 56 assertions全体
- Wabbajack archiveとMOD binary
- private path、raw CDN URL、cookie、token、user ID
- runtime logとserver/client test result
- Compatibility Graphの実装
