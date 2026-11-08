# 体の回転（Body Rotation）

ポーズを選んだ状態で、体全体（アーマチュア）を 3 軸まわりに回転させる機能です。傾いた面などにポーズを合わせるときに、クリップを作り直さなくても調整できます。

## 概要

- 3 つのラジアルパペット（Tilt X / Tilt Y / Tilt Z）と Reset Rotation で操作します。
- ライブラリごとに **Enable Body Rotation** で有効化します（**Enable Locomotion Animator** が必要）。
- 回転はポーズへ焼き込まず、専用のリグ（Transform の鎖）を Animator で回し、Armature を制約で追従させます。
- **ポーズ未選択の間は回転・高さともに無効**です（`AnimPosePlay` ゲート）。

## 仕組み

アバタールート直下に「回すだけの」Transform の鎖を作ります。Armature はリペアレントせず、制約で追従します。

```text
AvatarRoot
├─ Armature                          （位置も親子関係も変えない）
└─ AnimPoseBodyRotateRig             （Hips のワールド位置が支点）
   └─ Height  (localPosition.y)      ← 高さスライダー（回転の外側）
      └─ Y     (localRotation.y)     ← Tilt Y
         └─ X  (localRotation.x)     ← Tilt X
            └─ Z (localRotation.z)   ← Tilt Z
               └─ Offset             （アーマチュア位置。鎖と一緒に回る）
```

- 軸の順番は **Y → X → Z**。合成結果が `Quaternion.Euler(x, y, z)` になり、従来の焼き込み方式と同じ挙動になります。
- クリップは **3 軸 × Steps 本**だけ（軸ごとに独立した Transform、全ポーズ共通）。ポーズごとの `Steps³` 爆発はありません。
- Armature は `RotationConstraint` + `PositionConstraint` で追従します。**Unity** の制約として作成し、VRChat SDK の `AvatarDynamicsSetup` がアップロード時に `VRCRotationConstraint` / `VRCPositionConstraint` へ自動変換し、バインディングも張り替えます。
- 各クリップは制約に定数 `m_Enabled` カーブも書き込みます。これを書かないと、Avatar Optimizer が制約を `enabled = false` で焼き込んでしまい、「鎖だけ回って体が追従しない」状態になります。
- Play モード専用のプレビュードライバ（`APLBodyRotationPreviewDriver`）が制約の計算を毎フレーム反映するため、Unity 内でも回転を確認できます（アップロードビルドには含まれません）。

## ポーズゲート（未選択時は無効）

各リグレイヤーは内部パラメーター `AnimPosePlay` で切り替わる 2 ステートマシンです。

- **ポーズ未選択**（`AnimPosePlay = 0`）→ `Idle_*` ステートに留まり、回転ゼロ・高さ 0 で無効。
- **ポーズ選択中**（`AnimPosePlay = 1`）→ `Rotate_*` / `Height` に入り、スライダーが有効。

`AnimPosePlay` はポーズの Reserve ステートの `VRC_AvatarParameterDriver` が 1 をセットし、Reset レイヤーが 0 へ戻します。型は `Float` です。

## 高さ

- リグを使う場合（`enableFxAnimator`）、高さは同じ鎖（`AnimPoseBodyRotateRig/Height`）に載るため、体を回転させても傾きません。
- リグの高さ経路を使わないアバターでは、ロコモーションの高さブレンドを同じ `AnimPosePlay` ゲートで包み、ポーズ未選択時は無効にします。
- `minMaxHeight` は作者デフォルトの `1` です。

## セットアップ

1. `AvatarPoseLibrary` コンポーネントで **Enable Body Rotation** をオンにします。
2. **Enable Locomotion Animator** もオンにします（オフだとビルド時に警告し、回転が効きません）。
3. メニューは `Settings → Rotation` に「Tilt X / Tilt Y / Tilt Z / Reset Rotation」が追加されます。
4. ポーズを選び、Tilt X / Y / Z を動かして調整します。

## トラブルシューティング

| 症状 | 原因 | 対処 |
| --- | --- | --- |
| 回転が効かない | Enable Locomotion Animator がオフ | オンにする（コンソールにも警告） |
| 回転が効かない | Armature / Hips が見つからない | 「Body rotation is enabled but cannot be built.」を参照 |
| Unity（Play モード）で見えない | VRChat の制約は Unity が評価しない | プレビュードライバが有効。それでも動かなければ Avatar の Animator にビルト FX が割り当てられているか確認 |
| ポーズ未選択でも傾く | `AnimPosePlay` ゲートが効いていない | ビルドし直して Idle/Rotate レイヤーを確認 |
| 他プレイヤーから高さがずれる | 高さのオフセットが Root/RootT 経路で焼き込まれている | リグ経路（`enableFxAnimator`）を使うか、オフセットを 0 にする |

## 制限

- Unity エディタ単体では VRChat の制約（`VRCRotationConstraint` / `VRCPositionConstraint`）を評価しないため、厳密な確認は VRChat（または制約をエミュレートするツール）が必要です。
- 回転の支点は Hips です。別の支点には対応していません。
- 旧方式（`MotionBuilder.BuildBodyRotation` による RootQ 焼き込み）と比較すると、ポーズごとの細かい回転差分は持ちません（単一の共用リグで全ポーズを回す）。