# Before / after: local dependency identity resolution

観測日時: `2026-08-14T10:46:58Z`

## Scope

- 既存Web-only baselineは `analysis/requirements-research/dataset.csv` です。
- baselineの `unresolved_target_identity` 33観測を入力にしました。
- ローカル解析は `wabbajack/smorgasbord` を読み取り専用で走査しました。
- 依頼された `downloads/smorgasbord` は存在しませんでした: `false`。
- MO2設定が示す実ダウンロードディレクトリ `wabbajack/smorgasbord/downloads` は存在しました: `true`。
- 出力はこのディレクトリだけに保存しました。

## Before: Web-only target identity

- unresolved target observations: `33`。
- unique raw target names: `30`。
- target rows with Nexus mod ID: `0/33`。
- local evidence: `not_observable` in the Web-only baseline.

## After: local resolution

### Resolution status

- `ambiguous`: 2/33 (6.1%)
- `not_applicable`: 18/33 (54.5%)
- `not_found_locally`: 9/33 (27.3%)
- `partially_resolved`: 2/33 (6.1%)
- `resolved`: 2/33 (6.1%)

### Local identity fields

- local package observed: `4/33`。
- local Modlet observed: `2/33`。
- Nexus mod ID observed through local archive linkage and `.meta`: `3/33`。
- Nexus file ID observed through local archive linkage and `.meta`: `3/33`。
- any local evidence observed: `4/33` (12.1%)。

### Evidence contribution

- `multiple_local_sources`: 3/33
- `none`: 29/33
- `package_name_only`: 1/33

## Local inventory context

- package directories: `504`。
- ModInfo.xml: `493`。
- archives: `478`。
- archives with `.meta`: `475`。
- `.meta` files: `475`。
- `.meta` with Nexus mod ID: `468`。
- packages with multiple Modlets: `5`。

## Interpretation

**観測事実:** ローカル情報はWeb-only baselineの未解決行に、package、Modlet、archive、`.meta`の候補を追加できます。

**観測事実:** exact ModInfo名とexact package名を分けて扱う必要があります。

**推測:** Nexus mod ID / file IDは、target名の一致だけでは確定できません。packageとarchiveの対応が一意である場合だけ、補助的に付与できます。

**不確実:** これはrecallではありません。baseline側の自然言語抽出が正しいという前提を置いていません。

**安全策:** fuzzy name matchは候補提示に限定しました。自動でresolvedにはしません。
