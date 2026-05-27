# GameRepository

CaseStudyTeam22「Winner Takes All」（仮称）の Unity 6 クライアント + Node サーバー。

## クイックスタート

### 必要環境
- Unity 6 LTS（URP 3D テンプレート）
- Node.js（開発時のサーバー起動用。ビルド成果物には同梱 portable Node を使うため不要）
- Git LFS
- Windows PowerShell（portable Node 取得スクリプトの実行用）

### 起動方法
1. リポジトリを `git clone`（LFS 必須）
2. **portable Node を取得**：リポジトリ root の一つ上にある `Tools/download-node.ps1` を一度だけ実行する
   ```powershell
   powershell -ExecutionPolicy Bypass -File Tools\download-node.ps1
   ```
   - `Assets/StreamingAssets/Server/node.exe`（約 80MB）が配置されます
   - リポジトリには含まれていないため、**clone 後と再取得時のみ**実行が必要です
   - 配置済みの場合は何もせず終了します
3. Unity Hub で `GameRepository/` フォルダを開く
4. Unity で `Bootstrap.unity` を Play
   - サーバーは Unity 側が自動で起動します（旧 `start_server.bat` の手動起動は不要）
   - 同一 LAN 上に既存ホストがあれば自動でクライアントとして接続します
5. 2 人プレイは Unity の **Multiplayer Play Mode** を使用

#### Multiplayer Play Mode の初回セットアップ

1. メニュー `Window → Multiplayer → Multiplayer Play Mode` でウィンドウを開く
2. ウィンドウ内に「Player 2 / Player 3 / Player 4」のチェックボックスがあるので、**Player 2 を有効化（チェック ON）**
   - 有効化すると Virtual Player 2 のインスタンスが作成されます（初回は数分かかります）
3. ウィンドウは閉じても OK（設定は保存されます）

#### 対戦テスト手順

1. **Main Editor で Play** → P1（青色）として接続
2. 続けて **Virtual Player 2 のウィンドウで Play** → P2（赤色）として接続
   - **必ずこの順番**（Main → Virtual Player 2）。逆だと P1/P2 の割り当てが入れ替わります
3. Virtual Player 2 側で AI 相手として動かしたい場合は、Lobby 画面の **AI Toggle を ON にしてから Ready** ボタンを押す

## ドキュメント

- **コード構造**：[`CodeStructure_JP.md`](./CodeStructure_JP.md) — モジュール構成、asmdef 依存関係、各モジュールの責務
- **コーディング規約**：[`.editorconfig`](./.editorconfig) — 命名規則、インデント、改行、エンコード
- **AI 用指示**：[`.github/copilot-instructions.md`](./.github/copilot-instructions.md)

## ブランチ運用

| ブランチ | 役割 | 管理者 |
|---|---|---|
| `main` | 完成系 | 東澄空 |
| `develop` | 開発主線 | 東澄空 |
| `feature/日付/上の名前/タスク名` | 機能追加 | メンバー全員 |
| `release/日付` | リリース前テスト | 東澄空 |

## チーム
CaseStudyTeam22
