# ModScope スコープ監査 — 2026-08-15

## 監査の前提

本監査は、既存の構想を正当化するためのレビューではありませんわ。  
ModScopeを一度ゼロベースで再定義するための監査ですわ。

今回の対象は、次の資料、コード、テストです。

- [AGENTS.md](../../AGENTS.md)
- [docs/README.md](../README.md)
- [docs/design.md](../design.md)
- [docs/future-vision.md](../future-vision.md)
- [docs/adr/](../adr/)
- [docs/research/](./)
- [version snapshot 2026-08-14](version/snapshot-2026-08-14/README.md)
- [requirements research](requirements/)
- [compatibility research](compatibility/)
- [prior-art research](prior-art/)
- [src/](../../src/)
- [tests/](../../tests/)

主なresearch anchorは、[identity and version provenance ADR](../adr/identity-and-version-provenance.md)、[requirements local resolution findings](requirements/local-resolution/findings.md)、[compatibility findings](compatibility/findings.md)、[prior-art evidence model](prior-art/evidence-model.md)、[version snapshot findings](version/snapshot-2026-08-14/findings.md)です。

この文書では、次の3種類を分けます。

- **Verified**: 現在のコード、テスト、実データ、公式資料で確認できた事実。
- **Inferred**: Verifiedな事実から導いた製品判断。
- **Uncertain**: 現在の証拠では確定できない事項。

今回の監査では、コードの削除、UI変更、refactor、実装を行いません。

## 1. Executive conclusion

ModScopeの現在のスコープは広すぎますわ。

Wabbajackは、modlistの作成、配布、installer、archive取得、source metadata、content hash、manifest、検証、修復、reportを既に扱います。  
MO2は、profile、enabled状態、priority、download metadata、virtual filesystem、manager操作を扱います。  
Vortexも、profile、install、deployment、file conflict、dependency、recommendation、version checkを扱います。

したがって、ModScopeがこれらの機能を再実装しても、v0.1の固有価値にはなりません。

ModScopeに残る有力な価値は、次の横断説明ですわ。

> 現在の7DTD + MO2環境が、どのpackage、Modlet、archive、Nexus file、Wabbajack record、game version、ModInfo versionから構成されているかを、sourceとobserved time付きで説明する。

この説明には、次を含めます。

- 同じ意味に見える名前を同一identityへ自動統合しない。
- package、Modlet、archive、Nexus MOD、Nexus file、Wabbajack record、game version、author versionを分ける。
- versionを単一の数字へ圧縮しない。
- co-presenceをdependency、compatibility、runtime correctnessへ変換しない。
- Unknown、Unresolved、Not comparable、Diagnosticを正常な結果として表示する。

これは、現在のModScopeの「Browser primary」「controlled write」「Steam起動」「RuntimeOCD」「完全なsemantic conflict」「multi-game abstraction」を同時に持つ必要がないことを意味します。

### コードとテストの実体

Verifiedな実装範囲は、文書より大きい部分と小さい部分があります。

- C#には、MO2 source discovery、profileとmodlistのread、ModInfo.xmlとConfig XMLのparse、file index、static XML operation simulation、runtime evidence、RuntimeOCD adapter、controlled write、junction deploy、Steam起動があります。
- Desktopには、WebView2 browser host、browser tab、Browser page observation、Inspector、analysis、Deployment preview/applyのbridgeがあります。
- Webには、browser、Mod Library、Inspector、static conflict、runtime evidence、Deployment、Steam launchのUIとmock fixture経路があります。
- 一方で、srcにはWabbajack manifestを本製品のread modelへ取り込む専用実装は確認できません。Wabbajack関係の中心はresearch、snapshot、fixturesです。
- C# testは4 test projectで合計101件が成功しました。
- Webのsvelte-checkは0 errors、0 warningsでした。

テスト成功は、現行機能の実装品質を示します。  
テスト成功は、現行機能がv0.1に必要であることを示しません。

## 2. One-sentence product definition

**ModScopeは、Wabbajackが構成を再現しMO2が構成を管理する世界で、現在の7DTD + MO2環境を「何が、どのartifactとModletで、どのversion観測に基づいて存在し、何が未確定か」というevidence付き説明へ変換するread-onlyローカル環境インスペクターですわ。**

この一文で説明できない機能は、v0.1のスコープ外候補です。

## 3. Primary user problem

### Verifiedな既存ツールの役割

既存ツールは、主に次を解決します。

| 問題 | 既存ツールが担う役割 |
|---|---|
| modを導入、無効化、priority変更する | MO2、Vortex |
| virtual filesystemまたはdeploymentを作る | MO2、Vortex |
| modlistを作り、別環境へ再現する | Wabbajack |
| archiveを取得し、hashで検証する | Wabbajack、MO2、Vortex |
| listのREADME、manual step、external fileを配布する | Wabbajack |
| manager-side file conflictを表示し、load ruleを設定する | MO2、Vortex |
| gameを起動する | MO2、Vortex、Steam |

### ModScopeでしかまだ説明しにくい問題

ユーザーが実際に困る問いは、次の形です。

1. このMO2 profileで、実際に有効なものは何か。
2. MO2のこのpackage directoryは、何個のModletを含むか。
3. このModletのModInfo.xmlは、どのlocal metadataと対応するか。
4. このarchiveは、どのNexus fileまたはWabbajack recordに対応するか。
5. local ModInfo version、MO2 metadata、Nexus file version、Wabbajack record version、game versionは一致するか。
6. 一致しない場合、どのsourceが何を主張しているか。
7. どの結論がverifiedで、どの結論がUnknownまたはNot comparableか。
8. 現在の環境が、どのlistやarchive recordに観測上含まれていたか。

これは、installの問題ではありません。  
これは、current environment explanationの問題ですわ。

### 価値仮説

**Inferred**: 「何が入っているか」だけでなく、「なぜこのidentityとversionとして扱うのか」を手作業で複数のsourceから説明する負担に、独立した価値があります。

**Uncertain**: この説明機能が、ユーザーに継続利用される製品価値になるかは、まだowner playcheckまたは利用者検証で確認できていません。

したがって、v0.1は大きなworkflow製品にせず、1つのprofileと少数のModletについて説明が成立するかを検証する製品実験にします。

## 4. Prior-art boundary

### Wabbajack

[Wabbajack公式repository](https://github.com/wabbajack-tools/wabbajack)は、Wabbajackをautomated Modlist Installerとして説明しています。  
公式sourceには、Installer、Compiler、Downloader、VerificationCache、Hashing、Reporting、VFSなどの実装領域があります。

[Pre-Compilation](https://wiki.wabbajack.org/modlist_author_documentation/Pre-Compilation.html)は、MO2 download archiveと同名のmeta fileをsource追跡に使い、meta fileのgameName、modID、fileIDを使うと説明しています。  
[Wabbajack公式Wiki](https://wiki.wabbajack.org/)は、manifestにmod、author、version、size、linkなどが含まれると説明しています。

ModScopeは、次をWabbajackから奪いません。

- modlist compilation
- modlist distribution
- installer
- archive download
- archive hash verification
- archive healing
- list report
- list maintenance
- list author workflow

### Mod Organizer 2

[MO2公式repository](https://github.com/ModOrganizer2/modorganizer)は、MO2を任意の規模のmod collectionを管理し、installとuninstallを行うmanagerとして説明しています。  
[MO2 release information](https://github.com/ModOrganizer2/modorganizer/releases)は、USVFS、archive preview、Nexus category、game pluginなどのmanager ecosystemを示します。

ModScopeは、次をMO2から奪いません。

- profile management
- enabled状態の編集
- priority、load orderの編集
- download management
- virtual filesystem
- manager diagnostics
- launch workflow

ModScopeはMO2をsource of truthとして読みます。

### Vortex

[Vortexのfile conflict documentation](https://github.com/Nexus-Mods/Vortex/wiki/MODDINGWIKI-Users-General-Managing-File-Conflicts)は、同じfileを変更するmodを検出し、load ruleと個別file ruleを設定するmanager-side workflowを説明します。  
[Vortex APIの公式events](https://github.com/Nexus-Mods/vortex-api/blob/master/docs/EVENTS.md)には、install、deploy、purge、dependency、recommendation、download、version check、collection installのeventがあります。

ModScopeは、次をVortexから奪いません。

- mod install
- dependency install
- recommendation install
- deployment
- purge
- file conflict resolution
- version check
- collection install

### 7 Days to Die

[The Fun Pimps forumのXPath Modding Explanation Thread](https://community.thefunpimps.com/threads/xpath-modding-explanation-thread.7653/)は、ModletをMods配下に置き、game initialization時にXML変更をin-memoryで適用する歴史的なmodding modelを説明します。  
このsourceは、7DTDのModletとXML patchがModScopeのGame Adapter対象になり得ることを示します。

ただし、threadは古いcommunity documentationです。  
現在の全versionに対する完全なruntime contractとして扱いません。

7DTDには、Mod Launcher、Modlet Organizer、Nexus、community wikiなどの周辺ツールもあります。  
ModScopeは、7DTDのinstall、launcher、distributionを再実装しません。

### 重要な境界

既存ツールは、主に「導入する」「管理する」「再現する」を解決します。  
ModScopeが担当する候補は、既存のsourceを横断して「現在の環境を説明する」ですわ。

## 5. Current scope audit

次の表は、文書上の構想だけではなく、現在のコード、test、fixture、researchの実体を基準に評価します。

| 対象 | 現在確認できる実体 | 監査結果 |
|---|---|---|
| embedded/browser workspace | DesktopのWebView2 host、browser tab、history、Nexus search、Webのbrowser UIがあります。 | 製品の中心ではありません。後段のevidence sourceまたはexternal browserへ縮めます。 |
| Web page recognition | URL、title、mod name、Nexus URLを使うlocal match queryがあります。Nexus search resultのexact name確認もあります。 | page observationの補助です。ModScope固有価値ではありません。 |
| Mod Library / Views | Web UIにMod Library、search、role、profile view、saved view相当があります。 | manager UIと競合します。v0.1から外します。 |
| MO2 local inventory | MO2 source discovery、profile、modlist、enabled、priority、mod directory、file hash、ModInfo、Config XMLを読みます。 | CORE-1です。ただしread-onlyに限定します。 |
| package / Modlet / archive identity resolution | ADR、synthetic identity fixture、local packageとModletのone-to-many検証があります。現在のコードはlocal identityとmetadataを扱います。 | CORE-2です。identityを統合するのではなく、identity間のrelationを説明します。 |
| version provenance | ModInfo、MO2 meta、download meta、Nexus file、Wabbajack record、game versionを別roleで扱うresearchがあります。 | CORE-2です。version resolverではなくversion evidenceです。 |
| Wabbajack evidence | snapshotにWabbajack archive recordと比較結果があります。専用のsrc importerは確認できません。 | SUPPORTINGです。manifestはobservational evidenceとして読むだけにします。 |
| requirements / dependency extraction | Web-only researchでは43 dependency candidates、67 classified edgesがあります。local resolutionは多数がnot observableまたはunresolvedです。 | DEFERです。observationをdependencyへ昇格しません。 |
| compatibility evidence | ADR、research、compatibility fixtureがあります。56 assertionsのresearchはruntime未検証です。 | DEFERです。v0.1はcompatibility verdictを出しません。 |
| file overlap | file indexとforward/reverse referenceがあります。 | DEFERです。MO2、Vortexのmanager-side overlapと競合します。 |
| XML semantic conflict analysis | 7DTD XML operationのstatic simulationとdiagnosticがあります。testもあります。 | DEFERです。実装は残してfreezeします。完全なruntime semanticsではありません。 |
| runtime evidence | runtime log model、comparison query、diagnosticがあります。 | DEFERです。runtime inputとgame versionの証明範囲が不足します。 |
| RuntimeOCD integration | RuntimeOCD adapter、parser、comparison、testがあります。 | REMOVE / FREEZEです。第三者toolへの依存をv0.1の価値にしません。 |
| Inspector | profile context、ModInfo、XML、reference、diagnosticを表示するQuery projectionとUIがあります。 | CORE-3です。v0.1の主画面または主レポートにします。 |
| Compare | static conflict、runtime、source comparisonのqueryがあります。 | CORE-3はidentity/version evidence compareだけです。semantic/runtime compareはDEFERです。 |
| Deployment / controlled write | modlist replacement、backup、junction、process gate、rollback、verificationを持つDeployment projectがあります。 | REMOVE / FREEZEです。MO2の責務と書き込みリスクが大きいです。 |
| game launch | Steam URI固定のlauncherがあります。 | REMOVE / FREEZEです。ModScopeの説明価値ではありません。 |
| Site Adapter | docsに任意のSite Adapter境界があります。v0.1の専用adapter実装は確認できません。 | DEFERです。URL、title、manual evidenceで開始します。 |
| Game Adapter abstraction | 7DTD parsing境界は存在します。将来のgeneric abstractionはdocs上の境界です。 | SUPPORTINGです。7DTDを1つだけ実装し、抽象化を増やしません。 |
| agent-facing Query | Query projectとDesktop Contractsにread modelがあります。 | SUPPORTINGです。人間とagentの同一read modelは維持します。transportは作りません。 |
| CLI / MCP / local API構想 | docs上の候補です。今回のsolutionにCLI projectはありません。 | DEFERです。transportが先行すると製品目的が再び拡散します。 |
| multi-game対応のための抽象化 | docs上のfuture boundaryです。v0.1の複数game実装はありません。 | REMOVE / FREEZEです。7DTDの説明精度を優先します。 |

## 6. CORE / SUPPORTING / DEFER table

CORE capabilityは3つにまとめます。  
複数の要素を1つの責務へ束ねています。  
これ以上のCOREを増やすと、製品の入口が再び不明確になりますわ。

| 判定 | capabilityまたは対象 | v0.1の扱い | 理由 |
|---|---|---|---|
| **CORE-1** | Read-only local environment snapshot（MO2 local inventory、7DTD ModInfo、Modlet、Config XML） | Keep and actively develop | 現在の実環境を説明するための一次入力です。MO2を操作せず、明示されたsourceだけを読みます。 |
| **CORE-2** | Identity and version evidence（package、Modlet、archive、Nexus MOD、Nexus file、Wabbajack record、game version、author version） | Keep and actively develop | ModScope固有の差分です。異なるidentityとversion roleを保存し、match、mismatch、Unknownを説明します。 |
| **CORE-3** | Evidence Inspector（限定されたCompareを含む） | Keep and actively develop | ユーザーが「なぜそう表示されたか」をsource、locator、observed time付きで確認するためのsurfaceです。 |
| SUPPORTING | Web page recognition | 最小限だけ保持 | URL、title、manual observationをevidence inputにする余地を残します。Browserは必須にしません。 |
| SUPPORTING | Wabbajack evidence | 既存snapshotまたはmanifest inputとして保持 | Wabbajackのrecordはartifactとlist provenanceを補強します。co-presenceはcompatibilityへ変換しません。 |
| SUPPORTING | Minimal 7DTD Game Adapter | 7DTD専用として保持 | ModInfoとConfig XMLの意味をlocal evidenceへ投影するために必要です。generic game frameworkは作りません。 |
| SUPPORTING | agent-facing Query | read modelだけ保持 | 人間とagentの結果を分けないために必要です。CLI、MCP、local APIは作りません。 |
| DEFER | Mod Library / Views | v0.1から外す | Browse、install、collection管理に寄り、COREの説明に不要です。 |
| DEFER | requirements / dependency extraction | evidence observationだけ保持 | 研究結果のrecallとidentity resolutionが不足しています。 |
| DEFER | compatibility evidence | source claimとUnknownだけ保持 | co-presence、dependency、load order、runtimeを単一判定にできません。 |
| DEFER | file overlap | raw file relationだけ保持 | overlapとsemantic conflictを分ける必要があります。managerの再実装にもなります。 |
| DEFER | XML semantic conflict analysis | 既存コードをfreeze | 7DTD固有の有用性はありますが、v0.1のidentity/version説明を完成させる前に広げません。 |
| DEFER | runtime evidence | 既存モデルをfreeze | runtime source、game version、tool version、capture条件の検証が必要です。 |
| DEFER | Compareのsemantic/runtime領域 | evidence compareから分離 | 比較可能なidentity roleが揃わない入力を無理に比較しません。 |
| DEFER | Site Adapter | 未実装のまま | Nexus専用化とlive crawlerを避けます。 |
| DEFER | CLI / MCP / local API | 構想を保留 | transportは製品価値の証明後に決めます。 |
| REMOVE / FREEZE | embedded/browser workspace | v0.1の主役から除外 | WebView2、認証、session、browser security、page driftを抱えます。外部browserで代替できます。 |
| REMOVE / FREEZE | Deployment / controlled write | 新規作業を停止 | MO2のsource of truthを変更し、backup、rollback、process gateを必要とします。 |
| REMOVE / FREEZE | game launch | 新規作業を停止 | SteamとMO2の既存責務です。 |
| REMOVE / FREEZE | RuntimeOCD integration | 新規作業を停止 | third-party schemaとruntimeの不確実性が大きいです。 |
| REMOVE / FREEZE | multi-game abstraction | 7DTD以外を扱わない | 抽象化のための抽象化になります。 |

## 7. Browser-primary再評価

### A. Browserを製品の中心にする

**利点**

- Webでmodを探す行動とlocal contextを同じ画面に置けます。
- URL、title、page observationを直接受け取れます。
- 既存のModScope UIが既に動作しています。

**コスト**

- Browser engineを作らなくても、WebView2 host、navigation、history、session、認証、page drift、security boundaryを維持します。
- URLとtitleからのrecognitionは、identityやversionの証明になりません。
- WebView2を中心にすると、ModScopeが「便利なbrowser shell」と認識されます。
- Browser、Mod Library、Deployment、Steam launchが同じsurfaceへ集まり、managerとの境界が崩れます。
- Browserを起動する理由が、product definitionの一文から説明しにくくなります。

**判断**

Browser中心は推奨しません。

### B. Local environment inspectorを中心にし、Webをevidence sourceにする

**利点**

- MO2 profileと7DTD local dataが一次入力になります。
- Wabbajack record、MO2 metadata、ModInfo、archive、URLを同じevidence tableへ並べられます。
- UnknownとNot comparableを、画面の中心に置けます。
- Browserを開かなくても価値を提供できます。
- Browserを使う場合も、URL、title、manual observationという入力境界に限定できます。

**コスト**

- Web discoveryの楽しさは減ります。
- live Nexus metadataやlatest判定は別途source integrationが必要です。
- local snapshotのsource selectionとevidence displayに設計精度が必要です。

**判断**

製品の中心はBを推奨します。

### C. さらに小さい形にする

最小形は、次のread-only reportです。

1. 1つのMO2 sourceを選ぶ。
2. 1つのprofileを読む。
3. 1つのModletまたはarchiveを選ぶ。
4. local identity、version evidence、Wabbajack observation、Unknownを表示する。
5. sourceとobserved timeを開示する。

この形では、embedded browser、Mod Library、Deployment、Steam launchが不要です。

**最終判断**

製品仮説はBです。  
v0.1のdelivery shapeはCですわ。

Browserは「primary surface」ではなく、「必要な時だけ使うevidence source adapter」へ格下げします。

## 8. Wabbajackとの境界

### Wabbajackが既に解決している範囲

| 領域 | Wabbajackで確認できる範囲 | ModScopeの判断 |
|---|---|---|
| modlist作成 | MO2を基準にmodlistをcompileします。 | 再実装しません。 |
| distribution | .wabbajack、Gallery、custom repository、READMEでlistを共有します。 | 再実装しません。 |
| archive取得 | Nexus API、複数downloader、external file、download cacheを扱います。 | 再実装しません。 |
| hash / identity | archive source、file ID、size、content hashを使い、指定artifactを再取得・検証します。 | hash計算やarchive resolverを作りません。 |
| version pinning | list recordにmod version、file、source、size、hashなどを保存します。 | list versionとlocal versionを同一視しません。 |
| metadata | author、version、link、description、README、external file情報を提供します。 | metadataのsource roleを保持して参照します。 |
| update / maintenance | list更新、validation、mirror、patch、force-healing、reportがあります。 | list maintenanceを担当しません。 |
| dependency的な情報 | README、manual step、external file、listの構成要素があります。 | generic dependency graphへ変換しません。 |
| report / manifest | manifestとmodlist reportがあります。 | Wabbajack reportをlocal environment reportの代替にしません。 |
| installed environmentとの関係 | listを指定locationへinstallし、MO2ベースの構成を再現します。 | 現在の任意profileをcross-source説明する責務だけを補完します。 |

[Wabbajack CLI documentation](https://wiki.wabbajack.org/wabbajack_cli/Commands.html)には、compile、install、hash-file、modlist-report、validate-lists、verify-modlist-install、force-healなどのcommandが掲載されています。  
[Installing a Modlist](https://wiki.wabbajack.org/user_documentation/Installing%20a%20Modlist.html)は、README、external file、install location、download location、list separationを説明しています。  
[Auto-healing](https://wiki.wabbajack.org/technical_talk/Auto-healing%20%26%20Force-healing%20Overview.html)は、file validation、mirror、patch、reportを説明しています。

### Wabbajackのhashをどう扱うか

[WabbajackのHashing Overview](https://wiki.wabbajack.org/technical_talk/Hashing%20Overview.html)は、Wabbajackのhash routineをsecure hashではなく、content consistencyと速度を優先するものと説明しています。

したがって、ModScopeは次を分けます。

- Wabbajack content hash: archive artifactの一致を確認する証拠。
- Nexus modID / fileID: source siteのrecord identity。
- MO2 package: local install単位。
- 7DTD Modlet: ModInfoとfolder構造を持つgame unit。
- ModInfo version: Modletが自己申告したraw value。
- Wabbajack list version: listのrevisionまたはrecordの観測値。
- game version: 7DTDのruntime target。

hashが一致しても、Modletのruntime compatibilityは証明しません。

### 7DTDとWabbajackの注意点

[Wabbajackの現行Supported Games一覧](https://wiki.wabbajack.org/user_documentation/Supported%20Games%20and%20Mod%20Managers.html)には、2026-08-15時点で7 Days to Dieが掲載されていません。

これは、7DTD用のcustomまたはunofficialなWabbajack形式データが存在しないことを意味しません。  
これは、公式supported gameとして確認できないことを意味します。

したがって、ModScopeがlocal snapshotのWabbajack recordを読む場合は、次を表示します。

- source status: official、unofficial、local snapshot、unknown
- list identity
- list revisionまたはlist version
- observed time
- raw record

公式supported statusを推測しません。

## 9. Wabbajackをevidence sourceとして使う価値

Wabbajackは、ModScopeの競合相手ではありません。  
Wabbajackは、ModScopeのexternal observation sourceになり得ます。

### 使う価値があるobservational evidence

| evidence | 表示してよい意味 | 表示してはいけない意味 |
|---|---|---|
| recordがlistに存在する | このartifactまたはrecordが、このlistに採用された | runtimeで正常に動く |
| fileIDとsource URLがある | このrecordが特定のsite fileを指す | local Modletが同じである |
| local archive hashがrecord hashと一致する | local archiveとrecord artifactが一致する | game runtimeが互換である |
| 複数listが同じrecordを持つ | 複数の既知listで同じrecordが観測された | dependencyまたはcompatibilityがある |
| list revisionがある | list作者がそのrevisionでrecordをpinした | 現在のlatestである |
| version fieldがある | そのsourceがそのversion stringを記録した | ModInfo、Nexus MOD、game versionと同じversionである |

### 保存すべきminimum fields

Wabbajack recordを使う場合は、少なくとも次を保存します。

- source kind: Wabbajack manifestまたはarchive record
- list identity
- list versionまたはrevision
- record observed time
- target game claim
- archive name
- archive size
- Wabbajack content hash
- source URL
- Nexus modIDとfileID
- raw record locator
- local match method
- comparison status

### relationの名称

ModScopeで使えるrelationは、次のように限定します。

- observed in list
- artifact hash matches
- source file ID matches
- same MOD ID with different file candidate
- record version differs
- not comparable
- unresolved

次のrelationは、Wabbajackのco-presenceだけから作りません。

- depends on
- compatible with
- incompatible with
- runtime verified
- safe for existing save
- latest

### 現在のsnapshotから得られる判断

[version snapshot 2026-08-14](version/snapshot-2026-08-14/README.md)では、Wabbajack archive records、MO2 package、ModInfo、download metadataを比較しています。

確認できる観測値は、次のとおりです。

- Wabbajack archive records: 476
- local rows matching Wabbajack file ID: 26
- same MOD ID with different file candidate: 8
- Wabbajack version mismatchまたはunsafe comparison: 25
- UpdateAvailable: 0

この結果は、Wabbajackが有用なprovenance sourceであることを示します。  
この結果は、compatibilityまたはlatest statusを示しません。

特に、same MOD ID with different file candidateは、ModScopeがMod IDだけでidentityを確定してはいけないことを示します。

## 10. Proposed v0.1

### v0.1の勝利条件

**1つの明示されたMO2 sourceと1つのprofileについて、ModScopeが1つのModletまたはarchiveを、evidence付きで説明できたらv0.1は勝ちですわ。**

ユーザーは、次の5問へ答えを得ます。

1. このprofileで何が有効か。
2. このModletはどのpackage directoryにあるか。
3. このpackageまたはarchiveは、どのsource recordに対応するか。
4. 各sourceはどのversionを主張しているか。
5. なぜ一致、mismatch、Unknown、Not comparableになったか。

### CORE-1: Local environment snapshot

入力は、明示された1つのMO2 sourceと1つのprofileです。

- profile name
- modlist entry
- enabled状態
- priority
- package directory
- Modlet directory
- ModInfo.xml
- Config XML
- local file inventory
- local file hash
- supplied game version
- parser version
- observed time
- diagnostic

このCOREはread-onlyです。  
MO2 profile、modlist、mods、downloads、game folderを変更しません。

### CORE-2: Identity and version evidence

次のidentityを分けます。

- package
- Modlet
- archive
- Nexus MOD
- Nexus file
- Wabbajack list
- Wabbajack record
- ModInfo version
- Nexus file version
- Wabbajack list version
- game version
- author version

次の順序で説明します。

1. raw valueを保存する。
2. source roleを保存する。
3. deterministicなrelationだけを確定する。
4. ambiguous、conflicting、missingはUnknownまたはUnresolvedにする。
5. versionを同一概念へ正規化しない。
6. comparison statusとevidence locatorを表示する。

自動でlatestを決めません。  
fuzzy nameだけでlocal identityを決めません。

### CORE-3: Evidence Inspector

Inspectorは、1つのModletまたはarchiveを中心に表示します。

- local presence
- enabled状態
- priority
- package relation
- ModInfo raw fields
- archive relation
- MO2 metadata
- Nexus file evidence
- Wabbajack record evidence
- version observations
- exact match、mismatch、Unknown
- unresolved reason
- diagnostic
- source locator
- observed time

Compareは、identity roleが比較可能な場合だけ表示します。  
semantic conflict、runtime compatibility、save safetyをこのCOREに含めません。

### v0.1で許す入力と出力

許す入力:

- explicit local MO2 source
- local fixture
- local Wabbajack manifestまたはrecord
- user-provided URL、title、manual observation

許す出力:

- local environment explanation
- identity relation
- version provenance
- source evidence
- Wabbajack observed-in-list relation
- Unknown、Unresolved、Not comparable
- diagnostic

v0.1で外す入力:

- live Nexus crawler
- arbitrary website parser
- runtime game log
- RuntimeOCD log
- arbitrary external agent transport

## 11. Explicit non-goals

v0.1では、次を「便利そうでも絶対に作らない」機能とします。

1. Wabbajackのinstaller、compiler、distribution、archive downloader。
2. Wabbajack modlistの作成、更新、publish、healing。
3. MO2のprofile、enable、priority、download、VFSのreplacement。
4. Vortexのinstall、dependency resolver、deployment、conflict resolverのreplacement。
5. ModScopeからMO2のmodlistを変更するcontrolled write。
6. junction deployment、game folder変更、Steam launch。
7. embedded Browserを中心にしたWeb browsing workspace。
8. Nexusや任意siteのlive crawlerとSite Adapter群。
9. latest version、auto update、version pin repair。
10. descriptionやco-presenceだけからhard dependencyを確定する機能。
11. compatibilityをsingle booleanまたはruntime guaranteeとして出す機能。
12. 完全な7DTD XML semantic simulatorとruntime correctness判定。
13. RuntimeOCDの継続的なintegration。
14. multi-game対応のためのgeneric Game Adapter framework。
15. CLI、MCP、local API、cloud sync、agent backend。
16. denseなMod Library、saved Views、collection管理。

## 12. Existing code impact

削除やrefactorは、この監査では行いません。  
Candidate for deletionは、v0.1の検証後に別途判断する候補です。

| project / subsystem | 分類 | 理由と方針 |
|---|---|---|
| src/ModScope.LocalKnowledge | **Keep and actively develop** | v0.1の一次入力です。MO2 snapshot、7DTD ModInfo、Modlet、Config XML、local diagnostic、identity/version evidenceへ集中します。 |
| LocalKnowledge内のMo2SourceDiscovery、Mo2SnapshotReader、SevenDaysToDieParsing | **Keep and actively develop** | read-only local explanationの中心です。source scopeを明示し、未知を保持します。 |
| LocalKnowledge内のSemanticConflictAnalysis | **Keep but freeze** | 実装とtestは価値がありますが、v0.1の主目的ではありません。新しいoperationとsemantic claimを追加しません。 |
| LocalKnowledge内のRuntimeEvidence、RuntimeOcdAdapter | **Move out of v0.1 path** | runtime evidenceは後段です。既存コードとtestは保存します。新規integrationは止めます。 |
| src/ModScope.Query | **Keep and actively develop** | Inspectorとidentity/version evidenceのread modelに縮めます。runtime、Deployment、Libraryの投影は新規拡張しません。 |
| src/ModScope.Desktop.Contracts | **Keep but freeze** | 現在のbridgeを壊さず保持します。Browser、Deployment、runtime用の契約を増やしません。 |
| src/ModScope.Desktop | **Keep but freeze** | 既存のhostとInspector harnessを保ちます。Browser tab、Deployment、Steam launchを製品の新しい入口にしません。 |
| Desktop内のBrowserTabHost、Nexus search、SteamGameLauncher | **Move out of v0.1 path** | existing UIを削除せず、active developmentを停止します。 |
| src/ModScope.Web | **Move out of v0.1 path** | Browser、Mod Library、Deployment、runtime UIが製品の中心を広げています。UI変更は行いません。 |
| src/ModScope.Deployment | **Candidate for deletion** | MO2のsource of truthへwriteする複雑性が大きく、v0.1の説明価値に不要です。今回の削除は行いません。 |
| tests/ModScope.LocalKnowledge.Tests | **Keep and actively develop** | snapshot、identity、version、diagnosticのtestをv0.1の基準にします。runtimeとsemantic conflictのtestはfreezeします。 |
| tests/ModScope.Query.Tests | **Keep and actively develop** | Inspector、evidence projection、Unknown、Not comparableを基準にします。runtime比較はfreezeします。 |
| tests/ModScope.Desktop.Contracts.Tests | **Keep but freeze** | 現行bridgeの後方互換検証として保持します。新しいUI責務は追加しません。 |
| tests/ModScope.Deployment.Tests | **Move out of v0.1 path** | 現行write planeの安全性を記録します。v0.1のacceptanceには含めません。 |
| tests/Fixtures/mod-identity | **Keep and actively develop** | package、Modlet、archive、Nexus file、ambiguous、missing metadataの基準です。 |
| tests/Fixtures/compatibility、runtime fixtures | **Keep but freeze** | evidence modelの回帰資料として保存します。v0.1のpass criteriaには含めません。 |

### project単位の要約

| project | v0.1に対する位置 |
|---|---|
| ModScope.LocalKnowledge | 中核 |
| ModScope.Query | 中核のread model |
| ModScope.Desktop.Contracts | 凍結した境界 |
| ModScope.Desktop | 凍結したhost |
| ModScope.Web | v0.1経路から外す |
| ModScope.Deployment | 将来削除候補 |

既存コードが多いことは、残す理由になりません。  
ただし、削除は利用実績と代替delivery surfaceを確認してから行います。

## 13. What to stop working on immediately

直ちに新規作業を停止する対象は、次の5つですわ。

1. Browser workspace、WebView2 navigation、Browser recognition、Mod Library UIの拡張。
2. Deployment、controlled write、junction、Steam launchの拡張。
3. RuntimeOCD、runtime evidence、game runtime correctnessの拡張。
4. generic dependency、compatibility boolean、complete XML semantic conflictの拡張。
5. multi-game abstraction、Site Adapter群、CLI、MCP、local API、agent backendの拡張。

既存のコードとtestは削除しません。  
新規のscopeを追加しないことが停止の意味です。

## 14. Next 3 concrete tasks

### Task 1: v0.1 acceptanceを固定する

この監査を基に、次の5問をpass criteriaとして固定します。

- profileで何が有効か。
- Modletがどのpackageにあるか。
- archiveまたはsource recordとどう対応するか。
- 各version observationは何か。
- なぜUnknownまたはNot comparableなのか。

対象は、既存のmod-identity fixtureとversion snapshotです。  
Browser、Deployment、runtimeはacceptanceへ入れません。

### Task 2: read-only evidence reportを1本だけ通す

1つのexplicit MO2 source、1つのprofile、1つのModletまたはarchiveを対象にします。

次の3ケースを証明します。

- exact local relation
- one package to multiple Modlets
- ambiguousまたはconflicting version evidence

Wabbajack recordを使う場合は、observed-in-listとして記録します。  
compatibilityやdependencyへ変換しません。

### Task 3: owner playcheckで製品仮説を判定する

実データをread-onlyで読みます。  
ModScopeの出力だけで、ユーザーが5問へ答えられるか確認します。

答えられない場合は、BrowserやDeploymentを追加しません。  
不足しているevidenceまたはidentity boundaryだけを修正します。

この3 taskが完了するまで、既存のBrowser、Deployment、RuntimeOCD、multi-gameの作業を再開しませんわ。
