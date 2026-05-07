# GameRepository

CaseStudyTeam22「Winner Takes All」（仮称）の Unity 6 クライアント + Node サーバー。

## クイックスタート

### 必要環境
- Unity 6 LTS（URP 3D テンプレート）
- Node.js（サーバー起動用）
- Git LFS

### 起動方法
1. リポジトリを `git clone`（LFS 必須）
2. Unity Hub で `GameRepository/` フォルダを開く
3. 別途、**サーバーを起動**：リポジトリ root の `start_server.bat` をダブルクリック
   - 初回は `npm install` が走ります
   - `[GamblingAction Server] Running on port 3000...` の表示で起動完了
4. Unity で `Boot.unity` を Play
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
