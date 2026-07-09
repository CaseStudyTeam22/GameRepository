# 実装計画：クリック位置移動 & push/stamina仕様刷新

## 概要

現在のスクロール/方向キーベース of 操作を、**クリック位置を直接指定する操作**に変更する。
あわせてpush・defenseの仕様をスタミナ連動型に刷新し、スタミナを戦略的な意味のあるステータスにする。

**push は移動と押し出しを兼ねるコマンド**となる：
- 自分自身の移動量：クリックで選んだ1〜3マス
- 相手の押し出し量：**相手のスタミナ**に基づく計算

---

## 仕様整理（全変更分）

### 突進（push）の新仕様

| ケース | スタミナへの影響 | 吹き飛ばし距離 |
|---|---|---|
| **自分だけ押す** | 相手スタミナを `-power` | `max(1, 2 + floor((10 - 被弾後スタミナ) / 2))` |
| **互いに突進（ヘッドオン）** | 両者スタミナを `-power` | 吹き飛ばしなし |
| **相手がdefense中** | 相手スタミナを `-max(1, power - 相手currentDefensePower)` | knockback距離 `-2`（最小1） |
| **相手がguardian_skill中** | スタミナ削られず・吹き飛ばしなし（現行維持） | 0 |

### 吹き飛ばし距離の計算式

```
knockbackDist = max(1, 2 + floor((10 - targetStamina) / 2))
```

| 被弾後スタミナ | 吹き飛ばし距離 |
|:---:|:---:|
| 14以上 | 1マス |
| 12 | 1マス |
| 10 | 2マス（基準） |
| 8 | 3マス |
| 6 | 4マス |
| 4 | 5マス |
| 2 | 6マス |
| 0 | 7マス |

### defense（防御）の新仕様

- スタミナ軽減：被ダメ = `max(1, 攻撃側power - 自身currentDefensePower)`
- 吹き飛ばし軽減：knockbackDistが2以上の場合は `-2`（最小距離1）
- guardian_skillは変更なし（ノックバック0・スタミナ削られない）

### 現行仕様から削除/変更されるもの

| 項目 | 旧仕様 | 新仕様 |
|---|---|---|
| 吹き飛ばし距離計算 | `power + basePushPower + pushPowerBonus` | スタミナ基準（上記計算式） |
| pushのattackによるスタミナ減少 | attackコマンドのみ | pushで減少するように変更 |
| defenseの完全無効化 | knockback=0 | knockback軽減(-2)・ダメージ軽減に変更 |
| moveの移動量 | `power + moveSpeed補正` | powerのみ（補正なし）|

---

## 確認事項（解決済み）

| # | 質問 | 回答 |
|---|---|---|
| 1 | 移動ボタンの削除方針 | moveをコメントアウト。pushが移動+押し出しを兼ねる |
| 2 | moveキー入力 of 残存 | コメントアウトするだけでOK |
| 3 | InputActionAsset の所在 | Assets直下にデフォルト名で存在 |
| 4 | GridCursorView のprefab | 既存のSkillPreviewセルprefabを流用 |

---

## 作業分割

### Phase A：サーバーサイド変更（engine.js）

**先行実施可能。クライアント側変更の前にテスト可能。**

#### [MODIFY] [engine.js](file:///c:/project/gamble_action/GameRepository/Assets/StreamingAssets/Server/engine.js)

##### A-1. move の速度ボーナスを削除

```js
// 変更前
const finalPower = Math.max(1, power + baseSpeed + speedBonus);
// 変更後
const finalPower = Math.max(1, Math.min(3, power));
```

##### A-2. push の突進距離（自己移動）からpushPowerボーナスを削除

攻撃者の移動距離は選択したpower(1-3)のみ。pushPowerは吹き飛ばし計算に関与しなくなる。

```js
// 変更前: finalPushDist = power + basePushPower + pushPowerBonus + nextBonus
// 変更後: finalPushDist = Math.max(1, Math.min(3, power))
```

##### A-3. push のスタミナダメージ計算を追加（startDist === 1 のケース）

```js
// 通常push（相手がdefenseなし）
const staminaDmg = power;
target.stamina = Math.max(0, target.stamina - staminaDmg);

// knockback計算（スタミナ基準）
const knockbackDist = Math.max(1, 2 + Math.floor((10 - target.stamina) / 2));
```

##### A-4. defense 時の処理変更

```js
// 変更前: if (tIntent.type === 'defense') finalDist = 0;
// 変更後:
if (tIntent.type === 'defense') {
    const defPower = target.currentDefensePower || 0;
    const staminaDmg = Math.max(1, power - defPower);
    target.stamina = Math.max(0, target.stamina - staminaDmg);
    
    // knockback軽減
    const rawKnockback = Math.max(1, 2 + Math.floor((10 - target.stamina) / 2));
    finalDist = Math.max(1, rawKnockback - 2);
}
```

##### A-5. ヘッドオン（互いに突進）の処理変更

```js
// 両者pushのヘッドオン時
if (pf1 > 0 && pf2 > 0) {
    // 両者スタミナ削減
    p1.stamina = Math.max(0, p1.stamina - i1.power);
    p2.stamina = Math.max(0, p2.stamina - i2.power);
    // 吹き飛ばしなし（元位置に戻す）
    p1.x = p1.prevX; p1.y = p1.prevY;
    p2.x = p2.prevX; p2.y = p2.prevY;
    // 爆発演出イベントは維持
}
```

##### A-6. getPF 関数の扱い

- ヘッドオン判定（どちらもpushか否か）にのみ使用
- 吹き飛ばし距離の計算からは除去

---

### Phase B：クライアントサイド変更

**Phase A 完了後に着手。**

#### B-1. LocalIntentBus 拡張

##### [MODIFY] [LocalIntentBus.cs](file:///c:/project/gamble_action/GameRepository/Assets/_Project/Scripts/Domain/LocalIntentBus.cs)

```csharp
public class LocalIntent
{
    public string Mode { get; set; }
    public string Dir { get; set; }
    public int Power { get; set; } = 1;
    
    // 新規追加
    public int TargetX { get; set; } = -1;   // クリック先グリッドX
    public int TargetY { get; set; } = -1;   // クリック先グリッドY
    public int HoveredX { get; set; } = -1;  // ホバー中グリッドX
    public int HoveredY { get; set; } = -1;  // ホバー中グリッドY
    public bool IsConfirmed { get; set; }    // クリック確定済みか

    public bool IsActive => !string.IsNullOrEmpty(Mode) && Mode != "none";
}
```

`Set()` / `Clear()` メソッドも対応するフィールドを初期化するよう更新。

#### B-2. InputModule 刷新

##### [MODIFY] [InputModule.cs](file:///c:/project/gamble_action/GameRepository/Assets/_Project/Scripts/Input/InputModule.cs)

**削除（コメントアウト）**:
- `HandleWheel()` — スクロールによるpower変更
- `HandlePowerNumberKeys()` — 数字キーによるpower変更
- `HandleMouseMove()` の方向更新ロジック（方向→グリッド座標に置き換え）
- コマンドなしクリック → moveの挙動

**追加・変更**:
- `HandleMouseHover()` — マウス位置からホバーグリッド座標を計算し `LocalIntentBus` に反映
- `HandleMouseClick()` の書き換え — pushモード選択中のみクリック確定を受け付け
- `ResolveTargetCell()` — マウス位置→グリッド座標変換（既存 `TryWorldToGrid` 活用）
- `ClampToReachable()` — プレイヤー位置から十字方向1〜3マスにclamp
- InputAction による確定入力の検知（コントローラー対応）

**グリッド座標→Dir/Power変換**（サーバー送信前に変換）:
```csharp
private (string dir, int power) GridToIntent(int myX, int myY, int targetX, int targetY)
{
    int dx = targetX - myX, dy = targetY - myY;
    if (dx != 0) return (dx > 0 ? "right" : "left", Mathf.Abs(dx));
    if (dy != 0) return (dy > 0 ? "down" : "up",   Mathf.Abs(dy));
    return (null, 1);
}
```

#### B-3. GridCursorView（新規）

##### [NEW] GridCursorView.cs

`SkillPreview` ディレクトリに配置。

**表示内容**:

| 状態 | 表示 |
|---|---|
| pushモード選択中（ホバー前）| 行動可能マス（上下左右各3マス）をうっすらハイライト |
| ホバー中 | ホバーマスをプレイヤーカラーで強調 |
| クリック確定後 | 確定マスを明るく点滅（確定色） |
| コスト不足 | ホバーマスを赤系色で表示 |

**実装方針**:
- `LocalIntentBus.OnChanged` を購読
- `m_FallbackCellPrefab` を既存SkillPreviewから流用
- セルのObjectPoolingは `SkillPreviewView` と同じ構造を踏襲
- コスト不足判定: `me.Chips < pushCostTable[power - 1]`

#### B-4. SkillPreviewView / LineByPowerPattern 修正

##### [MODIFY] [LineByPowerPattern.cs](file:///c:/project/gamble_action/GameRepository/Assets/_Project/Scripts/Gameplay/SkillPreview/LineByPowerPattern.cs)

push モードのプレビューで「相手が吹き飛ばされる先」を表示するため、
相手のスタミナを使ってknockback距離を計算する。

```csharp
// pushプレビューの吹き飛ばし距離
private int CalcKnockback(PlayerDto opponent)
{
    if (opponent == null) return 2;
    int stamina = opponent.Stamina;
    return Mathf.Max(1, 2 + Mathf.FloorToInt((10 - stamina) / 2f));
}
```

`ResolveCells` のシグネチャに `IGameState` を渡すか、`LocalIntent` に相手スタミナ情報を持たせる形で対応。

##### [MODIFY] [SkillPreviewView.cs](file:///c:/project/gamble_action/GameRepository/Assets/_Project/Scripts/Gameplay/SkillPreview/SkillPreviewView.cs)

- pushパターン時に `m_State.Opponent` のスタミナを `pattern.ResolveCells` に渡す
- `LocalIntent.TargetX/Y` からDir/Powerを逆算してプレビューに使用

#### B-5. FlowPanelView 修正

##### [MODIFY] [FlowPanelView.cs](file:///c:/project/gamble_action/GameRepository/Assets/_Project/Scripts/UI/FlowPanelView.cs)

- スクロール関連UIの参照・処理を削除
- pushボタン押下時に GridCursorView の行動可能範囲ハイライトを開始するトリガーを追加（LocalIntentBusを通じて間接的に処理）

---

## 検証計画

### Phase A 検証（サーバー単体）

- push で相手スタミナが正しく減少する
- スタミナ10の相手に push → 2マス吹き飛び
- スタミナ8の相手に push → 3マス吹き飛び
- スタミナ12の相手に push → 1マス吹き飛び
- 互いに push → 両者スタミナ減少・吹き飛ばしなし
- defense中の相手への push → ダメージ軽減・knockback距離-2

### Phase B 検証（クライアント）

- pushボタン押下後、行動可能マス（十字形）がハイライトされる
- ホバーでカーソルが動く
- クリックで確定・Intent送信される
- コスト不足時に赤色フィードバック
- SkillPreviewが相手スタミナに基づいた吹き飛ばし先を表示
