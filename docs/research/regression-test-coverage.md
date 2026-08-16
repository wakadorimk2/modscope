# ModScope Local Knowledge 回帰テストの保証範囲

## 目的

この文書は、UIを対象外とした主要Local Knowledge flowの回帰保証を記録します。
対象flowは、MO2 source discovery、metadata extraction、version detection、source URL handlingです。

fixtureは、`tests/Fixtures/regression/7dtd-mo2-provenance/`にあります。
fixtureのID、URL、MOD名、versionは匿名synthetic valueです。
実際のarchive、credential、個人pathは含みません。

## Verified

| Flow | 現在テストで確認すること |
| --- | --- |
| MO2 source discovery | 明示したinstance rootの`ModOrganizer.ini`を読み、7DTDのDefault profile、mods path、profiles pathをcandidateへ投影する。 |
| Metadata extraction | profile entry、enabled state、`ModInfo.xml`のknown field、unknown observation、`meta.ini`のnumeric modID/fileID、version、URL、unknown keyを保持する。 |
| Package relation | 1 packageと1 Modletをexact identityとして扱う。1 packageと複数Modletの関係はpackage scopeで保持する。 |
| Version detection | `ModInfo.xml`、MO2 `meta.ini`、evidence manifest、Nexus APIを別observationとして保持する。prefix付きSemVerを正規化する。 |
| Version assessment | identityがexactでない場合は`NotAssessed`とする。値またはschemeが比較できない場合は`NotComparable`とする。 |
| Source URL | manifest `sourceUrl`を保持する。numeric modID/fileIDからderived Nexus File URLを生成する。`ModInfo.Website`のhost/path matchではqueryとfragmentを無視する。 |
| Query projection | candidateとInspectorへpackage relation、source artifact、version observation、comparison、diagnosticを投影する。absolute pathとraw metadataを投影しない。 |

## Not guaranteed

- live Nexusのlatest versionを取得できることは保証しません。
- `meta.ini`の`newestVersion`だけでupdate availableを判定しません。
- Wabbajack list version、ModInfo version、Nexus File version、game versionを同じversion seriesとは扱いません。
- version mismatchはruntime incompatibilityを意味しません。
- package-level source identityを、複数ModletそれぞれのNexus File identityへ分配しません。
- `meta.ini`の`url`はraw metadataとして保持しますが、現在のderived artifact URLの根拠には使いません。
- URLのauthority、所有者、redirect先、live pageの正当性は検証しません。
- arbitrary website、任意のversion表記、archive binaryの内容は対象外です。
- Windows Registry、実行中MO2、reparse point、権限エラーを実環境で保証しません。
- 7DTDのruntime compatibility、dependency、save safety、complete semantic conflictを保証しません。

## Diagnostic boundary

- metadataがない場合は、identityを推測せず`Missing`または`Unresolved`として保持します。
- numeric IDが不正な場合は、Nexus File identityを生成しません。
- ModInfo versionとMO2 versionが異なる場合は、local version conflict diagnosticを保持します。
- Nexus APIの応答が不正、欠落、別fileの場合は、versionを採用せずdiagnosticを返します。
- test fixtureのmanifestはtest inputです。productionの外部source claimを意味しません。

## Verification command

LocalKnowledge testsとQuery testsを同時に実行しません。
共有`obj`出力の競合を避けるため、次の順序で実行します。

```powershell
dotnet test tests\ModScope.LocalKnowledge.Tests\ModScope.LocalKnowledge.Tests.csproj -p:UseSharedCompilation=false
dotnet test tests\ModScope.Query.Tests\ModScope.Query.Tests.csproj -p:UseSharedCompilation=false
```

UI build、Desktop test、Deployment testは、このcoverageのacceptance対象外です。
