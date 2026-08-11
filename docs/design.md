# ModScope 設計

## 1. この文書の役割

この文書は、ModScopeの全体アーキテクチャと段階的な実装境界を定義します。

v0.1は最終ゴールではありません。v0.1は、MO2の7DTD MOD空間をread-onlyで読み取り、AI agentが利用できる構造化indexを生成する最初のvertical sliceです。

将来のsemantic conflict analysis、RuntimeOCD連携、GUI、write操作、ゲームadapterは、別フェーズで追加します。ただし、初期データモデルとsource boundaryは、それらの追加を妨げない形にします。

## 2. Problem statement

現在は、CodexなどのAI agentをMO2のディレクトリ直下で開き、MODの整理や調査を行っています。

この方法には次の問題があります。

- MO2全体が大きく、探索範囲が広い
- 数十から数百のMODを毎回全文探索する必要がある
- XML patchの対象、XPath、ロード順を横断して確認しにくい
- 同名ファイル競合と、意味論的なpatch競合を区別しにくい
- AI agentへ巨大なJSONや大量のraw XMLを毎回渡すと、context効率が悪い
- MO2の正本と調査用の派生データの境界が曖昧になりやすい

ModScopeは、MO2のデータを移行せず、再生成可能な構造化indexとquery layerを提供します。

## 3. Goals

- MO2の既存構造をsource of truthとして利用する
- 明示したMO2 profileをread-onlyで読み取る
- enabled状態、priority、MODごとのファイル一覧を取得する
- ModInfo.xmlとConfig XMLを構造化する
- XML patch operation、XPath、対象XML、attributeを検索可能にする
- MOD、ファイル、XML、patch targetのforward indexを作る
- XPath、対象XML、entity、property、attributeからMODへ戻るreverse indexを作る
- AI agentへ対象を絞ったquery resultを返す
- 入力の変更に対してincremental indexingできる構造を持つ
- static evidence、runtime evidence、解析結果を分離する
- 将来のsemantic conflict analysisを追加できる
- GUIがquery layerの派生データだけを利用できる
- 将来のwrite操作をread layerから分離できる

## 4. Non-goals

次は、全体の将来像には含まれますが、初期フェーズの完成条件ではありません。

- MO2本体の置き換え
- MO2 profile、modlist、mods、downloadsの自動変更
- 独自Mod Managerとしての機能一式
- 最初からのゲーム横断対応
- GUIの初期実装
- 完全なXML patch semanticsの初期実装
- RuntimeOCDのコードコピーまたは再実装
- 巨大な単一JSONをAI agentへ毎回読み込ませる設計

## 5. 全体データフロー

    MO2 source
      -> MO2 Adapter
      -> source snapshot
      -> 7DTD normalization
      -> forward / reverse index
      -> Query / Search layer
      -> Conflict Analyzer and RuntimeOCD Adapter
      -> GUI read model
      -> optional write layer

各矢印は、データと責務の境界を表します。

- MO2 AdapterはMO2の読み取りだけを担当します。
- 7DTD AdapterはModInfo.xml、Config XML、patch構文を解釈します。
- Indexerはraw inputから再生成可能な派生データを作ります。
- Query layerは必要な範囲だけを返します。
- Conflict Analyzerはstatic indexを意味論的に解釈します。
- RuntimeOCD Adapterは外部のruntime evidenceを取り込みます。
- GUIはread modelを表示します。
- write layerは将来の明示的な変更操作を担当します。

## 6. MO2から読むもの

### 6.1 入力境界

初期実装では、ユーザーが明示した次の入力を読み取ります。

- MO2 instanceのmods
- 使用するprofileのmodlist.txt
- 各MODのディレクトリとファイル

MO2のprofile path、modlistの行形式、priorityの解釈、無効MODの表現、欠落MODの扱いは、実データまたは確認済みの仕様で検証します。

### 6.2 modlist

各行について、次を保持します。

- raw line
- normalized mod identifier
- enabled / disabled
- profile上のpriority
- 解決したMOD directory
- 解決できなかった理由
- parser version

raw lineとnormalized valueが異なる場合も、両方を保持します。

### 6.3 MODファイル

各MODについて、次を取得します。

- MOD identifier
- directory name
- display name候補
- enabled
- priority
- relative file path
- file type
- size
- content fingerprint
- parse status

content fingerprintのアルゴリズムはversionedにします。fingerprintだけでなく、source pathとsnapshotを保持します。

## 7. 7DTD MODから抽出するもの

### 7.1 ModInfo.xml

ModInfo.xmlが存在する場合は、確認できた標準フィールドをnormalized valueとして保持します。

同時に、次も保持します。

- raw XMLまたは再現可能なsource reference
- unknown element
- unknown attribute
- parser warning
- ファイル位置
- 所属MOD

ModInfo.xmlのschema versionや配置の差異は、実データで確認します。

### 7.2 Config XML

Config/**/*.xmlを対象にします。

各XML documentについて、次を保持します。

- MOD
- relative path
- target XML候補
- encoding
- well-formed status
- parse diagnostic
- document fingerprint

### 7.3 XML patch operation

初期indexでは、次の情報をrawとnormalizedの両方で保持します。

- raw operation element name
- normalized operation kind
- XPath
- target XML
- target node候補
- attribute name
- patch fragment
- source file
- XML location
- MOD
- enabled状態
- MO2 priority

対象operationの候補には、次を含めます。

- append
- prepend
- set
- setattribute
- remove
- removeattribute
- insertBefore
- insertAfter
- その他の7DTD patch operation

operation名の意味、属性名、XPathの解釈、対象XMLの解決規則は、実仕様を確認してからsemantic analyzerへ実装します。未知のoperationはunknownとして保存します。

## 8. 内部モデル

### 8.1 SourceSnapshot

1回の読み取り結果を表します。

- snapshot id
- source root
- profile
- created time
- schema version
- parser version
- input manifest
- diagnostics

snapshotは、同じ入力から再生成できる派生データです。

### 8.2 ProfileState

- profile identifier
- raw modlist
- normalized mod state
- enabled / disabled
- priority
- unresolved entries

### 8.3 ModRecord

- stable mod id
- directory name
- display name
- enabled
- priority
- ModInfo data
- file references
- diagnostics

MOD名だけをstable idにしません。directory、profile、source identityなどの組み合わせを候補にし、実データで検証します。

### 8.4 FileRecord

- stable file id
- MOD
- normalized relative path
- file type
- size
- fingerprint
- parse status

### 8.5 XmlDocumentRecord

- file reference
- target XML
- encoding
- well-formed status
- node summary
- parse diagnostics

### 8.6 PatchOperationRecord

- operation id
- source file
- MOD
- enabled状態
- MO2 priority
- raw operation name
- normalized operation kind
- raw XPath
- normalized XPath候補
- target XML
- attribute
- patch fragment reference
- source location
- diagnostics

### 8.7 将来の解析モデル

将来は、次の派生モデルを追加します。

- TargetReference
- RuntimeEvidence
- Conflict
- EffectiveChange
- QueryResult

これらはsource dataを上書きしません。static evidence、runtime evidence、inference、diagnosticを区別します。

## 9. Indexとreverse index

### 9.1 Forward index

次の方向で追跡できます。

    profile
      -> mod
      -> file
      -> XML document
      -> patch operation
      -> target / XPath / attribute

### 9.2 Reverse index

次の検索を低コストで実行できる構造を作ります。

- gunAK47を変更するMOD
- items.xmlに触る有効MOD
- 同じXPathを変更するMOD
- 同じXPathとattributeを変更するMOD
- このMODが上書きする、または上書きされる対象
- 特定のentity、property、featureに関連するMOD群
- priorityによって結果が変わりそうな箇所

### 9.3 AI agent向け出力

AI agentにはindex全体を渡しません。

query resultは、原則として次を含む小さな説明単位にします。

- 結論
- 該当MOD
- enabled状態
- priority
- target XML
- XPath
- operation
- source path
- 根拠の種類
- uncertainty
- 関連するdiagnostic

必要な場合だけraw XMLや詳細なpatch fragmentを取得します。

## 10. Incremental indexing

入力manifestに、少なくとも次を記録します。

- source root
- profile
- modlist fingerprint
- MOD directory identity
- file relative path
- file fingerprint
- parser version
- schema version

更新方針は次のとおりです。

- MODファイルが変わった場合は、そのファイルと依存する派生レコードを更新します。
- MODが追加された場合は、そのMODだけを追加解析します。
- MODが削除された場合は、そのMODの派生レコードを削除または無効化します。
- enabled状態やpriorityだけが変わった場合は、profile projectionとpriority依存の派生結果を更新します。
- modlistが変わった場合は、影響範囲を再評価します。
- parser versionまたはschema versionが変わった場合は、必要な範囲を再生成します。
- 影響範囲を確定できない場合は、安全側に広い範囲を再解析します。

同じ入力と同じparser versionから、同じnormalized resultを生成できることを目標にします。

## 11. Conflict Analyzerの将来設計

初期indexは、patch operationの抽出と検索を担当します。意味論的な競合判定は後続レイヤーに置きます。

Conflict Analyzerは、少なくとも次を扱える構造を目指します。

- append
- prepend
- set
- setattribute
- remove
- removeattribute
- insertBefore
- insertAfter
- その他の7DTD patch semantics

将来の解析結果には、次を含めます。

- 競合対象
- 関係するMOD
- MO2 priority
- operation sequence
- 競合の種類
- 想定されるeffective result
- static evidence
- 判定confidence
- 未解釈のoperation
- 解決不能な理由

同名ファイルだけの競合と、同じtargetを意味論的に変更する競合を区別します。

判定できない場合に、正常または競合と断定しません。unknown、possible、verifiedなどの状態を保持します。

## 12. RuntimeOCD Adapter

RuntimeOCDのログや結果は、ModScopeのstatic indexへ直接混ぜません。

Adapterは、外部結果を次のようなruntime evidenceへ変換します。

- evidence source
- RuntimeOCD version
- game version
- capture time
- MOD identity
- target XML
- XPathまたは関連target
- observed operationまたはresult
- raw log reference
- import diagnostic

ModScopeは、次を比較できる構造を目指します。

- staticには存在するがruntimeで観測されない変更
- runtimeで観測されたがstaticに対応しない変更
- staticのeffective resultとruntime resultの差異
- load orderまたは条件分岐による差異

RuntimeOCDのコードをコピーする前提にはしません。ライセンスと公開仕様が確認できない場合は、integration範囲を制限します。

## 13. GUI read model

GUIは、query layerが提供する派生データだけを利用します。MO2のディレクトリを直接表示したり、index全体を一括表示したりしません。

最終的なGUIは、MO2の高密度画面を再現しません。

- 最初に概要と重要な判断を表示します。
- MODを目的、feature、影響範囲、状態などで段階的に絞り込みます。
- 詳細はMODやtargetを選択した後に表示します。
- conflict detailは必要時だけ開きます。
- 「何が起きているか」「なぜ重要か」「どの証拠があるか」を先に表示します。
- raw XML、XPath、attribute、priorityは詳細表示へ分離します。
- 大きな表やgraphを初期画面に表示しません。
- 色だけで状態を表しません。

GUI用read modelには、説明用のsummaryと、詳細へ進むreferenceを含めます。

## 14. Read / write boundary

### Read layer

- MO2 sourceを読む
- source snapshotを作る
- indexと解析結果を生成する
- MO2 sourceを変更しない

### Write layer（将来）

- enable / disable
- reorder
- profile変更
- その他のMO2操作

Write layerは、read layerと別の責務にします。実装する場合は、dry-run、変更差分、対象確認、明示承認、失敗時の復旧方針を必須にします。

## 15. Repository architecture候補

実装開始後の候補は次のとおりです。これは現在作成するディレクトリ構成ではありません。

    AGENTS.md
    docs/
      design.md
      future-vision.md
    src/
      source/
        mo2/
      game/
        seven-days-to-die/
      indexing/
      query/
      analysis/
        conflict/
        runtime/
      presentation/
        read-model/
      mutation/
    tests/
      fixtures/

7DTDを最初のgame adapterにします。抽象化は、実際に複数の実装が必要になった時点で増やします。

## 16. 技術選定

### 今決めること

- MO2をsource of truthとする
- read-only firstとする
- rawとnormalized valueを両方保持する
- evidenceとinferenceを区別する
- schema versionとparser versionを持つ
- fingerprintとinput manifestを持つ
- query resultを小さな説明単位にする
- static、runtime、derived dataを分離する
- GUIとwrite layerをsourceから分離する
- fixtureで実仕様を検証する

### 後回しにすること

- 実装言語
- SQLite、JSONL、その他のstorage engine
- CLI framework
- GUI framework
- index公開形式
- RuntimeOCDのtransport
- MO2 write API
- 第二ゲームのadapter
- 配布形式とinstaller

storage engineは後で選べます。ただし、forward query、reverse query、incremental update、provenanceを効率よく実現できる必要があります。

## 17. 調査が必要な事項

- MO2のmodlist.txtの実際の行形式とpriority semantics
- profile、mods、downloads、virtual filesystemの境界
- 欠落MOD、重複MOD名、無効MODの扱い
- ModInfo.xmlのschema差異
- 7DTDのXML patch operationと対象解決規則
- XPathの正規化可否
- XML encoding、namespace、malformed XMLの扱い
- RuntimeOCDの公開仕様、ログ形式、ライセンス
- junction、symlink、case sensitivityの扱い
- 大規模MOD空間での性能
- fixtureに含める実データの匿名化

## 18. 段階的実装

### Phase 0：設計とfixture

設計文書、実仕様の確認項目、最小fixtureを整えます。

### Phase 1：source snapshot（初期milestone / v0.1）

明示したMO2 profileから、modlist、enabled状態、priority、MOD一覧、ファイル一覧、fingerprintをread-onlyで取得します。

### Phase 2：XML index

ModInfo.xml、Config XML、XML patch operation、XPath、target XML、attributeを構造化します。

### Phase 3：query surface

forward index、reverse index、MOD、target、XPath、entity、property単位のqueryを提供します。

### Phase 4：semantic conflict analysis

patch operation semantics、priority、operation sequence、effective resultを解析します。

### Phase 5：RuntimeOCD integration

runtime evidenceを取り込み、static resultと比較します。

### Phase 6：低認知負荷GUI

概要、検索、filter、段階的詳細表示、conflict detail、evidence表示を追加します。

### Phase 7：controlled write layer

dry-run、差分、明示承認を備えたMO2操作を、必要性が確認できた場合に追加します。

### Phase 8：game adapter拡張

第二ゲームへの需要と共通性が確認できた場合だけ、game adapterの範囲を広げます。

## 19. Acceptance criteria

### Phase 1 / v0.1

- 明示したMO2 instanceとprofileを読み取れる
- MO2 sourceを変更しない
- modlistからenabled状態とpriorityを取得できる
- MODごとのファイル一覧を取得できる
- fingerprint付きsnapshotを生成できる
- 同じ入力から再現可能な結果を生成できる
- 変更したMODまたはファイルを識別できる

### Phase 2以降

- ModInfo.xmlとConfig XMLを構造化できる
- XML patch operation、XPath、target XML、attributeを検索できる
- malformed inputをdiagnosticとして残せる
- gunAK47、items.xml、同一XPath、上書き関係をqueryできる
- semantic conflictの根拠と不確実性を表示できる
- RuntimeOCD evidenceをstatic evidenceと区別できる
- GUIがquery layerの派生データだけを利用する
- GUIが認知負荷を下げた段階表示を提供する
- write操作をread layerから分離できる
- 追加のgame adapterを既存の7DTD解析へ影響させずに追加できる
