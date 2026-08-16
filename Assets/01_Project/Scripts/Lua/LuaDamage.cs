using System;
using UnityEngine;
using XLua;
using FpsDemo.Combat;

namespace FpsDemo.Lua
{
    /// <summary>从 Lua 获取部位伤害倍率，Lua 失败时回退 C# 默认值。</summary>
    public static class LuaDamage
    {
        // 缓存从 Lua 拿到的函数（LuaFunction 方式，不需要 CSharpCallLua 生成代码）
        private static LuaFunction _getMultiplier;
        private static bool _resolved;

        public static float GetMultiplier(DamageBodyRegion region)
        {
            // 懒加载：第一次调用时才去 Lua 拿函数（避免 bootstrap 里逐模块 Init）
            if (!_resolved)
            {
                _resolved = true;
                try
                {
                    _getMultiplier = LuaManager.Instance.Env.Global.Get<LuaFunction>("GetDamageMultiplier");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[LuaDamage] 获取 Lua 倍率函数失败: " + e.Message);
                    _getMultiplier = null;
                }
            }

            // Lua 没加载成功 → 回退 C# 兜底（保底倍率）
            if (_getMultiplier == null)
                return 1f;

            try
            {
                int regionInt = (int)region;
                object[] results = _getMultiplier.Call(regionInt);
                return results != null && results.Length > 0 ? Convert.ToSingle(results[0]) : 1f;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LuaDamage] Lua 倍率调用失败，回退 1f: " + e.Message);
                return 1f;
            }
        }
    }
}
