# Version mismatch cases

観測日時: `2026-08-14T10:01:09Z`

この文書は、収集したデータから機械的に件数を計算した結果です。推測は観測事実と分離します。

## 状態件数

| state | 件数 |
|---|---:|
| `ConflictingSources` | 22 |
| `EquivalentNotation` | 41 |
| `Exact` | 15 |
| `NotComparable` | 443 |
| `Unresolved` | 45 |

## 比較件数

| comparison state | 件数 |
|---|---:|
| `EquivalentNotation` | 42 |
| `Exact` | 77 |
| `NotComparable` | 119 |
| `Unresolved` | 58 |

## 理由コード件数

| reason code | 件数 | 代表行 |
|---|---:|---|
| `different_version_scheme` | 5 | `00156d8a7fbfe69d`: Cre_More Weapon Mods |
| `list_mod_version_mixed` | 105 | `d62ac3b297780be8`: (TMO) Performance PLUS v2.2_v2.5ST |
| `local_installed_version_unresolved` | 10 | `35106571437e07d1`: Contents_separator |
| `missing_comparable_version_pair` | 37 | `35106571437e07d1`: Contents_separator |
| `mo2_cache_drift` | 16 | `5806a601fcc1ad2b`: (V3) Oakraven Ammo Press |
| `modinfo_missing` | 10 | `35106571437e07d1`: Contents_separator |
| `modinfo_stale_candidate` | 18 | `d62ac3b297780be8`: (TMO) Performance PLUS v2.2_v2.5ST |
| `modinfo_vs_mo2_mismatch` | 22 | `d62ac3b297780be8`: (TMO) Performance PLUS v2.2_v2.5ST |
| `nexus_live_unavailable` | 467 | `5806a601fcc1ad2b`: (V3) Oakraven Ammo Press |
| `nexus_version_unverified` | 521 | `d62ac3b297780be8`: (TMO) Performance PLUS v2.2_v2.5ST |
| `no_safe_order` | 17 | `d62ac3b297780be8`: (TMO) Performance PLUS v2.2_v2.5ST |
| `source_binding_uncertain` | 45 | `35106571437e07d1`: Contents_separator |
| `wj_only_no_local_modinfo` | 451 | `ee5b892c10815bfb`: Mod.Organizer-2.5.2.7z |
| `wj_record_version_mismatch` | 25 | `5806a601fcc1ad2b`: (V3) Oakraven Ammo Press |
| `wj_same_mod_different_file` | 8 | `a7a8882d6cb2b86e`: 0-SCore |

## 指定カテゴリの確認

| カテゴリ | 件数 | 判定方法 |
|---|---:|---|
| 完全一致 | 77 | 機械集計 |
| 表記揺れ | 42 | 機械集計 |
| semantic/numeric version比較可能 | 119 | 機械集計 |
| 日付version | 2 | 機械集計 |
| author独自version | 4 | 機械集計 |
| game version混在 | 78 | 機械集計 |
| version体系の異なる配布物 | 25 | 機械集計 |
| 同じNexus MODの別file候補 | 8 | 機械集計 |
| ModInfo古い候補 | 18 | 機械集計 |
| ModInfo欠落 | 10 | 機械集計 |
| MO2 cacheとNexus不一致 | 16 | 機械集計 |
| Nexus version信頼性不足 | 521 | 機械集計 |
| source対応付け不確実 | 45 | 機械集計 |
| latestだが更新日が古い | 0 | 機械集計 |
| 古いversion表記だが現行game対応 | 0 | 機械集計 |
| list versionとMOD versionの混在 | 105 | 機械集計 |

## 代表比較

| パターン | package | left | right | state |
|---|---|---|---|---|
| 完全一致 | (TMO) Performance PLUS v2.2_v2.5ST | `2.4.0.0` | `2.4.0.0` | `Exact` |
| 表記揺れ | 2X or 1.5X Faster Vehicle Speeds - Bicycle Minibike Motorcycle Truck Gyrocopter - V1.0 to V2.5 | `1.0` | `1.0.0.0` | `EquivalentNotation` |
| MO2 cache差分 | (V3) Oakraven Ammo Press | `3.1.0.0` | `3.0.0.0` | `Unresolved` |
| Wabbajack record version差分 | (V3) Oakraven Ammo Press | `3.0.0.0` | `1.2.0.1` | `Unresolved` |
| list/MOD系列混在 | (TMO) Performance PLUS v2.2_v2.5ST | `3.1.1.39` | `2.2` | `NotComparable` |

`latestだが更新日が古い`と`古いversion表記だが現行game対応`は、今回の証拠だけでは判定できないため`0`です。0は不存在の証明ではありません。

## 実データの代表例

- local package: `98`
- local ModInfo.xml: `105`
- MO2 package meta.ini: `87`
- download .meta: `204`
- Wabbajack archive records: `476`
- Nexus live fetch: `blocked_cloudflare`

Wabbajack list version、Collection revision、Nexus MOD versionは別系列です。数値が似ていても、同じversionとは判定しません。

## 注意

Nexus live pageはCloudflareのJavaScript challengeで取得できませんでした。MO2の`newestVersion`はcache値として保存しました。Nexus latest file/versionの現行値は未取得です。
