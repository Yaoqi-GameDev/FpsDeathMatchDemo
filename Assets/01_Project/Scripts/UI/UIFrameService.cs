using FpsDemo.Core;
using UIFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FpsDemo.UI
{
    /// <summary>
    /// 全局 <see cref="UIFrame"/>：创建一次并跨场景保留；EventSystem 只保留 Frame 上那一份。
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

            // 优先挂到唯一 --DDOL-- 下；没有则自身 DDOL（仍只有一份 Frame）。
            var appRoot = DontDestroyThisRoot.AppRoot;
            if (appRoot != null)
            {
                Frame.transform.SetParent(appRoot.transform, true);
            }
            else if (Frame.GetComponent<DontDestroyThisRoot>() == null)
            {
                Frame.gameObject.AddComponent<DontDestroyThisRoot>();
            }

            ConfigureForGameView(Frame);
            return Frame;
        }

        /// <summary>
        /// 回大厅 / 进 Lobby：关窗、清多余 EventSystem、强制打开大厅并复位 CanvasGroup（避免 Fade 卡在 alpha=0）。
        /// </summary>
        public static void ShowLobbyMenu()
        {
            UnlockCursor();

            var frame = Frame;
            if (frame == null)
            {
                Debug.LogWarning("[UIFrameService] ShowLobbyMenu: no UIFrame.");
                return;
            }

            ConfigureForGameView(frame);
            frame.CloseAllWindows(animate: false);
            frame.HideAllPanels(animate: false);
            ResetAllScreenCanvasGroups(frame);

            if (!frame.IsScreenRegistered(LobbyMenuWindowController.ScreenId))
            {
                Debug.LogError(
                    "[UIFrameService] Lobby screen not registered: " + LobbyMenuWindowController.ScreenId);
                return;
            }

            frame.OpenWindow(LobbyMenuWindowController.ScreenId);
            // 取消渐入并完成过渡回调（否则 GraphicRaycaster 会一直被关掉，大厅点不了）。
            ForceScreenVisibleOpaque(LobbyMenuWindowController.ScreenId);
            EnsureGraphicRaycasterEnabled(frame);
            UnlockCursor();
        }

        /// <summary>进对局前关掉大厅等窗口（无动画，避免 DDOL 窗 alpha 卡在 0）。</summary>
        public static void CloseAllWindowsImmediate()
        {
            if (Frame == null)
                return;
            Frame.CloseAllWindows(animate: false);
            ResetAllScreenCanvasGroups(Frame);
        }

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
            var systems = Object.FindObjectsOfType<EventSystem>(includeInactive: true);
#pragma warning restore CS0618
            for (int i = 0; i < systems.Length; i++)
            {
                var es = systems[i];
                if (es == null)
                    continue;
                if (uiFrameRoot != null && es.transform.IsChildOf(uiFrameRoot))
                {
                    if (!es.gameObject.activeSelf)
                        es.gameObject.SetActive(true);
                    continue;
                }

                es.gameObject.SetActive(false);
            }
        }

        public static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static void EnsureGraphicRaycasterEnabled(UIFrame frame)
        {
            if (frame == null)
                return;
            var raycaster = frame.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = true;
        }

        public static void ClearFrameReference()
        {
            Frame = null;
        }

        private static void ResetAllScreenCanvasGroups(UIFrame frame)
        {
            if (frame == null)
                return;

#pragma warning disable CS0618
            var groups = frame.GetComponentsInChildren<CanvasGroup>(includeInactive: true);
#pragma warning restore CS0618
            for (int i = 0; i < groups.Length; i++)
            {
                var cg = groups[i];
                if (cg == null)
                    continue;
                // 不改 DarkenBG 等业务透明度以外的 Screen 根：Screen 根通常挂了 *Controller
                if (cg.GetComponent<IScreenController>() == null)
                    continue;
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        private static void ForceScreenVisibleOpaque(string screenId)
        {
#pragma warning disable CS0618
            var behaviours = Object.FindObjectsOfType<MonoBehaviour>(includeInactive: true);
#pragma warning restore CS0618
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not IScreenController ctl || ctl.ScreenId != screenId)
                    continue;

                var go = behaviours[i].gameObject;
                if (!go.activeSelf)
                    go.SetActive(true);

                var fades = go.GetComponents<FadeAni>();
                for (int f = 0; f < fades.Length; f++)
                {
                    if (fades[f] != null)
                        fades[f].CancelAnimation(1f);
                }

                var cg = go.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }

                return;
            }
        }
    }
}
