# ModScope 作業規則

## ModScope North Star

> **使っていて気持ちいい上に、異様に賢いMODマネージャを作る。**
>
> 「気持ちいい」とは、複雑なMOD環境を見つけ、整理し、理解し、操作する日常の体験が速く自然であること。
> 「異様に賢い」とは、単に情報を多く表示するのではなく、MOD・package・Modlet・archive・version・source・dependency・conflict・runtimeなどの関係を根拠付きで理解し、必要なときに説明できること。
>
> **既存Mod Managerとの機能重複は、それ自体をスコープ外の理由にしない。**
> ModScopeを日常的に使うために必要なら、managerとして一般的な機能も実装対象になり得る。
>
> 一方で、機能追加は常に次のどちらかを明確に改善しなければならない。
>
> 1. **使っていて気持ちいいか**
> 2. **異様に賢くなるか**
>
> どちらにも寄与しない抽象化・基盤・機能は作らない。

## プロジェクトの目的

ModScopeは、MODの発見、整理、理解、操作を、Web pageとLocal Mod Knowledgeの二つのprimary surfaceで支援するWorkspaceです。

ModScopeはMO2のsource of truthを置き換えません。
MO2のenable、disable、priority、profile、virtual filesystem、launchなどの成熟した責務は、機能ごとにPrior Artと相互運用性を確認して扱います。
ModScopeの日常利用に必要なmanager機能は、実装対象になり得ます。
その機能は、North Starの「気持ちよさ」または「賢さ」を明確に改善する必要があります。

Web pageはMODの発見と外部evidenceのprimary surfaceです。
Local Mod Knowledgeは、MOD環境の整理、理解、比較、診断、必要な操作のprimary surfaceです。

MO2のmods、profiles、downloads、MO2本体はsource of truthです。ModScopeが持つsnapshot、index、cache、normalized metadata、search result、conflict result、read modelは再生成可能な派生データです。

人間はAIを使わずにbrowse、inspect、compare、local environmentの理解を行えます。AIはoptional UXです。ただし、agentがLocal Mod Knowledgeへアクセスできる境界は必須です。

初期対象は7 Days to Die（7DTD）とMod Organizer 2（MO2）です。将来のGame Adapter境界を壊さない設計にします。ただし、prematureな複数game対応は行いません。

## 現在のフェーズ

明示的な実装依頼により、現在はLocal Knowledge基盤、Web UI縦切り、Controlled profile edit、junction deploy、Steam起動の実装フェーズです。

今回の実装で変更できる範囲は、次のとおりです。

- `AGENTS.md`
- `docs/design.md`
- `.serena/project.yml`
- `global.json`
- `ModScope.sln`
- `src/ModScope.LocalKnowledge/`
- `src/ModScope.Deployment/`
- `src/ModScope.Query/`
- `src/ModScope.Desktop/`
- `src/ModScope.Desktop.Contracts/`
- `src/ModScope.Web/`
- `tests/ModScope.LocalKnowledge.Tests/`
- `tests/ModScope.Deployment.Tests/`
- `tests/ModScope.Query.Tests/`
- `tests/ModScope.Desktop.Contracts.Tests/`
- `tests/Fixtures/`
- `scripts/`

今回の実装では、次の対象を作成しません。

- CLI実装
- 独自Browser engine実装
- Site Adapter
- 完全なsemantic conflict判定

C# / .NET 8のcore/query/contractsライブラリ、WPF / .NET 10のDesktop host、Svelte / TypeScript / ViteのWeb frontend、xUnitテストを使用します。
匿名synthetic fixtureを検証の基準にします。

Web frontendはpresentation layerです。
Web frontendはMO2 integration、filesystem access、profile parsing、Local Mod Knowledge、XML解析、write transactionを実装しません。
.NET / C#側がsource of truthです。
Desktop hostはWebView2 host、native bridge、Query projectionだけを担当します。
Browser WebView2へLocal context、MO2 path、LocalModSnapshotを注入しません。
frontend buildは'scripts/build.ps1'で実行します。

v0.1は、旧来の「source snapshotとAI用indexだけ」の縛りでは定義しません。現在の製品仮説は、7DTD + MO2のLocal Mod Knowledgeと、最小のBrowse → Recognize → Local awareness → Inspectの縦切りです。

## 作業判断基準

作業を始める前に、次の2点を確認します。

1. 今それはv0.1の完成に必要か。
2. これはModScopeのKnowledge Layerに属するか。それとも既存ブラウザ、MO2、external agentに任せる仕事か。

現在のフェーズに不要な機能は設計へ追加しません。将来機能を妨げる暫定的なsource boundaryも採用しません。

Web pageとLocal Mod Knowledgeを主役にします。
初期画面は、情報量と認知負荷を制御します。
高密度なMod Manager UIを既定の主画面にしません。

Browser engineを自作しません。WebView2、既存ブラウザ連携、browser automationなどは候補として調査しますが、特定のengineを設計の前提にしません。

RuntimeOCDを再実装しません。外部結果は、ライセンスと公開仕様を確認したAdapterからruntime evidenceとして取り込みます。

Nexus Mods専用にしません。未知のサイトではURL、title、基本page contentを扱います。既知サイトのSite Adapterは任意の拡張です。

Codex専用仕様へ密結合しません。CLI、structured files、local API、MCP、その他のagent-friendly interfaceは、共通のread modelへ接続する候補として比較します。

## Prior Art First

Before designing or implementing mod-management functionality, first investigate how established tools such as **Mod Organizer 2, Wabbajack, and Vortex** already solve the same problem.

For features involving installation, updates, versioning, dependencies, compatibility, load order, profiles, or mod-list distribution:

1. Check the existing behavior and data model of MO2, Wabbajack, and Vortex where relevant.
2. Identify what they already solve well.
3. Identify the remaining user pain or missing capability.
4. Prefer interoperability, reuse, or a complementary layer over reimplementing mature functionality.
5. Only implement overlapping functionality when ModScope has a clear reason to do it differently.

Before starting implementation, be able to answer:

> **How do MO2, Wabbajack, and Vortex already solve this, and what specifically remains unsolved for ModScope?**

関連する製品だけを調査します。毎回3製品を形式的に調査しません。

mature managerを明確な理由なく再実装しません。

Evidence rules:

- Unknownは有効な結果です。evidence不足時に推測で確定しません。
- provenanceとobserved timeを保持します。
- source claimはruntime verificationではありません。
- dependencyはcompatibilityではありません。
- manifest/list co-presenceはdependency、compatibility、runtime evidenceではありません。

## MO2の安全境界

- MO2のmods、profiles、downloads、MO2本体をsource of truthとして扱います。
- 通常のLocal Knowledge読み取りはread-onlyとします。
- Controlled Applyだけが、明示承認後に選択profileの`modlist.txt`を更新します。
- MO2のMOD本体を削除、移動、改名しません。
- `modlist.txt`はtimestamp付きbackup、temporary replacement、再読検証を使います。
- 実データの検証には、まずfixtureまたはread-onlyの一時コピーを使います。
- source pathは明示的に指定します。暗黙の探索範囲を広げません。
- index、cache、解析結果、GUI read modelはMO2の正本ではありません。
- write planeはread planeから独立した`ModScope.Deployment`に置きます。
- write planeには、preview、変更差分、対象確認、plan ID、明示的な承認、失敗時のrollbackを要求します。
- MO2または7DTDが実行中の場合は、プロセスを停止せずApplyをblockします。
- Steam起動はApply成功後だけ許可し、URIは`steam://rungameid/251570`に固定します。

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

Unknown is a valid result.を設計原則として扱います。
Version、Requirements、Compatibilityは、根拠が不足する場合に推測せず、UnresolvedまたはUnknownとして保持します。
file overlapはruntime conflictと同じ意味ではありません。
confirmedは保存したsource claimが確認済みであることを示します。
confirmedは7DTDの全runtime環境での動作保証を示しません。

作業では次を分けて記録します。

- verified：実データ、公式資料、または再現可能な検証で確認した事実
- inferred：複数の事実から導いた推測
- uncertain：確認が必要な事項
- diagnostic：解析できなかった入力と理由

入力に未知のXML patch operation、未知の属性、未知のサイト構造がある場合は、破棄せずraw情報とdiagnosticを保持します。

Requirements / Dependenciesでは、次の境界を維持します。

- `Requirement Observation`と`Dependency Edge`を同じ結果へ圧縮しません。
- `not_observable`は、依存関係の不在を意味しません。`unresolved`は、観測済みだがidentityまたは意味を確定できない状態です。
- list co-presenceや名前の類似だけをdependency evidenceにしません。
- target identityを名前だけで自動確定しません。raw targetとsource referenceを保持します。

## GUIとInspectorの原則

画面の主役はWeb pageとLocal Mod Knowledgeです。
Web page上のLocal contextはprogressive disclosureで表示します。
Local Mod Knowledge側では、Library、Inspector、Search、Compare、Diagnosisを必要な深さで表示します。

- 最初にpageと重要なlocal contextを表示します。
- installed、not installed、active profile、known version、known overlapなどを根拠付きで表示します。
- 不明な情報をunknownとして表示します。
- 詳細はInspector、Search、Compare、Diagnosisで段階的に開示します。
- raw XML、XPath、attribute、priorityは詳細表示へ分離します。
- 大きな表やgraphを初期画面に表示しません。
- 色だけに依存しません。
- GUIはquery layerの派生データだけを読みます。
- GUIからMO2へ直接書き込みません。
- Web frontendはdraftとdeployment commandだけを扱います。
- GamePathの絶対値、MO2 path、LocalModSnapshotはfrontendへ渡しません。

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
