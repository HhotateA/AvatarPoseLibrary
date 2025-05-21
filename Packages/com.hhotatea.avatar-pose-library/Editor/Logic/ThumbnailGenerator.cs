using UnityEngine;
using VRC.SDK3.Avatars.Components;
using UnityEditor;
using System;
using System.Collections.Generic;
using com.hhotatea.avatar_pose_library.editor;
using Object = UnityEngine.Object;

namespace com.hhotatea.avatar_pose_library.logic
{
    public class ThumbnailGenerator : IDisposable
    {
        private float AvatarHeight = 1.5f;

        private GameObject avatarGO;
        private GameObject cameraGO;
        private Transform headTransform;
        private Vector3 currentPosition;
        private float distance;
        
        private Dictionary<GameObject,int> currentLayers = new Dictionary<GameObject, int>();
        
        private Camera camera;
        private RenderTexture renderTexture;

        public ThumbnailGenerator(GameObject contextObject)
        {
            var descriptor = contextObject.GetComponentInParent<VRCAvatarDescriptor>();
            if (!descriptor)
            {
                Debug.LogWarning("VRCAvatarDescriptor が見つかりません。");
                return;
            }
            
            var animator = descriptor.GetComponent<Animator>();
            if (animator)
            {
                if (animator.avatar.isHuman)
                {
                    // HeadBoneの取得
                    headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
                }
            }

            // 身長の計算。多少余裕をもたせる。
            AvatarHeight = descriptor.ViewPosition.y * 1.2f + 0.1f;

            // アバターのセッティング
            avatarGO = contextObject;
            // avatarGO.transform.position = avatarGO.transform.position + ConstVariables.ThumbnailOffset;
            
            SetLayerRecursively(avatarGO, DynamicVariables.Settings.thumbnailLayer);

            // カメラ生成
            cameraGO = new GameObject("ThumbnailCamera");
            // cameraGO.hideFlags = HideFlags.HideAndDontSave;
            camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << DynamicVariables.Settings.thumbnailLayer;
            camera.orthographic = false;
            camera.fieldOfView = DynamicVariables.Settings.fieldOfView;

            // カメラ位置調整
            Vector3 center = avatarGO.transform.position + new Vector3(0, AvatarHeight / 2f, 0);
            distance = AvatarHeight / (2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            distance = Math.Max(distance * DynamicVariables.Settings.cameraDistance, AvatarHeight * 0.6f);
            camera.transform.position = center + new Vector3(0f, 0f, distance) + DynamicVariables.Settings.cameraOffset * AvatarHeight;
            camera.transform.LookAt(center);

            // カリング設定
            camera.nearClipPlane = distance * 0.01f;
            camera.farClipPlane = distance * 2.5f;

            // RenderTexture
            renderTexture = new RenderTexture(DynamicVariables.Settings.texSize, DynamicVariables.Settings.texSize, 24);
            camera.targetTexture = renderTexture;
            avatarGO.SetActive(false);
        }

        /// <summary>
        /// 指定したアニメーションポーズを適用し、サムネイルを生成します。
        /// </summary>
        public Texture2D Capture(AnimationClip poseClip)
        {
            if (poseClip == null) return null;
            avatarGO.SetActive(true);
            AnimationMode.StartAnimationMode();
            
            // 撮影の開始
            AnimationMode.BeginSampling();
            if (MotionBuilder.IsMoveAnimation(poseClip))
            {
                // アニメーションはちょっと再生する
                AnimationMode.SampleAnimationClip(avatarGO, poseClip, 0.5f);
            }
            else
            {
                AnimationMode.SampleAnimationClip(avatarGO, poseClip, 0f);
            }
            AnimationMode.EndSampling();

            // 頭にカメラを合わせる
            if (headTransform)
            {
                // カメラ位置調整
                var aPos = avatarGO.transform.position;
                var hPos = headTransform.position;
                Vector3 center = (aPos + hPos) * 0.5f;
                cameraGO.transform.position = center 
                                              + distance * Vector3.Lerp(Vector3.forward, headTransform.forward, DynamicVariables.Settings.lookAtFace).normalized
                                              + DynamicVariables.Settings.cameraOffset * AvatarHeight;
                cameraGO.transform.LookAt((center + hPos) * 0.5f);
            }
            
            camera.Render();

            RenderTexture currentActiveRT = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            tex.Apply();
            RenderTexture.active = currentActiveRT;

            AnimationMode.StopAnimationMode();
            avatarGO.SetActive(false);
            return tex;
        }

        public void Dispose()
        {
            if (currentLayers != null)
            {
                ResetLayers();
            }
            
            if (cameraGO != null)
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraGO);
                cameraGO = null;
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                renderTexture = null;
            }
            
            avatarGO.SetActive(true);
        }

        /// <summary>
        /// 撮影のためにレイヤーを変更する
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="layer"></param>
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            currentLayers.Add(obj,obj.layer);
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// レイヤーの初期化
        /// </summary>
        void ResetLayers()
        {
            foreach (var cl in currentLayers)
            {
                cl.Key.layer = cl.Value;
            }
            currentLayers = new Dictionary<GameObject, int>();
        }
    }
}