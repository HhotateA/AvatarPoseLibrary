# APLテレメトリ仕様

この文書は、Avatar Pose Library（APL）が送信するイベント種別とパラメータをまとめたものです。
実装上の送信データはUnity側で決定し、Google Apps Script（GAS）はGA4形式への変換とエラーレポートの中継だけを行います。

## 送信モード

| モード | UI上の選択 | 送信内容 |
|---|---|---|
| `minimal` | 最小限 | 共通パラメータのみ |
| `detailed` | すべて許可 | 共通、実行環境、イベント固有パラメータ |

どちらのモードでもイベントは送信されます。モードによって、各イベントへ付加するパラメータが変わります。

## イベント一覧

| イベント名 | 送信タイミング | 詳細モードで追加する情報 |
|---|---|---|
| `apl_first_session` | このPCでAPLのクライアントIDを初めて生成した後、最初のAPL初期化時 | 実行環境 |
| `apl_editor_session_started` | Unity Editorセッションで初めて最新バージョンを問い合わせるとき | 実行環境 |
| `apl_version_changed` | 前回記録したAPLバージョンと現在のバージョンが異なるとき | 実行環境、変更前バージョン |
| `apl_build_completed` | APLコンポーネントを含むビルドが完了したとき | 実行環境、ビルド情報 |
| `apl_build_failed` | APLのビルド処理から例外が通知されたとき | 実行環境、ビルド情報、失敗情報 |

`apl_build_failed` のGA4イベントとエラーレポートは別の送信です。エラーレポートの送信可否にかかわらず、通常のビルド失敗イベントは送信されます。

## GA4での共通情報

Unityから送った `event_name` はGA4のイベント名に、`client_id` はGA4のクライアントIDに使用されます。これらはイベントパラメータには重複して格納しません。

| 名前 | 型 | モード | 内容 |
|---|---|---|---|
| `event_name` | string | 共通 | 上記イベント一覧のイベント名 |
| `client_id` | string | 共通 | EditorPrefsへ保存する、このPCのAPLクライアントID（32文字のGUID） |
| `schema_version` | integer | 共通 | UnityからGASへ送るペイロード形式のバージョン |
| `telemetry_mode` | string | 共通 | `minimal` または `detailed` |
| `apl_version` | string | 共通 | 実行中のAPLバージョン |
| `engagement_time_msec` | integer | 共通 | GA4でイベントをエンゲージメント対象にするための値。現在は `1` |

GASはGA4への送信時に、広告用途の同意状態として `ad_user_data` と `ad_personalization` を `DENIED` に設定します。

## 詳細モードの実行環境

次のパラメータは、詳細モードの全イベントへ追加されます。

| パラメータ | 型 | 内容 |
|---|---|---|
| `event_id` | string | イベントごとに生成する32文字のGUID |
| `unity_version` | string | Unityバージョン |
| `vrcsdk_version` | string | VRCSDKパッケージまたはアセンブリのバージョン |
| `ndmf_version` | string | NDMFパッケージまたはアセンブリのバージョン |
| `session_id` | integer | Unity Editorセッション開始時刻を基にしたUnix時刻（秒） |
| `engagement_time_msec` | integer | 現在は `1` |

## イベント固有パラメータ

### `apl_first_session`

実行環境以外の固有パラメータはありません。

### `apl_editor_session_started`

実行環境以外の固有パラメータはありません。このイベントは最新バージョン取得と同じGETリクエストで送信されます。

必要なテレメトリ情報が付いていない旧バージョンからのGETは、GASが同じ `apl_editor_session_started` として記録します。その場合は互換用のクライアントIDとAPLバージョンを使用します。

### `apl_version_changed`

| パラメータ | 型 | 内容 |
|---|---|---|
| `previous_apl_version` | string | 前回EditorPrefsへ記録したAPLバージョン |

### `apl_build_completed`

| パラメータ | 型 | 内容 |
|---|---|---|
| `build_duration_ms` | integer | APLビルド計測開始から完了までの時間（ミリ秒） |
| `component_count` | integer | 有効なAPLコンポーネント数 |
| `library_count` | integer | 参照している重複なしのAPLデータ数 |
| `category_count` | integer | 全APLデータのカテゴリ数合計 |
| `pose_count` | integer | 全APLデータのポーズ数合計 |
| `humanoid` | integer | アバターのAnimatorがHumanoidなら `1`、それ以外は `0` |
| `audio_enabled` | integer | いずれかのAPLデータでAudio Modeが有効なら `1` |
| `locomotion_enabled` | integer | いずれかでLocomotion Animatorが有効なら `1` |
| `fx_enabled` | integer | いずれかでFX Animatorが有効なら `1` |
| `cache_enabled` | integer | いずれかでキャッシュが有効なら `1` |
| `auto_reset_enabled` | integer | いずれかで自動リセットが有効なら `1` |

### `apl_build_failed`

`apl_build_completed` と同じビルド情報に、次を追加します。

| パラメータ | 型 | 内容 |
|---|---|---|
| `build_stage` | string | 例外が発生したAPLビルド処理の段階 |
| `exception_type` | string | 例外型の完全名。取得できない場合は `UnknownException` |

## エラーレポート

エラーレポートはGA4イベントではありません。`request_type: "error_report"` のJSONとしてGASへPOSTし、設定されたWebhookへJSONファイル付きで中継します。

- 詳細モードでは、ビルド失敗時に自動送信します。
- 最小限モードでは確認ダイアログを表示し、許可された場合だけ送信します。
- 最小限モードで許可すると、以後の設定は詳細モードになります。
- バッチモードでは、最小限モードからエラーレポートを送信しません。

### エラーレポート本体

| パラメータ | 型 | 内容 |
|---|---|---|
| `request_type` | string | GASでエラーレポートを識別する値。常に `error_report`。Webhook添付時には削除 |
| `schema_version` | integer | ペイロード形式のバージョン |
| `report_id` | string | レポートごとに生成する32文字のGUID |
| `occurred_at_utc` | string | 例外発生時刻（UTC、ISO 8601） |
| `client_id` | string | このPCのAPLクライアントID |
| `apl_version` | string | APLバージョン |
| `unity_version` | string | Unityバージョン |
| `vrcsdk_version` | string | VRCSDKバージョン |
| `ndmf_version` | string | NDMFバージョン |
| `editor_platform` | string | Unity Editorの実行プラットフォーム |
| `build_stage` | string | 例外が発生したAPLビルド処理の段階 |
| `build_duration_ms` | integer | APLビルドの経過時間（ミリ秒） |
| `exception_type` | string | 例外型の完全名 |
| `error_text` | string | 例外の文字列表現。GAS中継時にローカルパスを伏せ字化 |
| `error_text_truncated` | integer | エラー本文を短縮した場合は `1`、それ以外は `0` |
| `stack_frames` | string[] | 型名とメソッド名のみのスタックフレーム |
| `component_count` | integer | 有効なAPLコンポーネント数 |
| `library_count` | integer | 重複なしのAPLデータ数 |
| `category_count` | integer | カテゴリ数合計 |
| `pose_count` | integer | ポーズ数合計 |
| `humanoid` | integer | Humanoidなら `1`、それ以外は `0` |
| `libraries` | object[] | APLデータごとの設定値。最大64件 |

### `libraries` の各要素

| パラメータ | 型 | 内容 |
|---|---|---|
| `category_count` | integer | APLデータ内のカテゴリ数 |
| `pose_count` | integer | APLデータ内のポーズ数 |
| `enable_height` | integer | Height Parameterの有効状態 |
| `enable_speed` | integer | Speed Parameterの有効状態 |
| `enable_mirror` | integer | Mirror Parameterの有効状態 |
| `enable_tracking` | integer | Tracking Parameterの有効状態 |
| `enable_deep_sync` | integer | Deep Syncの有効状態 |
| `enable_pose_space` | integer | Pose Spaceの有効状態 |
| `enable_cache` | integer | キャッシュの有効状態 |
| `enable_auto_reset` | integer | 自動リセットの有効状態 |
| `enable_locomotion` | integer | Locomotion Animatorの有効状態 |
| `enable_fx` | integer | FX Animatorの有効状態 |
| `suppress_additive` | integer | Additive Animator抑制の有効状態 |
| `audio_enabled` | integer | Audio Modeの有効状態 |
| `write_defaults` | string | Write Defaults設定値 |

各有効状態は、有効なら `1`、無効なら `0` です。

## サイズ制限

サイズ制限はUnity側で適用します。既定値は次の通りです。

| 対象 | 既定値 | 超過時の動作 |
|---|---:|---|
| 通常イベント | 8,192 bytes | イベントを送信せず警告を出力 |
| エラーレポート | 65,536 bytes | `error_text` を段階的に短縮し、必要なら `stack_frames` を削除。それでも超過する場合は送信しない |
| エラー本文 | 24,000文字 | 超過分を切り詰め、`error_text_truncated` を `1` に設定 |
| スタックフレーム | 32件 | 先頭から既定件数までを保存 |

## 送信しない情報

現在の実装では、氏名、VRChatアカウント情報、アバター名、GameObject名、APLデータ名、カテゴリ名、ポーズ名、アセットパスをテレメトリ項目として送信しません。

エラー本文に含まれるWindowsドライブパス、および `/Users`・`/home` 以下のパスは、Webhookへ中継する前にGASで `[local-path]` へ置換します。