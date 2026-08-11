# ModScope 将来像

## 1. 目的

ModScopeの最終ゴールは、MO2で管理された7DTD MOD空間を、次の2つの利用者が理解できる状態にすることです。

- AI agent：必要な証拠だけを取得し、短いqueryでMOD空間を探索する
- 人間：MOD構成、変更の影響、競合、最終結果を無理なく理解する

ModScopeはMO2を置き換えません。MO2をsource of truthとして残し、ModScopeは再生成可能なindex、解析結果、read modelを提供します。

## 2. 最終的な利用体験

### AI agent

AI agentは、数百MODと数千XML patchを毎回全文読みません。

次のような質問を、対象を絞った結果で処理します。

- gunAK47を変更しているMODはどれか
- items.xmlに触っている有効MODはどれか
- 同じXPathまたはattributeを変更するMODはあるか
- このMODが上書きする、または上書きされる対象は何か
- ロード順によって結果が変わりそうな箇所はどこか
- static解析とruntime観測が一致しない箇所はどこか

結果は、結論、根拠、source path、priority、uncertaintyを含む説明単位にします。

### 人間

人間向けのGUIは、情報を一度に多く表示しません。

最初に、次の情報を表示します。

- 現在のprofileの概要
- 重要な警告
- 影響の大きいMODまたはfeature
- 競合の可能性
- 次に確認すべき対象

利用者がMOD、feature、targetを選択した後に、詳細を表示します。

## 3. GUIの設計原則

### 3.1 情報を段階的に表示する

初期画面には概要だけを表示します。詳細は、検索、filter、MOD選択、target選択の後に表示します。

### 3.2 重要性を説明する

単に「競合あり」と表示しません。

次の順序で説明します。

1. 何が起きているか
2. なぜ重要か
3. どのMODが関係するか
4. どのpriorityが関係するか
5. どの証拠があるか
6. 何が未確認か

### 3.3 技術詳細を隠しすぎない

raw XML、XPath、attribute、patch fragment、ロード順は、詳細画面から確認できるようにします。

ただし、技術詳細を初期画面へ押し出しません。summaryからevidenceへ進める構造にします。

### 3.4 巨大な表とgraphを初期表示しない

巨大なMOD表やconflict graphを、初期画面の中心に置きません。

必要な対象を選択した後に、関係する部分だけを表示します。graphを表示する場合も、局所的なsubgraphに限定します。

### 3.5 色だけに依存しない

状態は、文章、アイコン、構造、ラベル、順序で説明します。色は補助情報とします。

## 4. 最終アーキテクチャ

最終的なModScopeは、次の責務を持ちます。

1. MO2 Adapter
2. 7DTD Adapter
3. source snapshot
4. normalized model
5. forward / reverse index
6. Query / Search layer
7. semantic Conflict Analyzer
8. RuntimeOCD Adapter
9. GUI read model
10. optional write layer
11. optional game adapter

各責務は、source data、derived data、evidence、inferenceを混ぜません。

## 5. Semantic conflict analysis

最終的なConflict Analyzerは、ファイル名の一致だけを検出しません。

次を考慮します。

- append
- prepend
- set
- setattribute
- remove
- removeattribute
- insertBefore
- insertAfter
- その他の7DTD XML patch semantics
- XPath
- target XML
- attribute
- operation sequence
- MO2 priority

解析結果には、少なくとも次を含めます。

- 競合対象
- 関係するMOD
- operation sequence
- priority
- expected effective result
- static evidence
- confidence
- 未解釈のoperation
- 判定不能の理由

判定不能な状態を、正常または競合と断定しません。

## 6. RuntimeOCDとの連携

RuntimeOCDの結果は、static analysisの代替ではありません。

ModScopeは、次の2種類のevidenceを分けて保持します。

- static evidence：ファイルとpatchから導いた事実
- runtime evidence：実ゲーム実行時に観測された事実

将来は、次を比較します。

- staticにはあるがruntimeで観測されない変更
- runtimeで観測されたがstaticに対応しない変更
- staticのeffective resultとruntime resultの差異
- load order、条件分岐、runtime stateによる差異

RuntimeOCDのコードをコピーする前提にはしません。Adapter接続、ログ取り込み、ライセンス確認を基本とします。

## 7. Read / writeの将来境界

### Read plane

read planeは、MO2 sourceを読み取り、snapshot、index、query result、解析結果を生成します。

### Write plane

write planeは、将来必要になった場合だけ追加します。

候補は次のとおりです。

- enable / disable
- reorder
- profile変更
- 変更の適用

write planeは、次の条件を満たす必要があります。

- read planeと独立している
- dry-runを提供する
- 変更前後の差分を表示する
- 対象profileとMODを明示する
- ユーザーの明示承認を要求する
- 失敗時の復旧手段を定義する
- MO2の実仕様に基づく

## 8. ゲーム横断のescape hatch

最初は7DTDとMO2だけを対象にします。

将来のゲーム対応を妨げないため、次の境界を維持します。

- source snapshot、file、evidence、queryは可能な範囲で汎用的にする
- ModInfo.xmlや7DTD patch semanticsは7DTD Adapterに置く
- Conflict Analyzerの意味論はgame-specific registryまたはadapterで扱う
- 2つ目のゲームを実際に扱う必要が生じるまで抽象化を増やさない

## 9. 段階的ロードマップ

### Phase 0：設計と仕様確認

設計文書、実データfixture、MO2と7DTDの調査項目を準備します。

完了条件は、実装者がsource boundaryとデータモデルを推測せずに作業を開始できることです。

### Phase 1：source snapshot

MO2 profile、modlist、enabled状態、priority、MOD directory、ファイル一覧をread-onlyで取得します。

完了条件は、同じ入力から再現可能なsnapshotを生成できることです。

### Phase 2：structured index

ModInfo.xml、Config XML、patch operation、XPath、target XML、attributeを構造化します。

完了条件は、MOD空間を全文探索せずに、対象XMLとXPathからMODへ戻れることです。

### Phase 3：query surface

AI agentと将来GUIが使うquery resultを提供します。

完了条件は、source path、priority、enabled状態、evidenceを含む小さな結果を返せることです。

### Phase 4：semantic conflict

patch semantics、priority、operation sequenceを解析します。

完了条件は、conflict、effective result、uncertaintyを根拠付きで説明できることです。

### Phase 5：runtime evidence

RuntimeOCDなどの外部結果をAdapter経由で取り込みます。

完了条件は、static evidenceとruntime evidenceを比較し、差異を表示できることです。

### Phase 6：低認知負荷GUI

概要、検索、filter、段階的詳細表示、conflict detail、evidence表示を実装します。

完了条件は、利用者が巨大な表やraw XMLを最初に読むことなく、重要な影響と確認対象を理解できることです。

### Phase 7：controlled write

必要性が確認できた場合に、dry-runと明示承認付きのMO2操作を実装します。

完了条件は、read planeを壊さずに変更内容を事前確認できることです。

### Phase 8：game adapter拡張

第二ゲームへの需要と共通性が確認できた場合だけ対応します。

完了条件は、7DTD固有の解析を壊さずに新しいgame adapterを追加できることです。

## 10. 最終的な成功条件

ModScopeは、次の状態を目指します。

- MO2の正本を変更せずにMOD空間を理解できる
- AI agentが必要な証拠だけをqueryできる
- 人間が低い認知負荷で影響範囲を確認できる
- 同名ファイル競合とsemantic conflictを区別できる
- static resultとruntime resultを比較できる
- 解析結果の根拠と不確実性を追跡できる
- GUIがquery layerの派生データだけを利用する
- write操作がread planeから分離されている
- 新しいgame adapterを必要な時だけ追加できる

## 11. 今後の判断基準

新しい機能を追加する前に、次を確認します。

- 実際のMO2または7DTDの問題を解決するか
- AI agentまたは人間の探索コストを下げるか
- source of truthを曖昧にしないか
- read-only境界を壊さないか
- GUIの認知負荷を増やさないか
- 将来のsemantic analysisやruntime comparisonを妨げないか
- 現在のフェーズで検証可能な小さなsliceにできるか
