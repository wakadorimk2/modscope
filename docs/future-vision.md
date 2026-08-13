# ModScope 将来像

## 1. 目的

ModScopeの将来像は、MODの発見、評価、導入判断、問題調査を、Web pageとlocal environmentの同じWorkspaceで行える状態です。

ModScopeはMod Managerではありません。MO2をsource of truthとして残します。ModScopeは、MO2から再生成可能なLocal Mod Knowledgeと、Web pageに対するLocal contextを提供します。

Web pageはprimary surfaceです。Local contextはprogressive disclosureします。Inspectorは、必要なときだけ根拠と技術詳細を開きます。

## 2. 中心となる利用体験

### 2.1 Human browser

ユーザーは、Nexus Mods、ランキングサイト、GitHub、Wiki、forum、作者サイト、ガイドサイトなどを自由に閲覧します。

ModScopeは、現在のpageを主画面に置きます。page observationから候補MODを示します。v0.1では、ユーザーがMOD identityを確認します。

確認後、ModScopeはcurrent profileとのLocal contextを表示します。

- installed / not installed / unresolved
- active profile
- enabled状態
- priority
- known version
- known dependencies
- known overlap
- possible overlap
- unknownまたはnot assessedの理由

詳細が必要な場合だけInspectorを開きます。

### 2.2 InspectorとCompare

Inspectorは、結論からevidenceへ進む構造にします。

1. 何が起きているか
2. なぜ重要か
3. 関係するMODとprofile
4. priorityとoperation
5. static evidenceまたはruntime evidence
6. 未確認事項
7. 必要な場合だけraw XML、XPath、attribute、patch fragment

Compareは、候補MODとcurrent profileの関係を説明します。単なる一致件数を主結果にしません。

### 2.3 Agent

Agentは、数百MODと数千XML patchを毎回全文読みません。

Agentは、次のようなqueryをLocal Mod Knowledgeへ送ります。

- このpageのMODはinstalledか
- 現在のprofileでenabledか
- このMODに関係するfiles、targets、XPathは何か
- 同じtargetやXPathを変更するMODは何か
- priorityによって結果が変わりそうな箇所は何か
- static evidenceとruntime evidenceが一致しない箇所は何か

結果は、結論、対象、根拠、source reference、priority、uncertainty、diagnosticを含む小さな説明単位にします。

AgentはCodexに限定しません。CLI、structured files、local API、MCP、その他のagent-friendly interfaceから、共通のread modelへ接続できる構造を目指します。

## 3. Product principles

### Web page first

MOD一覧を主画面にしません。ユーザーが現在見ているpageを主画面にします。

### Local context is progressive disclosure

local metadata、installed状態、profile、overlap、dependenciesを必要な範囲で表示します。raw XMLと巨大なgraphを初期表示しません。

### Everything useful without AI

browse、inspect、compare、local environmentの理解は、AIなしで成立します。

### Everything easy to inspect or automate with AI

人間向け表示とagent向け出力は同じ根拠へリンクします。AIだけの隠れた状態を作りません。

### Evidence before inference

source、normalized value、static evidence、runtime evidence、inference、uncertainty、diagnosticを分離します。

### Read-only first

MO2を変更しません。writeは別planeとして扱います。

### Site-independent and game-aware

Nexus専用にしません。最初は7DTD + MO2に集中します。将来のSite AdapterとGame Adapterの境界は維持します。

## 4. 将来アーキテクチャ

```text
Human browser surface
  -> page observation
  -> MOD identity confirmation
  -> Local context
  -> Inspector / Compare / Diagnosis

Agent browser boundary
  -> Web observation and evidence
  -> agent read model

MO2 source
  -> MO2 Adapter
  -> source snapshot
  -> Game Adapter
  -> Local Mod Knowledge
  -> query / reverse index

Runtime tool
  -> Runtime Adapter
  -> runtime evidence
  -> evidence comparison

Optional future write plane
  -> dry-run
  -> explicit approval
  -> MO2 operation
```

各レイヤーは、責務と根拠の種類を共有しません。

- Browsing LayerはWeb pageを扱います。
- Local Mod Knowledge LayerはMO2 sourceから生成します。
- Context LayerはpageのMOD identityとprofileを照合します。
- Analysis Layerはstatic evidenceまたはruntime evidenceを解釈します。
- Write planeはread planeから独立します。

## 5. Local Mod Knowledgeの進化

### 5.1 初期対象

最初の対象は7DTD + MO2です。

Local Mod Knowledgeは、次を扱います。

- MO2 instanceとprofile
- modlist
- enabled状態
- priority
- MOD directory
- ModInfo.xml
- MOD内file list
- Config XML
- XML patch operation
- target XML
- XPath
- entity、property、attribute
- reverse reference
- source path
- parser version
- diagnostics

### 5.2 将来の知識拡張

必要性が確認できた場合に、次を追加します。

- game versionとの関係
- known dependencies
- compatibility guideとの関係
- Web pageのauthor、version、category
- user-confirmed tags
- static conflict candidates
- runtime-observed changes
- effective resultの説明

Web pageの情報とMO2の事実を同じsourceとして扱いません。各値のprovenanceを保持します。

## 6. Browsing LayerとSite Adapter

### 6.1 Generic page observation

未知サイトでも、URL、title、基本page content、取得時刻、extraction statusを扱えることを目指します。

pageからMOD identityが自動確定しない場合があります。その場合は、ユーザー確認またはunresolvedを使います。

### 6.2 Site Adapter

既知サイトの構造化情報は、Site Adapterから追加します。

候補情報は、MOD name、author、version、game、required game version、dependencies、download information、description、categoryです。

Site Adapterは任意です。Site Adapterがない場合も、generic page observationを利用できます。

Site Adapterは、ModScopeのcore identity registryにはしません。ページ構造の変化、認証、rate limit、利用規約、licenseを考慮します。

### 6.3 Human browserとagent browser

Human browserは、閲覧、navigation、ユーザー確認を主目的にします。

Agent browserは、Web explorationとevidence取得を主目的にします。

同じLocal Mod Knowledgeを利用できます。ただし、browser engine、login session、vendor API、操作UIは共有前提にしません。

Kitesurfなどの外部backendは候補です。ModScopeを特定backendに依存させません。

## 7. Browser engineの調査方針

Browser engineを自作しません。

候補は次のとおりです。

- Windows WebView2などのembedded WebView
- 既存system browserとの連携
- browser extensionとlocal companion
- browser automationを使うprototype

評価項目は次のとおりです。

- navigationとJavaScript互換性
- authenticationとcookie
- page observationの取得
- local dataとのsecurity boundary
- page scriptからの隔離
- Windows配布と更新
- licensingとmaintenance
- Site Adapterとの接続性

最終選定は、最小vertical sliceで検証した後に行います。

## 8. Codexとexternal agent integration

Codexは、ModScopeのcore productではありません。Codexは、Local Mod Knowledgeを利用できるagentの一つです。

agent accessは、次の境界で設計します。

- 小さなquery result
- source reference
- evidence type
- uncertainty
- diagnostic
- raw detailへの明示的な参照

MCP、local API、CLI、structured filesのどれを採用するかは後で比較します。transportをLocal Mod Knowledgeの意味モデルに埋め込みません。

## 9. Semantic conflict analysis

将来のConflict Analyzerは、同名fileの一致だけを検出しません。

次を扱える構造を目指します。

- append
- prepend
- set
- setattribute
- remove
- removeattribute
- insertBefore
- insertAfter
- XPath interaction
- target XML
- attribute
- operation sequence
- MO2 priority

結果には、次を含めます。

- conflict candidate
- 関係するMOD
- operation sequence
- expected effective result
- static evidence
- confidence
- unknown operation
- 判定不能の理由

判定不能な状態を、正常またはconflictと断定しません。

## 10. Runtime evidence

RuntimeOCDは、検証と補強のための外部実装です。ModScopeの中心ではありません。

Runtime Adapterは、license、公開仕様、input、output、ログ形式を確認してから追加します。

保持するruntime evidenceは、次のとおりです。

- evidence source
- tool version
- game version
- capture time
- MOD identity
- targetまたは関連target
- observed operationまたはresult
- raw log reference
- import diagnostic

static evidenceとruntime evidenceを比較します。差異はinferenceまたはdiagnosticとして追跡します。

Phase 5-Aでは、外部toolに依存しないruntime evidence契約を追加します。
runtime evidenceは、tool、version、game version、capture time、explicit snapshot、MOD identity、target XML、XPath、observed operationまたはresult、raw log reference、diagnosticを保持します。
raw resultとruntime evidenceをstatic evidenceから分離します。
比較keyは正規化したtarget XML + XPathです。
比較結果は`Match`、`Different`、`RuntimeOnly`、`StaticOnly`、`Unknown`です。
assessmentが不足するruntime observationと、重複keyの食い違うassessmentは`Unknown`として保持します。
Query APIは現在のsnapshotと一致するexplicit snapshotだけを受け付けます。

Phase 5-Bでは、公開schema、license、anonymous sample log、tool versionとgame versionの取得方法を確認できた外部Runtime Adapterだけを追加します。
RuntimeOCDのschemaが確認できない場合は、RuntimeOCD Adapterを追加しません。

2026-08-13時点のGate確認では、手元のRuntimeOCD 0.15.2からtool versionだけを確認できました。
version-specificなlog schema、parser配布条件、匿名sample log、game version取得方法は未確認です。
この状態では、RuntimeOCD Adapterを作成せず、Phase 5-Aのstatic-only境界を維持します。

## 11. Read / writeの将来境界

Read planeは、MO2 source、snapshot、Local Mod Knowledge、query、Inspector、analysisを扱います。

Write planeは、必要性が確認できた場合だけ追加します。

候補はenable / disable、reorder、profile変更です。

Write planeには、次を要求します。

- read planeからの独立
- dry-run
- 変更前後のdiff
- 対象profileとMODの明示
- 明示承認
- 失敗時の復旧方針
- MO2の実仕様に基づく検証

Write planeが追加されても、ModScopeはMod Managerにはなりません。

## 12. Roadmap

### Phase 0：設計、仕様確認、fixture

source boundary、page observation、MOD identity confirmation、Local context、evidence modelを定義します。

### Phase 1：v0.1 Browser-first vertical slice（完了）

7DTD + MO2の1 profileをread-onlyで読み取ります。WPF + WebView2上でpage observationを取得します。ユーザー確認したMOD identityとcurrent profileを照合します。Inspectorで根拠を表示します。

v0.1は、site固有Adapter、複数game、完全なsemantic conflict、RuntimeOCD、MO2 write、特定agent backendを含めません。

### Phase 2：Structured Local Mod Knowledge（完了）

ModInfo、Config XML、patch operation、target、XPath、reverse indexを拡張します。
MO2のouter directoryと、7DTDが読むinner MOD rootを分離して保持します。
outer直下のrootは`Source`で記録します。
outer直下の子directoryのrootは`Inference`で記録します。
depth 2以上の候補はdiagnosticだけを残します。
rootを解決できないouterは、MOD recordを作らず、raw inventoryとmanifestへ保持します。
MO2 source discoveryを追加します。
実行中MO2、remembered source、`%LOCALAPPDATA%\ModOrganizer`、last-used instance情報、native pickerを既知の探索対象にします。
portable instanceとglobal instanceをcandidateとして統合します。
candidateにはreadiness、discovery evidence、diagnosticを保持します。
ready candidateが1件なら自動読込します。
複数件ならsource cardで選択します。
external Mods / Profiles pathは、MO2設定から解決したread-only pathだけを扱います。
RuntimeOCDの実行時結果、semantic conflict、effective result、MO2 writeは後続Phaseで扱います。

### Phase 3：Query、Inspector、必要なSite Adapter（完了）

neutral read modelを安定させます。必要性が確認できたsiteだけAdapterを追加します。

Phase 3完了メモ（2026-08-13）：

Local Mod Knowledgeのforward / reverse queryとInspector read modelを完了しました。
Query semanticsはsynthetic fixtureで再現可能に検証します。
実MO2のread-only統合確認は、source load、profile projection、page observation、Local context、Inspectorを対象にしました。
Site Adapter、agent transport、Compare UI、Runtime evidence、MO2 writeはPhase 3外として保留します。

### Phase 4：Semantic conflict（完了）

Phase 4では、active profileのenabled MODを、target XMLとXPathごとに解析します。
priority sequenceは`0→N`で表示し、実ゲーム上の勝者方向は未検証のinferenceとして扱います。
confidenceは、計算済みの`Compatible` / `Conflict`を`High`、`Possible`を`Medium`、`Unknown`または未評価を`Unknown`として表します。

`Data/Config` directoryは入力で明示します。基準XMLはread-onlyで読み、結果には相対path、SHA-256、`GameDataFile` source referenceだけを残します。

effective subsetは`set`、`setattribute`、`remove`、`removeattribute`、属性値への単純な`append`です。
child fragment、`prepend`、`insertBefore`、`insertAfter`、`csv`、unknown operation、未評価XPathはunknownとして保持します。
base XMLの欠落、parse失敗、XPath no matchもunknownです。

Query layerの`AnalyzeConflicts`は、target XML、XPath、limitでread modelを絞れます。
Desktop、Web、Runtime evidence、MO2 write、完全なXML patch engineはPhase 4に含めません。

### Phase 5：Runtime evidence

Phase 5-Aでは、中立runtime evidence契約を取り込み、static conflict resultとtarget XML + XPathで比較します。
Phase 5-Bでは、公開schemaとlicenseを確認できた外部Runtime Adapterだけを追加します。

### Phase 6：Workspace UIの拡張

Browser page、Local context、Inspector、Compare、Diagnosisを拡張します。高密度Mod Manager UIは作りません。

### Phase 7：Controlled write

必要性が確認できた場合に、dry-runと明示承認付きのMO2操作を追加します。

### Phase 8：Game Adapter拡張

第二gameへの需要と共通性が確認できた場合だけ対応します。

## 13. 最終的な成功条件

ModScopeは、次の状態を目指します。

- Web上のMOD探索とlocal environmentの理解が一つのWorkspaceでつながる
- AIなしでbrowse、inspect、compare、diagnoseが成立する
- Agentが必要なevidenceだけをqueryできる
- MO2のsource of truthを変更せずにlocal MOD空間を理解できる
- page identity、local record、runtime resultのprovenanceを追跡できる
- unknownとinferenceをverified factと区別できる
- file overlapとsemantic conflictを区別できる
- static evidenceとruntime evidenceを比較できる
- raw runtime result、relative raw log reference、explicit snapshot binding、comparison diagnosticを保持できる
- GUIがquery layerの派生データだけを利用する
- write planeがread planeから分離されている
- 必要なときだけSite AdapterとGame Adapterを追加できる

## 14. Clear deferred work

次は、要件と検証がそろうまで保留します。

- Browser engineの最終選定
- Browser engineの自作
- 複数Site Adapter
- agent Web backendの固定
- Kitesurfなどへの必須依存
- Codex専用integration
- 複数game対応
- 完全なXML patch semantics
- RuntimeOCDの再実装
- MO2 write
- installerとdistribution
- storage engineの固定
- 大規模cloud同期

## 15. 今後の判断基準

新しい機能を追加する前に、次を確認します。

- Web上のMOD探索またはlocal environmentの理解を改善するか
- Local Mod Knowledgeに属するか
- 既存ブラウザ、MO2、external agentの責務を不必要に奪っていないか
- 今のvertical sliceで検証可能か
- source of truthを曖昧にしないか
- read-only境界を壊さないか
- evidenceとinferenceを混ぜないか
- GUIの認知負荷を増やさないか
- 将来のsemantic analysis、runtime comparison、Game Adapterを妨げないか

## 16. Web UI vertical slice

現在のWeb UIは、.NET / C#のpresentation adapterです。

- Browser WebView2は外部Web pageを表示します。
- App WebView2はSvelte frontendを表示します。
- Desktop hostはWebMessage JSON contractを検証します。
- frontendはQuery projectionだけを表示します。
- frontendはMO2 source、LocalModSnapshot、filesystemへ直接アクセスしません。

現在のUIは、固定したGlobal Browser chromeを上段に置き、ModList、Browser Content、Contextを下段に分ける3列構成です。

- Toolbar WebView2はURL、navigation、MOD list toggle、Context toggleを表示します。
- ModList WebView2はactive profileのMOD一覧とprofile selectorを表示します。
- Browser WebView2は外部Web pageだけを表示します。
- Context WebView2はLocal context、例外確認、Developer tools、Inspectorを表示します。

Toolbar、ModList、Contextは、同じSvelte bundleを別surfaceとして読み込みます。
Desktop hostは各App WebViewへ同じstate、error、ready messageをbroadcastします。
ModListの幅は約280pxです。
MOD一覧だけをスクロール可能にします。
ToolbarはBrowser操作とURLだけを1行で固定します。
ModListとContextは独立して折りたためます。
この構成は情報設計の検証用です。
将来はContextをdrawerまたはoverlayへ折り畳みます。

v0.1では、次を保留します。

- frontend routerとadvanced tab management
- Compare、Settings、downloads
- AI chat、MCP、Codex automation
- MO2 write
- browser syncとChromium bundling

## 17. Conclusion-first Web UI refinement

現在のUIは、開発者向けのsource操作よりも、pageとLocal contextの結論を優先します。

- 通常画面はページ、status、profile、enabled state、priority、version、evidenceを表示します。
- identity確認は通常工程ではなく、認識失敗時の例外導線です。
- Developer toolsはfixtureと明示sourceを検証するために残しますが、初期状態では閉じます。
- Browser navigation完了後のObserveはDesktop hostが実行します。
- Profile selectorは、MO2設定から解決したProfiles directoryにあるprofileだけをread-onlyで切り替えます。
- Profile switch後はsession、candidates、profile nameを更新し、page observationは維持します。
- ModListはactive profileの全MODをpriority順で表示します。
- profileに存在するがMOD directoryがないMODはunresolvedとして表示します。
- MOD directoryに存在するがprofileに存在しないMODは、折りたたみ式の`Profile外`欄へ表示します。
- 他profileはactive profile表示後にbackground preloadし、selectorへPending、Loading、Ready、Failedを表示します。
- background preload中もprofile switchを許可し、選択profileを優先します。
- 認識失敗時の検索drawerは、全候補をdisplay name、directory name、MOD keyで検索する補助導線として維持します。
- `ModInfo.xml`の確認済みWebsiteだけを、内蔵Browserで開きます。
- WebsiteがないMODは、URLを推測せずリンクなしで表示します。
- 認識失敗時のlocal MOD選択も、同じ検索導線を使用します。

起動時にMO2 source discoveryを実行します。
候補がない場合は再探索とnative folder pickerを使用します。
Frontendにはabsolute pathを送信しません。
page identity自動認識は実装しません。
