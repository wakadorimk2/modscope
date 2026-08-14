# Confidence model

観測日時: `2026-08-14T10:01:09Z`

## Decision state

| state | 意味 | 自動処理 |
|---|---|---|
| `Exact` | 同じidentityとversion roleでraw valueが一致する。 | 許可 |
| `EquivalentNotation` | `v` prefix、末尾のzeroなど、明示した表記差だけがある。 | 許可 |
| `UpdateAvailable` | 同じNexus MOD identityで、selected fileより新しいlatest fileを安全に順序付けできる。 | Highのみ許可 |
| `ConflictingSources` | 同じidentity、同じversion roleのsourceが矛盾する。 | 禁止 |
| `NotComparable` | list、MOD、file、gameなどversion roleが異なる。 | 禁止 |
| `Unresolved` | identity、意味、latest、orderingの根拠が不足する。 | 禁止 |

`LikelyMatch` と `LikelyUpdateAvailable` は状態にしません。必要な場合は `comparison_confidence=Medium` と `reason_codes` で表現します。

## Source reliability

| source | 強い用途 | 弱い用途 |
|---|---|---|
| Nexus mod ID + file ID | identity | versionの意味が正しいこと |
| Wabbajack source + hash + size | 配布artifact identity | MODのlatest判断 |
| `ModInfo.xml` | local runtimeの自己申告version | latest判断 |
| MO2 `meta.ini` | MO2が取得した時点のversionとcache | 現在のNexus latest |
| download `.meta` | archiveとNexus fileの対応 | 現在のlatest |
| archive filename | 補助証拠 | identityの確定 |
| README、display name | 文脈 | 自動比較 |
| Collection/list version | list snapshot | MOD本体version |
| game version | compatibility | release ordering |

## Version scheme

`semver`、`numeric_dotted`、`date`、`author_defined`、`game_tagged`、`unknown`を別列に保存します。
`semver`は3要素の単純な数値形式だけを安全候補にします。4要素以上の値は`numeric_dotted`です。
`date`、`author_defined`、`game_tagged`は、文字列が取得できても順序を自動推定しません。

## 自動判定条件

1. identityをmod ID + file ID、またはWabbajack source + hash/sizeで固定する。
2. 比較する値のversion roleを固定する。
3. version schemeを同じにする。
4. 文字列正規化の内容を`reason_codes`へ保存する。
5. 同じroleの信頼できるsourceに矛盾がないことを確認する。
6. すべての値にevidence referenceを付ける。

## Unresolved条件

- ModInfo.xmlがない。
- package内に複数Modletがあり、source対応付けが一意でない。
- 同じNexus MOD IDに複数fileがあり、Wabbajackのfile選択がlocal fileと異なる。
- mod IDまたはfile IDがない。
- 名前だけでsourceを対応付けた。
- date version、author独自version、game tag付きversionを順序比較する。
- `newestVersion`だけでNexus latestを断定する。
- latest fileが削除、非表示、または複数候補である。
- list versionとMOD versionを比較する。
- Nexus live sourceを取得できない。
- ModInfo、MO2、Nexusの同じroleが矛盾する。

## Current limitation

Nexus直接HTTP取得はCloudflare challengeで遮断されました。MO2 cacheは保存しましたが、live Nexus latestの証拠としては扱いません。
