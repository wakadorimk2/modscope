# Research map

このディレクトリは、ModScopeの設計に使った観測、調査、snapshot、生成データを記録します。

Researchは製品仕様の正本ではありません。
調査結果は観測時刻、source、provenance、uncertaintyを維持します。
snapshotは現在のWebやNexusの状態を意味しません。

## 調査テーマ

| テーマ | 記録 |
| --- | --- |
| Prior ArtとEvidence Model | [evidence-model.md](prior-art/evidence-model.md) |
| Compatibility | [findings.md](compatibility/findings.md) |
| Requirements / Dependencies | [findings.md](requirements/findings.md) |
| Requirementsのlocal resolution | [snapshot README](requirements/local-resolution/README.md) |
| Version整合性 | [2026-08-14 snapshot README](version/snapshot-2026-08-14/README.md) |
| Smorgasbord local inventory | [local-inventory.md](snapshots/2026-08-14-smorgasbord/local-inventory.md) |

## 生成データ

### Requirements local resolution

- [local-inventory.json](requirements/local-resolution/artifacts/local-inventory.json)
- [resolution-evidence.json](requirements/local-resolution/artifacts/resolution-evidence.json)
- [resolution-results.csv](requirements/local-resolution/artifacts/resolution-results.csv)

### Version snapshot

- [collection-run.json](version/snapshot-2026-08-14/artifacts/collection-run.json)
- [dataset.csv](version/snapshot-2026-08-14/artifacts/dataset.csv)
- [dataset.json](version/snapshot-2026-08-14/artifacts/dataset.json)
- [snapshot-manifest.json](version/snapshot-2026-08-14/artifacts/snapshot-manifest.json)
- [smorgasbord.wj-records.json](version/snapshot-2026-08-14/artifacts/smorgasbord.wj-records.json)
- [smorgasbord.wabbajack.definition.json.gz](version/snapshot-2026-08-14/artifacts/smorgasbord.wabbajack.definition.json.gz)

その他のsnapshot metadataも、同じ[version artifact directory](version/snapshot-2026-08-14/artifacts/)へ保存します。

## ルール

- 調査本文は、raw dataを要約してもsource observationを失わないようにします。
- 生成データは、対応するsnapshotまたは調査単位の`artifacts/`へ置きます。
- privacy boundary、snapshot ID、観測時刻、未確認事項を変更しません。
- Researchの結果だけで、dependency、compatibility、version、runtime behaviorを断定しません。
