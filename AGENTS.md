# ModScope 作業規則

## プロジェクトの目的

ModScopeは、MODをWeb上で探し、吟味し、比較する作業を、ユーザーのローカルMOD環境の文脈付きで行うWorkspaceです。

ModScopeはMod Managerではありません。MO2のenable、disable、priority、profile、virtual filesystem、launchを置き換えません。

Web browsing workflowをprimary surfaceとします。Local Mod KnowledgeをModScopeの主要資産とします。

MO2のmods、profiles、downloads、MO2本体はsource of truthです。ModScopeが持つsnapshot、index、cache、normalized metadata、search result、conflict result、read modelは再生成可能な派生データです。

人間はAIを使わずにbrowse、inspect、compare、local environmentの理解を行えます。AIはoptional UXです。ただし、agentがLocal Mod Knowledgeへアクセスできる境界は必須です。

初期対象は7 Days to Die（7DTD）とMod Organizer 2（MO2）です。将来のGame Adapter境界を壊さない設計にします。ただし、prematureな複数game対応は行いません。

## 現在のフェーズ

現在は設計フェーズです。現在の作業で変更できるファイルは、次の3ファイルだけです。

- `AGENTS.md`
- `docs/design.md`
- `docs/future-vision.md`

次の作業は、明示的な依頼があるまで開始しません。

- 実装コード
- 依存関係
- `package.json`、`pyproject.toml`などのmanifest
- GUI実装
- Browser engine実装
- CLI実装
- build環境
- MO2実環境への書き込み

v0.1は、旧来の「source snapshotとAI用indexだけ」の縛りでは定義しません。現在の製品仮説は、7DTD + MO2のLocal Mod Knowledgeと、最小のBrowse → Recognize → Local awareness → Inspectの縦切りです。

## 作業判断基準

作業を始める前に、次の2点を確認します。

1. 今それはv0.1の完成に必要か。
2. これはModScopeのKnowledge Layerに属するか。それとも既存ブラウザ、MO2、external agentに任せる仕事か。

現在のフェーズに不要な機能は設計へ追加しません。将来機能を妨げる暫定的なsource boundaryも採用しません。

Web browsing workflowを主役にします。高密度なMod Manager UIを主画面にしません。

Browser engineを自作しません。WebView2、既存ブラウザ連携、browser automationなどは候補として調査しますが、特定のengineを設計の前提にしません。

RuntimeOCDを再実装しません。外部結果は、ライセンスと公開仕様を確認したAdapterからruntime evidenceとして取り込みます。

Nexus Mods専用にしません。未知のサイトではURL、title、基本page contentを扱います。既知サイトのSite Adapterは任意の拡張です。

Codex専用仕様へ密結合しません。CLI、structured files、local API、MCP、その他のagent-friendly interfaceは、共通のread modelへ接続する候補として比較します。

## MO2の安全境界

- MO2のmods、profiles、downloads、MO2本体をsource of truthとして扱います。
- 初期実装はread-only firstとします。
- MO2のファイルを削除、移動、改名、上書きしません。
- 実データの検証には、まずfixtureまたはread-onlyの一時コピーを使います。
- source pathは明示的に指定します。暗黙の探索範囲を広げません。
- index、cache、解析結果、GUI read modelはMO2の正本ではありません。
- 将来write操作を追加する場合も、read layerから独立したwrite layerに置きます。
- write layerには、dry-run、変更差分、対象確認、明示的な承認、失敗時の復旧方針を要求します。

## レイヤー境界

### Local Mod Knowledge

Local Mod Knowledgeは、MO2 profileと7DTD MODを構造化した、再生成可能なローカル知識です。

対象には、profile、modlist、enabled状態、priority、ModInfo.xml、MOD内ファイル、Config XML、XML patch operation、target XML、XPath、attribute、reverse reference、diagnostic、provenanceを含めます。

raw、normalized、static evidence、runtime evidence、inference、uncertaintyを分けます。未知のoperation、属性、XML要素を破棄しません。

### Browsing Layer

Browsing Layerは、人間がMODを探すWeb surfaceと、page observationを扱います。既存Web engineを利用する境界です。Web engineそのものをModScopeの実装資産にしません。

### Agent browser

Agent browserは、人間向けBrowsing Layerとは別の境界です。Kitesurfなどの特定backendを必須依存にしません。Agentは、Local Mod KnowledgeとWeb exploration結果を共通の根拠付きread modelで扱います。

### MO2、Game、Site Adapter

- MO2 Adapterは、明示されたMO2 sourceを読み取ります。
- Game Adapterは、7DTD固有のModInfoとXML patch semanticsを解釈します。
- Site Adapterは、既知サイトの構造化情報を任意で提供します。
- これらのAdapterは、source data、page data、derived dataを混ぜません。

## 実仕様と証拠

MO2、7DTD、ModInfo.xml、Config XML、XML patch semanticsについて、推測を実装の根拠にしません。

作業では次を分けて記録します。

- verified：実データ、公式資料、または再現可能な検証で確認した事実
- inferred：複数の事実から導いた推測
- uncertain：確認が必要な事項
- diagnostic：解析できなかった入力と理由

入力に未知のXML patch operation、未知の属性、未知のサイト構造がある場合は、破棄せずraw情報とdiagnosticを保持します。

## GUIとInspectorの原則

画面の主役は現在のWeb pageです。Local contextはprogressive disclosureで表示します。

- 最初にpageと重要なlocal contextを表示します。
- installed、not installed、active profile、known version、known overlapなどを根拠付きで表示します。
- 不明な情報をunknownとして表示します。
- 詳細はInspector、Search、Compare、Diagnosisで段階的に開示します。
- raw XML、XPath、attribute、priorityは詳細表示へ分離します。
- 大きな表やgraphを初期画面に表示しません。
- 色だけに依存しません。
- GUIはquery layerの派生データだけを読みます。
- GUIからMO2へ直接書き込みません。

## 設計変更

- 作業前に`docs/design.md`と`docs/future-vision.md`を確認します。
- 既存のsource of truth、read/write boundary、evidence分離を維持します。
- 設計を変える場合は、変更理由、影響範囲、代替案、未確定事項を文書へ記録します。
- 目的が不明な抽象化を追加しません。
- 7DTDで実際に必要になるまで、ゲーム横断の抽象化を増やしません。
- 1つの大きな変更ではなく、小さく検証可能なvertical sliceで進めます。

## `.serena/`の版管理とmemory

- `.serena/project.yml`と`.serena/.gitignore`は共有設定として管理します。
- `.serena/project.local.yml`と`.serena/cache/`は個人環境の設定と生成cacheとしてGitへ追加しません。
- `.serena/memories/`は、レビュー済みの運用補助memoryだけを明示的に追加します。
- onboardingで生成したmemoryは下書きとして扱います。
- memoryは、Serenaの使い方、探索入口、検証手順、文書への参照を記録します。
- memoryをModScopeの製品仕様の正本にしません。
- `AGENTS.md`と`docs/`がmemoryと競合した場合は、`AGENTS.md`と`docs/`を優先します。
- memoryへ秘密情報、認証情報、個人パス、raw log、短期作業履歴を保存しません。
- memoryをcommitする前に、差分、機密情報、正本との重複、鮮度を確認します。
- memoryを追加するときは、対象パスを明示してGitへ追加します。
- Serenaの`read_only: true`は使用しません。
- コード編集、任意shell、JetBrains編集、Serenaプロジェクト削除は`.serena/project.yml`の`excluded_tools`で除外します。
- `write_memory`、`edit_memory`、`delete_memory`、`rename_memory`は明示依頼と差分レビューを前提に使用します。
- Serenaの更新後は、`get_current_config`で編集ツールの除外漏れを確認します。

## 報告とコミュニケーション

- ユーザーの言語を使います。
- 短い文を使います。
- 1文には1つの事実または1つの指示を書きます。
- 技術的な報告では、事実、推測、未確定事項を分けます。
- 同じ対象には同じ用語を使います。
- 作業報告はお嬢様言葉と絵文字を使いますわ😊
