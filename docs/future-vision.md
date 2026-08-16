# ModScope 将来像

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

## 1. 目的

ModScopeの将来像は、MODの発見、整理、理解、導入判断、問題調査を、Web pageとLocal Mod Knowledgeの同じWorkspaceで行える状態です。

ModScopeはMO2をsource of truthとして残します。
MO2を置き換えずに、MO2から再生成可能なLocal Mod Knowledgeと、Web pageに対するLocal contextを提供します。
North Starへの寄与が明確なmanager機能は、候補へ入ります。
その機能は、Prior Art、安全境界、検証計画を満たす必要があります。

Web pageとLocal Mod Knowledgeは、製品の二つのprimary surfaceです。
Web pageは発見と外部evidenceを扱います。
Local Mod Knowledgeは、現在のMOD環境の整理、理解、比較、診断、必要な操作を扱います。
現在のv0.1 delivery shapeはBrowse-first vertical sliceです。
Local Mod Knowledgeを補助面へ限定する意味ではありません。

## 2. 中心となる利用体験

### 2.1 Human browser

ユーザーは、Nexus Mods、ランキングサイト、GitHub、Wiki、forum、作者サイト、ガイドサイトなどを自由に閲覧します。

ModScopeは、現在のpageを主画面に置きます。
page observationのURLとtitleから候補MODを示します。
強い一致が1件だけの場合はidentityを自動確認します。
複数候補、弱い一致、unresolved recordはユーザー確認へ戻します。

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

### 二つのprimary surface

Web pageは、MODの発見、作者の説明、compatibility guide、issue、Wiki、GitHubなどの外部evidenceを扱うprimary surfaceです。

Local Mod Knowledgeは、MO2 profile、package、Modlet、archive、version、source、dependency、conflict、runtimeなどの関係を扱うprimary surfaceです。

初期画面は、二つのsurfaceを必要な深さで表示します。高密度なmanager UIを既定の主画面にせず、情報量と認知負荷を制御します。

### Local context is progressive disclosure

local metadata、installed状態、profile、overlap、dependenciesを必要な範囲で表示します。raw XMLと巨大なgraphを初期表示しません。

### Everything useful without AI

browse、inspect、compare、local environmentの理解は、AIなしで成立します。

### Everything easy to inspect or automate with AI

人間向け表示とagent向け出力は同じ根拠へリンクします。AIだけの隠れた状態を作りません。

### Evidence before inference

source、normalized value、static evidence、runtime evidence、inference、uncertainty、diagnosticを分離します。

情報不足の場合は推測で確定しません。

`Unknown`、`Unresolved`、`Not assessed`は正常な結果です。

### Prior Art First

関連する成熟製品が既に解決している機能を確認してから、重複機能を設計します。

MO2、Wabbajack、Vortexを置き換えず、interoperability、reuse、complementary layerを優先します。

source claimはruntime verificationではありません。

dependencyはcompatibilityではありません。

manifest co-presenceはruntime evidenceではありません。

### Controlled write after read verification

通常のLocal Knowledgeはread-onlyです。controlled writeはread planeから分離します。
Phase 7では、明示承認付きのprofile edit、junction deploy、Steam起動だけを扱います。

### Site-independent and game-aware

Nexus専用にしません。最初は7DTD + MO2に集中します。将来のSite AdapterとGame Adapterの境界は維持します。

## 4. 将来アーキテクチャ

```text
Web page primary surface
  -> page observation
  -> MOD identity confirmation
  -> Local context
  -> Inspector / Compare / Diagnosis

Local Mod Knowledge primary surface
  -> MO2 source
  -> MO2 Adapter
  -> source snapshot
  -> Game Adapter
  -> Local Mod Knowledge
  -> Mod Library / Inspector / Compare / Diagnosis

Agent browser boundary
  -> Web observation and evidence
  -> agent read model

Shared read model
  -> query / reverse index
  -> evidence / provenance / uncertainty

Runtime tool
  -> Runtime Adapter
  -> runtime evidence
  -> evidence comparison

Controlled write plane
  -> deployment.preview
  -> explicit approval
  -> deployment.apply
  -> modlist backup / junction transaction / verification
  -> optional Steam launch
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

pageからMOD identityが自動確定しない場合があります。
その場合は、候補表示、ユーザー確認、またはunresolvedを使います。
自動認識は正規化したURLとtitleだけを使い、page本文は使いません。

強いURL一致は、scheme、query、fragment、末尾slashを正規化したhost/pathの一致です。
強い名称一致は、Unicode、空白、大文字小文字を正規化したtitleと、ModKey、DisplayName、DirectoryNameの完全一致です。
部分一致は候補表示だけに使います。

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

Phase 5-Bでは、Runtime comparisonだけを対象にしたLocal-only RuntimeOCD Adapterを追加します。

DL、導入、初回セットアップ、UI、Desktop、MO2 write、RuntimeOCD本体の再配布は対象外です。
ModScopeはRuntimeOCDのバイナリまたはソースを同梱しません。公開ソースは挙動確認の根拠だけに使います。

RuntimeOCD 0.15.2の手元ログでは、カテゴリdirectory、複数行になる説明block、説明blockの最後の`Source`行を確認しました。
Adapterはこの限定的なrecord形を独立実装します。正式schema互換性は主張しません。

`ToolVersion`は`0.15.2`だけを通常対応とします。欠落または別versionはdiagnosticを保持し、comparison statusを`Unknown`にします。
`GameVersion`はexplicit valueだけを使います。欠落時はdiagnosticを保持し、推測しません。

カテゴリ`R`、`EO`、`SC`、`AO`、`FP`はoperationと分けて保持します。
カテゴリから`NormalizedAssessment`へ自動変換しません。assessment不足は`Unknown`です。

Target XMLがない場合は、正規化XPathに一致するstatic candidateが1件だけのときに限り推定します。
候補が0件または複数件の場合は`Unknown`です。MOD identityだけでは推定しません。

Query APIは、現在ロード中のsnapshotと一致するexplicit `SnapshotId`だけを受け付けます。
raw resultとrelativeな`RuntimeLog` referenceはLocalKnowledgeだけに保持します。
read modelへraw log本文、raw result、raw log reference、絶対pathを出しません。

正式なversion-specific schema、anonymous sample log、game version取得方法は未確認として残します。
この不足は、将来の正式RuntimeOCD distribution adapterを追加する前に解決する課題です。

## 11. Read / writeの将来境界

Read planeは、MO2 source、snapshot、Local Mod Knowledge、query、Inspector、analysisを扱います。

Phase 7のWrite planeは、次を対象にします。

- 選択中MO2 profileの`modlist.txt`のenabled状態と順序
- MO2 `modsPath`の既知MOD rootを指す、Steam game rootの`Mods`配下junction
- Apply成功後の固定Steam URI起動

Write planeには、次を要求します。

- read planeからの独立
- dry-run
- 変更前後のdiff
- 対象profileとMODの明示
- 明示承認
- 失敗時の復旧方針
- MO2の実仕様に基づく検証

Game rootはMO2 `ModOrganizer.ini`の`General.gamePath`から読み取ります。Steam libraryの全探索は行いません。GamePathの絶対値はfrontendへ渡しません。

Apply前にMO2または7DTDが実行中の場合、processを停止せずblockします。失敗時はjunctionとmodlist backupを使ってrollbackします。rollback失敗時は`recovery-required`を返します。

既存junctionは、選択中MO2 `modsPath`の既知MOD rootへ解決できる場合だけModScope管理manifestへ採用します。foreign junction、実folder、重複MOD名、path衝突はApplyを止めます。

Write planeは、MO2のsource of truthを保護する限定された操作面です。
Write planeの存在はMO2の置き換えを意味しません。
将来のmanager機能も候補にできます。
その機能は、North Starへの寄与、Prior Art、安全境界、検証計画を満たす必要があります。

## 12. Roadmap

### Phase 0：設計、仕様確認、fixture

source boundary、page observation、MOD identity confirmation、Local context、evidence modelを定義しました。

### Phase 1：v0.1 Browser-first vertical slice（完了）

7DTD + MO2のread-only Browse → Recognize → Local context → Inspectorを成立させました。
site固有Adapter、複数game、完全なsemantic conflict、RuntimeOCD、MO2 write、特定agent backendは将来範囲です。

### Phase 2：Structured Local Mod Knowledge（完了）

structured Local Mod Knowledge、MO2 source discovery、profile projectionを追加しました。
MO2のouter / inner root、discovery evidence、diagnostic、read-only pathを分離して保持します。
RuntimeOCD、semantic conflict、effective result、MO2 writeは後続Phaseの責務です。

### Phase 3：Query、Inspector、必要なSite Adapter（完了）

neutral read model、forward / reverse query、Inspector read modelを完了しました。
必要性が確認できたsiteだけAdapterを追加します。
agent transport、Compare UI、Runtime evidence、MO2 writeは別Phaseです。

### Phase 4：Semantic conflict（完了）

active profileのenabled MODをtarget XMLとXPathごとに解析します。
effective subset、unknown operation、diagnostic、priority方向のuncertaintyを保持します。
Desktop、Web、Runtime evidence、MO2 write、完全なXML patch engineは別の責務です。

### Phase 5：Runtime evidence

neutral runtime evidenceとLocal-only RuntimeOCD comparisonを実装しました。
正式schema互換性、RuntimeOCD本体の再配布、MO2 writeは含めません。

### Phase 6：Workspace UIの拡張

Browser page、Local context、Inspector、Compare、DiagnosisをBrowse-first UIへ統合しました。
高密度なmanager UIを既定の主画面にしません。
MO2のsource of truthを直接置き換えない境界を維持します。

### Phase 6.5：Browse-first UI情報設計の整理（完了）

ModScope view、progressive disclosure、bounded history、Chrome型Toolbarを整理しました。
Bridge contractはversion `2`です。
page本文、absolute path、cookie、認証情報を永続化しません。
MO2 write、AI、MCP、独自Browser engine、browser syncは追加しません。

### Phase 6.6：情報減算UI（完了）

通常画面はWeb pageとLocal Mod Knowledgeを主役にします。
Contextには`RECOGNIZE`と最小Local summaryを表示します。
詳細なevidence、diagnostic、XML、priorityはInspectorへ段階表示します。
MO2 read-only境界、Query層、Bridge contract、Desktop host、Browser engineは変更しません。

### Phase 6.7：起動表示・Chrome風Toolbar・MOD URL導線整理（完了）

LOADING PROFILE表示、Chrome型Toolbar、History page、Mod URL解決の境界を整理しました。
ExactなNexus identity、source-backed Website、一意一致したNexus検索結果を区別し、曖昧な候補は検索ページに残します。
navigation intentはUiState、History metadata、Local Knowledgeへ保存しません。

### Phase 6.8：ロード遮断、Chrome palette、History page（完了）

foreground loading中の入力遮断、Chrome dark palette、History pageを実装しました。
HistoryにはURL、title、訪問時刻だけを保存し、page本文、absolute path、cookie、認証情報を保存しません。

### Phase 6.9：Nexus検索によるMODページ解決（完了）

Exactなpackage identityがある場合は7DTD Nexusの数値MOD IDから直接遷移します。
identityがない場合は7DTD Nexus検索から同一hostの数値MOD IDを候補化します。
検索名ごとの正規化後の完全一致が1件の場合だけ遷移し、空、曖昧、解析不能な結果は検索ページに残します。

### Phase 7：Controlled write（実装済み・freeze）

Controlled profile edit、junction deploy、Steam起動をwrite planeへ分離して実装しました。
Preview、明示承認、backup、rollback、再読検証を必須にします。
実環境owner Playcheckは、automated test、build、GUI evidenceとは別の証拠クラスです。
既存実装は削除せず、安全境界と回帰対象として保持します。
新しいDeployment拡張はfreezeします。

### Active focus：Evidence-first Local Knowledge

次のactive focusは、Evidence-first Local Knowledgeです。
既存のBrowser、Runtime、Deploymentを削除せずにfreezeし、次の4軸へ集中します。

1. **Version provenance**：
   `ModInfo.xml`、MO2 `meta.ini`、archiveまたはdownload metadata、Nexus、GitHub、Wabbajack、game versionを別observationとして保持します。
   identity未解決または比較不能なversion schemeは、確定比較へ昇格しません。
2. **Historical artifact**：
   old version、deprecated artifact、過去source recordを、current identityやcurrent versionと分離して説明します。
   old binaryを保管しません。
3. **Real-world validation**：
   大規模real-world profileで、identity、version、missing source、deprecated artifactのevidence coverageと失敗率を測定します。
   absolute path、account identifier、認証情報は保持しません。
4. **Identity resolution**：
   Web pageとlocal package、Modlet、archive、source recordのrelationを、AutoResolved、HumanReview、PartiallyResolved、Unresolvedへ分けます。
   package identity、version agreement、release associationを別結果として保持します。

active backlogは、[Evidence-first Local Knowledge milestone](https://github.com/wakadorimk2/modscope/milestone/6)の[Version provenance (#28)](https://github.com/wakadorimk2/modscope/issues/28)、[Historical artifact (#29)](https://github.com/wakadorimk2/modscope/issues/29)、[Real-world validation (#30)](https://github.com/wakadorimk2/modscope/issues/30)、[Identity resolution (#31)](https://github.com/wakadorimk2/modscope/issues/31)で管理します。

co-presence、list membership、manifest membership、名前の類似だけからdependency、compatibility、runtime保証を推測しません。

### Phase 8：Game Adapter拡張（Deferred）

第二Game AdapterはDeferredのまま保持します。
Evidence-first Local Knowledgeの4軸、需要、共通性、検証可能性が確認できるまで拡張しません。

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
- controlled write plane外のMO2 write
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
- controlled write境界とread-only境界を壊さないか
- evidenceとinferenceを混ぜないか
- GUIの認知負荷を増やさないか
- 将来のsemantic analysis、runtime comparison、Game Adapterを妨げないか

## 16. Current implementation reference

現在のWeb UI、Browser surface、Bridge contract、Conclusion-first表示、Deployment previewの仕様は、[ModScope設計](design.md)を正本とします。

将来像では、現在のUI細部を再記載しません。
将来の判断は、Web pageとLocal Mod Knowledgeの二つのprimary surfaceに基づきます。
Local contextはprogressive disclosureします。
AIなしでのinspectを維持します。
evidence before inferenceとcontrolled writeの原則を維持します。
