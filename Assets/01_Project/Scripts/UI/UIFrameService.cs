using FpsDemo.Core;
using UIFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// 全局 <see cref="UIFrame"/>：由 <see cref="UISettings.CreateUIInstance"/> 创建一次，
    /// 根物体挂 <see cref="DontDestroyThisRoot"/> 跨场景保留。
    /// </summary>
    public static class UIFrameService
    {
        public static UIFrame Frame { get; private set; }

        public static bool HasFrame => Frame != null;

        /// <summary>已有则复用；否则从 settings 创建并 DDOL。</summary>
        public static UIFrame Ensure(UISettings settings)
        {
            if (Frame != null)
            {
                ConfigureForGameView(Frame);
                return Frame;
            }

            if (settings == null)
            {
                Debug.LogError("[UIFrameService] UISettings is null.");
                return null;
            }

            Frame = settings.CreateUIInstance(instanceAndRegisterScreens: true);
            if (Frame == null)
            {
                Debug.LogError("[UIFrameService] CreateUIInstance returned null.");
                return null;
            }

            // 约定：仅根物体挂 DontDestroyThisRoot（勿在子节点再 DDOL）。
            if (Frame.GetComponent<DontDestroyThisRoot>() == null)
                Frame.gameObject.AddComponent<DontDestroyThisRoot>();

            ConfigureForGameView(Frame);
            return Frame;
        }

        /// <summary>
        /// Screen Space Camera + 低 Depth / 窄 FarClip 时，Scene 能看见、Game 看不见；
        /// DeathMatch 的 Overlay HUD 也会盖住 Camera 模式 UI。统一为 Overlay 高排序。
        /// </summary>
        public static void ConfigureForGameView(UIFrame frame)
        {
            if (frame == null)
                return;

            var canvas = frame.MainCanvas;
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.sortingOrder = 1000;
                canvas.additionalShaderChannels =
                    AdditionalCanvasShaderChannels.TexCoord1
                    | AdditionalCanvasShaderChannels.Normal
                    | AdditionalCanvasShaderChannels.Tangent;
            }

            if (frame.UICamera != null)
                frame.UICamera.enabled = false;

            DisableExtraEventSystems(frame.transform);
        }

        public static void DisableExtraEventSystems(Transform uiFrameRoot)
        {
#pragma warning disable CS0618
            var systems = Object.FindObjectsOfType<EventSystem>();
#pragma warning restore CS0618
            for (int i = 0; i < systems.Length; i++)
            {
                var es = systems[i];
                if (es == null)
                    continue;
                if (uiFrameRoot != null && es.transform.IsChildOf(uiFrameRoot))
                    continue;
                es.gameObject.SetActive(false);
            }
        }

        /// <summary>测试或关机时可清空引用（一般不必调用）。</summary>
        public static void ClearFrameReference()
        {
            Frame = null;
        }
    }
}
