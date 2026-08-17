# ModScope UI原則

## 文書の役割

この文書は、ModScopeのv0.1 UIを決める共通原則です。

この文書は、pixel単位のvisual specificationではありません。
新しいscreenやcomponentの判断を、毎回ゼロから始めないための基準です。

staticな基準画面は、[UI reference](ui-reference/index.html)で確認します。
製品定義と責務境界は、[ModScope設計](design.md)を正本とします。

## 1. Target userと最優先の仕事

### Target user

主対象は、複数source、version、profile、evidenceを日常的に扱うpower userです。

power userは、MO2の概念とtechnical vocabularyを理解します。
power userは、名前の類似だけでdependencyやcompatibilityを確定しません。
power userは、source、observed time、diagnostic、uncertaintyを確認します。

maintainerの知見は、provenance、version、conflict、compatibilityの表示へ反映します。
maintainer専用の管理画面は作りません。

初心者には、情報密度を下げずに軽いorientationを提供します。
orientationは、Browser、Mod Library、Local context、Inspectorの役割と最初の主actionだけを示します。
orientationはskipできます。

### 最優先の仕事

主導線は、Web pageからlocal確認です。

利用者は、表示中のMODについて次の事実を短時間で確認します。

- installed状態
- enabled状態
- known version
- sourceとevidence state
- 次にできる明示action

## 2. UIの中心原則

### 2.1 Browser-firstだが、Local Mod Knowledgeを同格に扱う

起動時の視線は、BrowserとWeb pageを主役にします。
Local Mod Knowledgeは、Web pageの隣でlocal contextを提供します。

Mod Library、Browser、Local contextを一つのWorkspaceへ置きます。
Browser integrationは、first-class workflowとして扱います。

### 2.2 高密度だが、視覚的に騒がしくしない

power userが大量のMODをscanできる密度を既定にします。
compact tableは、1920x1080で20〜30件程度を目標にします。

情報量は、装飾的なcardや余白より優先します。
高密度な情報は、列、行、見出し、短いlabelで整理します。

### 2.3 EvidenceをInferenceより先に表示する

画面は、ModScopeが観測した事実、推定、未知、確認要の状態を混ぜません。
evidence state、review/action state、severityを別の概念として表示します。

statusは短いtextとmarkerで表示します。
色だけで状態を伝えません。

### 2.4 Actionは明示的で、変更は予測可能にする

rowのselectionとactionを分けます。
single clickはselectionだけを行います。
Inspector、Web page、Edit、Applyは明示actionで実行します。

候補生成とpage observationは自動実行できます。
navigation、selection、identity confirmation、writeは、明示操作なしに変更しません。

### 2.5 Progressive disclosureを使う

通常画面は、結論と最小summaryを表示します。
詳細は、右Inspectorへ開きます。
raw XML、XPath、attribute、priority、詳細diagnosticはInspector内で折りたたみます。

大きなgraphやraw dumpを初期画面へ置きません。

### 2.6 AIは補助actionにする

AI-assisted functionalityは、Search、Inspector、Diagnosisの補助actionとして扱います。
AI専用の主画面や常設paneを、通常画面の主役にしません。

## 3. 基本layoutとvisual baseline

### 3.1 Reference layout

基準viewportは1920x1080です。

通常の三面構成は、次の順序です。

1. 左：Mod Library
2. 中央：Browser
3. 右：Local contextまたはInspector

中央のBrowserを最大の主面にします。
左と右のpaneは、必要な情報へすぐ進める幅を持ちます。

Toolbarは、tab、navigation、address、history、pane actionを提供する通常のbrowser chromeです。
Browserの基本操作を、hidden menuへ押し込みません。

### 3.2 Dark desktop baseline

ModScopeが所有するsurfaceは、dark desktop toolのvisual baselineを使います。
既存のChrome dark paletteを基準にします。

外部Web pageの色は、ModScopeのpaletteで変更しません。

gradient、巨大な角丸、装飾的な背景、常設の巨大cardは既定にしません。
visual treatmentとmotionは、情報理解、feedback、操作感、restrainedなdelightへ寄与する場合だけ使います。
注意を奪う装飾と、反復で煩わしくなるmotionは避けます。

## 4. Mod Library pattern

### 4.1 Compact table

Mod Libraryは、compact tableを既定にします。

base列は、次の順序で固定します。

1. Name
2. State
3. Version

Load OrderまたはPriorityが必要なViewでは、左端へ小さい序数を置けます。
Priorityのために太い専用列を常設しません。

contextに応じて補助列を1つ程度追加できます。
base列の位置は変えません。

### 4.2 View別のdefault sort

| View | default sort |
| --- | --- |
| Browse / All | Name |
| Enabled / Disabled | Load Order |
| Review / Identity unresolved / Profile unresolved | actionable state |
| Deployment | Load Order |
| Diagnosis | actionable state |

sort変更は、常に明示操作で行います。
Searchとfilter後も、選択中のsort順とload-order上の相対位置を維持します。
ad-hocなsort preferenceの保存は、この文書の範囲外です。
Saved Viewの保存仕様は、design.mdに従います。

### 4.3 Selection、multi-select、action

single clickはrowを選択します。
selectionは、page identity、Local context、Inspector resultを自動変更しません。

multi-selectは、compare、diagnosis、copyなどのread actionを優先します。
write bulk actionは、previewと明示承認を要求します。

主actionは、ContextまたはInspectorの見出し近くへ置きます。
row actionは、hover、focus、またはselection時に表示します。

mouse操作を主にします。
focus、row移動、Enter、Escape、検索、主要shortcutをkeyboardで完了できるようにします。

## 5. Evidence、status、warning

### 5.1 UI vocabulary

| 出典 | 目的 | 具体対象 | 役割 | 前後関係 | 候補語 | 初出定義 |
| --- | --- | --- | --- | --- | --- | --- |
| Local Mod Knowledgeとpage observation | 観測済みの事実を示す | source-backedなvalueまたはpage observation | evidence state | inferenceより前に表示する | Observed | 根拠を参照できる観測済みの事実 |
| Local Mod Knowledgeとanalysis | 複数の事実から導いた内容を示す | source-backed factから導いた説明 | evidence state | Observedを根拠として表示する | Inferred | 根拠と推論過程を追跡できる推定 |
| Local Mod Knowledgeとanalysis | 根拠不足を示す | valueまたはrelationを確定できない状態 | evidence state | 推測で埋めない | Unknown | 現時点で確定できない状態 |
| identity、diagnostic、controlled write | 人の確認や操作を促す | reviewまたは次actionが必要なrecord | review/action state | evidence stateと並行して表示する | Needs review | 根拠確認または明示操作が必要な状態 |
| operation、analysis、deployment | actionへの影響度を示す | actionを阻害する不整合 | severity | action前に表示する | Error | actionを阻害する、または明確な不整合 |
| operation、analysis、deployment | 確認の必要性を示す | actionは可能だが確認を推奨する状態 | severity | actionと理由を併記する | Warning | actionは可能だが確認を推奨する状態 |
| operation、analysis、deployment | 判断材料を示す | actionを阻害しない情報 | severity | context内で補助表示する | Info | 判断材料として表示する情報 |

主labelは、Observed、Inferred、Unknown、Needs review、Error、Warning、Infoを使います。
技術語、raw value、source、diagnosticはInspectorで表示します。

### 5.2 Context別の優先順位

| Context | 優先する情報 |
| --- | --- |
| Browse | identity、installed、version |
| Diagnosis | conflict、compatibility |
| Deployment | 対象、変更差分、rollback |

情報モデル上では、provenance、version、conflict、compatibilityを同格に扱います。
表示優先度だけをcontextに応じて変えます。

### 5.3 Warningの配置

Errorは、actionを阻害する理由と次の復旧手段を示します。
Warningは、actionを継続できることと確認理由を示します。
Infoは、判断材料としてcontext内へ置きます。

Unknownは、warning severityへ自動変換しません。
Needs reviewは、evidence不足または人の確認が必要な状態として独立表示します。

## 6. Actionと安全境界

read actionとwrite actionを、同じ意味のbuttonとして扱いません。

profile、modlist、deploymentへ影響するwrite actionは、次の順で進みます。

1. preview
2. targetと変更差分の確認
3. riskとrollbackの確認
4. explicit approval
5. apply後の再読検証

MO2または7DTDのsource of truthを、UIから直接変更しません。

### Disabled actionと件数の定義

disabled actionは、実行可能なactionに見える文言を残しません。
disabled actionは、現在の状態または必要な前提を短い理由で示します。
理由はtooltipまたはmenu内の補助文で確認できます。

同じ画面で異なる母集合を数える場合は、labelへ母集合を含めます。
Profile editorは`profile rows`を数えます。
通常のMod Libraryは`Local MOD records`を数えます。
separator rowはprofile rowsへ含め、Local MOD recordsへ含めません。
Deployment previewは`MODLIST changes`、`Junction operations`、`Diagnostics`を別のsummary itemで示します。
`Review and apply`は、Hostが`canApply`と`planId`を両方返した場合だけ表示します。
Applyがblockedの場合は、理由を表示し、実行可能なApply actionを表示しません。

### Trust / safetyのhard ban

次の3項目だけをhard banにします。

- silent writeまたはsilent state change
- evidenceの確実性をcolor-onlyで表示すること
- previewまたはexplicit approvalなしのdestructive action

AI-first、dashboard、card、gradient、animationなどのvisual preferenceは、hard banではありません。
これらは、過剰な装飾と認知負荷を避けるguidelineとして扱います。

## 7. 例外とreview checklist

既定patternから外れるUIは、次の内容をIssueまたはPRへ記録します。

- 対象userの仕事
- 既定patternで不足する点
- 採用する代替案
- 既存画面とevidence表示への影響

新しいscreenまたはcomponentは、owner visual reviewで確認します。

- 30秒程度で、表示中MODのinstalled、enabled、version、evidence stateを説明できる
- ModScopeの確定情報とUnknownを区別できる
- 次のread actionを迷わず選べる
- selectionだけでpage、identity、writeが変わらない
- 20〜30件の一覧をscanできる
- raw detailを開かなくても結論を理解できる

代表taskは、Web pageからlocal確認です。
static referenceは、mixed evidence stateを使います。

## 8. 適用範囲と変更境界

この文書は、Browser、Mod Library、Local context、Inspector、Compare、Diagnosis、Deployment previewへ適用します。

将来のfeatureへは、evidence、hierarchy、action、densityの核を再利用します。
将来のfeatureが既定patternから外れる場合は、例外の理由を記録します。

このIssueでは、Svelte UI、Bridge contract、Public API、C#型、MO2 integrationを変更しません。
