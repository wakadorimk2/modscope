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

### GitHub / Nexusの限定release observation

一般的なlatest crawler、汎用site parser、自動更新は採用しません。
現在のDesktop sessionで表示中のWebView2 DOMに限り、GitHub ReleasesとNexus MODのFiles surfaceを観測します。

- GitHubはrelease pageの最初のvisible release tagをrelease versionとして観測します。
- NexusはFiles surfaceの最初のvisible File versionをrelease versionとして観測します。
- network API、login情報、任意siteの推測抽出は使いません。
- 欠落、複数候補、非対応page、非対応versionはversionを確定せず、diagnosticを保持します。
- observationはsource site、対象URL、観測時刻、source referenceとともにsession evidenceへ保存します。
- package identity stateとWeb release association stateは別に保持します。
- package identityが`Ambiguous`でも、page identity、selected local Modlet、installed local context、automatic Web release scopeが確認できればupdate statusを計算します。
- confirmed release associationはMO2 package identityを`Exact`へ変更しません。
- release associationが未確定の場合、update statusは`Not assessed`です。
- release associationがconfirmedで比較可能な場合だけ、`Update available`、`Up to date`、`Installed newer`を表示します。
- game compatibilityは独立軸です。release version observationから互換性を推測しません。

#### release scope

GitHub ReleasesとNexus Filesでは、releaseまたはFile単位のvisible DOM blockをscopeとして保持します。

- GitHub Releasesは、visibleなrelease tag linkと同じrelease blockを1つの`GitHubRelease` scopeとして扱います。
- GitHubの`/releases/tag/...` pageは、URLとvisible page textを1つの`GitHubRelease` scopeとして扱います。
- Nexus Filesは、visibleなFile versionと同じrowまたはcardを1つの`NexusFile` scopeとして扱います。
- Nexus Descriptionは、page全体を`Page` scopeとして扱います。
- scopeは、kind、raw releaseまたはFile version、normalized version、releaseまたはFile URL、matched line、scope内visible textを保持します。
- scopeを確定できないclaimは破棄しません。raw claimへ`web.compatibility.release-scope-unresolved` diagnosticを付けます。
- latest release scopeは、release version observerが返した最初のvisible releaseまたはFile versionと一致するscopeです。
- latest scopeだけをcompatibility conclusionへ使います。過去scopeはhistoryへ残します。
- latest scopeにevidenceがない場合、過去scopeへfallbackしません。
- conflict時にwinnerを自動選択しません。

### GitHub / Nexusの限定Web compatibility observation

release version observationとは別に、現在のDesktop sessionで表示中のWebView2 DOMから互換性labelを観測します。
対象surfaceはGitHub Releases、Nexus MODのFiles surface、Nexus MODのDescription surfaceです。
対象labelは`Game Version`、`Supported Game Version`、`Supported for`、`Compatible with`、`Requires Game Version`です。

- raw line、raw value、normalized game version、optional build、relation、game context、source site、対象URL、観測時刻、diagnosticを保持します。
- `v3.1.0 (b14)`はraw valueを保持し、normalized version `3.1.0`とbuild `b14`へ分離します。
- game名がないlabelは、現在の7DTD adapter contextへ関連付けます。
- 他game名が明示された値はraw evidenceとして保持しますが、7DTD compatibilityへ変換しません。
- `Requires Game Version`はcondition evidenceです。単独ではpositive compatibility statusを生成しません。
- compatibility claimはrelease scopeまたはFile scopeへ関連付けます。scopeの異なるclaimを同じ階層で比較しません。
- positive targetが1つだけの場合はsource claimとして`Observed`を表示します。
- positive targetが複数あり内容が異なる場合はwinnerを選ばず、`Unknown`とconflict diagnosticを保持します。
- `Observed`はsource claimの観測結果です。runtime compatibilityを保証しません。
- network API、login情報、汎用site parser、自動更新は使いません。

観測はpage navigationと既存の`Observe now`で実行します。
既存の手動Web version入力はAdvanced evidenceのfallbackとして残します。

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
