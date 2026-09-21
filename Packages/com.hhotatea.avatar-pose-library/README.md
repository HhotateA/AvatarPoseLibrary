# Avatar Pose Library (APL) — GeoXTen Fork

**AvatarPoseLibrary (APL)** は、Unity 上で VRChat 向けのアバター用のポーズを管理・適用するためのツールです。アバターのポーズを効率的に制御し、VRChat アバターの制作やカスタマイズを支援します。
> **GeoXTen Fork (v1.2.44)** — オリジナル（HhotateA）に Variation Slider などの拡張を追加しています。オリジナル: https://github.com/HhotateA/AvatarPoseLibrary

---

## ✨ 特徴

- **パラメーター数を最小限に最適化**  
  複数のメニューを統合し、制御する Int パラメーターを最小限にします.

- **Variation Slider (v1.2.44 新機能) — make it easy for pose selection**  
  チェックボックス1つずつの代わりに **1つの Radial Puppet** (`AnimPoseVariation_{Guid}` Float `0-1`, `saved:true`) ですべてのポーズを操作 — **make it easy for pose selection**. `0% = idle/reset`, `1/N` 刻みでポーズを順に選択。カテゴリが1つの場合はメニュー名が `Pose 1 (10)` のようにカテゴリ名と同期。`Enable Variation Slider` で切り替え、従来の Toggle と完全互換.

- **サムネイル撮影機能**  
  アニメーションごとに自動でサムネイルを撮影し、メニュー画像を差し替え可能.

- **トラッキング抑制**  
  アニメーションごとに異なるトラッキング設定が可能。指だけを動かしたり、操作を完全に無効化することもできます.

- **コンポーネントで設定完了**  
  設定はコンポーネントベース。プレハブ化可能で再利用性も高いです.

- **より人間らしい動きに**  
  動きのないポーズを、自動で動画撮影に最適化された自然な動きに変換します.

- **許諾なしで再配布OK**  
  アニメーションやポーズデータの配布に、商用・非商用問わず許諾なしで使うことができます.

- **サイレント上書きインストール**  
  `Tools → Avatar Pose Library → Force Replace Install (Silent Overwrite)` で `unitypackage` の `already exists` ダイアログなしで上書き可能.

---

## 📦 導入方法

APL は 2つの方法で導入することができます。
**VCC (VRChat Creator Companion)** 経由の導入がおすすめです。  

### 1. VCC 経由での導入

1. VCC を開き、**Settings** に移動  
2. **Packages** タブをクリック  
3. **Add Repository** をクリック  
4. 入力欄に以下の URL を貼り付け  
   ```text
   https://HhotateA.github.io/AvatarPoseLibrary/index.json
   ```  
5. **Add** をクリック  
6. リポジトリ情報を確認し、**I Understand** をクリック  
7. 任意のプロジェクトの **Manage Project** から APL を追加

### 2. UnityPackage 経由での導入

1. あらかじめ以下の依存パッケージをプロジェクトに追加しておく  
   ```json
   "com.vrchat.avatars": "3.8.0",
   "nadena.dev.ndmf": "1.7.9",
   "nadena.dev.modular-avatar": "1.12.5"
   ```
2. 下記 URL から UnityPackage を取得  
   ```text
   https://github.com/HhotateA/AvatarPoseLibrary/releases/latest/
   ```
3. 任意の Unity プロジェクトを開き、UnityPackage をドラッグ＆ドロップ  
4. 追加ファイルを確認し、**Import** をクリック

---

## 🆕 更新履歴

### v1.2.44 (GeoXTen Fork) - 2026-08-29
- **Added Variation Slider** — `Enable Variation Slider` で全ポーズを1 Radialで操作 (カテゴリ名同期, `saved:true` で `68%` が保持される)
- メニュー生成を `MenuBuilder` で分岐、Animator を `1/N` インターバル (`Greater/Less` + `1/512` hysteresis) で選択、従来 Toggle と互換
- `MenuContext.variation` / `InspectorContext.enableVariationLabel` 追加
- `asmdef` 重複警告を解消 (単一 `Editor`/`Runtime` のみに整理)
- `Force Replace Install` ヘルパー + `AvatarPoseLibrary_Mod_Replace.bat` 追加

### v1.2.43 以前
- オリジナル HhotateA 版に準拠

---

## 🤝 PRの作成方法

1. Fork → `git checkout -b feature/your-feature-name`
2. 変更 → `git add .` → `git commit -m "Feature: ..."`
3. `git push origin feature/your-feature-name` → PR作成

※ バージョンは `package.json` で管理 (`1.2.44`).

---

## 💰 開発支援のお願い

APLは完全無料・無償利用OK。商用・非商用問わず再配布可能。収益があった場合は https://hhotatea.booth.pm/items/6902222 などで支援をお願いします。

## 📄 ライセンス

**MIT** — 商用・非商用問わず利用・再配布可能。詳細は [LICENCE](https://github.com/HhotateA/AvatarPoseLibrary/blob/main/Packages/com.hhotatea.avatar-pose-library/LICENCE)。

## 👯 共同製作メンバー

- ロゴ: lowteq https://x.com/lowteq_neko
- ストア: meron-farm https://meronfarm.booth.pm/
- Fork 拡張: GeoXTen https://github.com/GeoXTen

## 🔒 プライバシー

[プライバシーポリシー](https://hhotatea.github.io/AvatarPoseLibrary/privacy.html) を参照。

## 📞 連絡先

- HhotateA_xR https://x.com/HhotateA_xR
- GeoXTen https://github.com/GeoXTen

---

2026-08-29 v1.2.44 (GeoXTen Fork) / 2025-05-15 v1.0.0 (Original)
