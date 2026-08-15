# ADR: Compatibilityをevidence-backed assertionとして扱う

- Status：Accepted
- Date：2026-08-14
- Scope：Compatibility、Requirements、Versionの根拠表現

## Context

Compatibilityをtrueまたはfalseへ圧縮すると、patch、version、load order、save state、selection constraint、deployment contextが失われます。

今回の調査では、次の状態が同じMOD pairまたは同じcategoryに現れました。

- 明示的なconflict
- patchを条件にしたsupport
- 未検証のload-order workaround
- 旧versionのconflictと後続versionのfix
- dependency
- conflicting source claim
- manifest co-presenceだけのunknown
- startup fallback

## Decision

ModScopeは、Compatibilityをevidence-backed assertionとして扱います。
Compatibility Graphやruntime判定機能は、この変更では実装しません。

1つのassertionは、少なくとも次の概念を別々に持ちます。

| 概念 | 意味 | 意味しないもの |
| --- | --- | --- |
| relation | requires、conflict、patch requirement、load-orderなどの関係 | 関係の条件や証拠 |
| condition | version、patch、load order、save、selection、environmentなどの成立条件 | 無条件のruntime保証 |
| evidence | source URL、quote、locator、manifest observationなどの根拠 | 根拠のない推測 |
| confidence | 保存したclaimまたはobservationへの信頼度 | runtime成功率 |
| verification | source claim、static observation、runtime observationのどこまでを確認したか | confidenceの代替 |
| unresolved_reason | identity、version、runtime、scopeなどが未確定である理由 | 空のunknown |

## 固定する意味規則

- requiresはdependencyです。Compatibility assertionへ変換しません。
- choose oneはselection constraintです。pairwise conflictへ変換しません。
- load last might fix ... but is untestedはconditionalです。confirmed compatibleへ変換しません。
- old versionのconflictとnew versionのfixは、version scopeを分けて保存します。
- co-presenceはmembership evidenceです。runtime compatibilityではありません。
- file overlapはruntime conflictではありません。
- Wabbajack directiveはlist build observationです。patch blobとauthor intentを確認するまでcompatibility patchではありません。
- conflicting evidenceは、deployment contextごとに並列保持します。
- Unknownは正常なterminal resultです。空欄で表しません。
- runtime_verifiedは、実際のgameまたはserver/client runtime observationがある場合だけtrueにします。

現在のDesktop vertical sliceでは、GitHub Releases、Nexus Files、Nexus Descriptionのvisible DOMだけを限定観測します。
`Game Version`などの明示labelはrelease version observationと分離したWeb compatibility evidenceとして保持します。
`Requires Game Version`はcondition evidenceであり、positive compatibility assertionへ自動変換しません。
positive targetが複数あり内容が異なる場合はwinnerを選択せず、conflictとしてUnknownを返します。
Web source claimの`Observed`はruntime compatibilityの保証ではありません。

confirmed_compatibleとconfirmed_incompatibleは、保存したsource claimの確認を示します。
ModScopeがruntimeを確認していない場合、runtime guaranteeとして表示しません。

## Alternatives considered

### Boolean compatibility

不採用です。
条件と証拠の違いを失います。

### Manifest co-presenceをpositive evidenceにする

不採用です。
archive membership、enabled state、runtime successを区別できません。

### 先に汎用Compatibility Graphを実装する

保留します。
現在はassertionの意味と証拠境界を固定する段階です。
実装は、必要なqueryとruntime検証が確定してから追加します。

## Consequences

### Positive

- 人間がsource claimとruntime resultを区別できます。
- version、requirements、compatibilityを同じevidence disciplineで扱えます。
- conflicting evidenceを失いません。
- unknownの理由をInspectorへ渡せます。
- 将来のGraph、Query、UIへ安全に接続できます。

### Cost

- 1つのpairに複数assertionが必要です。
- conditionとevidenceの表示量が増えます。
- runtime verificationがない結果は、確定表示できません。

## Unresolved

- 7DTDの実runtime matrix
- Wabbajack patch blobの意味解析
- package、archive、Modletのidentity対応
- version-specific assertionの正規化規則
- 将来のpublic APIまたは永続schema
