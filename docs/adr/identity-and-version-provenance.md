# ADR: Identity and version provenance

- Status：Accepted
- Date：2026-08-14
- Scope：Local Mod Knowledge、identity resolution、version comparison

## Context

ModScopeLabの実環境では、archive、MO2 package、Modlet、Nexus Mod、Nexus Fileの件数と対応関係が一致しませんでした。
1 packageに17 Modletが入るケースもありました。
Nexus `modID`と`fileID`は取得できても、`.meta Version`は取得できませんでした。

その後のfollow-upでは、download archive側の`.meta`とは別に、MO2 packageの`meta.ini`を読み取りました。
`meta.ini`を利用できるrecordでは、fuzzy name matchingより強いlocal provenance evidenceを得られました。

この状態でversion文字列だけを比較すると、比較不能を一致または不一致として誤表示します。

## Decision

### 管理単位を分離する

次のentityを同一視しません。

> Archive != MO2 Package != Modlet != Nexus Mod != Nexus File

packageからModletへの関係は一対多を許可します。
`ModInfo.Name`やarchive名だけでstable identityを決めません。

ModScopeでは、次のidentityを分離します。

- `SourceArtifact`：取得元のNexus fileまたは配布artifact
- `MO2Package`：MO2が管理するlocal packageとpackage provenance
- `Modlet`：package内の7DTD runtime側のModlet

Nexus identityは、個々のModletではなく、取得済みのpackageまたはartifactへ主に所属させます。
1 packageに複数Modletが含まれる場合、各Modletに固有のNexus fileを推測しません。

利用可能な場合、MO2 packageの`meta.ini`をprimary local provenance anchorとして扱います。
明示的なprovenance metadataを、名前の類似によるfuzzy matchingより優先します。

### identityを先に解決する

version comparisonは、local Modletまたはpackageと、対象Nexus ModまたはNexus Fileの対応を解決した後に実行します。

> Resolve identity before comparing versions.

identityがmissingまたはambiguousの場合は、version parserで補完しません。

identityがresolvedでも、version observationが一致するとは限りません。
`ModInfo.xml` versionとMO2 package `meta.ini` versionは別のversion observationとして保持します。

### 比較不能を一致と扱わない

次の状態は`Unknown`または`not-comparable`として保持します。

- `.meta Version`が欠落している
- 対応するNexus Fileが複数ある
- `ModInfo.xml`が欠落している
- local Modletと配布元fileを一意に対応付けられない

`version source mismatch = 0`は、version一致の証拠ではありません。

### separatorは補助evidenceとして扱う

MO2 separator textは、requirements、選択制約、運用注意の候補を発見するsourceです。
raw text、source reference、provenanceを保持します。

separator textから、enable、dependency、compatibility、runtime behaviorを自動断定しません。

### evidence区分を維持する

raw、normalized、static evidence、inference、uncertainty、diagnostic、provenanceを混ぜません。
未知のmetadataや候補は破棄せず、diagnosticとともに保持します。

## Consequences

- version comparisonの前にidentity resolutionが必要になります。
- unresolved、ambiguous、not-comparableをfirst-class resultとして扱います。
- archive名とpackage名による簡易version判定は採用しません。
- MO2 package `meta.ini`を利用できる場合は、local provenanceの優先sourceとして扱います。
- identity resolution済みのrecordでも、version mismatchを別の結果として保持します。
- synthetic fixtureで、実環境の多対一および一対多の形を回帰検証できます。

## Rejected alternatives

- archive名をNexus File identityとして扱う
- `ModInfo.Name`をglobal identityとして扱う
- `.meta Version`欠落時にversion一致とみなす
- separator textをmachine-authoritative dependencyとして扱う

## Evidence

- [Smorgasbord local inventory](../research/snapshots/2026-08-14-smorgasbord/local-inventory.md)
- [Synthetic identity fixtures](../../tests/Fixtures/mod-identity/README.md)
