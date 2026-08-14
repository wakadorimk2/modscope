# Smorgasbord local inventory

## 調査の由来

この文書は、ModScopeLabで実施したSmorgasbordのローカルinventoryを、再現可能な知見へ圧縮した記録です。
実際のarchive、MOD本体、inventory全体、.meta全体はrepositoryへコピーしません。

- Source report：`C:\ModScopeLab\analysis\reports\smorgasbord-inventory.md`
- Source dataset root：`C:\ModScopeLab\wabbajack\smorgasbord`
- 観測時刻：`2026-08-14T10:01:18Z`
- Smorgasbord list version：`3.1.1.39`
- Smorgasbord application/tool version：`not captured`
- 観測方法：ゲームを起動しないローカルファイルのread-only走査
- Nexus追加問い合わせ：実施なし
- MO2設定：変更なし

## 観測結果

| 項目 | 観測値 | 根拠 |
|---|---:|---|
| archive | 475 | `downloads`内のarchive |
| MO2 package | 504 | `mods`内のpackage directory |
| `ModInfo.xml` | 493 | package内の`ModInfo.xml` |
| unique Modlet | 491 | `ModInfo.xml` path単位のModlet record |
| enabled | 341 | active profileの`modlist.txt` |
| disabled | 131 | active profileの`modlist.txt` |
| Nexus `modID` | 468 | `.meta` |
| Nexus `fileID` | 468 | `.meta` |
| `modID`と`fileID`の両方 | 468 | `.meta` |
| `.meta Version` | 0 | `.meta` |
| `ModInfo.xml Version` | 493 | `ModInfo.xml` |
| version比較可能 | 0 | `ModInfo.xml`と`.meta`の対応済みversion |
| 1 package内の複数Modlet | 5 | multiple-Modlet package record |
| version source mismatch | 0 | version comparison record |
| metadata不足のunresolved Modlet | 185 | missing/unmatched 182 + ambiguous 3 |
| `ModInfo.xml`なしのunresolved package | 34 | package diagnostic |

これらは同じ管理単位の件数ではありません。
したがって、475 archive、504 package、491 Modletを1対1で対応付けません。

## 代表的な形

### 1 packageから複数Modlet

`IZY Classic - Core`では、1 packageから17 Modletが観測されました。
package名をModlet identityとして扱うと、複数のModletを誤って統合します。

### 1 Nexus Modから複数file候補

CATUI型のケースでは、同じNexus `modID`に複数の`.meta` file候補が対応しました。
`modID`だけでは、取得したNexus fileを確定できません。

### 似たmetadataから複数候補

AGF型のケースでは、似た名前を持つ複数metadataが同じlocal Modletの候補になりました。
名前の近さだけでは、identityを確定できません。

### `ModInfo.Name`の重複

異なるpackageまたはpathに、同じ`ModInfo.Name`を持つModletが存在しました。
`ModInfo.Name`は表示metadataです。
stable local identityにはpathまたは別のsource evidenceが必要です。

### separator text

次のようなcurator-authored textが観測されました。

- `ONLY USE ONE BACKPACK MOD`
- `Hard Requirements - Do not disable`
- `Choose One! Cat, Steelshot, AGF`
- `Do not toggle mid-save`

separator textはrequirements、選択制約、運用上の注意を見つけるためのevidenceです。
機械的なdependency、compatibility、enable要求のauthoritative truthではありません。

## 解釈

### Verified

- `.meta`から`modID`と`fileID`を468件取得できました。
- `.meta Version`は0件でした。
- `ModInfo.xml Version`は493件でした。
- `ModInfo.xml`と`.meta`をidentity解決済みの比較単位として比較できるrecordは0件でした。
- 1 packageに複数Modletが存在しました。

### 設計上の帰結

`version source mismatch = 0`は、versionが一致したことを意味しません。
比較対象のidentityと比較可能なversionが不足していたため、比較できませんでした。

version parserを改善する前に、次のidentityを分離して解決する必要があります。

> Archive != MO2 Package != Modlet != Nexus Mod != Nexus File

> Resolve identity before comparing versions.

### 未確認事項

- Nexus上のlatest version
- runtime compatibility
- 実行時load order
- application/tool version

## 安全境界

- raw CDN URLは保存しません。
- `.meta`の`userData`は保存しません。
- token、cookie、API keyは保存しません。
- ゲームを起動しません。
- MO2設定とModScope本体を変更しません。

## Follow-up: MO2 package `meta.ini` identity mapping

### 出典と扱い

この節は、2026-08-14にユーザーが提供したfollow-up調査結果を記録します。
元の調査データ、詳細なsource locator、正確な観測時刻は提供されていません。
このrepositoryでは、このfollow-up結果を独立再走査していません。

この節の数値は、調査datasetの観測値です。
すべてのMO2または7DTD環境へ一般化しません。

### `meta.ini`読取後のidentity state

旧inventoryの初期結果は次のとおりでした。

| state | 件数 |
|---|---:|
| `matched` | 308 |
| `unmatched` | 182 |
| `ambiguous` | 3 |

MO2 packageの`meta.ini`を読んだfollow-upでは、次のstateになりました。

| state | 件数 | 扱い |
|---|---:|---|
| `AutoResolved` | 458 | 明示的なpackage provenanceで自動解決したrecord |
| `HumanReview` | 28 | 自動確定せず、人間確認へ送るrecord |
| `PartiallyResolved` | 1 | identity evidenceの一部だけを解決したrecord |
| `Unresolved` | 6 | identityを安全に確定できないrecord |

`HumanReview`の28件を、bundleの件数とは解釈しません。
これらはresolution stateの件数です。

### Provenanceの関係

調査で使用したidentityの流れは次のとおりです。

```text
Nexus file
  ↓ download archive + archive .meta
MO2 package + meta.ini
  ↓
Modlet(s)
```

MO2 packageの`meta.ini`は、利用可能な場合のlocal provenance anchorです。
Nexus identityは、個々のModletよりも取得済みのpackageまたはartifactへ主に所属させます。

IZY Classicのように、1 packageから17 Modletが生成されるbundleがあります。
この場合、17 Modletが同じ配布artifact由来であることは記録できます。
しかし、各Modletに固有のNexus fileを推測しません。

`SourceArtifact`、`MO2Package`、`Modlet`は別のidentityです。
明示的なprovenance metadataを、名前の類似によるfuzzy matchingより優先します。

既存inventoryの`.meta Version = 0`は、download archive側の`.meta`に関する観測です。
MO2 packageの`meta.ini`とは別のmetadataです。
したがって、既存観測をfollow-upの`meta.ini`結果で上書きしません。

### Version observations

identityを高い確度で解決しても、version observationの一致は保証されません。

| version comparison state | 件数 |
|---|---:|
| `equal` | 355 |
| `mismatch` | 94 |
| `incomparable` | 16 |

これらはversion comparisonの観測subsetです。
identity stateの458件と同じ母数であるとは推測しません。

`ModInfo.xml` versionとMO2 package `meta.ini` versionは、別のversion observationとして保持します。
identity resolutionとversion resolutionを同一視しません。

### 次の調査

identity mapping済みrecordへ、次のsourceを同じartifact identity上で再配置します。

- MO2 package `meta.ini`
- `ModInfo.xml`
- download archive `.meta`
- Wabbajack record
- Nexus file identity

その後、94件のmismatchを、各version observationの意味の違いごとに分類します。
分類前に、共通のversion比較規則やlatest判定を確定しません。

## 関連資料

- [Identity and version provenance ADR](../adr/identity-and-version-provenance.md)
- [Synthetic identity fixtures](../../tests/Fixtures/mod-identity/README.md)
