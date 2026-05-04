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
5. 2 人プレイは Unity の **Multiplayer Play Mode** を使用（`Window → Multiplayer → Multiplayer Play Mode`）
   - **手順**：Main Editor を Play → 次に Virtual Player 2 を Play
   - Virtual Player 2 側の AI Toggle を ON にすれば AI 相手として動かせます

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
