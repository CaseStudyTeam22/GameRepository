# 命名規則移行ドキュメント（feature/0504/jou/naming-conventions）

`.editorconfig` のルール対象 path を `Assets/_Project/Scripts/**/*.cs` に変更したことに伴い、
当該 path 配下のコードを命名規則に合わせて全面リネームしました。

このドキュメントは：
- **何を変えたか**（旧 → 新の全リスト）
- **何を変えなかったか**（DTO の例外）
- **Editor 側で手動でやらなければいけないこと**（SerializeField 参照の確認）

をまとめたものです。

---

## 1. 変えたもの

### 1.1 enum（接頭辞 `E` を付加）

| 旧 | 新 | 場所 |
|---|---|---|
| `GamePhase` | `EGamePhase` | `Domain/GamePhase.cs` |
| `SkillPatternType` | `ESkillPatternType` | `Core/Skills/SkillDefinition.cs` |

参照側（GameState、PlayerView、FlowPanelView、SkillPreviewView、SkillPatternRegistry など全箇所）も追従しました。
**enum メンバー名は PascalCase なので変更なし**（`Lobby`、`LineByPower` など）。

### 1.2 フィールド（接頭辞 `m_` を付加 + PascalCase）

非 DTO の **すべての非 public フィールドおよび `[SerializeField]` フィールド** を `m_` プレフィックス + PascalCase に統一しました。

主な例（全部ではありません）：

| ファイル | 旧 | 新 |
|---|---|---|
| `Bootstrap/GameInstaller.cs` | `_net` | `m_Net` |
| `Bootstrap/GameInstaller.cs` | `_state` | `m_State` |
| `Bootstrap/GameInstaller.cs` | `serverUrl` | `m_ServerUrl` |
| `Bootstrap/GameInstaller.cs` | `verboseProbeLogs` | `m_VerboseProbeLogs` |
| `Bootstrap/GameInstaller.cs` | `autoReady` | `m_AutoReady` |
| `Bootstrap/GameInstaller.cs` | `autoReadyAsAI` | `m_AutoReadyAsAI` |
| `Bootstrap/GameInstaller.cs` | `autoExchange` | `m_AutoExchange` |
| `Bootstrap/GameInstaller.cs` | `autoExchangeAmount` | `m_AutoExchangeAmount` |
| `Bootstrap/GameInstaller.cs` | `autoBuff` | `m_AutoBuff` |
| `Bootstrap/GameInstaller.cs` | `autoBuffId` | `m_AutoBuffId` |
| `Bootstrap/IntegrationTestProbe.cs` | `serverUrl` / `_net` / `_state` | `m_ServerUrl` / `m_Net` / `m_State` |
| `Domain/GameState.cs` | `_net` / `_players` / `_items` | `m_Net` / `m_Players` / `m_Items` |
| `Net/SocketIONetClient.cs` | `_socket` / `_handlers` | `m_Socket` / `m_Handlers` |
| `Input/InputModule.cs` | `worldCamera` / `_state` / `_board` / `_groundPlane` / `_activeSkillKey` / `_activeMode` / `_lastSentDir` / `_power` | `m_WorldCamera` / `m_State` / `m_Board` / `m_GroundPlane` / `m_ActiveSkillKey` / `m_ActiveMode` / `m_LastSentDir` / `m_Power` |
| `Gameplay/BoardView.cs` | `tileSize` / `tilePrefabLight` / `tilePrefabDark` / `generateTilesOnAwake` | `m_TileSize` / `m_TilePrefabLight` / `m_TilePrefabDark` / `m_GenerateTilesOnAwake` |
| `Gameplay/PlayerView.cs` | `sprite` / `baseRenderer` / `billboardTarget` / `hud` / `skillSet` / `moveDuration` / `moveEase` / `gravity` / `fallStopY` / `fallKickoffDuration` + 内部フィールド多数 | 全部 `m_Xxx` |
| `Gameplay/PlayerSpawner.cs` | `playerPrefab` / `board` / `_views` / `_state` | `m_PlayerPrefab` / `m_Board` / `m_Views` / `m_State` |
| `Gameplay/PlayerHudView.cs` | `staminaBar` / `billboardToCamera` / `_cam` | `m_StaminaBar` / `m_BillboardToCamera` / `m_Cam` |
| `Gameplay/StaminaBarView.cs` | `cellTemplate` / `cellsRoot` / `healthyColor` / `lowColor` / `emptyColor` / `lowThreshold` / `_cells` / `_lastStamina` | 全部 `m_Xxx` |
| `Gameplay/ItemSpawner.cs` | `itemPrefab` / `_views` / `_state` / `_board` | `m_ItemPrefab` / `m_Views` / `m_State` / `m_Board` |
| `Gameplay/ItemView.cs` | `sprite` / `chipsColor` / `moneyColor` / `billboardTarget` / `bobAmplitude` / `bobSpeed` + 内部フィールド | 全部 `m_Xxx` |
| `Gameplay/SkillPreview/SkillPreviewView.cs` | `fallbackSkillSet` / `fallbackCellPrefab` / `yOffset` / `opacity` / `cellTileFraction` / `powerAlphaScale` + 内部フィールド | 全部 `m_Xxx` |
| `Gameplay/SkillPreview/SkillPatternRegistry.cs` | `_map` | `s_Map`（static フィールドなので s_ プレフィックス） |
| `Gameplay/WhiteSprite.cs` | `_cached` | `s_Cached`（static） |
| `UI/FlowPanelView.cs` | `lobbyPanel` / `exchangePanel` / `buffPanel` / `roundOverPanel` / `gameOverPanel` / `mainGameStage` / `totalRounds` / `executeFlashSeconds` / 各色 / 内部フィールド多数 | 全部 `m_Xxx` |

**注意**：すべての `[SerializeField]` フィールドには **`[FormerlySerializedAs("旧名")]`** を追加しました。これにより既存の prefab / scene / ScriptableObject の参照値はリネーム後も保持されます。

### 1.3 `Core/Skills/SkillDefinition.cs` の特殊事情

`public List<SkillEntry> skills` は public フィールドでしたが、命名規則順守のため `private List<SkillEntry> m_Skills` + `public IReadOnlyList<SkillEntry> Skills` に変更しました。

`SkillEntry` クラスの `public string skillType` 等も同様に `private m_SkillType` + 読み取り専用プロパティ `SkillType` に変更しています。
このため、**pattern 系（`SkillPatternRegistry` / `SkillPreviewView`）のコードも `entry.skillType` → `entry.SkillType` のように追従修正済み**です。

### 1.4 アクセス修飾子

`.editorconfig` の `dotnet_style_require_accessibility_modifiers = error` ルールに従い、メソッドおよびフィールドに **`private` を明示的に追加**しました。

### 1.5 `LocalIntent`（プロパティ化）

`Mode` / `Dir` / `Power` は public フィールドだったものを **public プロパティ**に変更しました（既存の `LocalIntentBus.Current.Mode = ...` のような書き込みも互換）。

---

## 2. 変えなかったもの（重要）

### 2.1 DTO クラス（`Assets/_Project/Scripts/Core/Dto/**/*.cs`）

JSON 通信用の契約クラスは **PascalCase の public フィールドのまま** 維持しています：
- `PlayerDto.Id` / `Role` / `X` / `Y` / `Chips` / `Stamina` / etc.
- `ItemDto.Id` / `Type` / `X` / `Y`
- `EventDto.Type` / `X` / `Y` / etc.
- `*Message` 系（`InitMessage`、`SyncStateMessage`、`BeatMessage`、`SetIntentMessage`、etc.）
- 文字列定数の `static class`（`ServerEvents` / `ClientEvents` / `IntentTypes` / `Directions` / `Roles` / `BuffIds` / `EventTypes` / `VfxTypes`）

**理由**：
- DTO はサーバー（Node + Socket.io）との通信契約クラスであり、`Newtonsoft.Json` の `[JsonProperty("camelName")]` で JSON 名（camelCase）と C# 名（PascalCase）をマッピングしている
- `m_` プレフィックスを付けると参照側のコード（30+ 箇所）が大量に書き換えになり、可読性が低下する
- DTO の慣習として `m_` は使わないのが業界標準

**対応**：`.editorconfig` で `Assets/_Project/Scripts/Core/Dto/**` を命名ルールから除外する豁免セクションを追加しました。

### 2.2 interface

すでに `I` プレフィックスのため変更なし：`IGameState` / `INetClient` / `IBoardCoords` / `ISkillPattern`。

### 2.3 const フィールド（`GameConfig.cs`）

`public const int GridSize = 8;` のような const は伝統的に PascalCase で、`m_` を付けると `m_GridSize` となり可読性が悪い。
const は static class にしか存在せず `[SerializeField]` ではないため、Inspector や prefab に影響なし。
**今回は変更なし**としました。レビューで「const も m_ にすべき」となれば追加対応します。

### 2.4 enum メンバー / プロパティ / メソッド / メソッド引数

すべて PascalCase / camelCase で既に合規のため変更なし。

---

## 3. Editor 側で手動でやらなければいけないこと

`[FormerlySerializedAs]` を全 SerializeField に追加したため、**ほとんどのケースで参照値は自動で引き継がれます**。
ただし以下を順番に確認してください：

### 3.1 Unity Editor を起動して reimport

1. Unity Hub から `GameRepository` を開く
2. Console を開く（`Window → General → Console`）
3. プロジェクトロード時にコンパイルが走るので、**赤エラーが 0 になるまで待つ**
4. もしエラーがあれば、その内容を確認（命名規則違反は IDE 側のエラーなので Unity Console には出ない可能性あり；Rider / VS で開いて確認）

### 3.2 `Boot` シーンを開いて Inspector を一通りチェック

`Assets/_Project/Scenes/Boot.unity` を開いて、以下のオブジェクトの Inspector に `Missing` 表示がないことを確認：

| GameObject | コンポーネント | 確認するフィールド |
|---|---|---|
| `GameInstaller` | `GameInstaller` | Server Url、Verbose Probe Logs、Auto Ready、Auto Ready As AI、Auto Exchange、Auto Exchange Amount、Auto Buff、Auto Buff Id |
| `Board` | `BoardView` | Tile Size、Tile Prefab Light、Tile Prefab Dark、Generate Tiles On Awake |
| `PlayerSpawner` | `PlayerSpawner` | Player Prefab、Board |
| `ItemSpawner` | `ItemSpawner` | Item Prefab |
| `InputModule` | `InputModule` | World Camera |
| `SkillPreview` | `SkillPreviewView` | Fallback Skill Set、Fallback Cell Prefab、Y Offset、Opacity、Cell Tile Fraction、Power Alpha Scale |
| `Canvas/FlowPanel` | `FlowPanelView` | Lobby Panel、Exchange Panel、Buff Panel、Round Over Panel、Game Over Panel、Main Game Stage、Total Rounds、Execute Flash Seconds、Beat On Color、Final Beat On Color、Beat Off Color |

**Inspector のフィールド名表示について**：Unity は `m_FieldName` を自動的に `Field Name`（`m_` プレフィックスを除去 + 単語間にスペース挿入）として表示します。なので Inspector 上は **`Server Url`、`Tile Size`、`Item Prefab`** のように見えます（`M` は付かない）。これは Unity 標準の挙動で正常です。値が消えていれば手動でドラッグして再設定してください。

### 3.3 prefab を一通り開く

以下の prefab を開いて、ルートおよび子オブジェクトの Inspector を確認：

| Prefab | 確認 |
|---|---|
| `_Project/Prefabs/Player/Player.prefab` | `PlayerView` の Sprite、Base Renderer、Billboard Target、Hud、Skill Set、Move Duration、Move Ease、Gravity、Fall Stop Y、Fall Kickoff Duration |
| `_Project/Prefabs/UI/Hud/PlayerHud.prefab` | `PlayerHudView` の Stamina Bar、Billboard To Camera 。`StaminaBarView` の Cell Template、Cells Root、Healthy Color、Low Color、Empty Color、Low Threshold |
| `_Project/Prefabs/Item/Item.prefab` | `ItemView` の Sprite、Chips Color、Money Color、Billboard Target、Bob Amplitude、Bob Speed |
| `_Project/Prefabs/UI/Menu/MainGamCanvas.prefab` | （MainGameUI を内包する場合）`FlowPanelView` の参照 |
| `_Project/Prefabs/Board/TileLight.prefab` / `TileDark.prefab` | フィールド変更なし（`TileVisualAlign` のみ、フィールドなし） |

### 3.4 ScriptableObject の確認

`Assets/_Project/Skills/DefaultSkillSet.asset` を選択して Inspector を確認：
- `Skills` リストの中身（`SkillEntry` 要素）が消えていないか
- 各 entry の `Skill Type` / `Pattern Type` / `Cell Prefab Override` / `Tint Override` の値が保持されているか

`[FormerlySerializedAs]` のおかげで自動で旧 yaml フィールド名（`skills` / `skillType` / etc.）から新フィールド（`m_Skills` / `m_SkillType` / etc.）にマイグレーションされるはずです。
**もし値が消えていた場合**：手動で再入力（保険として元の `.asset` ファイルの yaml を `git diff` で参照可能）。

### 3.5 動作確認

最後に：

1. `start_server.bat` を起動
2. Unity の `Boot` シーンを Play
3. Multiplayer Play Mode で Virtual Player 2 を起動（AI Toggle ON）
4. 1 ラウンド完走（Lobby → Exchange → Buff → Countdown → Battle → RoundOver）

正常に動けば移行完了。

---

## 4. もし問題が発生したら

### 4.1 prefab 参照が完全に消えた
`[FormerlySerializedAs]` が効いていないケース。手動で Inspector に再ドラッグ。

### 4.2 コンパイルエラー
- `GamePhase` → `EGamePhase` への参照漏れ：grep で `\bGamePhase\b` を全文検索（このドキュメント書いた時点では 0 件のはず）
- `SkillPatternType` → `ESkillPatternType` への参照漏れ：同上
- `entry.skillType` のような旧 public フィールド参照：今回 SkillEntry のフィールドはプロパティ化されたので、`entry.SkillType`（PascalCase）に修正

### 4.3 .editorconfig のエラーが大量に出る
正常です。これが **「ルールが効くようになった証拠」**です。
ただし `Assets/_Project/Scripts/Core/Dto/**` 配下の DTO はエラーが出ないはず（豁免セクションを追加済み）。

---

## 5. 連絡

このリネームで疑問・問題があれば PR コメント、または Discord で連絡してください。
