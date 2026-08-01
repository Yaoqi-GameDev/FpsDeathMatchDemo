using FpsDemo.Core;
using UIFramework;
using UnityEngine;

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
                return Frame;

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

            return Frame;
        }

        /// <summary>测试或关机时可清空引用（一般不必调用）。</summary>
        public static void ClearFrameReference()
        {
            Frame = null;
        }
    }
}
