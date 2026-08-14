# Findings: local dependency identity resolution

観測日時: `2026-08-14T10:46:58Z`

## 結論

**観測事実:** Web-only baselineの `unresolved_target_identity` は33観測でした。

**観測事実:** ローカルMO2 treeには package、Modlet、archive、`.meta`、README/XML/config/DLLを観測できる入力がありました。

**観測事実:** 依頼パス `downloads/smorgasbord` は存在しませんでした。MO2が示す nested path `wabbajack/smorgasbord/downloads` は存在しました。

**観測事実:** local resolutionのstatusは次のとおりです。`{"ambiguous": 2, "not_applicable": 18, "not_found_locally": 9, "partially_resolved": 2, "resolved": 2}`。

**観測事実:** target kindの許容値は `mod`、`framework`、`game`、`tool`、`environment`、`manual_step`、`unresolved_reference` です。この33観測では `mod`、`environment`、`manual_step`、`unresolved_reference` を観測しました。framework、game、toolのtargetはこのcohortでは観測しませんでした。

**推測:** local scanは、Web-onlyでは未解決だったtargetの一部を、少なくともMO2 package / Modlet候補まで進められます。

**不確実:** target raw name自体が誤抽出の場合、local scanで名前が見つかってもdependencyとは断定できません。

## 1. Nexus構造化Requirementsで自動化できる範囲

- Nexus structured Requirementsはsourceとrelationshipを保持しやすい情報です。
- ただし、structured rowにLanguage、翻訳作者、説明文断片が混ざる場合があります。
- structured sourceでも、target Nexus mod IDがない場合はlocal identity resolutionが必要です。

## 2. Descriptionが必要になる範囲

- Descriptionはframework、game version、client-side、new saveなどの環境条件を補います。
- Descriptionの自然言語は、依存関係と機能説明を混同しやすいです。
- `requires` の主語と目的語を同じ文から抽出し、source excerptを保存する必要があります。

## 3. Local archive解析で追加できる情報

- `.meta`はNexus mod ID / file IDを直接提供します。
- archive filenameはpackageとの対応候補を提供します。
- ModInfo.xmlはModlet Name / DisplayName / Versionを提供します。
- README、XML/config、DLL名は補助evidenceです。
- XML/configやHarmonyフォルダの存在だけではhard dependencyになりません。

## 4. Nexus IDによるgraph構築可能性

- Nexus mod IDをnode identityにできます。
- Nexus file IDをarchive/version evidenceにできます。
- package→archiveの名前リンクが一意でない場合、IDをpackageへ自動伝播してはいけません。
- 推奨edgeには、`source`, `evidence`, `confidence`, `relationship_type`, `resolution_status`を必須にします。

## 5. Smorgasbord内存在確認の限界

- local treeに存在することは、依存関係を意味しません。
- disabled packageも存在します。`in_smorgasbord` と `enabled_state`を分けます。
- requested download directoryが存在しないため、入力パスの再現性を別記録します。
- archive形式によっては、インストール済みModInfoは読めてもarchive内READMEは未観測です。

## 6. Version constraintの実用性

- `.meta`のfile IDはファイルidentityです。version constraintの意味とは別です。
- ModInfo Version、archive filename、READMEのversionは別sourceとして保存します。
- version constraintはrawとnormalizedを分け、比較できない場合は未解決にします。

## 7. False positive防止

- exact ModInfo identityを最も強いlocal name evidenceとして扱います。
- package名だけはpartialまたはlow confidenceにします。
- name similarityだけの候補は `ambiguous` にして自動resolvedにしません。
- `Language`、作者名、`new game`、`stronger hardware`、文の断片はdependency targetから分離します。
- framework markerはlow evidenceです。明示local dependency metadataと区別します。

## 8. 人間確認を残す範囲

- structured requirementのtargetが翻訳、作者、Language行に見える場合です。
- packageとModletが1対多の場合です。
- archiveとpackageの名前が異なる場合です。
- fork、rename、bundle、patch、Core名がある場合です。
- required / optional / conflictがsource間で異なる場合です。

## 9. ModScopeへの推奨データモデル

```text
subject_node
target_node
relationship_type
confidence
resolution_status
target_kind
source_refs[]
evidence[]
matched_fields[]
conflicting_fields[]
version_constraint_raw
version_constraint_normalized
in_smorgasbord
enabled_state
requirement_group { id, operator }
```

`A -> B`だけを保存しないでください。identity resolutionとdependency interpretationを別レイヤーにしてください。

## 10. このPoCの限界

- ground truthはありません。coverageは観測可能性です。
- runtime動作確認はしていません。
- local `.meta`がarchiveとpackageを直接指すとは限りません。
- Web baselineの自然言語抽出に誤検出が含まれる可能性があります。
- 本成果物はresearch artifactです。ModScope本体には実装していません。
