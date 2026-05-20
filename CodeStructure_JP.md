# コード構造説明書（GamblingAction Unity クライアント）

このドキュメントは、Unity クライアント側のコード構造をモジュールごとに簡潔にまとめたものです。詳細な仕様は `Doc/`（外部リポジトリ管理）の設計書を参照してください。

---

## 1. 前提

- **Unity バージョン**：Unity 6 LTS（URP 3D テンプレート）
- **アートスタイル**：ペーパーマリオ風（3D 空間 + sprite キャラクター）
- **ネットワーク方針**：NGO は使用せず、既存の Node + Socket.io サーバーをそのまま流用
- **サーバー本体**：`Assets/StreamingAssets/Server/`（リポジトリ root の `start_server.bat` で起動）
- **対戦テスト**：Unity 6 標準の **Multiplayer Play Mode** を使用
  - 初回セットアップ：`Window → Multiplayer → Multiplayer Play Mode` ウィンドウを開き、**Player 2 を有効化**（チェック ON、初回はインスタンス作成に数分かかる）
  - 起動順：Main Editor を Play → 続けて Virtual Player 2 を Play（順番が逆だと P1/P2 の割り当てが入れ替わる）
  - Virtual Player 2 側で Lobby の「AI Toggle」を ON にすれば AI 相手として動かせる
  - 詳しい手順は [`README.md`](./README.md) を参照

---

## 2. ディレクトリ構成

```
Assets/
├── _Project/                  ← 自作コード／アセットはすべてこの下
│   ├── Scenes/
│   │   └── Boot.unity        起動シーン（GameInstaller がモジュールを組み立てる）
│   ├── Scripts/              C# コード本体（asmdef でモジュール分割）
│   │   ├── Core/             純 C#。DTO、定数、無依存
│   │   ├── Net/              Socket.io ラッパー
│   │   ├── Domain/           クライアント側ステートミラー、フェーズ管理
│   │   ├── Gameplay/         ボード／プレイヤー／アイテム表示、HUD、スキルプレビュー
│   │   ├── UI/               フローパネル（Lobby / Exchange / Buff / RoundOver / GameOver / MainStage）
│   │   ├── Input/            入力（マウス方向 + QWER 長押し + ホイール蓄力）
│   │   ├── Audio/            オーディオ（雛形のみ）
│   │   └── Bootstrap/        起動エントリ（GameInstaller / IntegrationTestProbe）
│   ├── Prefabs/              Player / Item / Board / FX / UI の prefab
│   ├── Art/                  アート素材
│   ├── Audio/                サウンド素材
│   └── Settings/             URP / Renderer 設定
├── StreamingAssets/
│   └── Server/               Node サーバー本体（npm install + node server.js）
└── Plugins/                  外部プラグイン（DOTween、SocketIOUnity など）
```

---

## 3. asmdef モジュール依存関係

```
Core    ←  Net  ←  Domain  ←  Gameplay
                          ←  UI
                          ←  Input
                          ←  Audio
                                    ↑
                              Bootstrap（全部に依存して組み立て）
```

**重要なルール**：
- `Gameplay` / `UI` / `Input` / `Audio` は `Net` を直接参照してはいけません
- ネットワーク → ステート → 表現 のデータフローを保つため、すべて `Domain` の `IGameState` 経由で通信します
- 違反すると asmdef のコンパイルエラーになります（バグではなく仕様）

---

## 4. モジュール別の責務

### Core（依存なし）
- **DTO**：`PlayerDto` / `ItemDto` / `EventDto` / `IntentDto` / クライアント・サーバーメッセージ
  - `Newtonsoft.Json` の `[JsonProperty("camelName")]` で camelCase ↔ PascalCase をマッピング
- **定数**：`GameConfig`（GridSize, BeatIntervalMs, GameDurationSec, etc.）
- **イベント名／コマンド名**：`ServerEvents` / `ClientEvents` / `IntentTypes` / `Directions` / `BuffIds` などすべて static class で集約。文字列を直書きしない
- **Skills**：`SkillDefinition`（ScriptableObject）と `SkillPatternRegistry`（プレビュー描画用）

### Net（Core に依存）
- `INetClient`：Socket.io 抽象インターフェース（Connect / Emit / On / Off）
- `SocketIONetClient`：SocketIOUnity ベースの実装。バックグラウンドスレッドの受信を Unity メインスレッドへ転送

### Domain（Core + Net に依存）
- `IGameState` / `GameState`：プレイヤー、アイテム、現在の拍、フェーズなどのクライアント側ミラー
  - サーバーイベントを購読してミラーを更新し、C# `event` で Gameplay / UI に再ブロードキャスト
- `GamePhase`（enum）：`Lobby / Exchange / BuffSelection / Countdown / Battle / RoundOver / GameOver`
- `GameStateLocator` / `BoardCoordsLocator`：静的ロケーター。`Bootstrap` が Set し、各モジュールが `.Current` で参照
- `LocalIntentBus`：ローカル意図（プレビュー用）の一時バッファ。`Input` が Set し、`SkillPreview` が読む
- `IBoardCoords`：グリッド座標 ↔ ワールド座標の変換インターフェース。実装は `Gameplay/BoardView`

### Gameplay（Core + Domain に依存。Net には依存しない）
- `BoardView`：8×8 のタイル生成、座標変換、`IBoardCoords` 実装
- `PlayerSpawner` / `PlayerView`：プレイヤー prefab の生成と移動補間（DOTween）、ペーパーマリオ風 billboard
- `ItemSpawner` / `ItemView`：チップ／お金アイテムの生成と表示
- `PlayerHudView` / `StaminaBarView`：プレイヤー頭上の HUD（World Space Canvas）
- `TileVisualAlign` / `WhiteSprite`：補助表示
- `SkillPreview/`：QWER 押下時の効果範囲プレビュー描画

### UI（Core + Domain に依存。Net には依存しない）
- `FlowPanelView`：ゲームフロー全体の UI コントローラ
  - `OnPhaseChanged` を購読して、フェーズに応じて Lobby/Exchange/Buff/RoundOver/GameOver/MainGameStage の表示を切り替える
  - MainGameStage 内では `OnPlayersChanged` でプレイヤー名／所持金／チップを更新
  - `OnBeatChanged` でメトロノーム灯（NormalBeat 1-3 + FinalBeat）と TimeBar（中心から両端へ縮む）を駆動
  - `Phase == Countdown` の時に READY → 3 → 2 → 1 → GO のシーケンスを再生
  - `Phase == Battle` 突入時に `Round X/3` を更新（クライアント側でローカルカウント、将来サーバーから配信予定）
- `SceneLoader`：シーン切り替えユーティリティ

### Input（Core + Domain に依存。Net には依存しない）
- `InputModule`：マウス方向 + QWER 長押しモード + ホイール蓄力（power 1/2/3）
  - 入力を `IGameState.SubmitIntent()` 経由でサーバーへ送信
  - `LocalIntentBus` にも書き込み、`SkillPreview` がプレビューを表示

### Bootstrap（全モジュールに依存）
- `GameInstaller`：起動シーンで `Net → Domain` の順に new し、Locator に登録
- `IntegrationTestProbe`：起動疎通テスト用の小さな probe

---

## 5. プロトコル

サーバー ↔ クライアント間のメッセージ仕様は `Doc/Protocol.md`（外部管理）で凍結されています。
イベント名／フィールド名を変更する場合は **サーバー（`engine.js` / `server.js`）+ Unity（`Core/Dto/`）+ Protocol.md** の 3 箇所を必ず同期してください。

---

## 6. 開発時の注意

### コーディングスタイル
- リポジトリの `.editorconfig` に従う
- インデントは Tab、改行は LF、エンコードは UTF-8 BOM
- 命名規則は `.editorconfig` 末尾の表（クラス／インターフェース／enum／フィールドの prefix ルール）を参照
  - **注意**：現在 `Assets/_Project/Scripts/` 配下のコードは `.editorconfig` の `[Assets/Code/**/*.cs]` セクションの対象外です。命名規則の段階的な適用は別 PR で進める予定（未対応箇所が残っています）

### Git ワークフロー
- `main` / `develop` は管理者（東澄空さん）のみが扱う
- 機能追加は `feature/日付/上の名前/タスク名` のブランチを切り、PR で `develop` にマージ
- 例：`feature/0504/jou/initial-import`

### サーバー起動
1. リポジトリ root の `start_server.bat` をダブルクリック
2. 初回は `npm install` が走るので少し時間がかかります
3. `[GamblingAction Server] Running on port 3000...` と表示されれば OK
4. 終了は対象コマンドプロンプトを閉じる、または Ctrl+C

### 既知の制約（開発期のみの暫定対応）
- **Socket.io の `pingTimeout` を 5 分（300000ms）に設定**しています（`server.js`）
  - 理由：Multiplayer Play Mode の Virtual Player がフォーカスを失うと Unity がメインスレッドを停止し、Socket.io のハートビートが止まって踏み越えてキックされる
  - **正式リリース前に必ず 20000ms に戻し、クライアント側で自動再接続を実装してください**

---

## 7. 連絡先

不明点は実装担当者まで Slack / Discord で連絡してください。
