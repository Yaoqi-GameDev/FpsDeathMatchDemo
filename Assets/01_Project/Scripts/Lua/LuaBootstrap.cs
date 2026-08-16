using FpsDemo.AssetBundles;
using UnityEngine;

namespace FpsDemo.Lua
{
    /// <summary>
    /// Lua 启动入口：订阅 <see cref="AssetBundleRuntime.Initialized"/>，
    /// 在 AB 初始化完成后初始化 LuaManager 并执行 Main 脚本。
    /// 挂在场景任意常驻物体上即可（建议 --DDOL-- 下）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LuaBootstrap : MonoBehaviour
    {
        private void OnEnable()
        {
            AssetBundleRuntime.Initialized += OnAbInitialized;
            // 若 AB 已先初始化完成（事件已触发过），这里直接执行，避免漏调
            if (AssetBundleRuntime.IsInitialized)
                OnAbInitialized();
        }

        private void OnDisable()
        {
            AssetBundleRuntime.Initialized -= OnAbInitialized;
        }

        private void OnAbInitialized()
        {
            LuaManager.Init();
            LuaManager.Instance.DoFile("Main");
        }
    }
}
