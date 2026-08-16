
using AssetBundleFramework;
using UnityEngine;
using XLua;

public class LuaManager
{
    private static LuaManager _instance;
    public static LuaManager Instance=>_instance;
    private LuaEnv _env;
    public LuaEnv Env=>_env;

    private static bool _initialized;

    private const string LuaRootPath="Assets/01_Project/AssetBundleAssets/Lua/";

    public static void Init(){
        if (_initialized) return;

            var mgr = new LuaManager();
            mgr._env = new LuaEnv();
            mgr._env.AddLoader(mgr.LoaderFromAB);

            _instance = mgr;
            _initialized = true;
    }

    private byte[] LoaderFromAB(ref string filePath){
        string url=LuaRootPath+filePath+".lua.bytes";
        try
        {
            IResource resource = ResourceManager.instance.Load(url, false);
            TextAsset text=resource?.GetAsset<TextAsset>();
            if (text == null)
            {
                Debug.LogWarning("[LuaManager] 未找到 Lua 脚本: " + url);
                return null;
            }
            return text.bytes;
        }
        catch (System.Exception e)
        {
            // 资源不在当前 AB 包中（如 StreamingAssets 兜底包未含 Lua）时：
            // 返回 null 让 xLua 报"模块未找到"，而不是抛异常打断 AB 初始化。
            Debug.LogWarning($"[LuaManager] 加载 Lua 脚本失败: {url} ({e.Message})");
            return null;
        }
        
    }

    public object[] DoString(string chunk, string chunkName = "chunk")
        => _env.DoString(chunk, chunkName);

    public object[] DoFile(string filePath)
    {
        try
        {
            return _env.DoString($"return require '{filePath}'", $"DoFile:{filePath}");
        }
        catch (System.Exception e)
        {
            // 模块未找到/脚本错误不应阻断游戏流程（AB 初始化在更外层 try-catch 里，
            // 这里的异常如果冒上去会把 AssetBundleRuntime enabled=false）。
            Debug.LogWarning($"[LuaManager] DoFile '{filePath}' 失败: {e.Message}");
            return null;
        }
    }

    public T GetGlobal<T>(string name)
        => _env.Global.Get<T>(name);

    public void SetGlobal(string name, object value)
        => _env.Global.Set(name, value);

    public void Tick()
        => _env?.Tick();

    public void Dispose()
    {
        if (_env == null) return;
        _env.Dispose();
        _env = null;
        _instance = null;
        _initialized = false;
    }

}
