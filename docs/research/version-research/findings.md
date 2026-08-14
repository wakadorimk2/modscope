# Findings

観測日時: `2026-08-14T10:01:09Z`

## 観測事実

- 既存MO2のpackage数: `98`。
- `ModInfo.xml`数: `105`。
- package `meta.ini`数: `87`。
- download `.meta`数: `204`。
- Wabbajack archive record数: `476`。
- Wabbajack file IDまで一致したlocal行: `26`。
- 同じNexus MOD IDだがfile IDが異なるWabbajack候補を持つlocal行: `8`。
- そのpackage数: `8`。
- Wabbajack record versionとlocal download metadata versionが不一致または安全比較不能なlocal行: `25`。
- Smorgasbord list version: `3.1.1.39`。
- Smorgasbord Collection ID: `445130`。
- Smorgasbord Collection revision: `39`。
- Nexus live fetch: `blocked_cloudflare`。
- MO2 cache driftを含む行: `16`。
- ModInfo欠落を含む行: `10`。
- ModInfoがMO2 package versionより低い候補: `18`。

## 推測ではない解釈

- MO2 `newestVersion`は、MO2が過去に取得したcache値です。
- Wabbajack list versionは、MOD本体versionではありません。
- Collection revisionは、Collection snapshotのrevisionです。
- `ModInfo.xml` versionは、local Modletの自己申告値です。
- Nexus live latestが取得できない場合、UpdateAvailableを確定できません。

## ModScopeへの提案

1. MOD identityとversion comparisonを別処理にする。
2. `modID + fileID`、Wabbajack source、archive hashを優先する。
3. package、archive、Modlet、Nexus MOD、Nexus fileを別entityにする。
4. list version、file version、ModInfo version、game versionを別roleにする。
5. `newestVersion`の差だけでは更新通知を出さない。
6. 矛盾は自動修復せず、evidence付きmanual reviewへ送る。
7. Nexus live取得不能を、Nexus versionの推測で埋めない。
8. `Medium` confidenceは表示用に限定し、自動更新処理へ渡さない。

## 予想外だった点

- MO2 cacheのinstalled versionと`newestVersion`が一致しない行があります。
- 同じ数値形式でも、release version、build timestamp、game compatibilityを区別する必要があります。
- Wabbajack CDNの公開download URLは、通常のGETでmanifest本体ではなくSPAを返しました。CDN indexとdefinition/parts endpointを使って取得しました。

## 未解決

- Nexus live latest file/version。
- Wabbajack候補が同じMODの別fileである場合のlatest判定。
- packageと複数Modletのsource対応付け。
- `ModInfo.xml`が欠落するpackageのruntime version。
- 自由形式のauthor versionの順序。
