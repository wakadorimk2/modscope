# Community trust conditions research memo

## 1. 文書の役割

この文書は、7 Days to Die（7DTD）MODコミュニティの公開発言、公開mod authorの知見、および既存のModScope調査を、v0.1の検証候補へ変換するresearch memoです。

この文書は製品仕様の正本ではありません。

製品仕様の正本は、[`docs/design.md`](../design.md)と[`docs/future-vision.md`](../future-vision.md)です。

この文書でいう「信用」は、数値的な信頼度ではありません。

主張のidentity、scope、source、observed time、verification level、未確認事項を追跡できる状態を指します。

この文書は、次の5段を使います。

```text
Concern
  -> Trust requirement
  -> Failure case
  -> Expected behavior
  -> Acceptance test candidate
```

`Acceptance test candidate`は候補です。

合格済みテストを意味しません。

## 2. 調査範囲と境界

- 観測日：2026-08-16
- 観測時刻：2026-08-16T01:50:37+09:00
- 対象：7DTD、MO2、Local Mod Knowledge
- v0.1の焦点：identity、version、source freshness、compatibility、dependencyの誤判定防止
- runtime保証、save破壊判定、write操作は対象外
- Source registerと5段の記録はappend-onlyで追加します。後続の観測は、新しいrecordとobserved timeを追加します。

公開コミュニティ発言は、技術的事実へ直接昇格させません。

公開mod authorの知見は、mod authorによるdomain inputとして保存します。

既存のModScope調査は、外部sourceの代替ではありません。

既存調査は、ModScopeが採用したevidence boundaryとsynthetic fixtureへの対応を示します。

`verified`は、sourceまたはrepository recordにその記録が存在することを示します。

sourceの主張が7DTD runtimeで正しいことを保証しません。

`inferred`は、複数のsourceまたは既存設計から導いたModScope向けの設計判断です。

`uncertain`は、今回のsourceまたは観測だけでは確定できない事項です。

## 3. Source register

`observed_at`は、このmemoでsourceを確認した時刻です。

sourceの公開日時、更新日時、observed timeを同じ値として扱いません。

### S1：7DTD Mod Structure

- Source URL：[7 Days to Die Wiki - Mod Structure](https://7daystodie.wiki.gg/wiki/Mod_Structure)
- Source kind：公式wikiの技術資料
- Author / role：wiki contributors。個人著者は今回の観測で確定できません。
- Source date：安定した更新日時を今回の観測で確定できません。
- `observed_at`：2026-08-16T01:50:37+09:00
- Short summary：`Mods`配下のmod folder、`ModInfo.xml`、`Config`、`Resources`などの構造を説明します。`ModInfo.xml`をMOD認識に必要なファイルとして説明します。`Version`の例も示します。
- Evidence status：`verified`（ページ本文の記載を確認）。
- Limitations：wikiの説明は個別MODのruntime検証ではありません。Nexus File、MO2 package、Modletのidentity対応も証明しません。

### S2：2026 mod status tracker

- Source URL：[7 Days to Die Mods in 2026: Active, Stagnant or Dead](https://wiki.7d2d.net/mods/mod-status-2026/)
- Source kind：community-maintained status tracker
- Author / role：7D2D Wikiの編集ページ。個人著者は今回の観測で確定できません。
- Source date：ページ表示の更新日は2026-08-01です。
- `observed_at`：2026-08-16T01:50:37+09:00
- Short summary：modのlatest build、対応game version、状態を表にします。古いversionであることと、MODが悪いことを分けます。versionはprimary sourceで確認し、確認できない場合は推測せずに表示すると説明します。
- Evidence status：`verified`（ページの説明と表を確認）。
- Limitations：trackerはsecondary sourceです。`Active`や`Safe to run`はsource claimです。個別環境のruntime matrixやsave safetyを証明しません。statusは更新で変わります。

### S3：7d2d Modding community discussion

- Source URL：[Reddit r/7daystodie - 7d2d Modding](https://www.reddit.com/r/7daystodie/comments/1v54ev9/7d2d_modding/)
- Source kind：公開community discussion
- Author / role：community discussion author。7DTD modding hubの作成者として投稿しています。
- Source date：ページは投稿時期を「3 weeks ago」と表示します。正確な投稿日は今回の観測で確定できません。
- `observed_at`：2026-08-16T01:50:37+09:00
- Short summary：古いforum、dead link、未完成documentに情報が分散する問題を説明します。mod watcherでlink、version、imageを更新する構想を示します。current versionに保つことを重視し、古い情報はAssembly-CSharpなどでcross-checkするよう勧めます。
- Evidence status：`verified`（投稿本文とコメントの記載を確認）。技術的な提案の一般性は`uncertain`です。
- Limitations：投稿者の設計と意見です。watcherの完全な動作、Assembly-CSharpを通した正しさ、全MODへの適用範囲は確認していません。

### S4：Vortexの7DTD load-order issue

- Source URL：[Vortex issue #3259 - Enhancement: 7 Days to Die Load Ordering](https://github.com/Nexus-Mods/Vortex/issues/3259)
- Source kind：Prior Art issueとcommunity request
- Author / role：issue reporter。Vortex issueの投稿者です。
- Source date：2019-02-14にopen。今回の観測ではissueはclosedです。
- `observed_at`：2026-08-16T01:50:37+09:00
- Short summary：Alpha 17の記録として、modletはalphabeticalにloadされ、folder renameがload order変更手段だったと説明します。`ModInfo.xml`のName、description、version、XPath processing、load order依存、conflictの例を記録します。
- Evidence status：`verified`（issue本文に記録されたhistorical claimを確認）。
- Limitations：歴史的なissue本文です。現在のVortex実装、現在の7DTD load order、個別MODのruntime結果を保証しません。issue本文はforum postの引用を含みます。

### S5：Public mod-author troubleshooting knowledge

- Source URL：[Basic troubleshooting for mods](https://www.nexusmods.com/7daystodie/articles/787)
- Source kind：mod authorによる公開troubleshooting guide
- Author / role：公開mod author。7DTD mod authorです。
- Source date：2024-07-16に追加。2026-07-14に編集と表示されます。
- `observed_at`：2026-08-16T01:50:37+09:00
- Short summary：logにはgame version、loaded mods、load order、mod version、warning、errorが現れると説明します。full recent logを共有し、少量ずつまたはsplit-halfで切り分ける手順を示します。description、comments、load order、conflict、new save/world、backupの確認も扱います。
- Evidence status：`verified`（公開記事の記載を確認）。技術的な一般化は`inferred`または`uncertain`です。
- Limitations：個人のguideと事例です。全MODのruntime保証ではありません。公開logには個人情報やIP addressが含まれ得るため、共有前のsanitizationが必要です。

### M1：Identity and version provenance ADR

- Source URL / locator：[docs/adr/identity-and-version-provenance.md](../adr/identity-and-version-provenance.md)
- Source kind：ModScope internal accepted ADR
- Author / role：ModScope design record
- Source date：2026-08-14
- `observed_at`：2026-08-16T01:50:37+09:00
- Short summary：Archive、MO2 Package、Modlet、Nexus Mod、Nexus Fileを分離します。`meta.ini`を利用できる場合のlocal provenance anchor、identityを先に解決する順序、`NotComparable`、`ConflictingSources`、source timeの保持を定義します。
- Evidence status：`verified`（repository recordを確認）。内容はModScopeのaccepted designです。
- Limitations：このADRは外部ecosystemのruntime保証ではありません。今回のmemoは、このADRを変更しません。

### M2：Requirements observation model ADR

- Source URL / locator：[docs/adr/requirements-observation-model.md](../adr/requirements-observation-model.md)
- Source kind：ModScope internal accepted ADR
- Author / role：ModScope design record
- Source date：2026-08-14
- `observed_at`：2026-08-16T01:50:37+09:00
- Short summary：`Source -> Requirement Observation -> Identity Resolution -> Relationship Classification -> Requirement Assertion / Dependency Edge`の順序を定義します。name aloneとlist co-presenceはdependency edgeを生成しないと定めます。
- Evidence status：`verified`（repository recordを確認）。内容はModScopeのaccepted designです。
- Limitations：このADRはgeneric dependencyのruntime truthではありません。未観測を不在として扱いません。

### M3：Compatibility findings

- Source URL / locator：[docs/research/compatibility/findings.md](compatibility/findings.md)
- Source kind：ModScope research snapshot summary
- Author / role：ModScope research record
- Source date：2026-08-14
- `observed_at`：2026-08-16T01:50:37+09:00
- Short summary：source claim、static observation、runtime observationを分けます。`confirmed_compatible`と`confirmed_incompatible`は保存source claimの極性であり、7DTD runtime successではないと明記します。load-order workaround、conflicting evidence、co-presenceを別に保持します。
- Evidence status：`verified`（repository recordを確認）。このmemoの設計入力です。
- Limitations：snapshotのsource範囲と観測日時に依存します。runtime verificationは未実施です。

### M4：Version snapshot findings

- Source URL / locator：[docs/research/version/snapshot-2026-08-14/findings.md](version/snapshot-2026-08-14/findings.md)
- Source kind：ModScope local-evidence research snapshot
- Author / role：ModScope research record
- Source date：2026-08-14T10:01:09Z
- `observed_at`：2026-08-16T01:50:37+09:00
- Short summary：Smorgasbord list version、Collection revision、`ModInfo.xml` version、Nexus file、game versionなどを別roleとして扱います。Nexus live fetch blocked、MO2 cache drift、同一Nexus MOD IDで異なるfile ID、`newestVersion` cacheを記録します。
- Evidence status：`verified`（repository recordを確認）。数値はsnapshotの観測値です。
- Limitations：snapshotは現在のWeb状態を意味しません。Nexus live latestを確定していません。数値は一般的なruntime保証ではありません。

## 4. Community concernから受入候補への変換

次の表の`Expected behavior`はModScope側の`inferred`な設計判断です。

`Concern`のsource claimと、ModScopeの推論を同じ事実として扱いません。

| ID | Concern | Trust requirement | Failure case | Expected behavior | Acceptance test candidate | Source / existing mapping |
|---|---|---|---|---|---|---|
| CT-01 | 同名または類似名のpackage、Modlet、Nexus候補があります。名前だけではidentityを一意にできません。 | identityには、local path、MO2 metadata、`modID + fileID`、または明示されたsource locatorを使います。name similarityは候補化だけに使います。 | name-only matchで1件を選び、`AutoResolved`を出します。 | raw nameと候補を保持します。identityは`Unresolved`または`HumanReview`にします。`AutoResolved`を生成しません。**inferred** | `duplicate-modlet-name`と`ambiguous-meta`で、name-only入力が`AutoResolved`を出さず、候補と理由を保持することを確認します。候補です。合格済みテストではありません。 | M1、M2、[`duplicate-modlet-name`](../../tests/Fixtures/mod-identity/duplicate-modlet-name/evidence/manifest.json)、[`ambiguous-meta`](../../tests/Fixtures/mod-identity/ambiguous-meta/evidence/manifest.json) |
| CT-02 | 1つのpackageに複数のModletが含まれます。Modletごとにversionやsource対応が異なる場合があります。 | `SourceArtifact -> MO2Package -> Modlet(s)`を分離します。packageのsource identityを、child Modletのidentityへ自動コピーしません。 | 1 packageのNexus Fileまたはpackage versionを、すべてのModletへ割り当てます。 | packageと各Modletを別recordで保持します。Modlet単位のNexus Fileを証拠なしに推測しません。対応が閉じない場合は`Unresolved`または`PartiallyResolved`、versionは`NotComparable`にします。**inferred** | `multi-modlet-package`の17 Modletで、packageを1 Modletへ圧縮しないことを確認します。1つのpackage versionを全childへ割り当てないことを確認します。候補です。 | S1、M1、[`multi-modlet-package`](../../tests/Fixtures/mod-identity/multi-modlet-package/evidence/manifest.json) |
| CT-03 | 同一Nexus `modID`に複数の`fileID`があります。`modID`だけでは選択Fileを決められません。 | release associationには、selected `fileID`、source URL、observed timeを保持します。`modID`単独をFile identityにしません。 | 別の`fileID`をlatestとみなし、`UpdateAvailable`を表示します。 | candidate Fileを並べます。identityは`Unresolved`または`HumanReview`にします。release/update statusは`Not assessed`または`NotComparable`にします。`UpdateAvailable`を出しません。**inferred** | `same-nexus-mod-multiple-files`で、同じ`modID`の3 File候補から自動選択しないことを確認します。`UpdateAvailable`を出さないことを確認します。候補です。 | M1、M4、[`same-nexus-mod-multiple-files`](../../tests/Fixtures/mod-identity/same-nexus-mod-multiple-files/evidence/manifest.json) |
| CT-04 | list version、Collection revision、game version、Modlet versionは、似た数値でも意味が異なります。 | versionごとにsource role、raw value、normalized value、comparison scopeを保持します。同じroleだけを比較します。 | list versionやCollection revisionをMOD versionと比較します。game versionをMOD release versionと比較します。 | 比較不能な組合せを`NotComparable`にします。各version observationを別表示します。数値の近さで一致や更新を断定しません。**inferred** | Version snapshotのlist version `3.1.1.39`、Collection revision `39`、`ModInfo.xml` version、game versionを別roleで検査します。list/collection/game roleを含むsynthetic fixtureは未作成です。`version-role-mixing`を追加fixture候補として記録します。候補です。 | M1、M4、[`version snapshot findings`](version/snapshot-2026-08-14/findings.md)、[`identity fixture README`](../../tests/Fixtures/mod-identity/README.md) |
| CT-05 | 同一identityかつ同一version roleに、異なるsource claimが存在します。cache、`ModInfo.xml`、MO2 metadata、Wabbajack recordが一致しない場合があります。 | source claimをsource URL、locator、observed timeとともに保持します。winnerを自動選択しません。 | recency、数値の大小、source kindだけで1つのversionを正とします。 | `ConflictingSources`にします。必要な場合は`HumanReview`を要求します。比較結果を`NotComparable`へ落とす理由を表示します。`Trusted`状態やtrust scoreを追加しません。**inferred** | 同一identity・同一roleの異なるversion claimを持つ`same-identity-same-role-conflict` fixtureを追加候補にします。既存の[`conflicting-evidence`](../../tests/Fixtures/compatibility/06-conflicting-evidence.json)は、異なるdeployment contextでwinnerを自動選択しない回帰入力です。候補です。 | M1、M3、M4、[`conflicting-evidence`](../../tests/Fixtures/compatibility/06-conflicting-evidence.json) |
| CT-06 | MO2のcached `newestVersion`は、過去の取得値です。live sourceが欠落または取得不能のことがあります。 | source kind、observed time、cache/live状態、diagnosticを保持します。latestはlive evidenceがある場合だけ扱います。 | cached `newestVersion`または古いsnapshotを、現在のlatestとして表示します。 | `newestVersion`をcache observationとして表示します。live sourceがない場合、latestと`UpdateAvailable`を断定しません。statusは`Not assessed`にします。**inferred** | Version snapshotの`blocked_cloudflare`とcache driftを入力にして、live latestがない場合に`Not assessed`となることを確認します。`cached-latest-with-live-missing`を追加fixture候補にします。候補です。 | S2、S3、M4、[`version snapshot findings`](version/snapshot-2026-08-14/findings.md)、[`mismatch cases`](version/snapshot-2026-08-14/mismatch-cases.md) |
| CT-07 | compatibility label、author description、status trackerの「safe」表示があります。これらはruntime testの代わりになりません。 | source claim、static observation、runtime observationを分離します。game version、mod version、patch、load order、runtime verificationを別fieldで保持します。 | labelだけで`confirmed compatible`またはruntime guaranteeを表示します。 | labelはsource claimまたは`Observed`として表示します。runtime evidenceがない場合は`Not assessed`にします。source claimをruntime保証へ変換しません。**inferred** | compatibility fixtureの`runtime_verified: false`を基準に、source claimだけでruntime successを表示しないことを確認します。`unknown-co-presence`と`fallback-not-compatibility`を使います。候補です。 | S2、S4、S5、M3、[`compatibility fixture README`](../../tests/Fixtures/compatibility/README.md)、[`unknown-co-presence`](../../tests/Fixtures/compatibility/07-unknown-co-presence.json)、[`fallback-not-compatibility`](../../tests/Fixtures/compatibility/08-fallback-not-compatibility.json) |
| CT-08 | name similarity、list co-presence、separator textがあります。これらはdependency identityやdependency meaningを確定しません。 | raw textを`Requirement Observation`として保持します。identity resolutionとrelationship classificationの後だけ`Dependency Edge`を生成します。 | 同じlistに存在するだけでdependency edgeを生成します。似た名前だけでtarget identityを確定します。 | raw target、source URL、locator、relation wording、observed timeを保持します。targetが未解決なら`Unresolved`にします。name similarityとco-presenceだけでは`Dependency Edge`を生成しません。**inferred** | `separator-with-selection-constraint`、`unknown-co-presence`、name similarity caseで、raw observationは残し、dependency edgeを生成しないことを確認します。候補です。 | M2、M3、[`separator-with-selection-constraint`](../../tests/Fixtures/mod-identity/separator-with-selection-constraint/evidence/manifest.json)、[`requirements findings`](requirements/findings.md) |
| CT-09 | 7DTDのMOD認識に`ModInfo.xml`が関係します。packageに欠落、誤配置、または解析不能な`ModInfo.xml`が存在します。 | `ModInfo.xml` presence、path、raw field、parser diagnosticを保持します。directory nameやarchive nameを代替identityにしません。 | `ModInfo.xml`がないpackageを有効なModletとして扱います。archive nameからversionを補完します。 | package recordとdiagnosticを保持します。Modlet identityは`Unresolved`、versionは`NotComparable`にします。推測したversionを出しません。**inferred** | `missing-modinfo`で、Modlet recordを作らない場合でもpackage diagnosticが残ることを確認します。`Unresolved`と`NotComparable`を表示します。候補です。 | S1、M1、[`missing-modinfo`](../../tests/Fixtures/mod-identity/missing-modinfo/evidence/manifest.json)、[`identity fixture README`](../../tests/Fixtures/mod-identity/README.md) |
| CT-10 | load orderはfolder order、MO2 priority、source記載のcondition、runtime effectで意味が異なります。公開mod-author troubleshooting guideとVortex issueはload orderの重要性を示します。 | declared load-order claim、current local order、runtime observationを別に保持します。load orderをdependencyやcompatibilityへ自動変換しません。 | 「load last」やalphabetical orderのsource claimだけで、現在のprofileが正常と断定します。load-order workaroundを検証済みと表示します。 | source claimはscope付きで表示します。current local orderまたはruntime evidenceがない場合は`Not assessed`にします。conflict resolutionを自動実行しません。**inferred** | `03-load-order-workaround-untested`と公開mod-author troubleshooting guideのload-order記載を使い、未検証workaroundをruntime verifiedにしないことを確認します。current local orderを含む追加fixtureは必要に応じて追加候補にします。候補です。 | S4、S5、M3、[`load-order-workaround-untested`](../../tests/Fixtures/compatibility/03-load-order-workaround-untested.json)、[`compatibility findings`](compatibility/findings.md) |

## 5. ADRとfixtureへの対応

このmemoの受入候補は、既存のADRとsynthetic fixtureへ次のように対応します。

| Concern IDs | Existing ADR / research | Existing fixture coverage | Additional fixture candidate |
|---|---|---|---|
| CT-01 | [Identity and version provenance ADR](../adr/identity-and-version-provenance.md)、[Requirements observation model ADR](../adr/requirements-observation-model.md) | `duplicate-modlet-name`、`ambiguous-meta` | なし |
| CT-02 | [Identity and version provenance ADR](../adr/identity-and-version-provenance.md) | `multi-modlet-package` | なし |
| CT-03 | [Identity and version provenance ADR](../adr/identity-and-version-provenance.md)、[Version snapshot findings](version/snapshot-2026-08-14/findings.md) | `same-nexus-mod-multiple-files` | なし |
| CT-04 | [Identity and version provenance ADR](../adr/identity-and-version-provenance.md)、[Version snapshot findings](version/snapshot-2026-08-14/findings.md) | snapshot observationのみ | `version-role-mixing`（list version、Collection revision、game version、Modlet version） |
| CT-05 | [Identity and version provenance ADR](../adr/identity-and-version-provenance.md)、[Compatibility findings](compatibility/findings.md) | `conflicting-evidence`は異なるdeployment contextのconflict | `same-identity-same-role-conflict` |
| CT-06 | [Version snapshot findings](version/snapshot-2026-08-14/findings.md)、[Version mismatch cases](version/snapshot-2026-08-14/mismatch-cases.md) | snapshot observationのみ | `cached-latest-with-live-missing` |
| CT-07 | [Compatibility findings](compatibility/findings.md) | `unknown-co-presence`、`fallback-not-compatibility` | なし |
| CT-08 | [Requirements observation model ADR](../adr/requirements-observation-model.md)、[Requirements findings](requirements/findings.md) | `separator-with-selection-constraint`、`unknown-co-presence` | なし |
| CT-09 | [Identity and version provenance ADR](../adr/identity-and-version-provenance.md) | `missing-modinfo` | なし |
| CT-10 | [Compatibility findings](compatibility/findings.md) | `load-order-workaround-untested` | current local orderとruntime absenceを同時に表すfixtureが必要になった場合に追加 |

追加fixture候補は、このmemoでは作成しません。

候補を実装へ昇格する場合は、source locator、raw claim、observed time、expected status、未確認事項を先に定義します。

## 6. 採用する境界と採用しないもの

### 採用候補

- `ModInfo.xml`、MO2 `meta.ini`、download metadata、Nexus、Wabbajack、game versionを別observationとして扱います。
- identityを先に解決します。
- version roleを保持してから比較します。
- source freshnessとcache状態を表示します。
- source claim、static observation、runtime observationを分けます。
- `Requirement Observation`と`Dependency Edge`を分けます。
- `AutoResolved`、`HumanReview`、`PartiallyResolved`、`Unresolved`、`NotComparable`、`ConflictingSources`、`Not assessed`を既存の意味で使います。

### 採用しないもの

- 新しい`Trusted`状態
- 数値trust score
- name similarityだけのidentity確定
- packageからModlet単位のNexus File推測
- `modID`だけを使ったFile identity確定
- list version、Collection revision、game versionとMOD versionの無条件比較
- cached `newestVersion`だけからのlatestまたは`UpdateAvailable`断定
- compatibility labelだけからのruntime保証
- name similarityまたはco-presenceだけからのdependency edge
- runtime保証、save破壊判定、write操作

## 7. Memo review checklist

このmemoを後続のADRまたは実装testへ昇格する前に、次を確認します。

| Check | 判定条件 |
|---|---|
| 5段構成 | 各`CT-*`に`Concern`、`Trust requirement`、`Failure case`、`Expected behavior`、`Acceptance test candidate`がある。 |
| Source reverse lookup | 各`CT-*`のSource IDまたはlocatorをSource registerから逆引きできる。 |
| Evidence boundary | `verified` source claim、`inferred` product behavior、`uncertain` limitationを混同しない。 |
| Existing status | 新しいstatusやscoreを導入せず、既存statusの意味を使う。 |
| Fixture traceability | 既存fixture、snapshot observation、または追加fixture候補のいずれかを記録する。 |
| Candidate label | `Acceptance test candidate`を合格済みtestとして記述しない。 |

このreview checklistの確認は文書構造の確認です。

7DTD runtimeでの動作確認ではありません。
