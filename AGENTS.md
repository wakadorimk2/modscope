# ModScope 作業規則

## プロジェクトの目的

ModScopeは、Mod Organizer 2（MO2）で管理された大量の7 Days to Die（7DTD）MODを、人間とAI agentが安全かつ効率よく探索・理解・解析するための補助システムです。

ModScopeはMO2を置き換えません。MO2の既存ファイル構造をsource of truthとして扱います。ModScopeが持つindex、cache、解析結果、GUI用データは、原則として再生成可能な派生データです。

最終ゴールは、次の機能を段階的に提供することです。

- MO2 profile、modlist、modsディレクトリの読み取り
- 7DTDのModInfo.xmlとConfig XMLの構造化
- AI agentが利用しやすいforward indexとreverse index
- XML patch semanticsを考慮したsemantic conflict analysis
- RuntimeOCDなどのruntime evidenceとの比較
- 認知負荷を下げた人間向けGUI
- 明示的な承認を伴う、将来のMO2 write操作

## 現在のフェーズ

現在は設計フェーズです。設計フェーズでは、次の3ファイルだけを変更対象とします。

- AGENTS.md
- docs/design.md
- docs/future-vision.md

次の作業は、明示的な依頼があるまで開始しません。

- 実装コード
- 依存関係
- package.json、pyproject.tomlなどのmanifest
- GUI
- CLI
- build環境
- MO2実環境への書き込み

## フェーズ判断

v0.1は最終ゴールではありません。v0.1は、read-only source snapshotと構造化indexを成立させる最初のvertical sliceです。

作業を始める前に、次の2点を確認します。

1. 今のフェーズの完成に必要か。
2. 最終ゴールの設計境界を壊さないか。

現在のフェーズに不要な機能は実装しません。ただし、将来機能を妨げる暫定的なデータ設計やsource boundaryは採用しません。

## MO2の安全境界

- MO2のmods、profiles、downloads、MO2本体をsource of truthとして扱います。
- 初期実装はread-only firstとします。
- MO2のファイルを削除、移動、改名、上書きしません。
- 実データの検証には、まずfixtureまたはread-onlyの一時コピーを使います。
- source pathは明示的に指定し、暗黙の探索範囲を広げません。
- index、cache、解析結果はMO2の正本ではありません。
- 将来write操作を追加する場合も、read layerから独立したwrite layerに置きます。
- write layerには、dry-run、変更差分、対象確認、明示的な承認を要求します。

## 設計変更

- 作業前にdocs/design.mdとdocs/future-vision.mdを確認します。
- 既存の設計意図、source of truth、read/write boundaryを維持します。
- 設計を変える場合は、変更理由、影響範囲、代替案、未確定事項を記録します。
- 目的が不明な抽象化を追加しません。
- 7DTDで実際に必要になるまで、ゲーム横断の抽象化を増やしません。
- 1つの大きな変更ではなく、小さく検証可能なvertical sliceで進めます。

## 実仕様と証拠

MO2、7DTD、ModInfo.xml、Config XML、XML patch semanticsについて、推測を実装の根拠にしません。

作業では次を分けて記録します。

- verified：実データ、公式資料、または再現可能な検証で確認した事実
- inferred：複数の事実から導いた推測
- uncertain：確認が必要な事項
- diagnostic：解析できなかった入力と理由

入力に未知のXML patch operationや未知の属性がある場合は、破棄せずraw情報を保持します。

## RuntimeOCDなどの外部実装

RuntimeOCDなどの外部実装を利用または参照する場合は、先にライセンス、公開仕様、入力・出力形式、技術的境界を確認します。

コードをコピーして再実装することを前提にしません。原則としてAdapterで接続し、外部のruntime evidenceとModScopeのstatic evidenceを区別します。

## GUIの原則

GUIはMO2の高い情報密度を再現しません。目的は、情報を増やすことではなく、重要な判断を理解しやすくすることです。

- 最初に概要と重要な判断を表示します。
- 詳細は検索、filter、選択操作の後に表示します。
- raw XML、XPath、attribute、ロード順は段階的に開示します。
- 大きな表やグラフを初期画面に表示しません。
- 色だけに依存しません。
- GUIはquery layerの派生データだけを読みます。
- GUIからMO2へ直接書き込みません。

## 報告とコミュニケーション

- ユーザーの言語を使います。
- 短い文を使います。
- 1文には1つの事実または指示を書きます。
- 技術的な報告では、事実、推測、未確定事項を分けます。
- 同じ対象には同じ用語を使います。
- 作業報告はお嬢様言葉と絵文字を使いますわ😊
