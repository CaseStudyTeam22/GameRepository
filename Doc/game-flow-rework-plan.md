# ゲーム Flow 再構築 計画メモ

> このファイルは作業の引き継ぎ用。新しい会話ウィンドウはまずこれを読んでから続行する。
> 対話は中文、ファイル内テキスト（コメント/log/commit/PR）は日本語。

## ブランチ

- 作業ブランチ: `feature/0524/Jou/game-flow-rework`
- 起点: `develop/0511`（PR #17 マージ済み = ステージ登場演出 + キャラ登場ディゾルブが入った最新版、commit `f4fdae5`）
- 工程パス: `C:\Users\hamis\OneDrive\Desktop\casestudy\GameRepository`

## ゴール（このタスク全体）

ゲーム開始フローを再構築し、先後順序を整える。特に「待機ロビー + キャラ選択」を独立させる。
最終的な想定フロー:

```
Title → Lobby（待機 + キャラ選択 + 準備確認）→ ゲーム本体（ステージ生成 → キャラ生成 → 321 → アイテム）→ Result
```

格闘ゲームの待機・準備画面を参考にする。

## 現状診断（重要・調査済み）

- **フェーズ（順序）はサーバ駆動**。`EGamePhase`（`Assets/_Project/Scripts/Domain/GamePhase.cs`）:
  `Lobby → Exchange → BuffSelection → Countdown → Battle → RoundOver → GameOver`。
  クライアントは `GameState.OnPhaseChanged` を受けて対応 UI を出すだけ。順序の「頭脳」はサーバ側。
- **既に Lobby フェーズは存在する**が独立していない。`Assets/_Project/Scripts/UI/FlowPanelView.cs` が
  1 クラスで全部（パネル切替・カウントダウン・ビート表示・タイムバー・プレイヤースロット・buff ボタン・
  結果シーン遷移）を抱えている。今の「簡易 overlay の Lobby」= `m_LobbyPanel` + ReadyButton。
  この密結合が「散らかっていて再構築したい」原因。
- **シーン構成**（Build Settings 順 = マクロな flow）:
  `TitleScene (index 0) → Boot（ゲーム本体）→ ResultScene`。
  ゲーム本体シーン = **`Boot.unity`**。多シーン + シーン遷移は既に採用済みのパターン
  （`FlowPanelView` 末尾に `SceneManager.LoadScene("ResultScene")` あり）。
- 前タスクの成果が使える: `BoardView.GenerateBoard()`（外部トリガ）、`BoardView.OnBoardReady`
  （ステージ完成シグナル）、`PlayerSpawner` は OnBoardReady 後に待ち時間を置いて全キャラ同時生成。

## 方針: 「両方改めるが、段階的に」

ユーザーの意向:「サーバもクライアントも最終的には改める。ただし一歩ずつ」。
独立したロビー専用シーンを新規作成し、シーン遷移で本体へ入る。

### ステップ分け

- **Step 1（今ここ・クライアントのみ）**: 独立 Lobby シーン + シーン遷移の骨組み。
  「準備」ボタン → `SceneManager.LoadScene("Boot")`。まずシーン切替を通す。UI 詳細は後。サーバ未接続。
- **Step 2（クライアント側ネット接続）**: Lobby シーンを既存 `GameState`/ネットに接続。
  「相手の参加待ち + 両者 Ready」を既存 Lobby フェーズで動かす。
- **Step 3（サーバ改修）**: キャラ選択を双方同期させる必要があれば、サーバに `CharacterSelect`
  フェーズ + 同期ロジックを追加（主程・東澄空と協調）。サーバ改修は最後に回す。

各ステップ単体で動作確認できる順序。サーバ改修を最後にして前段が詰まらないようにする。

## 採用アーキテクチャ（このタスクで確定）

「常駐の頭脳 + 各シーン独自 UI」に分離する。

- **常駐の頭脳**: `GameInstaller`（接続 + `GameState` を保持）を `DontDestroyOnLoad` で常駐させ、
  最初に読み込む `Bootstrap` シーンで生成。シーン遷移しても接続と状態は途切れない。
  Wwise 初期化も本来ここに常駐させる対象（hosaka 担当・後で追加）。
- **各シーンの UI**: Lobby の準備画面、Boot の戦闘 HUD 等は各シーンに置き、
  どれも `GameStateLocator.Current` 経由で頭脳を購読する。
- 想定マクロ flow: `Bootstrap → Title → Lobby → Boot → Result`。

## 進捗

### 完了
- ブランチ `feature/0524/Jou/game-flow-rework` を `develop/0511` から作成。
- `SceneLoader.cs`（`GamblingAction.UI`）:
  - `Load()` / `LoadScene(string)`。`m_LoadOnStart`（Start で自動遷移。ボタンの無い Bootstrap 用）を追加。
- `GameInstaller.cs`: `DontDestroyOnLoad` で常駐化。`OnDestroy` は `Instance == this` のときのみ後始末
  （重複実例が自滅するとき、生きている方の状態・接続を壊さないため）。
- `Bootstrap` シーン作成: `GameInstaller.prefab` を配置 + `SceneLoader`（`Scene Name`=`TitleScene`,
  `Load On Start` on）。`Boot` から旧 `GameInstaller` 実例を削除（参照ゼロを確認済み）。
- `LobbyScene` シーン作成。Build Settings:
  `Bootstrap → TitleScene → LobbyScene → Boot → ResultScene`。

### 未完
- **旧 SceneLoader の置換**（重要・進行中）: `Assets/Script/SceneLoader.cs`（namespace 無し）が別に存在。
  これは遷移時に `DontDestroyOnLoad` 対象を全破棄するため、常駐化と衝突する。
  `TitleUI` / `MenuUI` / `ResultUI` / `TernResultUI` の 4 prefab で使用中。
  → 4 prefab を新 `GamblingAction.UI.SceneLoader` に差し替え、旧スクリプトは削除する。
  - 遷移先: TitleUI「開始」= `LobbyScene`（旧 `Boot`）。旧 `MenuScene`（存在しない死リンク）も `LobbyScene` に。
    MenuUI=`TitleScene`、ResultUI=`TitleScene`/`Boot`、TernResultUI=`ResultScene` は維持。
- 動作確認: `Bootstrap` から Play → Title → Lobby → Boot まで
  `GameStateLocator.Current is null` が出ないこと。

## 次の小ステップ
- 旧 SceneLoader 置換が済んだら、本体の準備 UI（AI takeover = ReadyButton + ReadyAsAIToggle）を
  Lobby に持っていく検討。ただし `FlowPanelView` 密結合のため、まず Lobby 用 View へ切り出す要否を判断（Step 2）。

## 注意・約束（プロジェクト規約）
- コメント/log/commit/PR は日本語。事実のみ（比喩・装飾禁止）。Tooltip も同じ。
- URP 3D、紙芝居（ペーパーマリオ）風: 3D 空間 + sprite キャラ。
- DOTween は無料版 API のみ（Pro 限定不可）。
- asmdef 階層あり。Editor 専用コードは Editor asmdef
  （`GamblingAction.Gameplay.Editor` を前タスクで新設済み）。
- `.unity` シーンファイルは手書きせず Unity 上で作成する（手書きは GUID 等で壊れやすい）。
- 破壊的 git 操作・外部公開・サーバ改修は事前確認。commit/push はユーザーが指示したときのみ。
- Wwise の警告（SoundBank subfolder 等）と音が出ない件は別件・hosaka 担当。今回触らない。
