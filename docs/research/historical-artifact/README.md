# Historical artifact research

## Scope

このresearch laneは、7 Days to DieとMO2の実環境で発生する、artifact identity、時間軸、version driftのdirty caseを採掘します。

MO2のinstance、profile、package、ModInfo.xml、meta.ini、download metadataを、MO2のsource of truthとして扱います。
Wabbajackの実ファイルまたはlist fileを、source recordの根拠として扱います。

MO2とWabbajackはread-onlyで扱います。
MO2のprofile、MOD、archive、downloadsは変更しません。
旧binaryは保存しません。
Wabbajackの展開物はcommitしません。

今回のcommitはproduction C#、production schema、Query、Web contract、MO2 write planeを変更しません。

## Pilot status

| 項目 | 状態 |
| --- | --- |
| run ID | run-2026-08-15T16-47-42Z-historical-artifact-pilot |
| target game | SevenDaysToDie |
| MO2 target profile | Default |
| MO2 input | 観測済み。read-only |
| owner-local Wabbajack input | 未提示 |
| existing Wabbajack snapshot | 二次evidenceのみ |
| accepted real case | 0 |
| candidate case | 8 |
| human review limit | case familyごとに最大20件 |
| public manifest refetch | 実施していない |

owner指定のlocal Wabbajack pathが、このrunでは与えられませんでした。
そのため、既存repository snapshotから採掘したcaseはcandidateに留めます。
candidateをaccepted case、current installation、deprecated candidateへ昇格させません。

既存のdocs/research/version/snapshot-2026-08-14は、すでにcommitされたsanitized snapshotです。
このlaneでは、そのrecordを形状確認とcandidate抽出にだけ使います。
公開manifestを再取得しません。

## Evidence boundary

| evidence | role | 保持する事実 |
| --- | --- | --- |
| owner-selected MO2 instance/profile | primary | profile、package、ModInfo、MO2 metadata、local archive metadata |
| owner-selected Wabbajack file/list | primary | list identity、revision、game、archive record、hash、size、source、MOD ID、File ID |
| existing committed Wabbajack snapshot | secondary | candidateの形状と既存観測の参照 |
| public URL | auxiliary | local inputまたはMO2 metadataに含まれる場合だけ参照 |

source pathはopaque source IDと相対locatorへ変換します。
個人path、user name、cookie、token、private URL、raw archiveは保存しません。

## Collection protocol

1. ownerがMO2 instance、target profile、Wabbajack fileまたはlistを明示します。
2. run ID、source ID、target profile、list identity、observed timeを記録します。
3. MO2からmodlist.txt、package directory、ModInfo.xml、meta.ini、明示されたdownload metadataをread-onlyで読みます。
4. Wabbajack fileからlist identity、revision、target game、archive name、size、hash、source、MOD ID、File ID、record versionをstreamで読みます。
5. sourceを、MOD ID + File ID、archive hash、size + archive name、明示package bindingの順でjoinします。
6. nameとfilenameはcandidate生成だけに使います。
7. case familyごとに最大20件を人間reviewへ渡します。
8. sanitization checkがpassするまで、広い範囲の採掘へ進みません。

Wabbajack fileを読めない場合は、formatを推測しません。
diagnosticとして記録します。
ownerが指定していないpathを暗黙に探索しません。

## Taxonomy

### Time axis

- current_installation: 現在のMO2 profileから直接観測したpackage。
- current_source_record: 選択したWabbajack listまたはrevisionに存在するrecord。
- historical_observation: 過去のlist、revision、source recordとして観測したrecord。
- deprecated_candidate: obsolete、removed、deprecatedの明示evidenceがあるcandidate。
- unresolved: currentとhistoricalの関係を安全に確定できない状態。

Wabbajackに存在するだけでは、current installationと判定しません。
古いobserved timeだけでは、deprecated candidateと判定しません。

### Identity state

Exact、Ambiguous、Missing、Conflicting、Unresolvedを使います。

同じMOD IDに複数File IDがある場合は、candidateまたはAmbiguousにします。
packageと複数Modletの関係は、package-level relationに限定します。

### Version comparison

Equal、Mismatch、NotComparable、NotAssessedを使います。

Wabbajack list versionをMOD versionへ変換しません。
低いversionをdeprecated candidateへ変換しません。
not_observableは、値またはsourceが観測できない状態です。
unresolvedは、観測済みだがidentityまたは意味を確定できない状態です。

## Candidate findings

| case | family | identity | version | time | review |
| --- | --- | --- | --- | --- | --- |
| hart-001 | current installation vs historical record | Exact | Mismatch | current + historical | uncertain |
| hart-002 | same MOD ID with multiple File IDs | Ambiguous | NotComparable | unresolved | uncertain |
| hart-003 | stale ModInfo vs newer metadata | Exact | Mismatch | current | uncertain |
| hart-004 | MO2 cache drift | Exact | Mismatch | current | uncertain |
| hart-005 | Wabbajack file ID or hash mismatch | Conflicting | NotComparable | unresolved | uncertain |
| hart-006 | package with multiple Modlets | Unresolved | NotAssessed | current | uncertain |
| hart-007 | missing ModInfo or metadata | Missing | NotAssessed | current | uncertain |
| hart-008 | current or historical axis unresolved | Unresolved | NotComparable | unresolved | uncertain |

この表はcandidateの分類です。
実環境のruntime compatibility、dependency、latest、deprecatedを主張しません。

## このPRで分かったこと

### 確認できた事実

- MO2のDefault profileには、98件のprofile entryがあります。
- 98件のpackage directoryがあります。
- enabled entryは54件です。
- disabled entryは44件です。
- ModInfo.xmlは105件あります。
- package metadataは87件あります。
- download metadataは204件あります。
- dirty caseは8件のcandidateとして整理できました。
- candidateの内訳は、accepted 0件、rejected 0件、uncertain 8件です。
- diagnosticを伴うcandidateは6件です。

### 採掘で分かったこと

- MO2 packageのmeta.ini、download metadata、ModInfo.xmlは、同じversionを示さない場合があります。
- ModInfo.xmlのversionとpackage metadataのversionは、別のversion observationとして保持する必要があります。
- MO2のnewestVersionはcache observationです。
- newestVersionの差だけでは、live latestやupdate availableを確定できません。
- 同じMOD IDに複数File IDがある場合、MOD IDだけではselected fileを確定できません。
- 1つのpackageに複数Modletがある場合、package-level sourceを各Modletへ割り当てられません。
- ModInfo、package metadata、download metadataの欠落は、identityやversionの不在を意味しません。
- Wabbajack list versionは、MOD versionとは別のversion seriesです。

### このPRで確定していないこと

- owner-local Wabbajack fileが未提示のため、Wabbajack側のprimary evidenceは未完了です。
- 既存のWabbajack snapshotは、candidate抽出用のsecondary evidenceです。
- そのため、今回の8件をaccepted real caseへ昇格させていません。
- deprecated、latest、runtime compatibility、dependencyは判定していません。

### ModScopeへの示唆

artifact identity、package identity、Modlet identity、Nexus File identityを分離します。
current installation、current source record、historical observation、deprecated candidateを分離します。
identity stateとversion comparison stateを別々に表示します。
not_observableとunresolvedを別々に保持します。

## Rejected interpretations

- Wabbajack recordの存在だけでinstalledを確定する解釈をrejectします。
- list versionをMOD versionとして比較する解釈をrejectします。
- observed timeだけでdeprecatedを確定する解釈をrejectします。
- versionが低いだけでdeprecatedを確定する解釈をrejectします。
- list co-presenceからdependency、compatibility、runtimeを推定する解釈をrejectします。
- packageに複数Modletがあるだけで、各Modletへ同じNexus Fileを割り当てる解釈をrejectします。
- fuzzy name matchだけでidentityを確定する解釈をrejectします。

## Unknowns and limitations

owner-local Wabbajack fileのpath、list identity、revision、raw recordは未入力です。
そのため、Wabbajack側のprimary evidenceは未完了です。
既存snapshotのpublic-derived recordは、owner-local sourceの代替になりません。

Nexus live latestはこのlaneで取得しません。
MO2 newestVersionはcache observationです。
source identityがresolvedでも、version agreementは保証しません。
file overlapはruntime conflictではありません。
件数はこのsnapshotの観測値です。
件数から一般的なruntime compatibilityまたはdependencyを主張しません。

## Fixtures

匿名synthetic fixtureは、tests/Fixtures/mod-identity/historical-artifact/にあります。
fixtureはraw archiveを含みません。
fixture内のmeta.ini、download metadata、Wabbajack recordはsynthetic valueです。

| fixture | 再現する形 |
| --- | --- |
| case-01-current-vs-historical | current MO2 packageとhistorical Wabbajack record |
| case-02-multiple-file-candidates | 同じMOD IDに複数File ID |
| case-03-stale-modinfo | staleなModInfoと新しいmetadata |
| case-04-bundle-missing-evidence | 複数Modlet packageとmissing evidence |

## Verification

artifacts/sanitization-check.jsonは、absolute path、secret、private URL、raw binary、raw archiveの不在を確認します。
artifacts/snapshot-manifest.jsonは、このdirectoryのartifact hashを記録します。
fixtureのmanifestはJSONとしてparseし、各fixtureにprofile、evidence、synthetic ModInfoを確認します。

owner-local Wabbajack fileが指定された次runでは、source-wj-primary-001をobservedへ更新します。
その後にcandidate reviewを行い、accepted、rejected、uncertain、diagnosticの件数を再計算します。
