# GameRepository

CaseStudyTeam22「Winner Takes All」（仮称）の Unity 6 クライアント + Node サーバー。

## クイックスタート

### 必要環境
- Unity 6 LTS（URP 3D テンプレート）
- Git LFS
- Node.js（**初回のみ必要**。`node_modules` を取得するために使用）

### 起動方法
1. リポジトリを `git clone`（LFS 必須）
2. **portable Node を配置**：チーム共有ドライブから `node.exe` をダウンロードし、以下のパスに直接置く
   ```
   Assets/StreamingAssets/Server/node.exe
   ```
   - 約 80MB。git で配ると壊れるため共有ドライブ経由で配布しています
   - URL はチーム内で別途共有しています
3. Unity Hub で `GameRepository/` フォルダを開く
4. **初回のみ**：Unity メニュー `Tools → GamblingAction → サーバー依存を導入 (npm install)` を一度実行する
   - `Assets/StreamingAssets/Server/node_modules/` が生成されます（リポジトリには含まれていません）
   - 実行には PC に Node.js がインストールされている必要があります
5. Unity で `Bootstrap.unity` を Play
   - サーバーは Unity 側が自動で起動します（旧 `start_server.bat` の手動起動は不要）
   - 同一 LAN 上に既存ホストがあれば自動でクライアントとして接続します
6. 2 人プレイは Unity の **Multiplayer Play Mode** を使用

### ビルド成果物の配布時の注意

別 PC に渡す前に、Unity メニュー `Tools → GamblingAction → ビルド出力に setup-firewall.bat を配置` を実行してください。フォルダ選択ダイアログでビルド出力（game.exe があるフォルダ）を選ぶと、**game.exe と同じ階層に `setup-firewall.bat` が書き出されます**。

配布先のユーザーには次の手順を伝えてください:
1. `setup-firewall.bat` を右クリック →「管理者として実行」
2. 「設定が完了しました」と表示されたら game.exe を起動

このバッチを実行しなくても、ゲーム初回起動時に Windows Defender のダイアログが表示されたら「アクセスを許可する」を選べば同じ効果になります。バッチは取りこぼし対策です。

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
