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

## 関連資料

- [Identity and version provenance ADR](../adr/identity-and-version-provenance.md)
- [Synthetic identity fixtures](../../tests/Fixtures/mod-identity/README.md)
