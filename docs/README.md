# ModScope documentation map

このディレクトリは、現在仕様、将来方針、設計判断、調査記録、ユーザー知見を分けて管理します。

## 推奨読順

1. [ModScope設計](design.md)
2. [ModScope将来像](future-vision.md)
3. [ADR](adr/)
4. [Research map](research/README.md)
5. [ユーザー知見](user-knowledge.md)

## 文書の正本と役割

| 場所 | 正本とする内容 | 扱わない内容 |
| --- | --- | --- |
| [`design.md`](design.md) | 現在の製品定義、アーキテクチャ、責務境界、実装範囲、受入条件 | 将来の候補だけの詳細 |
| [`future-vision.md`](future-vision.md) | 将来像、設計原則、拡張方針、保留事項 | 現在の実装仕様の詳細 |
| [`adr/`](adr/) | Acceptedとした設計判断、代替案、帰結 | 未確定の調査結果 |
| [`research/`](research/README.md) | 観測、source、provenance、snapshot、diagnostic | production schemaやruntime保証 |
| [`user-knowledge.md`](user-knowledge.md) | ユーザー報告と、そこから分けて記録した仮説 | 一般化した製品仕様 |

## 証拠の扱い

- Researchは、観測時点の記録です。現在のWebやNexusの状態を保証しません。
- Source claim、static observation、runtime evidence、inference、uncertainty、diagnosticを混ぜません。
- Unknownは有効な結果です。未確認の情報を推測で補いません。
- `docs/research/**/artifacts/`のデータは、対応する調査記録から再利用します。
- MO2、7DTD、外部sourceの正本は、ModScopeの派生データではありません。

新しい現在仕様は`design.md`へ、将来の選択肢は`future-vision.md`または対応するresearchへ記録します。
