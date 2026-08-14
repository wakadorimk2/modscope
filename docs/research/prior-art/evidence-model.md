# ModScope Prior-Art / Evidence Model Research

## 1. 文書の役割

この文書は、ModScopeのPrior Art First調査と、調査から導いたEvidence Modelの設計判断を記録します。

この文書はresearch noteです。

`CompatibilityAssertion`は概念モデルです。

この文書ではproduction schema、database migration、wire contractを定義しません。

調査日は2026-08-14です。

対象版は、インストール済み版ではありません。

公式sourceを確認したときのresearch snapshotです。

## 2. Evidence status

### Observed / Verified

公式資料、実データ、またはユーザーが明示した検証で確認した事実です。

### Inferred

複数のObserved / Verifiedから導いたModScope向けの設計判断です。

### Uncertain

今回のsource、実データ、またはlocatorでは確定できない事項です。

Uncertainは、機能が存在しないことを意味しません。

### Diagnostic

解析できなかった理由、sourceの制約、または検証範囲の不足です。

## 3. 中心用語の対応表

| 出典 | 目的 | 具体対象 | 役割 | 前後関係 | 候補語 | 初出定義 |
|---|---|---|---|---|---|---|
| ユーザー指定資料 §5 | booleanで互換性を確定しないため | 条件とevidenceを伴う関係主張 | compatibility比較の説明単位 | source evidenceからassertionを作り、UIでは条件と未確認事項を表示する | CompatibilityAssertion | 条件、evidence、confidence、verificationを分離して保持する互換性の説明単位 |

`CompatibilityAssertion`は、subject、relation、objectまたはscope、conditions、evidence、confidence、verification level、review state、unresolved reasonを持ち得る概念上の説明単位です。

この用語は、runtimeで確認済みの互換性だけを意味しません。

## 4. 公式sourceと調査対象版

| 製品 | 調査対象版 | 公式source | 確認した範囲 |
|---|---|---|---|
| MO2 | v2.5.2 | [repository](https://github.com/Modorganizer2/modorganizer)、[v2.5.2 release](https://github.com/Modorganizer2/modorganizer/releases/tag/v2.5.2)、[USVFS](https://github.com/Modorganizer2/usvfs)、[Nexus update-check source](https://github.com/Modorganizer2/modorganizer/blob/master/src/organizer_en.ts) | manager、Nexus連携、update check、LOOT、plugin order、virtual filesystem、file conflictの表示 |
| Wabbajack | 4.2.1.4 | [repository](https://github.com/wabbajack-tools/wabbajack)、[releases](https://github.com/wabbajack-tools/wabbajack/releases)、[installation documentation](https://wiki.wabbajack.org/user_documentation/Installing%20a%20Modlist.html) | manifest、mod version、hash、link、README、external file、list installation |
| Vortex | 2.5.0 stable / 2.6.0-beta.1 | [repository](https://github.com/Nexus-Mods/Vortex)、[releases](https://github.com/Nexus-Mods/Vortex/releases)、[API](https://github.com/Nexus-Mods/vortex-api)、[Events](https://github.com/Nexus-Mods/vortex-api/blob/master/docs/EVENTS.md)、[FAQ](https://github.com/Nexus-Mods/Vortex/wiki/MODDINGWIKI-Users-FAQ)、[load-order rules](https://github.com/Nexus-Mods/Vortex/wiki/MODDINGWIKI-Users-General-Managing-your-Load-Order) | profile、dependency、recommendation、version check、load-order rule、conflict resolution、deployment、health diagnostics |

Vortex APIのGitHub repositoryは2026-07-14にarchiveされています。

現在のAPI type definitionsは`@nexusmods/vortex-api`へ移行しています。

これはsource freshnessに関するDiagnosticです。

### 4.1 Fact-level evidence register

次の表は、6章のObserved / Verifiedの事実グループへ対応するsource locatorです。

| 製品 | 事実グループ | Source locator | 確認日 | 対象版 |
|---|---|---|---|---|
| MO2 | profile、enabled、priority、plugin order、file conflict | [MO2 repository](https://github.com/Modorganizer2/modorganizer) | 2026-08-14 | v2.5.2 |
| MO2 | Nexus連携とupdate check | [MO2 update-check source](https://github.com/Modorganizer2/modorganizer/blob/master/src/organizer_en.ts) | 2026-08-14 | v2.5.2 |
| MO2 | virtual filesystem | [USVFS](https://github.com/Modorganizer2/usvfs) | 2026-08-14 | v2.5.2 ecosystem |
| MO2 | LOOT、plugin master order、plugin sorting rule | [MO2 v2.5.2 release](https://github.com/Modorganizer2/modorganizer/releases/tag/v2.5.2) | 2026-08-14 | v2.5.2 |
| Wabbajack | manifest、mod version、hash、link | [Wabbajack repository](https://github.com/wabbajack-tools/wabbajack)、[releases](https://github.com/wabbajack-tools/wabbajack/releases) | 2026-08-14 | 4.2.1.4 |
| Wabbajack | README、external file、installation location、list separation | [installation documentation](https://wiki.wabbajack.org/user_documentation/Installing%20a%20Modlist.html) | 2026-08-14 | 4.2.1.4 |
| Vortex | profile、install、deployment、conflict resolution | [Vortex repository](https://github.com/Nexus-Mods/Vortex) | 2026-08-14 | 2.5.0 stable / 2.6.0-beta.1 |
| Vortex | dependency、recommendation、version check event | [Vortex API](https://github.com/Nexus-Mods/vortex-api)、[Vortex Events](https://github.com/Nexus-Mods/vortex-api/blob/master/docs/EVENTS.md) | 2026-08-14 | 2.5.0 stable / 2.6.0-beta.1 |
| Vortex | LOOT、custom rule、group rule、load-order diagnostic | [Vortex load-order rules](https://github.com/Nexus-Mods/Vortex/wiki/MODDINGWIKI-Users-General-Managing-your-Load-Order)、[Vortex FAQ](https://github.com/Nexus-Mods/Vortex/wiki/MODDINGWIKI-Users-FAQ) | 2026-08-14 | 2.5.0 stable / 2.6.0-beta.1 |

この表は、公式sourceを確認した範囲を記録します。

sourceが確認できないcompatibility実例は、9.4節のUncertain入力として別に保持します。

## 5. Prior Art First

ModScopeは、mod-management機能を設計または実装する前に、関連する成熟製品の既存機能を確認します。

毎回3製品すべてを形式的に調査する必要はありません。

対象機能に関連する製品を調査します。

調査では、次を確認します。

1. 既存製品が何を解決しているか。
2. 既存製品が何を十分に解決していないか。
3. ModScopeが重複機能を持つ明確な理由があるか。
4. interoperability、reuse、complementary layerで解決できるか。

実装開始前に、次の問いへ回答できる状態を要求します。

> How do MO2, Wabbajack, and Vortex already solve this, and what specifically remains unsolved for ModScope?

## 6. Prior-art比較

### 6.1 MO2

#### Observed / Verified

- MO2はmod collectionを管理するmod managerです。
- profile、enabled状態、priority、plugin load order、file conflictをmanager側で扱います。
- Nexus連携によりmod downloadとupdate checkを提供します。
- USVFSにより、選択したprocessから見えるvirtual filesystemを提供します。
- v2.5.2 releaseではLOOT support、plugin master order、plugin sorting ruleなどが確認できます。

#### Uncertain

- 汎用mod requirementsを、hard、optional、recommendedなどへ正規化する公開schemaは、今回の公式資料では確認していません。
- local version、Nexus mod version、Nexus file versionの完全なprovenance modelは、今回の公式資料では確定していません。
- 7DTD固有のsemantic conflictまたはruntime compatibilityをMO2 coreが一般化して判定する仕様は、今回の公式資料では確認していません。

#### ModScopeの判断

MO2のprofile、enable、priority、virtual filesystem、launch、manager-side conflict resolutionを置き換えません。

ModScopeは、MO2 sourceをread-onlyで読み取り、Web observationとlocal stateを比較する補完層です。

### 6.2 Wabbajack

#### Observed / Verified

- Wabbajackは、modlist全体を別環境へ再現するautomated Modlist Installerです。
- manifestは、mod、author、version、size、linkなどを保持します。
- archive hashとdownload linkにより、固定されたlistの再現を支えます。
- listごとにREADMEがあります。
- external fileを事前に取得する手順があります。
- installation locationは、game locationや別listと分離します。
- list同士はmergeしません。

#### Inferred

- Wabbajackの主な更新単位は、公開されたlistまたはmanifestです。
- Wabbajackのmanifestは、任意の現在profileを継続的に解析するread modelではありません。
- manifest内のco-presenceは、同時enabled、runtime compatibility、save safetyを証明しません。

#### Uncertain

- 任意のMO2 profileに対するlive compatibility判定の仕様は、今回の公式資料では確認していません。
- manifestが一般的なmod dependency graphとして利用できる範囲は、今回の公式資料では確定していません。

#### ModScopeの判断

Wabbajackのinstaller、distribution、list compilation、archive acquisitionを再実装しません。

manifest、version、hash、link、READMEを、将来のstatic evidence sourceとして扱う余地だけを残します。

### 6.3 Vortex

#### Observed / Verified

- Vortexはprofile、mod installation、deployment、conflict resolutionを統合したmod managerです。
- profileごとにenabled modとconfigurationを保持します。
- dependencyとrecommendationのinstall eventがあります。
- Nexus integrationにversion check eventがあります。
- LOOTとcustom plugin rule、group ruleを使います。
- cyclic ruleをdiagnosticとして表示します。
- stagingとdeploymentを分けます。
- deploymentではmod fileをgame directoryへlinkします。
- purgeではdeployment linkを除去します。
- game extensionは、game version identification、load order validation、file mergeなどを拡張できます。

#### Uncertain

- requirementsのhard、optional、OR条件の意味は、game extensionとmetadataに依存します。
- 7DTD固有のsemantic/runtime compatibilityをVortex coreが一般化して判定する仕様は、今回の公式資料では確認していません。

#### ModScopeの判断

Vortexのprofile、dependency resolver、deployment、manager-side conflict resolution、LOOT sortingを再実装しません。

ModScopeは、Vortexが管理する状態と、Web observation、MO2 local state、source provenanceを同じ比較文脈へ持ち込む層として補完します。

### 6.4 共通比較表

| 対象機能 | MO2 | Wabbajack | Vortex | ModScopeの判断 |
|---|---|---|---|---|
| 正本となるデータ | MO2 instance、profile、mod、download、virtual filesystem | `.wabbajack` manifest、archive hash、link、README | profile state、staged mod、deployment manifest、game extension | 既存製品の正本を置き換えず、read-only evidenceとして参照する |
| versionの保持方法 | local metadataとNexus update check | list version、mod version、hash、link | provider metadataとversion check event | local versionとWeb observed versionを根拠付きで比較する |
| requirements | plugin master、game/plugin check、installer条件 | README、external file、list-specific manual step | dependency、recommendation、required file、tool | 意味を分離し、自由記述だけでhard dependencyを確定しない |
| profile | profile、enabled、priority | list installationの出力に依存 | profile、enabled、configuration | manager profileを操作しない |
| priority / load order | plugin priority、LOOT、game plugin rule | compiled listの再現 | LOOT、plugin rule、group rule | load-order ruleをdependencyやcompatibilityと混ぜない |
| deployment | USVFSによるprocess-local virtualization | installerのinstall output | staging、link deployment、purge | deploymentを再実装しない |
| file overlap | virtual file tree、overwrite表示 | hashと再現性を中心に扱う | conflict resolution、modtype conflict | file overlapをsemantic conflictと分ける |
| freshness | Nexus応答とlocal metadataに依存 | 公開manifestの発行時点に依存 | provider、cache、loginに依存 | observed timeを保持し、共通TTLを推測しない |
| unknown / failure | connection、metadata、manager diagnostic | missing file、download、hash、README step | health check、cycle rule、deployment error | Unknown、Unresolved、Diagnosticを別結果として表示する |
| ModScopeとの重複 | local profile、file、load order | manifest、version、hash、link | dependency、version、conflict、deployment | 重複機能をmanagerとして再実装しない |
| ModScopeに残る領域 | Webとの比較、requirementsの横断 | current local state、runtime evidence | MO2 sourceとのcross-source comparison | provenance、7DTD semantic evidence、runtime evidence、unknownを扱う |
| 判断 | 補完 | 補完 | 補完 | 置き換えではなくcomplementary layer |

## 7. Evidence Model

### 7.1 Unknown is a valid result

情報不足の場合、ModScopeは推測して確定しません。

`Unknown`、`Unresolved`、`Not assessed`は正常な結果です。

次の場合にbooleanへ変換しません。

- source同士が衝突している。
- identityを一意に確定できない。
- versionを安全に比較できない。
- compatibility条件が不足している。
- source claimしか存在しない。
- runtime evidenceがない。

### 7.2 Evidence must be preserved

判定を表示する場合、可能な限り次を保持します。

- raw value
- normalized value
- source
- source locator
- observed time
- provenance
- confidence
- verification level
- unresolved reason
- diagnostic

ユーザーが次を確認できる状態を目指します。

> Why does ModScope believe this?

### 7.3 Do not collapse different meanings

次のversionを同じ概念へ統合しません。

- local version
- Nexus mod version
- Nexus file version
- Wabbajack list version
- game version

次の関係も同じ概念へ統合しません。

- dependency
- compatibility
- incompatibility
- load-order rule
- file overlap
- selection constraint
- semantic conflict
- runtime observation

## 8. Requirements Model

Requirementsは、少なくとも次の意味へ分けて保持します。

| 種類 | 意味 |
|---|---|
| game version | 対象gameのversion条件 |
| hard dependency | 満たさないと対象機能を利用できない依存関係 |
| optional dependency | 条件付きで利用できる依存関係 |
| recommended mod | 作者またはlistが推奨するmod |
| tool / script extender | 外部toolまたはscript extenderの前提 |
| account requirement | loginまたはaccountが必要な条件 |
| hardware requirement | hardware、memory、diskなどの条件 |
| manual step | ユーザーが手動で行う手順 |
| unknown | 種類または条件を確認できない状態 |

Descriptionなどの自由記述だけでhard dependencyを自動確定しません。

DependencyをCompatibilityへ統合しません。

## 9. Compatibility Model

Compatibilityは、単純な`A compatible with B`というbooleanでは扱いません。

`CompatibilityAssertion`は、条件付きのevidence-backed assertionとして扱います。

### 9.1 条件

assertionには、次の条件が関係し得ます。

- mod version
- game version
- patch presence
- patch version
- load order
- save state
- list context
- server / client condition
- source type
- evidence strength
- runtime verification

### 9.2 概念モデル

次の形を概念モデルとして保持します。

```text
CompatibilityAssertion

subject
relation
object / scope

conditions
  mod_versions
  game_version
  patch
  patch_version
  load_order
  save_state
  client_server
  list_context

evidence[]
  source
  locator
  quote / extracted fact
  evidence_class

status
confidence
verification_level
review_state
unresolved_reason
```

`status`、`confidence`、`verification`は別フィールドです。

`status`、`confidence`、`verification`を1つのfieldへ統合しません。

作者がconflictを明示していてもruntime未検証なら、claim confidenceとruntime verificationを別に保持します。

`confirmed compatible`と`confirmed incompatible`は、runtime確認済みと誤解されないよう、製品用語として慎重に定義します。

### 9.3 互換性の4分類

| 分類 | 扱い |
|---|---|
| dependency | 必須、任意、推奨、欠落を保持する。dependency充足をcompatibility保証にしない |
| load-order rule | before、after、priority、cycle、未検証workaroundを保持する |
| file overlap | 同一path、上書き関係、priority、static observationを保持する |
| semantic / runtime evidence | XML patch、target、XPath、runtime result、runtime未検証を保持する |

### 9.4 ユーザー指定のcompatibility実例

次の例は、ユーザー指定資料から受け取った研究入力です。

今回のrepositoryにはsource locatorがありません。

そのため、すべて`Uncertain`として扱います。

以下は設計上のラベルです。各例の実在、成立、または一般性を事実として断定しません。

| 例 | 設計上の意味 | Evidence status | Source locator |
|---|---|---|---|
| Patch-dependent compatibility | patch Pと条件が揃う場合だけcoexistできる関係が必要 | Uncertain | current repository and supplied text do not provide one |
| Conflicting evidence | source claimが衝突する場合に単一booleanへ閉じない | Uncertain | current repository and supplied text do not provide one |
| Untested load-order workaround | conflict claim、possible workaround、untestedを分離する | Uncertain | current repository and supplied text do not provide one |
| Version-scoped conflict | old versionのconflictとlater versionのfixed claimを上書きしない | Uncertain | current repository and supplied text do not provide one |
| Dependency is not compatibility | dependency chainをcompatibility保証にしない | Uncertain | current repository and supplied text do not provide one |
| Startup success is not compatibility | game startedをproper patchの代替にしない | Uncertain | current repository and supplied text do not provide one |
| Manifest co-presence is not runtime evidence | list内のco-presenceをenabled、runtime、save safetyの証拠にしない | Uncertain | current repository and supplied text do not provide one |

URLやsource locatorを推測で追加しません。

## 10. 次のread-only vertical slice

Prior-art調査の結果、次の縦切りを`Installed version vs Web observed version`とします。

### 10.1 入力

Local Knowledge側:

- local mod identity
- raw local version
- source / provenance

Web observation側:

- URL
- title
- raw observed version
- source
- observed_at

### 10.2 出力

- local version
- observed Web version
- comparison result
  - `same`
  - `web_newer`
  - `local_newer`
  - `unknown`
- provenance
- observed_at
- diagnostics

### 10.3 Version comparison rule

安全に比較可能なversion formatだけ上下判定します。

比較不能な形式についてsemverを推測しません。

raw versionを保持します。

初回実装では共通freshness TTLを定義しません。

observed timeを保持・表示します。

### 10.4 Explicitly out of scope

- auto update
- MO2へのwrite
- deployment
- Steam launch
- requirements auto resolution
- compatibility boolean判定
- CLI
- multi-game generalization
- AI integration

## 11. ModScopeのproduct boundary

### Inferred design decision

ModScopeは、MO2、Wabbajack、Vortexを置き換えません。

ModScopeは、local stateとWeb上の観測情報を、provenance付きで比較・理解する層として補完します。

| 判断 | この調査での扱い |
|---|---|
| 置き換え | 既存3製品のmanager、installer、distribution、deployment、manager-side conflict resolutionを置き換えない |
| 補完 | local state、Web observation、requirements evidence、compatibility evidenceをprovenance付きで比較する |
| 保留 | auto update、requirements resolver、compatibility engine、CLI、複数game、AI integrationを実装しない |
| 未確認 | source locatorまたは検証が不足する事項を、存在しない機能または確定した関係として扱わない |

主要な差別化候補は、次のとおりです。

- installed/local versionとWeb versionの比較
- requirements情報の横断
- compatibility evidenceの横断
- 7DTD固有のsemantic evidence
- runtime evidence
- provenance
- observed time
- unknown / unresolved

### 保留

次は、今回の設計判断だけでは実装しません。

- auto update
- Mod Manager化
- Wabbajack installerまたはdistribution
- Vortex deploymentまたはconflict resolution
- requirements resolver
- compatibility engine
- 複数game generalization
- CLI
- AI integration

## 12. Open items

- MO2の汎用requirements schema。
- Wabbajackのlive arbitrary-profile compatibility。
- Vortexの7DTD固有semantic/runtime判定。
- source間の完全なversion provenance。
- ユーザー指定compatibility実例のsource locator。

未確認事項を、機能が存在しないとは記録しません。
