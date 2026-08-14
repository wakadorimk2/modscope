# Version整合性調査 snapshot

このディレクトリは、7 Days to DieとMO2のMOD version整合性調査を固定した記録です。

この記録は、現在のNexus latestを表しません。
この記録は、製品の公開schemaや自動更新判定を確定しません。

## Snapshot identity

- snapshot ID: `986c7a1d87c2`
- observation time: `2026-08-14T10:01:09Z`
- local scope: `existing_mo2`
- Wabbajack list: `Smorgasbord`
- list version: `3.1.1.39`
- Collection ID: `445130`
- Collection revision: `39`
- public source: [Fluffernuttersandwich/Smorgasbord](https://github.com/Fluffernuttersandwich/Smorgasbord)
- Nexus live fetch: `blocked_cloudflare`

Wabbajackのraw manifest本体は保存されていません。
公開manifest partsから抽出したsanitized recordsを保存しています。

## Fixed observations

| observation | count |
| --- | ---: |
| MO2 package | 98 |
| `ModInfo.xml` | 105 |
| package `meta.ini` | 87 |
| download `.meta` | 204 |
| Wabbajack archive record | 476 |
| dataset row | 566 |
| comparison | 296 |
| local row with matching Wabbajack file ID | 26 |
| local row with same MOD ID but different file ID candidate | 8 |
| Wabbajack record version mismatch or unsafe comparison | 25 |

### Dataset state

| state | count |
| --- | ---: |
| `Exact` | 15 |
| `EquivalentNotation` | 41 |
| `ConflictingSources` | 22 |
| `NotComparable` | 443 |
| `Unresolved` | 45 |

### Comparison state

| state | count |
| --- | ---: |
| `Exact` | 77 |
| `EquivalentNotation` | 42 |
| `NotComparable` | 119 |
| `Unresolved` | 58 |
| `UpdateAvailable` | 0 |

`UpdateAvailable`は、今回のsnapshotから確定していません。
`0`は、対象が存在しないことの証明ではありません。

## Evidence boundary

- Nexus MOD ID + file IDは、MOD identityの強い証拠です。
- Wabbajack source + hash + sizeは、配布artifact identityの強い証拠です。
- `ModInfo.xml`は、local Modletが宣言するversionです。
- MO2 `meta.ini`は、MO2が取得したversionとcacheです。
- download `.meta`は、local archiveとNexus fileの対応です。
- archive filename、README、display nameは補助証拠です。
- Wabbajack list versionとCollection revisionはMOD本体versionではありません。
- game versionはrelease versionとは別のcompatibility情報です。

Nexus live pageを取得できなかったため、MO2 `newestVersion`だけでlatestを確定していません。
同じMOD IDでもfile IDが異なる場合は、更新可能と判定していません。

## Stored files

- `consultation-brief.md`: 相談用ブリーフ
- `findings.md`: 観測事実と調査上の提案
- `mismatch-cases.md`: state、reason code、代表比較の集計
- `confidence-model.md`: 調査で使用したconfidenceとversion schemeの案
- `dataset.csv`: 表形式のsnapshot
- `dataset.json`: comparison、observation、evidenceのsnapshot
- `collection-run.json`: 収集範囲と件数
- `mo2-mtime-verification.json`: MO2実データへの変更確認
- `sanitization-check.json`: 匿名化確認
- `wj-fetch-status.json`: Wabbajack取得状態
- `smorgasbord.*.json`: 公開manifest由来のsanitized input
- `snapshot-manifest.json`: 保存ファイルのサイズとSHA-256

## Privacy and reproducibility

保存データは、MO2のpackage本体やMOD archiveを含みません。
local absolute path、credential、cookie、tokenは保存しません。
local path referenceはsnapshot内の相対pathだけを使います。

`sanitization-check.json`と`mo2-mtime-verification.json`は、調査時の確認結果です。
このrepositoryでlive sourceを再取得しません。
保存ファイルの完全性は`[snapshot-manifest.json](artifacts/snapshot-manifest.json)`で確認します。

## Repository boundary

この記録は`docs/research`だけに属します。
`docs/design.md`、`docs/future-vision.md`、C#、Query、Web contract、xUnitの仕様を変更する根拠にはしません。
自動更新提案へ利用する場合は、別の設計判断と実装検証が必要です。

収集スクリプト、MO2実データ、MOD binary、raw manifest本体は、このsnapshotに含めません。
