# Synthetic identity fixtures

このfixture群は、ModScopeLabで確認したidentityとversion provenanceの形を再現します。
実際のMOD、archive、Nexus URL、.meta userDataは含みません。

各caseは次の構成を持ちます。

- `profile/modlist.txt`：MO2 profileのraw入力
- `mods/`：synthetic packageとModletのdirectory tree
- `evidence/manifest.json`：test-onlyの中立evidence manifest

## Manifestの境界

`manifest.json`はproduction contractではありません。
将来のidentity resolverテストが、local treeと配布元候補の関係を読むためのfixture形式です。

- `entities`はarchive、package、Modlet、Nexus Mod、Nexus Fileを表します。
- `relations`はentity間の関係を表します。
- `observations`はmissing、ambiguous、separatorなどの観測事実を表します。
- `expected`はfixtureが再現するidentityとversionの状態を表します。
- すべてのID、名前、version、pathはsyntheticです。
- versionが`null`のとき、値は欠落しています。

`expected.identityStatus`は`resolved`、`ambiguous`、`unresolved`、`not-applicable`のいずれかです。
`expected.versionStatus`は`comparable`、`not-comparable`、`not-assessed`のいずれかです。

## Cases

| Case | 再現する形 |
|---|---|
| `single-package-single-modlet` | 1 archive → 1 package → 1 Modlet |
| `multi-modlet-package` | 1 package → 17 Modlet |
| `duplicate-modlet-name` | 異なるpathの同一`ModInfo.Name` |
| `ambiguous-meta` | 1 local Modlet → 複数Nexus Mod候補 |
| `missing-meta` | `modID/fileID`はあるが`.meta Version`がない |
| `missing-modinfo` | packageに`ModInfo.xml`がない |
| `same-nexus-mod-multiple-files` | 1 Nexus Mod → 複数Nexus File候補 |
| `separator-with-selection-constraint` | separator textと選択制約 |
| `historical-artifact/case-01-current-vs-historical` | current MO2 packageとhistorical Wabbajack record |
| `historical-artifact/case-02-multiple-file-candidates` | 同じMOD IDに複数File IDとcandidate selection |
| `historical-artifact/case-03-stale-modinfo` | staleなModInfoと新しいpackage/download metadata |
| `historical-artifact/case-04-bundle-missing-evidence` | 複数Modlet packageとmissing evidence |

`historical-artifact`の4件は、Wabbajack record、MO2 metadata、download metadataをsynthetic valueで表します。
実際のarchive、公開URL、owner path、credentialは含みません。
