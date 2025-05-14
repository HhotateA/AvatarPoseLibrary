# Avatar Pose Library (APL)

**AvatarPoseLibrary (APL)** は、Unity 上で VRChat 向けのアバター用のポーズを管理・適用するためのツールです。アバターのポーズを効率的に制御し、VRChat アバターの制作やカスタマイズを支援します。

---

## ✨ 特徴

- **パラメーター数を最小限に最適化**  
  複数のメニューを統合し、制御する Int パラメーターを最小限にします。

- **サムネイル撮影機能**  
  アニメーションごとに自動でサムネイルを撮影し、メニュー画像を差し替え可能。

- **トラッキング抑制**  
  アニメーションごとに異なるトラッキング設定が可能。指だけを動かしたり、操作を完全に無効化することもできます。

- **コンポーネントで設定完了**  
  設定はコンポーネントベース。プレハブ化可能で再利用性も高いです。

- **より人間らしい動きに**  
  動きのないポーズを、自動で動画撮影に最適化された自然な動きに変換します。

- **許諾なしで再配布OK**  
  アニメーションやポーズデータの配布に、商用・非商用問わず許諾なしで使うことができます。

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

## 🤝 PRの作成方法

1. フォークの作成

リポジトリ右上の「Fork」ボタンをクリックし、自分のアカウントにコピーします。

2. ブランチを作成

フォークしたリポジトリをクローンし、新しいブランチを切ります。

```git checkout -b feature/your-feature-name```

3. 変更を加える

必要な修正や追加を行い、コミットメッセージは明確に書きます。

```git add .```  
の後、

```
git commit -m "Add: 追加したアセットの概要"  
git commit -m "Feature: 追加した機能の概要"  
git commit -m "Fix: 修正したバグの概要"  
```  

のように、カンタンに変更内容を記述してください。

4. プッシュ

作成したブランチをリモートにプッシュします。

```git push origin feature/your-feature-name```

5. プルリクエストを作成

オリジナルリポジトリのページに移動し、「Compare & pull request」をクリック。

- タイトルと詳細な説明を入力

- 関連 Issue があればリンクを貼る

6. レビューとマージ

メンテナーがレビューを行い、問題なければマージされます。

フィードバックがあれば、追加コミットで修正してください。

※ バージョンコードの更新は行わないでください。  

- メジャーバージョン：互換性のない変更・機能追加  
- マイナーバージョン：互換性のある変更・機能追加  
- パッチバージョン：軽微なバグ修正  

---

## 💰 開発支援のお願い

APLは、完全無料・無償利用OKのツールとして公開されています。 商用・非商用問わず再配布も可能で、アニメーション販売やアバター改変で自由にご活用いただけます。

もしAPLを利用したコンテンツ（アバター、衣装、ポーズ集など）で一定以上の収益や反響があった場合は、 開発者の活動継続のために投げ銭・宣伝などでご支援いただけると嬉しいです！

Donation => https://hhotatea.booth.pm/items/6902222

皆さんの支援が、APLのさらなるアップデートや新機能開発につながります。

---

## 📄 ライセンス

このプロジェクトは **MIT ライセンス** の下で提供されます。

- 商用・非商用問わず利用することが可能です。
- 再配布する場合も、許諾は必要ありません。

バグ報告・機能提案・プルリクエストは歓迎です！  
GitHub の Issues や PR を通じてご参加ください。

ライセンスファイルは、 [LICENCE](https://github.com/HhotateA/AvatarPoseLibrary/blob/main/Packages/com.hhotatea.avatar-pose-library/LICENCE) を参照してください。

---

## 👯 共同製作メンバー

- ロゴ作成：lowteq  
https://x.com/lowteq_neko

- ストア作成：meron-farm  
https://meronfarm.booth.pm/

---

## 📞 連絡先

質問は、以下の連絡先にお願いします。

- ほたてねこまじん @HhotateA_xR  
https://x.com/HhotateA_xR

---

2025-05-15 v1.0.0