今回の結果を、ChatGPTへそのまま渡せる相談用ブリーフに整理いたしましたわ。
以下をコピーしてお使いくださいませ 📝

```text
7 Days to DieのMOD version整合性判定について相談したいです。

目的は、約500 MOD規模の運用で、

- installed version
- latest version
- 更新可能性
- game version互換性

を高信頼に判定することです。

判定率よりも、誤判定を避けることを重視します。
特に、version文字列が取得できても、その値の意味が異なる場合は自動判定しません。

## 調査範囲

### 既存MO2

既存のMO2環境を読み取り専用で調査しました。

このMO2環境はSmorgasbordそのものとは断定していません。
dataset上では `existing_mo2` として別管理しています。

観測数:

- package: 98
- ModInfo.xml: 105
- package meta.ini: 87
- download .meta: 204
- profile: Defaultなど複数

### Smorgasbord

公開Wabbajack manifestだけを取得しました。
Wabbajack本体、7 Days to Die、MO2本体は変更していません。

観測値:

- list version: 3.1.1.39
- Nexus Collection ID: 445130
- Collection revision: 39
- Wabbajack archive record: 476
- 公開manifestから抽出したdataset行: 451
- 既存MO2行と同じmodID/fileIDまで対応できたlocal行: 26

Nexus live pageはCloudflare challengeで取得できませんでした。
そのため、Nexusの現行latest file/versionは未確定です。

## 重要な観測結果

### MO2 cache

MO2の `meta.ini` について、

- `version` と `newestVersion` の不一致: 16件
- ModInfo.xml欠落: 10件
- ModInfo.xml versionがMO2 package versionより低い候補: 18件

MO2の `newestVersion`は、現在のNexus latestではなく、
MO2が過去に取得したcache値として扱うべきです。

### Wabbajackとlocalの矛盾

Wabbajack record versionとlocal download metadata versionが、
不一致または安全比較不能だったlocal行: 25件。

代表例:

- MOD: `(V3) Oakraven Ammo Press`
- Nexus mod ID: 5105
- local file ID: 41522
- ModInfo.xml version: 3.0.0.0
- MO2 version: 3.1.0.0
- MO2 newestVersion: 3.0.0.0
- download .meta version: 3.0.0.0
- Wabbajack record version: 1.2.0.1
- Wabbajack archive filenameには3.0.0.0が含まれる

このケースでは、Wabbajackのversion値をそのままMOD本体versionの正解とは扱えません。

### 同じMOD IDの別file

同じNexus mod IDですが、local file IDとWabbajack file IDが異なる候補を持つlocal行: 8件。

代表例:

- MOD: 0-SCore
- local file ID: 44553
- Wabbajack候補 file ID: 45359
- Wabbajack archive filename:
  `0-SCore 6176 3.1.22.801 ...`
- Wabbajack record version: 1.2.4.1601

同じMOD IDというだけでは、更新可能とは判定できません。
別file、別addon、別game-version対応物の可能性があります。

### version scheme

観測された分類:

- semantic/numeric version比較可能: 119比較
- 日付version: 2件
- author独自version: 4件
- game version混在: 78件
- source対応付け不確実: 45件

例:

- `1.0` vs `1.0.0.0`
- `23.260413.182941`
- `3.1.9.1528`
- `3.1.1.39`というWabbajack list version
- `39`というCollection revision

これらを単純な文字列比較や数値比較だけで処理するのは危険です。

## 現在の判定状態

dataset row単位:

- Exact: 15
- EquivalentNotation: 41
- ConflictingSources: 22
- NotComparable: 443
- Unresolved: 45

comparison単位:

- Exact: 77
- EquivalentNotation: 42
- NotComparable: 119
- Unresolved: 58
- UpdateAvailable: 今回は確定なし

`latestだが更新日が古い`、
`古いversion表記だが現行game対応`
は、今回の証拠だけでは判定できません。

## 現在採用しているsourceの役割

- Nexus mod ID + file ID:
  MOD identityの強い証拠
- Wabbajack source + hash + size:
  配布artifact identityの強い証拠
- ModInfo.xml:
  local runtimeが宣言するversion
- MO2 meta.ini:
  MO2が取得した時点のversionとcache
- download .meta:
  local archiveとNexus fileの対応
- archive filename:
  補助証拠
- display name、README:
  弱い補助証拠
- Wabbajack list version:
  modlistのversion
- Collection revision:
  Collection snapshotのversion
- game version:
  compatibility用の独立情報

## 現在の状態モデル

comparison_state:

- Exact
- EquivalentNotation
- UpdateAvailable
- ConflictingSources
- NotComparable
- Unresolved

comparison_confidence:

- High
- Medium
- Low

Mediumは表示・manual review用です。
自動更新提案には使いません。

## 自動判定してよい条件

次の条件をすべて満たす場合だけ、自動判定したいです。

1. MOD identityがmodID + fileIDなどで確定している。
2. 比較するversionのroleが同じである。
3. version schemeが安全に比較できる。
4. 同じroleのsourceに矛盾がない。
5. latest fileを一意に特定できる。
6. version orderingの根拠がある。
7. すべての値に証拠sourceがある。

## Unresolvedに落とす条件

次の場合はUnresolvedにしたいです。

- ModInfo.xmlがない。
- packageと複数Modletのsource対応が不明。
- modIDまたはfileIDがない。
- 名前だけで対応付けている。
- 同じMOD IDだがfile IDが異なる。
- Wabbajack list versionとMOD versionを比較している。
- Collection revisionとMOD versionを比較している。
- game versionをrelease versionとして扱っている。
- 日付versionやauthor独自versionの順序を推測している。
- MO2 newestVersionだけでlatestと断定している。
- Nexus latest fileが削除・非表示・複数候補。
- Nexus live sourceを取得できない。
- ModInfo、MO2、Wabbajack、Nexusの同じroleが矛盾している。

## 相談したいこと

以下を評価してください。

1. このsource role分離は十分ですか。
2. installed versionとlatest versionを分離するentity設計は妥当ですか。
3. Wabbajackのrecord versionを、Nexus file versionの補助証拠として扱う方法は妥当ですか。
4. 同じMOD IDでfile IDが違う場合、どの追加証拠があればUpdateAvailableに昇格できますか。
5. `1.0`と`1.0.0.0`をEquivalentNotationにする条件は十分に安全ですか。
6. ModInfo.xml、MO2 meta.ini、download .meta、Wabbajack、Nexusの信頼度をどう重み付けすべきですか。
7. Nexus live取得不能時に、どこまで表示してよく、どこからUnresolvedにすべきですか。
8. 約500 MOD運用で、manual reviewを最小化しながら誤更新通知を防ぐ実装案を提案してください。
9. この設計に不足している観測データや、追加すべき分類があれば指摘してください。

回答では、以下を分けてください。

- 観測事実
- 推測
- 自動判定可能な条件
- manual reviewが必要な条件
- 実装提案
```

参照用の成果物はこちらです。

- [dataset.csv](./dataset.csv)
- [dataset.json](./dataset.json)
- [mismatch-cases.md](./mismatch-cases.md)
- [confidence-model.md](./confidence-model.md)
- [findings.md](./findings.md)

要点を一言でまとめると、今回の調査は「version文字列の比較」よりも、「identity・version role・配布artifact・latest性の分離」が重要だと示していますわ。
