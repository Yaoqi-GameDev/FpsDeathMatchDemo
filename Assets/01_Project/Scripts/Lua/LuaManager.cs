
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
        IResource resource = ResourceManager.instance.Load(url, false);
        TextAsset text=resource?.GetAsset<TextAsset>();
        if (text == null)
            {
                Debug.LogWarning("[LuaManager] 未找到 Lua 脚本: " + url);
                return null;
            }
            return text.bytes;
        
    }

    public object[] DoString(string chunk, string chunkName = "chunk")
        => _env.DoString(chunk, chunkName);

    public object[] DoFile(string filePath)
        => _env.DoString($"return require '{filePath}'", $"DoFile:{filePath}");

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
