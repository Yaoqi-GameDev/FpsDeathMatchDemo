# FpsDemoNetWork xLua 逻辑热更接入方案

> 目标：在现有 AssetBundle 资源热更框架上，接入 xLua 实现**游戏逻辑（Lua 脚本）热更**。
> 现状：美术资源（特效 prefab）热更已跑通，版本管理/下载/校验完整。

---

## 一、现有框架回顾（基础认知）

```
[打包] BuildSetting.xml + Builder.cs
  ├─ AssetPath 目录 + Suffix 后缀 → 打成一个 AB
  ├─ 产物输出到 BuildRoot (../AssetBundle)
  └─ 上传到本地服务器 (localhost:8080)

[启动] AssetBundleRuntime（DDOL，DefaultExecutionOrder = -450）
  ├─ Awake → Bootstrap 协程
  │    └─ CheckAndUpdate(): 下载 version.json → 对比本地 → 下载新版到 persistent/{version}/
  │    └─ Initialize(): ResourceManager.Initialize(平台目录)
  ├─ Update/LateUpdate → ResourceManager.instance.Update()/LateUpdate()
  └─ 加载资源: ResourceManager.instance.LoadWithCallback(assetPath, async, callback) → IResource.GetAsset<T>()

[版本管理] AssetBundleUpdateManager / AssetBundleVersionValidator
  ├─ 版本清单 version.json：version + files(path/size/hash SHA256)
  ├─ 下载目录: persistent/AssetBundles/{version}.downloading → 校验后 rename 为 {version}
  ├─ 当前版本: persistent/AssetBundles/current.json
  └─ 加载优先级: persistent 有有效版本 → 用它；否则 → StreamingAssets 兜底
```

**核心结论**：Lua 脚本打进 AB 就是普通 TextAsset 资源，现有框架**零改造**即可承载它。

---

## 二、总体架构（接入后）

```
服务器 (localhost:8080)
  ├─ version.json（版本清单）
  └─ AssetBundles/{version}/Windows/
       ├─ vfx_xxx.ab（美术资源，已有）
       └─ lua_xxx.ab（Lua 脚本，新增）

客户端启动顺序
  ① AssetBundleRuntime.Awake → CheckAndUpdate → Initialize
  ② LuaBootstrap（AB 初始化完成后触发）→ 创建 LuaManager
  ③ LuaManager：new LuaEnv + 注册 Loader（从 AB 读 TextAsset）
  ④ DoString("require 'Main'") → 游戏逻辑在 Lua 层启动
```

---

## 三、实施步骤（5 个阶段）

### 阶段 A：导入 xLua（一次性）

| 步骤 | 内容 |
|------|------|
| A1 | 复制 xLua 到项目：`Assets/XLua`（C# 源码）+ `Assets/Plugins`（各平台原生库）+ `Tools/`（注入工具，放项目根目录） |
| A2 | 添加宏 `HOTFIX_ENABLE`：`Edit → Project Settings → Player → Scripting Define Symbols`（做 hotfix 才需要；不做可跳过） |
| A3 | 配置 `GenConfig.cs`（Assets/00_Scripts/）：标记 Lua 高频调用的 C# 类型 `[LuaCallCSharp]`、委托 `[CSharpCallLua]` |
| A4 | 执行 `XLua → Generate Code` 生成包装代码 |

### 阶段 B：Lua 脚本打进 AB

1. **建目录**（放在会被 AB 打包的路径下）：

```
Assets/01_Project/AssetBundleAssets/Lua/
  ├─ Main.lua.bytes           ← Lua 入口（require 各模块）
  ├─ Game/Player.lua.bytes    ← 业务模块示例
  └─ Game/Battle.lua.bytes
```

2. **BuildSetting.xml 加打包项**：

```xml
<BuildItem
    AssetPath="Assets/01_Project/AssetBundleAssets/Lua"
    ResourceType="Direct"
    BundleType="File"
    Suffix=".bytes" />
```

3. **走现有打包流程**：Builder → 生成 Lua AB → 同步到本地服务器 + 更新 version.json。

> 注意：`.lua.bytes` 在 Unity 中是 **TextAsset**，`ResourceManager.GetAsset<TextAsset>()` 可直接取出。

### 阶段 C：LuaManager + 从 AB 加载的 Loader

```csharp
// LuaManager.cs —— 纯 C# 单例（参考 xluaPractice 项目）
public class LuaManager
{
    private static LuaManager _instance;
    public static LuaManager Instance => _instance ?? (_instance = new LuaManager());

    private LuaEnv _env;
    public LuaEnv Env => _env;

    private LuaManager()
    {
        _env = new LuaEnv();
        _env.AddLoader(LoaderFromAB);   // 关键：Loader 从 AB 加载
    }

    // ⚠️ 坑①：CustomLoader 是同步接口，必须用 async:false 同步加载
    private byte[] LoaderFromAB(ref string filePath)
    {
        // 路径规则必须与 Builder 打包路径严格一致（坑③）
        string url = "Assets/01_Project/AssetBundleAssets/Lua/" + filePath + ".lua.bytes";
        IResource resource = null;
        ResourceManager.instance.LoadWithCallback(url, false, r => resource = r);

        TextAsset text = resource?.GetAsset<TextAsset>();
        return text == null ? null : text.bytes;
    }

    public object[] DoString(string chunk, string chunkName = "chunk")
        => _env.DoString(chunk, chunkName);

    public object[] DoFile(string filePath)
        => _env.DoString($"return require '{filePath}'", $"DoFile:{filePath}");

    public T GetGlobal<T>(string name) => _env.Global.Get<T>(name);
    public void SetGlobal(string name, object value) => _env.Global.Set(name, value);

    public void Dispose()
    {
        if (_env == null) return;
        _env.Dispose();
        _env = null;
        _instance = null;
    }
}
```

### 阶段 D：启动顺序接线（唯一要动现有框架的地方）

**Lua 必须等 AB 初始化完成后才能启动**（否则 Loader 读不到 AB）。

方案：新增 `LuaBootstrap.cs`，由 `AssetBundleRuntime.Initialize()` 末尾触发。

```csharp
// AssetBundleRuntime.cs —— 在 Initialize() 最后加一行
private void Initialize()
{
    // ... 现有初始化逻辑 ...
    IsInitialized = true;
    LuaBootstrap.Init();   // ← 新增：AB 就绪后启动 Lua
}
```

```csharp
// LuaBootstrap.cs
public static class LuaBootstrap
{
    public static void Init()
    {
        // ① 清理缓存（如果之前加载过）
        // ② 启动 Lua 入口
        LuaManager.Instance.DoString("require 'Main'");
    }
}
```

### 阶段 E：逻辑热更验证（闭环 Demo）

```
1. 写一个 Lua 模块（如 Game/Battle.lua.bytes），打印一条逻辑
2. 打包 → 更新服务器 version.json（版本号 +1）
3. 启动客户端 → CheckAndUpdate 下载新版本 → require Main → 看到 Lua 输出
4. 修改 Lua 逻辑（如把伤害公式 1.5 → 2.0）
5. 重新打包 → 更新服务器（版本号 +2）
6. 重启客户端 → 逻辑变化 → 逻辑热更成功 ✅
```

与特效热更唯一区别：热更的是 TextAsset（代码），不是 prefab。**框架完全复用**。

---

## 四、关键坑清单（务必注意）

| # | 坑 | 说明 | 对策 |
|---|----|------|------|
| ① | Loader 同步性 | xLua CustomLoader 是**同步** `byte[]` 接口，不能异步回调 | 用 `LoadWithCallback(..., async: false)` |
| ② | 启动顺序 | Lua 必须在 AB Initialize **之后**创建 | LuaBootstrap 挂在 Initialize 末尾 |
| ③ | 路径一致性 | ResourceManager url 必须与 Builder 打包路径严格一致（含 `Assets/` 前缀） | 统一常量管理路径 |
| ④ | 版本缓存 | 热更后 `package.loaded` 不清 → 旧逻辑不生效 | `package.loaded["模块"] = nil` 后重新 require |
| ⑤ | 脚本后缀 | `.lua` 不被 Unity 识别，必须 `.lua.bytes` | 打包/加载都用 `.bytes` |

---

## 五、后续可扩展（可选）

- **Hotfix 兜底**：给少数关键 C# 类加 `[Hotfix]`，线上用 `xlua.hotfix` 补丁紧急修 Bug（需阶段 A2 的宏）
- **加密**：Lua bytes 打包前异或/AES 加密，Loader 里解密（防反编译）
- **AB 内 Lua 与资源配置分离**：Lua 单独一个 bundle，减少逻辑热更的下载体积

---

## 六、实施工作量评估

| 步骤 | 工作量 | 说明 |
|------|--------|------|
| 导入 xLua | 小 | 复制 + 宏 + GenConfig + Generate Code |
| Lua 进 AB | 小 | 建目录 + BuildSetting 加一项 + 打包 |
| LuaManager + Loader | 小 | 一个类，参考已有代码 |
| 启动接线 | 极小 | AssetBundleRuntime 加一行 |
| 热更验证 | 中 | 走一遍"打包→传服务器→下载→生效"闭环 |

**总计：半天到一天可完成主流程。**
