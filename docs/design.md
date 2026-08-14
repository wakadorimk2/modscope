# ModScope 設計

## 1. 文書の役割

この文書は、ModScopeの製品定義、現在のアーキテクチャ、責務境界、v0.1の範囲を定義します。

現在のリポジトリはLocal Knowledge基盤とGUI縦切りの実装フェーズです。
Local KnowledgeとQueryはC# / .NET 8で実装します。
DesktopはWPF / .NET 10で実装します。
独自Browser engine、CLI、任意のMO2管理操作は実装しません。Phase 7では、controlled write planeだけを実装します。

v0.1は、AI用index単体ではありません。7DTD + MO2のLocal Mod Knowledgeと、最小のBrowse → Recognize → Local awareness → Inspectを検証するvertical sliceです。

## 2. Product definition

ModScopeは、MODをWeb上で探し、吟味し、比較する作業を、ユーザーのローカルMOD環境の文脈付きで行うMOD Workspace / Browserです。

画面の主役は、ユーザーが現在見ているWeb pageです。Local Mod Knowledgeは、必要なときにpageの横または下へ表示するcontextです。

この文書では、次の用語を使います。

- page observation：Human browserまたはagent browserから取得したURL、title、基本page contentなどの事実
- MOD identity confirmation：pageが示す候補MODを、高信頼自動認識またはユーザーが確認した状態
- Local context：確認したMOD identityと、現在のMO2 profileを照合した派生結果
- Inspector：Local contextの根拠と詳細へ段階的に進むための画面またはread model

ModScopeは、AIを使わなくてもbrowse、inspect、compare、local environmentの理解が成立することを目指します。AI agentは、同じLocal Mod Knowledgeへ効率よくアクセスできます。

## 3. Core problem

現在のMOD調査では、Web上の候補MODと、MO2が管理する現在環境を別々に確認します。

この分離には、次の問題があります。

- Web pageを見ながらinstalled状態を確認しにくい
- active profile、enabled状態、priorityをその場で確認しにくい
- 類似MOD、dependencies、known overlapを同じ文脈で確認しにくい
- MO2の多数のMODとXMLを毎回全文探索する必要がある
- file overlapとsemantic XML conflictを区別しにくい
- AI agentへ大量のraw XMLや巨大なJSONを渡すとcontext効率が低下する
- MO2のsource of truthとModScopeの派生データが混ざりやすい

ModScopeは、MO2のデータを移行しません。MO2をsource of truthとして読み取り、Web pageとlocal environmentを結ぶ再生成可能なKnowledge Layerを提供します。

## 4. Primary user workflow

### Discover

ユーザーは、Nexus Mods、ランキングサイト、GitHub、Wiki、forum、独立系MODサイト、ガイドサイトなどを閲覧します。

### Recognize

ModScopeは、page observationのURLとtitleから候補MODを示します。
正規化したURLまたは名称の強い一致が1件だけの場合は、hostがidentityを自動確定します。
複数候補、弱い一致、unresolved recordは自動確定しません。
ユーザーはいつでも候補を検索してidentityを手動確認できます。

### Local awareness

ModScopeは、確認したMOD identityを、現在のprofileと照合します。

表示する情報は次のとおりです。

- installed / not installed / unresolved
- active profile
- enabled状態
- priority
- known version
- known dependencies
- known overlap
- unknownまたはnot assessedの理由

情報が不足する場合は、推測で埋めません。unknownとして表示します。

### Inspect

ユーザーは、必要なときだけInspectorを開きます。

Inspectorは、metadata、files、Config XML、patch operation、target XML、XPath、attribute、diagnostic、provenanceを表示します。

### Compare

ユーザーは、候補MODと現在環境を比較します。比較結果は、static evidenceとuncertaintyを含む説明単位にします。

### Diagnose

ユーザーは、overlap、priority、XML patch interaction、semantic conflictの可能性を調査します。

v0.1では、完全なsemantic conflict判定を行いません。判定できない状態をunknownまたはpossibleとして残します。

### Advanced exploration

Codexなどのagentは、Local Mod Knowledgeをqueryします。必要な場合だけ、別のagent browser backendからWeb evidenceを取得します。

## 5. Product boundaries

### 5.1 Mod Managerではない理由

MO2は、installation、enable / disable、priority、profile、virtual filesystem、launchを担当する既存ツールです。

ModScopeは、MO2を置き換えません。初期段階ではMO2をread-onlyで読み取ります。

MO2操作は、read layerから独立したwrite layerに置きます。Phase 7のwrite planeはprofileの`modlist.txt`と、7DTD game rootの管理junctionだけを対象にします。Mod Manager全体は作りません。

### 5.2 Browsingがprimary surfaceである理由

MODの発見と評価は、MOD一覧から始まるとは限りません。ランキング、compatibility guide、issue、Wiki、作者説明、GitHubなどのWeb contentから始まります。

したがって、最初に表示する対象はMOD一覧ではなく現在のWeb pageです。Local contextは、pageを理解するための補助情報としてprogressive disclosureします。

### 5.3 Goals

- Web上のMOD探索とlocal environmentの理解を結び付ける
- MO2をsource of truthとして扱う
- 7DTD + MO2のLocal Mod Knowledgeを構造化する
- URL、title、基本page contentを扱う汎用Browsing Layerを持つ
- site固有情報を任意のSite Adapterで追加できる境界を持つ
- Search、reverse index、Inspectorで必要な証拠だけを返す
- 人間とagentが同じread modelを利用できるようにする
- static evidence、runtime evidence、inference、uncertaintyを分離する
- 将来のsemantic conflict analysisとcontrolled writeを妨げない

### 5.4 Non-goalsとdeferred work

- MO2本体の置き換え
- 独自Mod Managerの機能一式
- 初期からの複数game対応
- Nexus Mods専用設計
- Browser engineの自作
- RuntimeOCDの再実装
- 特定AI製品への密結合
- 初期画面の高密度Mod一覧
- v0.1での完全なsemantic conflict判定
- v0.1でのRuntimeOCD連携
- 任意のMO2管理操作と、未承認のMO2 write
- MO2設定にない外部profile pathの探索
- v0.1での複数Site Adapter

## 6. Conceptual architecture

```text
MO2 source
  -> MO2 Adapter
  -> source snapshot
  -> 7DTD Adapter
  -> Local Mod Knowledge
  -> Search / reverse index / read model
  -> Local context / Inspector / Compare / Diagnosis

Human browser
  -> Browsing Layer
  -> page observation
  -> MOD identity confirmation
  -> Local context

Agent browser
  -> separate Web exploration boundary
  -> page observation and Web evidence
  -> Local context or agent read model

Runtime evidence
  -> Runtime Adapter
  -> separate runtime evidence
  -> comparison layer

Controlled write plane
  -> deployment.preview
  -> explicit approval
  -> deployment.apply
  -> modlist backup / junction transaction / verification
  -> optional Steam launch
```

各矢印は責務境界を示します。

- MO2 Adapterは、解決済みのMO2 sourceを読み取ります。
- 7DTD Adapterは、7DTD固有のmetadataとXML形式を解釈します。
- Local Mod Knowledgeは、MO2 sourceから生成する再生成可能な派生データです。
- Browsing Layerは、Web pageとpage observationを扱います。
- Local contextは、page側のMOD identityとlocal profileを結ぶ派生結果です。
- Runtime evidenceは、static evidenceと別に保持します。
- write planeは、read planeと別の責務です。

## 7. Local Mod Knowledge Layer

Local Mod Knowledgeは、ModScopeのcore assetです。GUIだけの内部データにしません。

### 7.1 v0.1の入力境界

解決した、7DTD + MO2の1 instanceを読み取ります。
active profileを先に読み取ります。
Profiles directoryからread-onlyのprofile catalogを取得し、active表示後に他profileをbackground preloadします。

- profileの`modlist.txt`
- `mods/`内のMOD directory
- MOD内のファイル一覧
- `ModInfo.xml`が存在する場合のmetadata
- `Config/**/*.xml`
- XML patch operationのraw情報と、確認できるnormalized情報

MO2のdownloadsとvirtual filesystemは、v0.1の必須入力にしません。

MO2 sourceは、既知の場所と実行中MO2からread-onlyで候補化します。
全ドライブの再帰探索は行いません。探索範囲と待ち時間を予測可能にするためです。
候補は、実行中MO2、前回成功したsource、`%LOCALAPPDATA%\ModOrganizer`直下、last-used instance情報、native pickerの順で追加します。
portable instanceはMO2実行ファイルの親directoryまたはpickerで選択したrootから解決します。
global instanceは`%LOCALAPPDATA%\ModOrganizer\<instance>`から解決します。
候補のreadinessは、`gameName`、well-formedな`ModOrganizer.ini`、`mods`、`profiles`、selected profileの`modlist.txt`で判定します。
候補のevidenceは、`RunningProcess`、`Remembered`、`GlobalInstance`、`NativePicker`と、`Source`または`Inference`を分離して保持します。
候補IDと候補順は、正規化したroot、profile、evidenceの規則から決定します。
前回成功したsourceは`%LOCALAPPDATA%\\ModScope\\mo2-source.json`へrootとprofileだけを保存します。
保存pathは起動時に再検証します。無効な保存pathは自動選択しません。
Developer toolsでは、明示的なsource path入力を引き続き使用できます。

MO2設定がinstance外のModsまたはProfiles directoryを指定する場合があります。
ModScopeは、INIに明示され、正規化後に存在し、reparse pointでなく、Profiles側に`modlist.txt`があるpathだけをread-onlyで扱います。
ModScopeは、解決したpathへ書き込みません。

### 7.2 保持する事実

#### Source snapshot

- snapshot id
- source root
- instanceとprofile
- created time
- schema version
- parser version
- input manifest
- diagnostics

#### Profile state

- raw modlist line
- normalized MOD identity
- enabled / disabled
- priority
- MO2 outer directory
- resolved 7DTD inner root
- unresolved reason

#### MOD record

- stable MOD id候補
- MO2 outer directoryとroot resolution
- resolved inner root directory name
- root discoveryのevidence種別
- display name候補
- enabled状態
- priority
- ModInfo metadata
- file references
- diagnostics

MO2 profile entryはouter directory単位で保持します。
7DTD MOD recordはresolved inner root単位で生成します。
outer直下の`ModInfo.xml`は`Source`としてrootを解決します。
outer直下の子directoryにある`ModInfo.xml`は`Inference`としてrootを解決します。
子directoryのrootは、1つのouterから複数生成できます。
2階層以上の`ModInfo.xml`候補はraw path付きdiagnosticだけを保持します。
rootを解決できないouterはMOD recordを生成しません。
そのouterのraw inventory、manifest hash、`mod.root.not_found`を保持します。
stable MOD keyは`mods/`からのnormalizedな`outer`または`outer/inner` pathです。
`ModInfo.Name`はmetadataであり、stable MOD keyに使いません。

#### MO2 source candidate

- instance name
- game name
- profile name
- candidate readiness
- discovery evidence
- diagnostic

Frontendにはabsolute pathを送信しません。
Desktop hostはcandidate IDから内部保持したpathを解決します。

#### File record

- MOD
- normalized relative path
- file type
- size
- content fingerprint
- parse status
- source reference

#### XML documentとpatch operation

- relative path
- target XML候補
- encoding
- well-formed status
- node summary
- raw operation name
- normalized operation kind候補
- raw XPath
- normalized XPath候補
- attribute
- patch fragment reference
- source location
- diagnostics

未知のoperation、属性、elementは破棄しません。raw情報とdiagnosticを保持します。

### 7.3 Provenanceとevidence

各結果は、次の区分を持ちます。

- source：MO2またはWeb pageから直接得た事実
- normalized：parserが構造化した値
- static evidence：local fileとXMLから導いた事実
- runtime evidence：実ゲーム実行時に観測した事実
- inference：複数のevidenceから導いた推測
- uncertainty：確認できない事項
- diagnostic：解析できなかった理由

source path、page URL、取得時刻、parser version、snapshot idなど、再確認に必要なreferenceを保持します。

## 8. Adapter boundaries

### 8.1 MO2 Adapter

MO2 Adapterは、次だけを担当します。

- MO2 source candidateの発見と検証
- portable instanceとglobal instanceの境界の保持
- 明示されたinstance、profile、Mods、Profiles pathのread-only読み取り
- modlistのraw保持とnormalized projection
- enabled状態とpriorityの取得
- MO2 outer directoryと7DTD inner rootの解決
- depth 0 / depth 1のroot discoveryと、depth 2以上のdiagnostic化
- inner rootを基準にしたMOD fileとsource referenceの解決
- 欠落、重複、未解決入力のdiagnostic化

MO2 Adapterは、MO2のファイルを書き換えません。7DTDのXML semanticsを解釈しません。

### 8.2 Game Adapter

7DTD Adapterは、次を担当します。

- ModInfo.xmlの構造化
- Config XMLの解析
- XML patch operationの抽出
- target XML、XPath、entity、property、attributeの候補化
- 7DTD固有のunknownとdiagnosticの保持

Game Adapterの境界は将来の拡張を妨げないようにします。ただし、第二gameが必要になるまで共通抽象化を増やしません。

### 8.3 Site Adapter

Site Adapterは任意です。

既知サイトでは、次の情報を構造化する候補とします。

- MOD name
- author
- version
- game
- required game version
- dependencies
- download information
- description
- category

未知のサイトでは、少なくともURL、title、基本page contentを扱います。Site Adapterがないことを理由に、page observation全体を破棄しません。

Site Adapterはlocal installationの正しさを決めません。ページ情報とlocal evidenceを分離します。

## 9. Browsing Layer

### 9.1 Human browser

Human browserは、ユーザーがWeb contentを見るsurfaceです。

v0.1では、既存Web engine上でURLを開きます。engineの選定は別途調査します。

ModScopeは、Browser engineを自作しません。ページの表示、navigation、script実行、認証、cookie、security boundaryを既存runtimeに委ねます。

### 9.2 Page observation

page observationは、次の最小情報を持ちます。

- URL
- page title
- 取得可能な基本page contentまたはcontent reference
- observation time
- source browserまたはagent backend
- extraction status
- diagnostic

ページ情報からMOD identityが確定しない場合があります。
確定しない状態を自動推測で閉じません。
自動認識はpage URLとtitleだけを使います。
page本文は自動認識の入力に使いません。

### 9.3 MOD identity confirmation

強いURL一致は、scheme、query、fragment、末尾slashを正規化したhost/pathの一致です。
強い名称一致は、Unicode、空白、大文字小文字を正規化したpage titleと、ModKey、DisplayName、DirectoryNameの完全一致です。
部分一致は候補表示だけに使います。
強い一致が同じMOD keyで1件だけの場合は、自動確定とInspector表示を行います。
それ以外は、ユーザーが候補MODを確認または選択します。

確認後、page observationとlocal MOD recordを結びます。結合できない場合は、unresolvedとして表示します。

### 9.4 Securityとprivacy

Browser Layerの研究では、次を確認します。

- page scriptとlocal dataの分離
- cookie、認証情報、page contentの扱い
- local pathやprofile情報の公開範囲
- agentへ渡す情報の最小化
- Web pageからMO2 write planeへ到達できないこと

## 10. Human browserとagent browser

Human browserは、閲覧とユーザー確認を主目的にします。

Agent browserは、Web explorationとevidence取得を主目的にします。

両者は同じLocal Mod Knowledgeを利用できます。ただし、次を共有しません。

- browser engineの実装
- login sessionの前提
- page操作のUI
- agent backendのvendor API

Agent browserの候補には、local browser automation、既存ブラウザ連携、外部agent browser backendを含めます。Cloudflare Kitesurfなどは候補の一つです。特定backendをModScopeの必須依存にしません。

## 11. Local context、Inspector、Search

### 11.1 Local context

Local contextは、MOD identity confirmationとcurrent profileを照合した派生結果です。

最低限、次を返します。

- candidate MOD identity
- installed / not installed / unresolved
- active profile
- enabled状態
- priority
- known versionまたはunknown
- known dependenciesまたはunknown
- known overlap、possible overlap、not assessed
- evidence reference
- uncertainty
- diagnostic

Web pageに情報がない場合は、dependenciesやversionを推測しません。

### 11.2 Inspector

Inspectorは、Local contextから根拠へ進むためのread modelまたは画面です。

表示順は次のとおりです。

1. 結論
2. なぜ重要か
3. 関係するMOD、profile、priority
4. evidence
5. 未確認事項
6. 必要な場合だけraw XML、XPath、attribute、patch fragment

InspectorはMO2 directoryを直接source of truthとして扱いません。query layerの派生データと、明示されたsource referenceを利用します。

### 11.3 Searchとreverse index

Forward indexは、次の追跡を可能にします。

```text
profile
  -> MOD
  -> file
  -> XML document
  -> patch operation
  -> target / XPath / attribute
```

Reverse indexは、次のqueryを低コストで提供します。

- MODからfiles、XML、targetsへ戻る
- target XMLから関係するMODへ戻る
- XPathから関係するMODへ戻る
- entity、property、attributeから関係するMODへ戻る
- 同一targetまたは同一XPathの候補を探す
- priorityによって結果が変わりそうな候補を探す
- pageのcandidate MODとlocal recordsを照合する

Query resultは、巨大なindexを返しません。結論、対象、enabled状態、priority、source reference、evidence type、uncertainty、diagnosticを含む小さな説明単位にします。

## 12. Incremental indexing

入力manifestには、少なくとも次を保持します。

- source root
- instanceとprofile
- modlist fingerprint
- MOD directory identity
- relative path
- file fingerprint
- parser version
- schema version

更新方針は次のとおりです。

- MOD fileが変わった場合は、該当fileと依存するderived recordを更新します。
- MODが追加された場合は、そのMODを追加解析します。
- MODが削除された場合は、そのMODのderived recordを削除または無効化します。
- enabled状態またはpriorityだけが変わった場合は、profile projectionとpriority依存結果を更新します。
- 影響範囲を確定できない場合は、安全側に広い範囲を再解析します。
- parser versionまたはschema versionが変わった場合は、必要な範囲を再生成します。

同じ入力、parser version、schema versionから同じnormalized resultを生成できることを目標にします。

Page observationとWeb cacheは、MO2 sourceの代替にしません。Web側の変化とlocal snapshotの変化を別に扱います。

## 13. RuntimeOCD integration boundary

RuntimeOCDはModScopeの中心ではありません。RuntimeOCDのコードをコピーまたは再実装しません。

Runtime Adapterは、ライセンス、公開仕様、入力形式、出力形式を確認した後に設計します。

外部結果は、次のruntime evidenceとして保持します。

- evidence source
- tool version
- game version
- capture time
- MOD identity
- target XMLまたは関連target
- observed operationまたはresult
- raw log reference
- import diagnostic

static evidenceとruntime evidenceは混ぜません。比較結果はinferenceまたはdiagnosticとして扱います。

### 13.1 Phase 5-A：中立runtime evidence契約

2026-08-13のPhase 5-Aでは、外部runtime toolに依存しない契約と比較処理を追加します。

- `RuntimeEvidenceDocument`は、tool name、tool version、game version、capture time、explicit `SnapshotId`、profile、observations、diagnosticsを保持します。
- `RuntimeEvidenceObservation`は、MOD identity、target XML、XPath、observed operation、raw result、raw log referenceを保持します。
- raw resultは必ず保持します。normalized assessmentは、Adapterが根拠付きで変換できる場合だけ保持します。
- raw log referenceは相対pathの`RuntimeLog` source referenceで保持します。raw log本文とabsolute pathはread modelへ出しません。
- 比較keyは正規化したtarget XMLとXPathです。MOD identityだけでstatic groupへ結び付けません。
- 比較結果は`Match`、`Different`、`RuntimeOnly`、`StaticOnly`、`Unknown`です。
- normalized assessmentが不足する場合、または同じkeyのassessmentが食い違う場合は`Unknown`とdiagnosticを返します。
- Query APIは、現在のsnapshotと一致するexplicit `SnapshotId`のruntime evidenceだけを受け付けます。
- Phase 5-AはUI、MO2 write、永続storage、汎用runtime log parserを含めません。

### 13.2 Phase 5-B：外部Runtime Adapterの条件

Phase 5-Bは、Runtime comparisonだけを扱います。DL、導入、初回セットアップ、UI、Desktop、MO2 write、RuntimeOCD本体の再配布は含めません。

RuntimeOCD Adapterは、Local-only Gateで追加します。ModScopeは、RuntimeOCDのバイナリまたはソースを同梱しません。公開ソースは挙動確認の根拠だけに使います。GPLv3のコードはコピーしません。

確認した公開資料は、[RuntimeOCD公式説明](https://community.thefunpimps.com/threads/runtimeocd.41946/)、[公開ソース](https://github.com/Aevum11/7DTD-RuntimeOCD)、[GPLv3 license](https://github.com/Aevum11/7DTD-RuntimeOCD/blob/main/COPYING)、[0.15.2変更履歴](https://github.com/7BytesToDie/mods/blob/main/RuntimeOCD/changelog.md)です。

次のGate資料は未確認のままです。

- version-specificなlog schema
- licenseとparser配布条件
- anonymous sample log
- tool versionとgame versionの取得方法

この不足は、正式なRuntimeOCD schema互換性を主張しない理由として記録します。Local-only Adapterは、独立実装した限定parserとして扱います。

#### 13.2.1 Local-only Adapterの契約

手元に展開したRuntimeOCD 0.15.2の`ModInfo.xml`から、tool version `0.15.2`は確認できました。
手元の0.15.2ログでは、カテゴリ別directory、複数行になる説明block、説明blockの最後の`Source`行を確認しました。Target XMLは独立fieldとして出力されません。

`RuntimeOcdImportRequest`は、explicit `SnapshotId`、explicit log directory、tool version、game version、capture timeを受け取ります。log directoryはread-onlyで読み取ります。絶対pathはdocument、diagnostic、read modelへ保存しません。

`RuntimeOcdAdapter`は、説明blockとその直後の`Source`行を1 observationとして解析します。カテゴリは`R`、`EO`、`SC`、`AO`、`FP`を保持します。未知カテゴリ、未知operation、壊れたrecord、duplicate observationは破棄しません。

`ToolVersion`は`0.15.2`だけを通常対応とします。欠落または別versionはdiagnosticを保持し、comparison statusを`Unknown`にします。`GameVersion`はexplicit valueを優先し、欠落時はdiagnosticだけを追加します。推測fallbackは使いません。

RuntimeOCDカテゴリから`NormalizedAssessment`への自動変換は行いません。カテゴリは実害を直接意味しません。RuntimeOCDだけのassessment不足は`Unknown`として比較します。

Target XMLがない場合は、正規化XPathに一致するstatic candidateが1件だけのときに限り推定します。推定時は`runtime.targetxml.inferred`を保持します。候補が0件または複数件の場合は`Unknown`です。MOD identityだけでは推定しません。

`Match`、`Different`、`RuntimeOnly`、`StaticOnly`、`Unknown`に加え、推定targetを伴う一致または差異を`InferredMatch`、`InferredDifferent`で表します。assessmentが不足する場合は`Unknown`です。

raw result、relativeな`RuntimeLog` reference、diagnosticはLocalKnowledgeに保持します。Query read modelは、category、operation、target、XPath、assessment、diagnosticだけを返します。raw log本文、raw result、raw log reference、絶対pathは返しません。

Query APIは、現在ロード中のsnapshotと一致するexplicit `SnapshotId`だけを受け付けます。snapshot不一致を確認するまでlog pathへアクセスしません。既存のsynthetic `CompareRuntimeEvidence`は維持します。

#### 13.2.2 Gate確認結果（2026-08-13）

正式schema、anonymous sample log、game version取得方法は未確認です。したがって、RuntimeOCD 0.15.2の完全なschema互換性は未確定です。

Local-only Adapterは、公開挙動と手元のsynthetic logで検証します。実ログはGitへ保存しません。RuntimeOCD本体の再実装と再配布は行いません。

## 14. Read / write boundary

### Read plane

Read planeは、次を担当します。

- MO2 sourceの読み取り
- source snapshotの生成
- Local Mod Knowledgeの生成
- query、Inspector、Local contextの生成
- static evidenceとruntime evidenceの読み取り

Read planeはMO2 sourceを変更しません。

### Write plane

Phase 7のwrite planeは、次の限定された操作だけを扱います。

- 選択profileの`modlist.txt`のenabled状態と順序
- MO2 `modsPath`の実MOD rootを指す、Steam game rootの`Mods`配下のjunction
- Apply成功後のSteam URI起動

実装には、次を必須にします。

- read planeからの独立
- dry-run
- 変更前後の差分
- 対象profileとMODの明示
- ユーザーの明示承認
- 失敗時の復旧方針
- MO2の実仕様に基づく検証

`General.gamePath`はMO2の`ModOrganizer.ini`から読み取ります。Steam libraryの全探索は行いません。絶対GamePathはfrontendへ渡しません。

Apply前にMO2または7DTDの実行中processを確認します。実行中の場合は停止せずblockします。

Applyは、profile、MO2 source、game `Mods`の再読、timestamp付き`modlist.txt` backup、junction差分、target検証、一時ファイル置換、再読検証、rollbackを行います。rollbackが失敗した場合は`recovery-required`を返します。

ModScopeが管理したjunctionだけを削除します。既存junctionは、選択中MO2 `modsPath`の既知MOD rootへ解決できる場合だけmanifestへ採用します。実folder、foreign junction、重複MOD名、path衝突はblockします。

## 15. v0.1 scope

### 15.1 含めるもの

- 7DTD + MO2の1 instance、active profile、read-only profile catalog
- read-onlyのMO2 snapshot
- modlist、enabled状態、priority、MOD directory、ファイル一覧
- ModInfo.xmlのmetadata
- Config XMLの軽量な構造化
- XML patch operation、target XML、XPath、attributeのraw保持と候補化
- forward indexとreverse indexの最小形
- WPF + WebView2上の最小Browse surface
- URL、title、基本page contentのpage observation
- URL/titleによるLocal MOD候補検索
- 高信頼な一意候補の自動identity確認
- ユーザーによるMOD identity confirmation fallback
- installed、not installed、active profile、known versionなどのLocal context
- Inspectorによるlocal metadata、files、XML reference、diagnosticの確認
- unknown、unresolved、not assessedの明示
- Query layerが提供するneutral read model
- page observation、自動または手動のMOD identity confirmation、Local context、Inspectorの縦切り

### 15.2 含めないもの

- site固有Adapter
- 複数game対応
- 完全なsemantic conflict判定
- RuntimeOCD連携
- 任意のMO2管理操作
- controlled write plane外のMO2へのwrite
- MO2管理操作を持つ高密度Mod Manager UI
- 特定AI製品への専用統合
- agent Web backendの固定
- Browser engineの自作

## 16. Browser engine options to investigate

技術選定は確定しません。次の候補を比較します。

- WindowsのWebView2などのembedded WebView
- 既存のsystem browserとの連携
- browser extensionとlocal companionの組み合わせ
- browser automationを利用するprototype

比較軸は次のとおりです。

- navigationとJavaScript互換性
- authenticationとcookieの扱い
- page observationの取得方法
- local bridgeの安全性
- page scriptからlocal dataを隔離できるか
- Windowsでの配布と更新
- licensingとmaintenance
- 将来のSite Adapterとの接続性

ChromiumやBrowser engineそのものを自前で実装する案は採用しません。

## 17. Agent web backend options to investigate

次の候補を比較します。

- structured page observationを受け取るlocal interface
- local browser automation
- 既存ブラウザのagent連携
- 外部agent browser backend
- Cloudflare Kitesurfなどの候補サービス

比較軸は、認証、再現性、取得できるevidence、privacy、cost、vendor lock-in、failure diagnosticsです。

Agent backendの選定は、Local Mod Knowledgeのschemaと分離します。

## 18. Implementation-agnostic decisions

現時点で確定することは、次のとおりです。

- MO2はsource of truth
- read-only first
- Browser Layerは既存Web engineを利用する境界
- Web pageはprimary surface
- Local Mod Knowledgeはcore asset
- page observationとlocal contextを分離する
- rawとnormalized valueを保持する
- static evidence、runtime evidence、inference、uncertaintyを分離する
- source referenceとprovenanceを保持する
- query resultを小さな説明単位にする
- Human browserとagent browserを分離する
- Site Adapter、Game Adapter、MO2 Adapterの責務を分離する
- Codex、Kitesurf、SQLite、JSONL、MCPなどを必須の実装選択にしない

storage engine、CLI framework、GUI framework、transport、installer、MO2 write APIは後回しにします。

### 18.1 初回実装の決定

2026-08-11の明示的な実装依頼により、Local Knowledge基盤の初回実装を開始します。

- 実装言語はC#です。
- target frameworkは.NET 8です。
- 成果物はクラスライブラリとxUnitテストです。
- 検証入力は匿名synthetic fixtureです。
- 入力パスはinstance、profile、modsを明示指定します。
- MO2 sourceはread-onlyで扱います。
- snapshot、normalized value、source reference、diagnosticを保持します。
- `modlist.txt`のraw lineを保持します。MO2の画面順はファイル末尾から始まるため、末尾の有効なprofile entryへpriority 0を付け、上へ向かって`0→N`を採番します。
- file overwriteの勝者、semantic conflict、patch operationの意味解釈は行いません。

Python、TypeScript、Browser先行の実装は今回の代替案として採用しません。

匿名fixtureでは、実MO2環境と7DTD MODの形式差を完全には検証できません。
実データによるread-only検証は、初回実装後の未確定事項として残します。

### 18.2 GUI縦切りの決定

2026-08-11の明示的な実装依頼により、最小GUI縦切りを開始します。

- `ModScope.Query`はLocal KnowledgeをDesktop向けread modelへ投影します。
- `ModScope.Desktop`はWPF / .NET 10を使用します。
- WebView2は既存のbrowser runtimeを利用します。
- WebView2とLocal contextは左右に配置します。
- page scriptへlocal MOD情報を注入しません。
- page observationはObserve操作で取得します。
- GamePathからData/Configを推定し、存在する場合は静的解析を自動開始します。
- page observation後とprofile switch後にLocal MOD候補を再検索します。
- 強い一致が1件だけの場合はidentityを自動確認し、Inspectorを開きます。
- 複数候補、弱い一致、unresolved recordは手動確認へ戻します。
- page observationはv0.1ではメモリ上のbounded previewだけを保持します。
- MO2 sourceはread-onlyです。

GUIは`LocalModSnapshot`を直接読みません。
GUIはQuery layerのprojectionだけを読みます。

## 19. Risksとunknowns

- URLとtitleだけでMOD identityを安全に候補化できるか
- 高信頼自動確認と手動fallbackが、実際の探索負荷を下げるか
- Web pageの認証、cookie、scriptを安全に扱えるか
- local profile情報を必要最小限だけpage contextへ出せるか
- Site AdapterなしでどこまでLocal contextを提供できるか
- ModInfo.xmlのschema差異を安全に扱えるか
- 7DTD XML patch semanticsの実仕様をどこまでverifiedにできるか
- malformed XML、encoding、namespace、case sensitivityをどう扱うか
- 大規模MOD空間でincremental indexingを維持できるか
- RuntimeOCDの公開仕様、license、ログ形式が十分か
- page data、local data、agent dataのprivacy boundaryを検証できるか

## 20. Research questions

- MO2のmodlist.txtの実際の行形式とpriority semanticsは何か
- profile、mods、downloads、virtual filesystemの境界は何か
- 欠落MOD、重複MOD名、無効MODをどう表示するか
- ModInfo.xmlの配置とschema差異は何か
- 7DTDのXML patch operationとtarget解決規則は何か
- XPathをどこまで正規化できるか
- WebView2、既存ブラウザ連携、extension方式の安全性と制約は何か
- Agent backendから取得可能なpage evidenceは何か
- page contentをLocal Mod Knowledgeへ保存する範囲はどこまでか
- fixtureへ含める実データをどう匿名化するか

## 21. Conceptual repository architecture

実装開始後の候補です。現在この構成を作成しません。

```text
AGENTS.md
docs/
  design.md
  future-vision.md
src/
  browsing/
    human-surface/
    page-observation/
    agent-boundary/
  local-knowledge/
    source/
      mo2/
    game/
      seven-days-to-die/
    indexing/
    query/
    provenance/
  context/
    recognition/
    local-context/
    inspector/
    compare/
    diagnosis/
  adapters/
    sites/
    runtime/
  analysis/
    conflict/
  mutation/
tests/
  fixtures/
```

Browser、Local Mod Knowledge、context projection、analysis、mutationを分離します。具体的なdirectoryやmodule名は実装時に再確認します。

## 22. Staged implementation

### Phase 0：設計とfixture

実仕様、境界、最小fixture、page observationの仮説を整理します。

### Phase 1：v0.1の最小Browser-first vertical slice

1 profileをread-onlyで読み取ります。Local Mod Knowledgeを生成します。WPF + WebView2上でpage observationを取得し、ユーザー確認したMOD identityとLocal contextを結びます。Inspectorで根拠を開きます。WebView2はbrowser engineの実装資産ではなく、Browsing Layerのhostです。

### Phase 2：structured Local Mod Knowledge（完了）

ModInfo、Config XML、patch operation、target、XPath、reverse indexを拡張します。
既存のPhase 1 Local Knowledgeを、raw、normalized、inference、diagnostic付きの静的なstructured modelへ拡張します。
MO2 source discoveryを追加します。
portable instanceとglobal instanceを、実行中MO2、remembered source、AppData、native pickerのevidence付きで候補化します。
候補が1件なら自動読込します。複数件ならPickerで選択します。
external Mods / Profiles pathは、MO2設定から解決したread-only pathだけを扱います。
RuntimeOCD、semantic conflict、effective result、MO2 writeはこのPhaseの対象外です。

完了判定メモ（2026-08-13）：
MO2 source discovery、profile切り替え、page observation、MOD identity confirmation、Local context、Inspectorを実MO2のread-only環境で確認しました。
Core、Resourceなどの配布単位とMO2 record単位のgroupingは未確定です。
Phase 2ではMO2 recordを独立したraw sourceとして保持し、package groupingは後続課題とします。

### Phase 3：QueryとInspector core（完了）

neutral read modelを安定させます。既存のLocalKnowledgeIndexを、C# Query APIからread-onlyで検索できます。
Site Adapterは、具体的な必要性が確認されるまで追加しません。

2026-08-13のPhase 3実装範囲は次のとおりです。

- `Mod`、`File`、`XmlFile`、`PatchOperation`、`TargetXml`、`XPath`、`Entity`、`Property`、`Attribute`をQuery対象にします。
- forward queryとreverse queryを、正規化後のOrdinal完全一致で提供します。
- pathの区切り文字を正規化します。`TargetXml`では`Config/`接頭辞も既存parserと同じ規則で除去します。
- Query結果はfrom、to、relation、evidence、owner MODのprofile state、enabled state、priority、operation、diagnosticを含みます。
- limitは呼び出し側が指定します。未指定時は全件を返します。
- Inspector read modelはpatch operation、target、XPath、entity、property、attribute、unknown operation、diagnosticを保持します。
- unknown operation、unresolved owner、inferenceを破棄しません。

この変更の理由は、Phase 2で生成したforward / reverse indexをQuery layerから利用できなかったためです。
影響範囲は`ModScope.Query`とQuery testsです。
Desktop bridge、Web UI、agent transportは変更しません。
Site Adapterを先に追加する代替案は、具体的なサイト要件が未確認のため採用しません。
semantic conflictとeffective resultはPhase 4の対象として残します。

Phase 3完了判定メモ（2026-08-13）：

- 自動検証では、solution全体の57テスト、solution build、frontendのcheckとbuildが成功しました。buildは警告0件、エラー0件でした。
- Query testsをPhase 3のQuery semanticsに対する再現可能な受入証拠とします。9種類のQuery node、forward / reverse query、path区切り、`Config/`接頭辞、limit、未ロードsnapshotを検証しました。
- Query resultはowner MOD、profile state、enabled state、priority、operation、evidence、diagnosticを返します。
- Inspector read modelはModInfo、file list、XML reference、unknown operation、inference、unresolved owner、diagnosticを保持します。未知の情報を破棄しません。
- 実MO2のread-only GUI確認では、native pickerでportable sourceを選択し、profile projection、`NotebookServer`のactive profile、verified Websiteのpage observation、Local MOD identity、installed、enabled、priority、version、Inspectorを確認しました。
- 自動検証と実MO2 GUI確認は別の証拠として扱います。実MO2のabsolute pathは文書へ保存しません。
- GUI確認前後で、MO2の`modlist.txt`と対象MODの入力ファイルのSHA-256は一致しました。MO2 sourceは変更していません。
- Phase 3の対象外は、agent transport、Compare UI、Site Adapter、Runtime evidence、MO2 writeです。これらを追加しません。

### Phase 4：semantic conflict（完了）

2026-08-13のPhase 4では、active profileのenabled MODを対象に、target XMLとXPathごとのsemantic conflict解析を追加しました。

- `SevenDaysToDieBaseDataSource`が、明示された7DTD `Data/Config` directoryをread-onlyで読みます。
- `SevenDaysToDieConflictAnalyzer`が、profile priorityの`0→N`順でoperation sequenceを作ります。
- semantic conflict groupの`confidence`は、計算済みの`Compatible` / `Conflict`を`High`、`Possible`を`Medium`、`Unknown`または未評価を`Unknown`として返します。
- priorityの実ゲーム上の勝者方向は確定せず、`inference`とuncertaintyとして保持します。
- 基準XMLの結果は相対path、SHA-256、`GameDataFile` source referenceで保持します。absolute pathは結果へ出しません。
- `set`、`setattribute`、`remove`、`removeattribute`、属性値への単純な`append`をeffective subsetとして評価します。
- child fragment、`prepend`、`insertBefore`、`insertAfter`、`csv`、unknown operation、標準XPathで評価できない式は`unknown`またはdiagnosticとして保持します。
- 同じ値は`compatible`、異なる値は`conflict`、removeと変更は`conflict`、順序依存または未検証の組み合わせは`possible`とします。
- base XMLの欠落、parse失敗、XPath no matchは`unknown`とします。
- Query layerへ`AnalyzeConflicts`と`ConflictAnalysisReadModel`を追加しました。Desktop、Desktop Contracts、Web frontendは変更しません。
- Runtime evidence、完全なXML patch engine、MO2 write、UI表示はPhase 4の対象外です。

Phase 4の検証は、匿名synthetic fixtureで行います。solution test、build、`git diff --check`を受入証拠とします。

### Phase 5：runtime evidence

Phase 5-Aでは、中立runtime evidence契約を取り込み、Phase 4のstatic conflict groupとtarget XML + XPathで比較します。
raw result、source reference、diagnosticを保持します。
Phase 5-Bでは、Runtime comparisonだけを対象にしたLocal-only RuntimeOCD Adapterを追加します。正式schema互換性、DL、導入、UI、MO2 write、RuntimeOCD本体の再配布は含めません。

### Phase 6：Workspace UIの拡張

Browser page、Local context、Inspector、Compare、Diagnosisを段階的に拡張します。高密度Mod Manager UIは作りません。

Phase6のvertical sliceでは、Desktop bridgeへanalysis commandと`UiState.Analysis`を追加しました。
native folder pickerでbase Data/ConfigとRuntimeOCD logsを選択し、pathをDesktop sessionだけに保持します。
Context WebViewへstatic conflict、runtime comparison、evidence、provenance、uncertainty、diagnosticを段階表示します。
Compareは確認済みcandidate MODに絞り、Diagnosisはactive profile全体を表示します。
Phase4 synthetic fixtureとPhase6 RuntimeOCD logをDesktop outputへpackし、Developer toolsから再現できます。
MO2 write、AI chat、MCP、browser engine変更、browser syncは実装しません。

### Phase 7：controlled write（実装）

Phase 7は、Controlled profile edit、junction deploy、Steam起動のvertical sliceです。

- `src/ModScope.Deployment/`がread planeから独立したwrite planeです。
- `DeploymentDraft`はprofile識別子、MOD key、enabled状態、順序だけを持ちます。
- `DeploymentPlan`はplan ID、modlist差分、junction差分、source/profile/game fingerprint、blocking diagnosticを持ちます。
- `IModDeploymentService.Preview`は実ディスクを再読してplanを作ります。
- `IModDeploymentService.Apply`はplan IDと明示承認を要求します。
- `DeploymentResult`は`Applied`、`Blocked`、`RecoveryRequired`を返します。
- profileのcomment、空行、separator、未知行は`modlist.txt`のraw lineとして保持します。
- junctionの所有情報はModScope派生manifestに保存します。
- game rootはMO2 `ModOrganizer.ini`の`General.gamePath`から解決します。
- frontendへGamePath、MO2 path、LocalModSnapshotを渡しません。
- `game.launch`はpayloadを持たず、成功したApply後だけ有効です。
- Steam URIは`steam://rungameid/251570`に固定します。

Phase 7の実環境owner Playcheckは、匿名fixtureとMO2一時コピーの検証後に実施します。実行中MO2または7DTDを停止しません。

### Phase 8：Game Adapter拡張

第二gameへの需要と共通性が確認できた場合だけ、game adapterの範囲を広げます。

## 23. Acceptance criteria

### v0.1

- 解決した7DTD + MO2の1 instanceとactive profileをread-onlyで読み取れる
- Profiles directoryのread-only profile catalogを取得し、他profileをbackground preloadできる
- modlistからenabled状態とpriorityを取得できる
- MODのfile list、ModInfo metadata、軽量なXML referenceを取得できる
- raw情報、normalized value、source reference、diagnosticを保持できる
- snapshotとinput manifestを生成できる
- 同じ入力とparser versionから再現可能なnormalized resultを生成できる
- 既存Web engine上で任意URLを開ける設計境界を持つ
- URL、title、基本page contentからpage observationを作れる
- ユーザーがMOD identityを確認できる
- confirmed identityからinstalled / not installed / unresolvedを返せる
- active profile、enabled状態、priority、known versionを根拠付きで返せる
- 情報不足をunknownまたはnot assessedとして返せる
- Inspectorからlocal metadata、files、XML reference、diagnosticへ進める
- WPF + WebView2のDesktop appでBrowse、Observe、identity confirmation、Local context、Inspectorを実行できる
- GUIがQuery layerのprojectionだけを利用する
- MO2のMOD本体を変更しない
- controlled Applyで、選択profileの`modlist.txt`と管理junctionだけを明示承認付きで変更できる
- Apply失敗時にrollbackまたは`recovery-required`を返す
- Apply成功後だけ固定Steam URIを起動できる
- GamePathの絶対値をfrontendへ渡さない
- Codex、特定Site Adapter、特定agent backendへ依存しない

### v0.1以降

- Phase 3で達成済み：target XML、XPath、entity、property、attributeからMODへreverse queryできる
- semantic conflictとeffective resultをevidence付きで説明できる
- Runtime evidenceをstatic evidenceと区別して比較できる
- Site Adapterを追加してもgeneric page observationを壊さない
- GUIがquery layerの派生データだけを利用する
- write planeをread planeから分離できる（Phase 7で実装）
- 7DTD固有解析を壊さずにGame Adapterを追加できる

## 24. Web UI presentation layer

2026-08-11のWeb UI実装依頼により、既存WPF画面をWeb frontendへ移行する縦切りを追加します。

### 24.1 Responsibility boundary

- .NET / C#はsource of truthです。
- Local KnowledgeとQueryは、MO2、filesystem、profile、XML、diagnosticを担当します。
- Web frontendは、表示、navigation、identity confirmation、progressive disclosureを担当します。
- Web frontendは、MO2 parsing、Local Mod Knowledge、XML semantics、write operationを持ちません。
- ModScope.Desktop.ContractsはQuery modelとfrontendの間のUI専用DTOです。
- Contracts projectはQuery projectを参照しません。
- Desktop hostだけがQuery modelをUI contractへ変換します。

### 24.2 Three WebView2 surfaces

Desktopは4つのWebView2を、Global Browser chrome + MOD list + Content / Contextとして配置します。

- Toolbar WebView2：全幅のnavigation、Home、tabs、history、pane icon
- ModList WebView2：active profileのMOD一覧、profile selector、profile preload state
- Browser WebView2：ユーザーが閲覧する外部Web page
- Context WebView2：Local context、例外確認、Developer tools、Inspector

Toolbar、ModList、Contextは、同じfrontend bundleをsurface query付きで読み込みます。
Toolbarは`?surface=toolbar`を使用します。
ModListは`?surface=mod-list`を使用します。
Contextは`?surface=context`を使用します。

任意サイトをfrontendのiframeへ移しません。
Browser WebView2へWPF panelを重ねません。
WPFはwindow、WebView2 host、native bridgeに限定します。

下段は、左からModList `280px`、Browser `3*`、Context `2*`です。
Toolbarは全列にまたがります。通常は96pxの2段構成で表示します。History pageを開いてもhost rowは拡張しません。
ModListを閉じるとBrowser columnが広がります。
ModListの見出しにprofile load stateとscanning progressを表示します。
MOD一覧だけをスクロール可能にします。MOD rowはcompact表示にし、disabled rowは灰色系で表示します。
Context columnは、ToolbarのContext buttonまたはCtrl/Cmd+Iで非表示にできます。
非表示中もContext WebView2のstateとInspector stateを破棄しません。
ModList columnはToolbarのMOD list buttonまたは`layout.setModListVisible`で非表示にできます。
active profileを先に表示し、他profileはbackground preloadします。
profile selectorはpending、loading、ready、failedを表示します。
将来はContextをdrawerまたはoverlayへ折り畳める構造へ進めます。

### 24.3 Bridge contract

Web frontendとDesktop hostはWebView2 WebMessageを使います。

JSON contract versionは'2'です。
JSON propertyはcamelCaseです。
日時はUTC ISO-8601です。

frontendからhostへ送るcommandは次です。

- browser.navigate
- browser.newTab
- browser.selectTab
- browser.closeTab
- browser.home
- browser.history
- browser.selectHistory
- browser.back
- browser.forward
- browser.reload
- browser.observe
- knowledge.useFixture
- knowledge.loadSource
- knowledge.discoverSources
- knowledge.selectSource
- knowledge.selectRoot
- knowledge.switchProfile
- identity.confirm
- inspector.open
- deployment.preview
- deployment.apply
- game.launch
- analysis.selectBaseData
- analysis.selectRuntimeLogs
- analysis.analyzeConflicts
- analysis.compareRuntimeEvidence
- analysis.useFixture
- layout.setContextVisible
- layout.setModListVisible
- layout.setToolbarExpanded

hostからfrontendへ送るmessageは、state、error、readyです。
Toolbar、ModList、Contextの各App WebViewへ同じmessageをbroadcastします。
stateはUI stateの完全なsnapshotです。

Hostは次を検証します。

- message source origin
- contract version
- request id
- command name
- payload shape
- Browser URL scheme

Browser WebView2へlocal context、absolute MO2 path、LocalModSnapshotを送信しません。
Observeは固定scriptでbody textをbounded previewとして取得します。
Inspectorはinspector.open commandの後に取得します。

Profile catalogは、MO2設定から解決したProfiles directoryだけをread-onlyで読み取ります。
`modlist.txt`を持つ通常directoryだけを候補にします。
reparse pointと、MO2設定にない暗黙のglobal pathは読み取りません。
`profiles` directoryがない場合は、現在のexplicit profileだけを候補にします。
Profile switchは新しいsnapshotをread-onlyで生成します。
Profile pathの絶対値はfrontendへ送信しません。
`deployment.preview`はdraftを受け取り、hostが実ディスクを再読します。
`deployment.apply`はplan IDと明示承認を受け取ります。
`game.launch`はpayloadを持ちません。
Source candidateにはGamePathの絶対値を渡さず、ゲーム対象の準備状態だけを渡します。

### 24.3.2 Base Data inference and Local MOD recognition

Desktop hostは、内部MO2 sourceの`General.gamePath`から`GamePath\\Data\\Config`を推定します。
推定directoryが存在する場合は、base Dataを設定してstatic analysisを自動開始します。
存在しない場合は、`Data\\Configが見つかりません`を表示し、manual folder pickerをfallbackにします。
推定pathはDesktop sessionだけに保持します。

`browser.observe`後、source load後、profile switch後に、Queryのread-only match queryを実行します。
candidate recordにはMOD key、display name、directory name、match kind、strength、evidenceだけを投影します。
disabled、profile外のreadable MODを候補へ含めます。
unresolved recordは候補へ表示できますが、自動確認の対象にしません。
page本文、GamePath、MO2 path、LocalModSnapshotはWeb stateへ送信しません。
`autoInspectToken`は自動Inspector表示を一度だけ通知するopaque tokenです。

### 24.3.3 Phase6 analysis bridge and display rules

Phase6は既存Queryの`AnalyzeConflicts`と`CompareRuntimeOcdEvidence`をDesktop hostから呼び出します。
Desktop hostは、選択したbase Data/Config directoryとRuntimeOCD logs directoryをsession-onlyで保持します。
native folder pickerはabsolute pathを受け取ります。
absolute pathはWeb state、diagnostic、frontend messageへ返しません。
runtime log本文、raw RuntimeOCD result、raw result pathはWeb stateへ返しません。

`UiState.Analysis`は、入力ready state、static conflict result、runtime comparison result、analysis operation stateを持ちます。
analysis resultはprofileまたはsourceを変更した時に破棄します。
analysis failureでは既存resultを保持し、statusとdiagnosticを更新します。
解析中は重複実行、profile変更、source変更を無効にします。
runtime comparisonのcapture timeはDesktop hostがUTCで生成します。
tool versionとgame versionがない場合は`Unknown`として表示します。

Context WebViewはLocal contextを最上段に表示します。
Compareは確認済みcandidate MODに関係するgroupだけを表示します。
Diagnosisはactive profile全体のgroupを表示します。
static evidenceとruntime evidenceは別カードに表示します。
target XML、XPath、MOD、priority、operation sequence、confidence、uncertainty、diagnostic、source referenceを段階的に表示します。
assessmentは`Match`、`Different`、`Possible`、`Unknown`、`Not assessed`、`Inferred`を文字で表示します。
解析前は`未確認`と表示します。
resultが空でも`競合なし`とは表示しません。
Inspectorは確認済みcandidate MOD以外のreadable MODも開けます。
raw XML、XPath、attribute、patch detailは折りたたんで表示します。
MO2はread-onlyのまま維持します。

### 25.1 Phase6.5 Browse-first UI情報設計

Phase6.5は、既存のBrowse、Recognize、Inspect、Compare、Diagnosisを壊さずに表示責務を整理します。
routerは追加しません。
Context WebView内のmode切替を使用します。

Toolbarは、左ペインと右ペインをアイコンで切り替えます。
アイコンにはtooltipとaria-labelを付けます。
Ctrl/Cmd+IのContext shortcutは維持します。

ModListの標準表示は`ModScope view`です。
`Foundation`、`Compatibility`、`Content`、`Unknown`の順に分類します。
分類はQuery layerのstatic evidenceだけを使います。
`Foundation`は依存関係を意味しません。
role assessmentは`Verified`、`Inferred`、`Unknown`で表示します。
分類内の順序はMO2 priorityを維持し、同順位はMOD keyで決定します。
根拠がないMODは`Unknown`へ置きます。
MO2のpriorityはprofileの上から下へ`0→N`で保持します。`modlist.txt`の保存順はこの画面順と逆です。
ModScope viewは`Foundation`、`Compatibility`、`Content`、`Unknown`の順で表示し、分類内だけpriority順を使います。
他のreadable MODからtarget XMLとして参照されるMODは、base roleの静的証拠として`Foundation / Inferred`へ投影します。
これは依存関係やゲーム内のwinner方向を断定しません。

Contextの表示順は`RECOGNIZE`、Local awareness、Analysis summary、Inspect導線です。
通常Contextは対応可能なdiagnostic要約と件数だけを表示します。
raw code、raw value、source detail、Page details、Developer toolsはDebugへ移します。
SettingsはMO2 sourceの状態、変更、再読込、復旧操作だけを表示します。
MO2 source未選択時はOnboardingを表示します。
source選択後は通常Contextからsource cardを隠します。

Browserは既存WebView2を使用します。
各tabは独立したWebView2を持ちます。
tabのURL、title、navigation、scroll、form stateは実行中のWebView2が保持します。
起動時は保存済みtab URLを再読込し、保存済みlast pageをactive tabへ復元します。
保存がない場合はローカルBrowse Homeを開きます。
Historyはbounded metadataとしてURL、title、訪問時刻だけを保存します。
page本文、raw observation、absolute path、cookie、認証情報は保存しません。
URL直接入力は通常Toolbarに置きます。http、https、file、aboutのabsolute URLだけを受け付けます。

Bridge contract versionは`1`を維持します。
Browser tab、History、active tab、MOD roleのstateは既存stateへ追加します。
`browser.newTab`、`browser.selectTab`、`browser.closeTab`、`browser.home`、`browser.history`、`browser.selectHistory`はactive tabへ適用します。
Desktop hostはbrowser persistenceをlocal metadataへ限定します。
Historyは新しいBrowser tabの`about:history`ページとして生成します。
History pageはURL、title、訪問時刻だけをHTML encodeして表示します。
History pageのentry linkは通常の`http` / `https` navigationとしてactive tabへ適用します。
`about:history`はlast pageとして復元できます。
page本文、raw observation、absolute path、cookie、認証情報はHistoryへ保存しません。
`layout.setToolbarExpanded`は互換性のため残しますが、History操作は使用しません。
MO2 write、AI、MCP、独自Browser engine、browser syncは追加しません。

### 25.2 Phase6.6 情報減算UI

Phase6.6は、既存のBrowse、Recognize、Analysis、Inspectorの結果を維持し、通常画面の情報量だけを減らします。
Browser chrome、MO2 read-only境界、Query projectionは変更しません。

通常Contextは`RECOGNIZE`と最小Local summaryだけを表示します。
Local summaryは、installed / not installedとenabled / disabledだけを表示します。
profile名、version、priority、evidence、uncertainty、raw diagnostic、XML、provenanceは通常Contextへ表示しません。
Analysisは小さなstatus lampで`Running`、`Not assessed`、`Assessed`、`Issue`を文字とtooltipで示します。
lampはInspector modeを開きます。
確認済みMODがある場合はMOD Inspectorを開き、確認済みMODがない場合はactive profileのDiagnosisを開きます。

Inspectorは右ペイン内の置換modeです。
固定overlayと背景backdropは使用しません。
Inspectorの上部に`Back to Context`を表示します。
結論は最初から表示し、static evidence、runtime evidence、raw XML、patch operation、raw diagnosticは閉じた状態にします。
FilesはInspectorの初期状態で閉じ、展開時だけ全ファイルを表示します。
Mod Roleはrole chip、assessment chip、短い`Reason:`要約だけを初期表示し、詳細reasonとrole evidenceはdisclosureへ移します。
profile、MO2 source、analysis inputの変更時は古いInspector表示を閉じます。
Runtime comparisonの実行導線はDebugへ残します。

通常MOD rowはMOD名、version、enable状態のlampだけを表示します。
role、assessment、profile state、priority、verified Website状態はhover、keyboard focus、Inspectorで表示します。
disabled MODは名前、背景、lampを灰色系で表示します。
MOD名はellipsisで省略します。
Website導線、Inspector導線、ModScopeの固定順序、priority順は維持します。

### 25.3 Phase6.6 表示状態と境界

Context、Settings、Debugのmode切替は維持します。
通常Contextから独立した`ANALYSIS`、`DIAGNOSTICS`、`STATIC EVIDENCE`の展開カードを削除します。
raw diagnosticはDebugだけに表示します。
InspectorはContext WebView内で完結し、中央BrowserとBrowser chromeを覆いません。
通常Contextの背景と境界は低コントラストにし、中央Web pageを主役として維持します。
MO2 write、AI、MCP、独自Browser engineは追加しません。

### 25.4 Phase6.7 起動表示、Chrome型Toolbar、MOD URL導線

Desktop hostはWPF client area全体へ`LOADING PROFILE` overlayを表示します。
WebView2初期化、source discovery、source load、foregroundのprofile switchをoverlay対象にします。
background profile preloadはoverlay対象にしません。
overlayは対象profile、operation phase、取得できるcompleted / totalを表示し、値がないphaseはindeterminate progressを表示します。
loading中はclient areaの操作を無効にし、成功後は閉じます。失敗時は閉じて既存diagnosticを表示します。
operation stateは既存`OperationStateChanged`を使い、UiStateとbridge contractへ項目を追加しません。

通常Toolbarは96pxのChrome型2段構成です。
上段へtab strip、active tab、tab close、new tabを置きます。
下段へback、forward、reload、home、URL入力、Go、History、pane icon、shortcut hintを置きます。
Toolbarは通常96pxで固定します。
History pageを開いてもToolbar高さは変更しません。
`layout.setToolbarExpanded`は互換性のため残しますが、通常History操作からは呼び出しません。
Browser engineとWebView2構成は変更しません。

ToolbarはChrome darkのtabstripとnavigation rowへ分離します。
active tabは明るいsurfaceと丸い上端で表示し、inactive tabは透明背景と控えめなhoverで表示します。
new tab buttonはtab listの末尾へ置き、tabと一緒に横スクロールします。
navigation rowのURL入力はomniboxとして表示し、Goはcompactな`↵` iconで表示します。
History、MOD list、Context、shortcut hintは右側のaction groupへまとめます。

ModListはModScope viewの固定順序だけを表示します。
順序は`Foundation`、`Compatibility`、`Content`、`Unknown`です。
分類内はMO2 priority、同順位はMOD keyで決定します。
`MO2 order`切替と設定項目は持ちません。

MOD Websiteは`Verified`、`Inferred`、`No usable URL`へ分類します。
有効な既存Websiteはそのまま開きます。
無効または欠落する場合はdisplay name、directory name、MOD keyから7DTD Nexus検索URLを作ります。
検索結果から`/7daystodie/mods/{numericId}`形式のリンクを抽出し、検索名と正規化後に完全一致する候補が1件だけの場合に正規ページへ遷移します。
一致しない場合、複数候補の場合、検索結果を解析できない場合は検索ページを表示したままにします。
`Inferred`はクリック可能ですが、ページの存在確認ではありません。
`No usable URL`はbuttonにせず、既存のBrowser scheme検証を維持します。

`browser.navigate`のoptionalな`nexusSearchName`は、推定MOD検索の一時的なnavigation intentです。
手入力の検索URL、通常のURL移動、Verified Websiteには付けません。
`nexusSearchName`はUiStateや永続Local Knowledgeへ保存しません。
Verified Websiteの404はBrowser diagnosticとして扱い、自動検索へ変更しません。

### 25.5 Phase6.8 ロード遮断、Chrome palette、History page

Desktop hostはWPF client areaのWebView2子windowを含めて入力を遮断します。
foregroundのloading中はToolbar、ModList、Context、Browser tabの`IsEnabled`と`IsHitTestVisible`を無効にします。
薄いグレーのloading panelはclient areaだけを覆います。
OSタイトルバーはloading中も操作できます。
background profile preloadではloading panelを表示しません。
loading完了または失敗後はclient UIを再有効化します。

ModScopeが所有するsurfaceはChrome dark paletteを使います。
baseは`#202124`、navigationは`#292a2d`、panelは`#303134`、borderは`#3c4043`です。
primary textは`#e8eaed`、muted textは`#9aa0a6`です。
外部Web pageの色は変更しません。

History buttonはpopupを開かず、新しいBrowser tabを開きます。
新しいtabのtitleは`History`で、内部URLは`about:history`です。
History pageはDesktop hostが生成し、URL、title、訪問時刻だけを表示します。
History metadataはbounded local dataとして保存し、page本文、raw observation、absolute path、cookie、認証情報は保存しません。

### 25.6 Nexus検索によるMODページ解決

推定MODページは、slug URLを直接開かず、7DTD Nexus検索を起点にします。
Desktop hostは検索結果のDOMから同一Nexus hostの数値ID MODリンクだけを読み取ります。
表示名は大文字・小文字、アクセント、句読点、区切り文字を正規化して比較します。
完全一致が1件だけの場合だけ、数値IDの正規URLへ遷移します。
曖昧な結果、空の結果、ログイン要求、DOM変更、解析失敗では検索ページを保持します。
検索結果の最上位候補を根拠なく採用しません。
Verified Websiteはsource referenceを優先し、404時も検索fallbackを行いません。

### 24.3 読み込み性能とProfile投影

静的MOD knowledgeとProfile projectionを分離します。
静的MOD knowledgeは、MOD record、file inventory、XML observation、diagnostic、LocalKnowledgeIndexを含みます。
Profile projectionは、`modlist.txt`のraw line、enabled state、priority、Profile state、profile hash、snapshot IDを含みます。

`Mo2SnapshotReader`は、正規化した`ModsPath`、parser version、schema versionをkeyにprocess-scoped static catalogを保持します。
cacheはMO2のsource of truthを置き換えません。
cacheはメモリ上の再生成可能な派生データです。

cache hitの判定では、MOD treeをcontent readせずにrelative path、file size、最終更新時刻、reparse stateを比較します。
metadataが変わった場合はstatic catalogを破棄し、静的MOD knowledgeを再生成します。
cache missではouter MOD単位のscanを最大2並列で実行します。
並列結果はouter path、inner path、file path、diagnosticの決定的な順序でmergeします。
`ModInfo.xml`と`Config/**/*.xml`は、scan時に取得したbyte bufferからhashとXML observationを生成します。

初回読み込みとProfile switchはDesktop UI threadの外で実行します。
active profileのsnapshotを先にactive sessionへ適用します。
他profileのsnapshotはstatic catalogを再利用して1件ずつbackground preloadします。
background preloadはactive sessionとMOD一覧を置き換えません。
profile switchはbackground preloadをキャンセルし、選択profileの読み込みを優先します。
bridge stateはoperation kind、busy state、background flag、target profile nameを保持します。
読み込み中は現在のsession、candidate、page observationを保持します。
成功後だけProfile stateとlocal contextを更新します。
失敗時は既存stateを保持し、statusまたはsource cardへ要約を表示します。
foreground operation中だけsource操作を無効にします。
background preload中もprofile switchを許可します。

### 24.3.1 Operation progress rail

初回ロード、source load、Profile switchは、派生UI stateとしてoperation progressを公開します。
Progress stateはsnapshot ID、manifest、normalized data、LocalKnowledgeIndexへ含めません。

`KnowledgeOperationUiState`はoperation kind、busy state、background flag、対象Profile、phase、completed、totalを持ちます。
実数を安全に取得できないphaseではcompletedとtotalをnullにします。

outer MOD folderの並列scanだけは、folder数をtotalとして決定的な進捗を報告します。
inner MOD recordの数とは区別します。
cache hitではstatic knowledgeを再利用するphaseを報告します。
index構築とProfile projectionは不定形phaseとして表示します。

Desktop hostはoperation tokenでstale callbackを破棄します。
progress通知は最大20回/秒へ間引きます。
Web UIは150msを超えたoperationだけ、ModListのprofile見出しへprogress railを表示します。
短時間のcache hitでは、progress railを表示しません。

progress railは現在のBrowser pageとLocal contextを隠しません。
operation失敗時は既存stateを保持し、既存のstatus summaryを表示します。
MO2のsource of truthとread-only境界は変更しません。

### 24.4 Frontend build

Web frontendはSvelte、TypeScript、Viteを使用します。
router、Redux、UI component library、theme systemはv0.1に追加しません。

src/ModScope.Web/distをDesktop outputのWebAssetsへコピーします。
App WebView2はvirtual host https://appassets.modscopeから静的frontendを読み込みます。
App host以外のnavigationは拒否します。

次のコマンドをcanonical buildとします。

~~~powershell
pwsh -NoProfile -File scripts/build.ps1
~~~

scriptはnpm ci、frontend check、frontend build、dotnet buildを順に実行します。
node_modules、frontend dist、生成済みDesktop assetsはGit管理対象外です。

## 25. Conclusion-first Web UI

2026-08-12のUI整理では、通常画面から開発者向けの配線を隠します。

- Browser chromeはURL、戻る、進む、reloadを表示します。
- NavigateとObserveの明示ボタンは通常画面から削除します。
- Browser navigation完了後にDesktop hostがObserveを自動実行します。
- Local contextはidentity確認手順ではなく、結論カードとして先に表示します。
- installedとnot installedは結論カードで表示します。
- unresolvedとunknownは認識失敗カードとして表示します。
- 認識失敗時だけlocal MOD選択とnot installed確認を表示します。
- raw observationはPage detailsへ折りたたみます。
- fixture、explicit MO2 source path、手動ObserveはDeveloper toolsへ移します。

起動時にMO2 source discoveryを実行します。
ready候補が1件なら自動読込します。
ready候補が複数件ならsource cardで選択します。
candidateがない場合は再探索とnative folder pickerを表示します。
unsupported candidateとload failureはsource cardへ要約表示します。
ModListのProfile selectorは、解決済みsource内のread-only profile switchだけを実行します。
高信頼page identity自動認識を追加しました。overlap判定は追加しません。
既存のQuery modelとread-only境界を維持し、profile catalogとlayout stateを明示的なread modelへ追加します。

Profile-first Browser UI整理では、左のModListにactive profileの全MODをpriority順で表示します。
profileに存在するがMOD directoryがないMODはunresolvedとして表示します。
MOD directoryに存在するがprofileに存在しないMODは、折りたたみ式の`Profile外`欄へ分離します。
enabled、disabled、unresolved、priority不明の状態を同じ一覧で表示します。
profile selectorはModListへ移し、profile名とPending、Loading、Ready、Failedを表示します。
active profileを先に表示し、他profileはactive表示後にbackground preloadします。
全候補を常設一覧から削減するため、認識失敗時の検索drawerは補助導線として維持します。
検索対象はdisplay name、directory name、MOD keyです。
`ModInfo.xml`から得た有効なabsolute http / https Websiteを最優先します。
Websiteが無効または欠落する場合は、MOD名から7DTD Nexus検索URLを作ります。
検索結果から数値IDのMODリンクを抽出し、検索名と正規化後に完全一致する候補が1件だけの場合に正規ページへ遷移します。
曖昧な結果、検索結果の解析失敗、remote 404やnavigation failureはBrowser diagnosticまたは検索ページとして扱います。
認識失敗時のlocal MOD選択も、同じ検索結果から行います。
