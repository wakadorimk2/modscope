# ModScope 設計

## 1. 文書の役割

この文書は、ModScopeの製品定義、現在のアーキテクチャ、責務境界、v0.1の範囲を定義します。

現在のリポジトリはLocal Knowledge基盤とGUI縦切りの実装フェーズです。
Local KnowledgeとQueryはC# / .NET 8で実装します。
DesktopはWPF / .NET 10で実装します。
独自Browser engine、CLI、MO2 writeは実装しません。

v0.1は、AI用index単体ではありません。7DTD + MO2のLocal Mod Knowledgeと、最小のBrowse → Recognize → Local awareness → Inspectを検証するvertical sliceです。

## 2. Product definition

ModScopeは、MODをWeb上で探し、吟味し、比較する作業を、ユーザーのローカルMOD環境の文脈付きで行うMOD Workspace / Browserです。

画面の主役は、ユーザーが現在見ているWeb pageです。Local Mod Knowledgeは、必要なときにpageの横または下へ表示するcontextです。

この文書では、次の用語を使います。

- page observation：Human browserまたはagent browserから取得したURL、title、基本page contentなどの事実
- MOD identity confirmation：pageが示す候補MODを、ユーザーが確認または選択した状態
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

ModScopeは、page observationから候補MODを示します。v0.1では、MOD identityをユーザーが確認します。

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

将来のMO2操作は、read layerから独立したwrite layerに置きます。write layerを追加する場合も、Mod Manager全体を作りません。

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
- v0.1でのMO2 write
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

Optional future write plane
  -> explicit approval
  -> MO2 operation
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

解決した、7DTD + MO2の1 instanceと1 profileを読み取ります。

- profileの`modlist.txt`
- `mods/`内のMOD directory
- MOD内のファイル一覧
- `ModInfo.xml`が存在する場合のmetadata
- `Config/**/*.xml`
- XML patch operationのraw情報と、確認できるnormalized情報

MO2のdownloads、virtual filesystem、他profileは、v0.1の必須入力にしません。

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

ページ情報からMOD identityが確定しない場合があります。確定しない状態を自動推測で閉じません。

### 9.3 MOD identity confirmation

v0.1では、ユーザーが候補MODを確認または選択します。

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

Write planeは、将来必要性が確認できた場合だけ追加します。

候補はenable / disable、reorder、profile変更です。

実装する場合は、次を必須にします。

- read planeからの独立
- dry-run
- 変更前後の差分
- 対象profileとMODの明示
- ユーザーの明示承認
- 失敗時の復旧方針
- MO2の実仕様に基づく検証

## 15. v0.1 scope

### 15.1 含めるもの

- 7DTD + MO2の1 instanceと1 profile
- read-onlyのMO2 snapshot
- modlist、enabled状態、priority、MOD directory、ファイル一覧
- ModInfo.xmlのmetadata
- Config XMLの軽量な構造化
- XML patch operation、target XML、XPath、attributeのraw保持と候補化
- forward indexとreverse indexの最小形
- WPF + WebView2上の最小Browse surface
- URL、title、基本page contentのpage observation
- ユーザーによるMOD identity confirmation
- installed、not installed、active profile、known versionなどのLocal context
- Inspectorによるlocal metadata、files、XML reference、diagnosticの確認
- unknown、unresolved、not assessedの明示
- Query layerが提供するneutral read model
- page observation、手動MOD identity confirmation、Local context、Inspectorの縦切り

### 15.2 含めないもの

- site固有Adapter
- 複数game対応
- 完全なsemantic conflict判定
- RuntimeOCD連携
- MO2へのwrite
- 常時表示の高密度Mod一覧
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
- `modlist.txt`のprofile orderを初回のpriority根拠として保持します。
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
- MOD identityはユーザーが確認します。
- page observationはv0.1ではメモリ上のbounded previewだけを保持します。
- MO2 sourceはread-onlyです。

GUIは`LocalModSnapshot`を直接読みません。
GUIはQuery layerのprojectionだけを読みます。

## 19. Risksとunknowns

- URL、title、基本page contentだけでMOD identityを有用に候補化できるか
- ユーザー確認を含むpage recognitionが、実際の探索負荷を下げるか
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

### Phase 3：QueryとInspector core

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

### Phase 4：semantic conflict

patch semantics、priority、operation sequence、effective resultを解析します。

### Phase 5：runtime evidence

Runtime Adapterからruntime evidenceを取り込み、static evidenceと比較します。

### Phase 6：Workspace UIの拡張

Browser page、Local context、Inspector、Compare、Diagnosisを段階的に拡張します。高密度Mod Manager UIは作りません。

### Phase 7：controlled write

必要性が確認できた場合に、dry-runと明示承認付きのMO2操作を追加します。

### Phase 8：Game Adapter拡張

第二gameへの需要と共通性が確認できた場合だけ、game adapterの範囲を広げます。

## 23. Acceptance criteria

### v0.1

- 解決した7DTD + MO2の1 instanceと1 profileをread-onlyで読み取れる
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
- MO2 sourceを変更しない
- Codex、特定Site Adapter、特定agent backendへ依存しない

### v0.1以降

- target XML、XPath、entity、property、attributeからMODへreverse queryできる
- semantic conflictとeffective resultをevidence付きで説明できる
- Runtime evidenceをstatic evidenceと区別して比較できる
- Site Adapterを追加してもgeneric page observationを壊さない
- GUIがquery layerの派生データだけを利用する
- write planeをread planeから分離できる
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

Desktopは3つのWebView2を、Global Browser chrome + Content / Contextとして配置します。

- Toolbar WebView2：全幅のURL、navigation、profile、Context toggle
- Browser WebView2：ユーザーが閲覧する外部Web page
- Context WebView2：Local context、例外確認、Developer tools、Inspector

ToolbarとContextは、同じfrontend bundleをsurface query付きで読み込みます。
Toolbarは`?surface=toolbar`を使用します。
Contextは`?surface=context`を使用します。

任意サイトをfrontendのiframeへ移しません。
Browser WebView2へWPF panelを重ねません。
WPFはwindow、WebView2 host、native bridgeに限定します。

BrowserとContextの初期比率は`3*:2*`です。
Context columnは、ToolbarのContext buttonまたはCtrl/Cmd+Iで非表示にできます。
非表示中もContext WebView2のstateとInspector stateを破棄しません。
2面構成から3面構成への変更は、Global Browser chromeの視線移動を検証する暫定surfaceです。
将来はContextをdrawerまたはoverlayへ折り畳める構造へ進めます。

### 24.3 Bridge contract

Web frontendとDesktop hostはWebView2 WebMessageを使います。

JSON contract versionは'1'です。
JSON propertyはcamelCaseです。
日時はUTC ISO-8601です。

frontendからhostへ送るcommandは次です。

- browser.navigate
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
- layout.setContextVisible

hostからfrontendへ送るmessageは、state、error、readyです。
ToolbarとContextの両方へ同じmessageをbroadcastします。
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
bridge stateはoperation kind、busy state、target profile nameを保持します。
読み込み中は現在のsession、candidate、page observationを保持します。
成功後だけProfile stateとlocal contextを更新します。
失敗時は既存stateを保持し、statusまたはsource cardへ要約を表示します。
Profile dropdownとsource操作はbusy中だけ無効にします。

### 24.3.1 Operation progress rail

初回ロード、source load、Profile switchは、派生UI stateとしてoperation progressを公開します。
Progress stateはsnapshot ID、manifest、normalized data、LocalKnowledgeIndexへ含めません。

`KnowledgeOperationUiState`はoperation kind、busy state、対象Profile、phase、completed、totalを持ちます。
実数を安全に取得できないphaseではcompletedとtotalをnullにします。

outer MOD folderの並列scanだけは、folder数をtotalとして決定的な進捗を報告します。
inner MOD recordの数とは区別します。
cache hitではstatic knowledgeを再利用するphaseを報告します。
index構築とProfile projectionは不定形phaseとして表示します。

Desktop hostはoperation tokenでstale callbackを破棄します。
progress通知は最大20回/秒へ間引きます。
Web UIは150msを超えたoperationだけ、画面上端のprogress railを表示します。
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
通常画面のProfile dropdownは、解決済みsource内のread-only profile switchだけを実行します。
page identity自動認識とoverlap判定は追加しません。
既存のQuery modelとread-only境界を維持し、profile catalogとlayout stateを明示的なread modelへ追加します。
