# FpsDemo

Unity 练习项目：目标为**简单多人死斗 FPS**；当前按阶段推进，先做**单人本地**移动与玩法原型。

---

## 项目目标（长期）

- 快节奏、偏爽感位移（非 CS 式急停、不追求竞技级站定射击）。
- 移动原型阶段完成后，再接入网络与死斗规则。
- 未来支持**自定义改键**（计划迁移至 Unity **新 Input System**）；原型期使用 **Legacy Input Manager**。

---

## 当前技术约定

| 项 | 说明 |
|----|------|
| 角色移动 | `CharacterController` |
| 输入（原型） | 旧 Input Manager；读输入尽量集中在少数入口，便于后续替换 |
| 输入（未来） | 新 Input System + Actions，用于改键与多设备 |
| 网络 | **第一阶段不考虑**；单机验证玩法与手感 |
| 战斗射线 | **Hitscan**；`Physics.RaycastAll`，**`QueryTriggerInteraction.Collide`**，角色 **Hitbox 可用 Is Trigger** |
| 部位伤害 | 武器只填 **基础伤害**（`HitscanWeaponConfig`）；最终伤害 = 基础 × **部位倍率**。Hitbox 挂 **`HitboxBodyRegion`**（头 / 上身 / 四肢）；倍率可选 **`BodyDamageMultiplierConfig`**（菜单 *Create → FpsDemo → Data → Body Damage Multiplier Config*），未拖则用内建 **头 2 / 上身 1 / 四肢 0.7** |
| Layer | 角色可在 **Player** 层；武器射线默认 **包含** Player，**自伤**由脚本按 **根物体** 跳过；环境/靶子常用 **Default** 等 |
| 统一复活点 | 场景里<strong>一个</strong> **`MatchSpawnPoints`**（随机 + 共用冷却 + XZ 近身避让存活参赛者）；**`PlayerDeathRespawn`** / **`FpsTestDummyEnemy`** 拖同一引用。参战单位宜同挂 **`MatchParticipant`** 以便近身判定。玩家还可选 **`Fallback Respawn Point`**；假人无全局点时仍可用圆内随机 |

---

## 操作与输入（设计定稿）

### 鼠标

- **默认锁定光标**（FPS 常规）。
- **反转 Y 轴**：默认**关闭**（设置里可开）。

### 键盘（原型期键位，最终以实现为准）

| 动作 | 键位 |
|------|------|
| 移动 | WASD |
| 跳跃 | Space |
| 疾跑 | Shift |
| 蹲 | Ctrl（**按住**，非切换） |
| 交互（上梯等） | F |
| 开火 | 鼠标左键（默认；`FpsInput` 可改键） |
| 瞄准（ADS） | 鼠标右键（默认；`FpsInput` 的 **Aim Mouse Button**） |
| 换弹 | R（默认） |
| 武器槽 0 / 1（多配置时） | **1** / **2**（主键盘数字，默认 `Alpha1` / `Alpha2`，见 `FpsInput`） |

### HUD（弹药）

- **`AmmoHub`**（`Scripts/UI/`）：挂在 **弹药 UI 根物体**（与 **TMP** 同物体或父级），拖 **`FpsHitscanWeapon`**、**TMP_Text**；格式默认 **`{0} / {1}`**（弹匣 / 备弹）。**Crosshair** 用 **Image** 与 **Ammo** 并列于 **Canvas** 下即可，不必挂在 `AmmoHub` 上。

### HUD（准星命中反馈）

- **`FpsCrosshairHitFeedback`**：挂在 **Crosshair 的 `Image`** 上（或拖 **`Image`**），拖 **`FpsHitscanWeapon`**；订阅 **`ShotHitDamageable`**，仅当 **`true`**（命中 **IDamageable**）时短时变色，可调 **Normal / Hit** 色与 **闪持续秒数**。

### Ctrl：蹲与滑铲

- **按住 Ctrl** 且**不在**「可滑铲窗口」内 → **蹲**（胶囊与相机高度仍按蹲；**蹲移速倍率仅贴地生效**，空中不按蹲把水平速度往低走，便于滑铲跳等保留惯性）。
- **可滑铲窗口**：**正按着 Shift 疾跑**，或 **刚松开 Shift 后的短时间内**（宽限秒数见 `FpsPlayerMotor` 的 `Sprint Slide Grace Seconds`）。
- 窗口内 **本帧按下 Ctrl**、贴地、冷却结束 → **滑铲**。

### 滑铲条件

- **贴地**、冷却结束、**本帧按下 Ctrl**、且（**按住疾跑** 或 **疾跑宽限计时未归零**）。

### 滑铲跳

- **滑铲过程中按 Space**：立刻结束滑铲并恢复站立，沿滑铲方向水平速度为 **滑铲速度 × Slide Jump Horizontal Multiplier**（默认 1，与滑铲同速）；**连跳计数 +1**（与站立起跳同一套惩罚）。另可调 `Slide Jump Up Velocity`。

### 梯子（第二阶段）

- **靠近梯子 + 按下 F** 上梯；再按 **F** 下梯，**Space** 跳离。
- 在梯子上 **W / S** 沿梯子 **up** 上下，**A/D** 侧移（见 `FpsLadder`）。

---

## 移动与手感规则

### 基础

- **无落地硬直**。
- **不做**「像 CS 那样的急停」；保留一定惯性，偏爽感。

### 连跳惩罚

- 连续起跳时：**跳多了跳不高、跳不远**（衰减高度与水平起跳量）。
- **重置条件**：仅在**落地后极短时间内**视为可重置连跳计数（偏严，抑制无限连跳）；具体毫秒数实现时调参。

---

## 分阶段范围

### 第一阶段（已完成）

- 单人、本地、无网络。
- **基础第一人称**：视角、WASD、跳跃、蹲（按住）、疾跑、连跳惩罚。
- **玩法与移动原型均在 `DeathMatch` 场景验证**；`Lobby` 预留给日后**取名、接网络、进房**等流程。

### 第二阶段（已实现）

- **滑铲**：疾跑或疾跑宽限内 **本帧按下 Ctrl**；否则 **按住 Ctrl** 为蹲；滑铲结束有短冷却。水平移动仅 **一个 Acceleration**。
- **梯子**：进入 **Trigger** 后按 **F** 上梯，再按 **F** 下梯，**Space** 朝前跳离；**W/S** 沿梯子 **`Transform.up`** 上下，**A/D** 沿梯面左右微移。梯子物体挂 **`FpsLadder`**，**Collider 勾选 Is Trigger**，旋转使 **up** 指向梯顶。

### 第三阶段（战斗与 HUD，已实现）

- **Hitscan 武器**：数值在 **`HitscanWeaponConfig`**（ScriptableObject；示例 **`Hitscan_Rifle_Standard`**、**`Hitscan_Pistol_Standard`**）；`FpsHitscanWeapon` 拖 **`_configs`**（多份 = 多槽位），**`1` / `2`** 切槽，**每槽独立弹药**；可选 **`_weaponVisualRoots`** 与槽位下标对齐，切枪 **SetActive** 模型。从 **MainCamera** 射线；**`LayerMask`** 未勾选时用 **`Physics.DefaultRaycastLayers`**（含 Player），**`RaycastAll`** 由近到远解析，**同一根物体**不扣血。**弹匣 / 备弹 / 换弹**；连发射速可配。
- **伤害**：`IDamageable` + **`Health`**（每次扣血 **`Damaged`**；致命时 **`Died`** + **`CombatKillBus`**）。可受伤物体挂 Collider + `Health`，默认死亡 **`Destroy`**）。
- **弹药 HUD**：`AmmoHub` 读武器公开属性写入 **TextMeshPro**；**`--UI--` → GamePlayCanvas → Canvas** 下与 **Crosshair** 并列；**Canvas Scaler** 建议 **Scale With Screen Size**；弹药 **RectTransform** 锚 **右下角** 以适配分辨率。
- **准星命中**：`FpsCrosshairHitFeedback` 订阅 **`ShotHitDamageable`**（仅命中可受伤目标为 `true`）。
- **本地受击全屏红闪**：**`FpsPlayerHurtOverlayFeedback`** 挂在**全屏拉伸**的 **`Image`** 上（**Raycast Target** 关）；**Player Health** 可空（运行时解析本地 **`MatchParticipant`**）；**Canvas Sort Order** 建议低于 **`PlayerDeathRespawn`** 的死亡灰幕。
- **音效**：**`FpsHitscanWeapon`** 发 C# 事件 **`ShotFired`** / **`ReloadStarted`** / **`DryFire`**（弹匣空且本帧按下开火，见武器）；**`FpsWeaponAudioObserver`** 订阅并拖 **`AudioClip`**（含可选 **空枪**），经单例 **`AudioManager.PlayOneShot2D`** 播放。**人机（世界空间枪声）**：同物体挂 **`AudioSource`**（**Spatial Blend = 1**）+ **`FpsAiWeaponSpatialAudio`**，拖 **`FpsAiHitscanWeapon`**、**`AudioClip`**（开火/换弹可选），在音源上 **`PlayOneShot`**，不走路由 **`AudioManager`** 2D。**命中（人/墙）**：**`FpsHitscanSurfaceAudioFeedback`** 订阅 **`ShotResolved`**，拖 **`_hitDamageableClip`** / **`_hitWorldClip`**（仅 Player；人机命中音未接）。

### 近期计划（未实现）

- **玩法闭环**：**参战者 + 局内规则**（**`MatchParticipant`** / **`MatchManager`**）已实现；待接 **HUD**、**玩家血量与区域复活**、正式结算美术。
- **打击与可读性**：命中/受击 **VFX 或贴花**（可订阅 **`ShotResolved`** / 武器已有事件）；受击音效仍走 **`AudioManager`** 与观察者模式。
- **关卡与复活**：**`PlayerDeathRespawn`** 拖 **`MatchSpawnPoints`**（随机其一）；可选单点 **`Fallback Respawn Point`**；两者皆无时会在控制台警告且**不移动**。
- **输入**：原型稳定后迁移 **新 Input System**、自定义键位。
- **网络**：单机循环与规则清晰后再接（与 README「第一阶段不考虑网络」一致）。

### 死斗单机第一版（需求对齐，待实现）

**布局参考**：《无畏契约》死斗模式——**中部顶栏**为时间与排行，**右上**击杀播报，**左下**血量，**左上**预留小地图；与现有 **弹药（右下）**、**准星（中）** 同屏共存。

| 区域 | 第一版内容 |
|------|------------|
| **顶部（中）** | **剩余时间**（倒计时）；**实时击杀排行**——**真人 + 场上所有人机**均视为独立参战者，同榜显示（为日后网络同步预留数据口径）。排行具体样式（头像/条）可先简化，框架与瓦一致。 |
| **右上** | **击杀播报**：**击杀者名字 → 被击杀者名字**（**第一版不显示武器**）。**自己参与**的条目需**视觉区分**。最多 **5** 条；单条停留时间 **可配置**（Inspector），便于测试调参。 |
| **左下** | **玩家血量**：先做**简单血条**（与弹药、准星同级，后续可换美术资源）。受伤/死亡等复杂 UI 切换**暂不列入第一版**。 |
| **左上** | **预留**小地图区域（可先空或占位框）。 |

**规则与流程（第一版）**

- **胜利**：倒计时 + **目标击杀数**；时间到若无人达标则**比击杀数**，**允许平局**（与先前共识一致）。
- **有人达到目标击杀**：**先播报数秒**，再**结束本局**（播报时长可配置）。
- **结算**：正式美术未就绪时先用**占位 UI**（文案/按钮可后续替换）。结算显示时 **暂停玩法**（`Time.timeScale = 0`），仅可操作结算 UI；**UI 与计时**用 **Unscaled Time**，避免与玩法一起冻结。
- **质感迭代（未做）**：日后可将结算/终局由「全停」改为 **全场景慢动作**（`timeScale` 为 **0～1** 可配）；需注意 UI/音效仍建议与 **unscaled** 或单独管线配合。
- **玩家死亡**：**不销毁**玩家物体，进入死亡状态后按复活逻辑处理（与 `Health` 配置配合，避免 `Destroy` 玩家）。**复活位置**：**区域内随机**（与假人类似，可配置中心/半径或独立复活区）。

---

## 场景说明

| 场景 | 用途 |
|------|------|
| `Assets/01_Project/Scenes/Lobby.unity` | **日后**：玩家名、匹配/网络、进房等大厅流程；**当前阶段**不承载玩法验证。 |
| `Assets/01_Project/Scenes/DeathMatch.unity` | **死斗与全部玩法**：移动、战斗、规则等均在本地于此场景开发与试玩。 |

> 若场景职责有变更，请在本表与「变更记录」中同步更新。

---

## 文档维护约定

- **本 README 为设计与范围的主文档**：实现、键位、分阶段、规则有变更时，**应同步更新本文**。
- 大改动建议在文末 **变更记录** 中写一行摘要（日期 + 说明）。

### 全局对象与 DontDestroyOnLoad（约定）

- **不要**在「非场景根」的管理器脚本里写 **`DontDestroyOnLoad(gameObject)`**（子物体会触发警告，且不符合 Unity 对根物体的要求）。
- **做法**：在 **Hierarchy 顶层** 空物体（如 **`--DDOL--`**）上挂 **`DontDestroyThisRoot`**；**AudioManager**、日后其它 **全局单例** 一律作为该根的 **子物体**（或单独再建一个顶层根 + 同一组件），**管理器脚本内不再写 DDOL 语句**。
- 新增全局对象时优先复用同一 DDOL 根，避免多处各写一遍 DDOL。

---

## 变更记录

| 日期 | 说明 |
|------|------|
| 2026-04-18 | **`NetworkKillFeedBroadcaster`**（场景物体 + **`NetworkObject`**）：服务器订阅 **`CombatKillBus`** → **`ClientRpc`** 播报；**`DeathmatchHudView`** 联机时忽略总线插入、用 **`AppendKillFeedFromNetwork`**（含 Host） |
| 2026-04-18 | **`PlayerHitscanNetBridge`**（Player 根）：Owner **`ServerRpc`** 提交射线，**`FpsHitscanWeapon.ServerResolveShot`** 仅在服务器扣血；联机时本机再 **`Resolve(applyDamage:false)`** 做弹孔等表现；未联网或无桥接时武器行为同单机 |
| 2026-04-18 | **`NetworkHealthBridge`**（Player 根）：服务器 `NetworkVariable` 同步血量，客户端 `Health.ApplyMirrorFromNetwork`；**`PlayerDeathRespawn`** 复活后 **`NotifyLocalReviveAfterDeath`**；**`DeathmatchHudView`** 每帧 **`TryResolvePlayerHealth`**；编辑器/Development 主机按 **F9** 仅 **`IsOwner`** 测扣血（避免 Host 上给全场玩家扣血） |
| 2026-04-18 | **`HitscanWeaponAmmoSync`**（Player 根，与 **`PlayerHitscanNetBridge`** 同挂）：服务器 **`NetworkVariable`** 同步最多 4 槽弹匣/备弹；**`SubmitHitscanShotServerRpc`** 先 **`ServerTryConsumeRound`** 再 **`ServerResolveShot`**；换弹结束 **`RequestApplyReload`**、复活重置 **`RequestResetAmmoFromOwner`**；**`FpsHitscanWeapon`** 联机时不再本地扣弹 |
| 2026-04-18 | **`AmmoHub`**：`_weapon` 可空，运行时按 **`MatchParticipant.IsLocalPlayer`** 解析武器（场景未拖引用时右下角弹药仍会更新；此前 `_weapon` 为空则整段不刷新、只显示 TMP 默认字） |
| 2026-04-08 | **`FpsThirdPersonLocomotionAnimator`**：**`IsGrounded`** / **`VerticalSpeed`**；**`FpsPlayerMotor`**：**`VerticalVelocity`**；**`speed`** / **`IsCrouch`** / **`IsAiming`** 同前；**Third Person Root** 隐藏网格 |
| 2026-04-08 | **`FpsAdsWorldFov`**：主相机开镜 **FOV** 平滑过渡（**`FpsInput.AimHeld`**）；**`Hip Fov`=0** 时 **Start** 读取当前相机；勿挂手臂相机 |
| 2026-04-08 | **`FpsWeaponViewModelAnimator`**：**`Aiming`** 默认脚本插值（**`Smooth Aiming Parameter`**），避免混合树单帧 0/1 硬切；可调 **Blend In/Out Speed** |
| 2026-04-08 | **`FpsInput`**：**`AimHeld`**（默认鼠标右键，可改 **`FpsMouseButton`**）；**`FpsWeaponViewModelAnimator`**：同步 Infima **`Aim`** / **`Aiming`**（手臂），武器模型 **`Aiming`**；瞄准时关 **`Running`** 姿势 |
| 2026-04-08 | **`FpsWeaponViewModelAnimator`**：手臂 **`Running`** ← **`FpsPlayerMotor.ShouldDriveArmsSprintRunningPose`**（贴地、正常模式、**Shift + WASD 有移动**；非滑铲/爬梯）；**`FireHeld`** 关 **`Running`**；**`LateUpdate`** 写入（晚于 **`FpsPlayerMotor`** 本帧移动） |
| 2026-04-08 | **`FpsPlayerMotor`**：**开镜（AimHeld）** 时目标速度为 **走路**（可选关）；**`ShouldDriveArmsSprintRunningPose`** 同步为假；第三人称 **`speed`** 随 **`HorizontalSpeed`** 落在走路区间 |
| 2026-04-08 | **第一人称视图动画**：**`FpsWeaponViewModelAnimator`** 直接订阅 **`FpsHitscanWeapon`**（**`CrossFade` 辅助逻辑** 内嵌于本类）；**删除**旧桥接 / Infima 接收器；**移除** **`IWeaponViewModelAnimationSource`** |
| 2026-04-08 | **`FpsHitscanWeapon`**：可选 **`DryFire`**（弹匣空 + 本帧按下开火）、**`WeaponSlotChanged(int)`**（切槽 / 复活回槽 0）；**`_emitDryFireWhenEmpty`**；换弹开始后同帧不触发空枪 |
| 2026-04-08 | **连杀链路**：**`KillStreakTracker`**（Player 根，**`CombatKillBus`** + 时间窗）→ **`StreakChanged`**；**`KillStreakAudioFeedback`** 播音；**`KillStreakHudPlaceholder`**（`DeathmatchHUD` + **`KillStreakPlaceholder`** **`TMP_Text`**） |
| 2026-04-08 | **`FpsHitscanSurfaceAudioFeedback`**（仅 Player）：`ShotResolved` + `HasWorldHit`；`HitDamageable` → `S_WEP_Impact_Bullet_02`，否则 `S_WEP_Impact_Bullet_01` |
| 2026-04-08 | **`HitscanImpactVfxFeedback`**：订阅 `ShotResolved`；`HitDamageable` 用 `VFX_Blood_01`，否则 `VFX_Classic_03`（WALLCOEUR 包）；挂 **Player** 与 **Bot1** |
| 2026-04-08 | 回退：移除 **`PlayerHitscanVfxFeedback`** / **`AiHitscanVfxFeedback`**（射击命中/枪口特效脚本与场景挂载），武器逻辑仍为 `FpsHitscanWeapon` / `FpsAiHitscanWeapon` |
| 2026-04-08 | 结算时解锁光标；`FpsPlayerLook` 在对局已结束时不左键重锁光标、不转视角，避免 Again 等 UI 点击被 FPS 光标逻辑吞掉 |
| 2026-04-08 | 初版：移动方案、输入路线、Ctrl/梯子/滑铲/连跳/分阶段与 README 维护约定 |
| 2026-04-08 | 蹲为按住；Lobby 仅预留网络/大厅，玩法与原型均在 DeathMatch |
| 2026-04-08 | 新增 `FpsInput` / `FpsPlayerLook` / `FpsPlayerMotor` 第一阶段脚本与 README 挂载说明 |
| 2026-04-08 | 角色碰撞与相机默认缩小 15%（×0.85），与 `DeathMatch` Player 对齐 |
| 2026-04-08 | Player 胶囊半径 0.5m、占位网格 XZ 不再与 Y 同比缩小，减轻过瘦感 |
| 2026-04-08 | 胶囊半径改为 0.35m（偏窄），避免明显宽于门框 |
| 2026-04-08 | 第二阶段：`FpsPlayerMotor` 滑铲与梯子；`FpsLadder`；`FpsInput` 增加 F 与蹲按下沿 |
| 2026-04-08 | 滑铲改为按水平速度 + 单按 Ctrl，不再要求 Shift 与疾跑计时 |
| 2026-04-08 | 水平移动「加速」与「减速」分参（`Acceleration` / `Deceleration`） |
| 2026-04-08 | 恢复单一 `Acceleration`；滑铲改为疾跑或松 Shift 后宽限时间内按 Ctrl |
| 2026-04-08 | 滑铲跳：结束滑铲起跳；水平为 **滑铲速度 × Slide Jump Horizontal Multiplier**（默认 1，与滑铲同速）+ 垂直 `Slide Jump Up Velocity` |
| 2026-04-08 | 蹲伏移速倍率仅 **贴地** 应用；空中不按蹲减速（胶囊仍可蹲姿） |
| 2026-04-08 | `FpsInput` 增加 **开火**（默认鼠标左键，枚举 `FpsMouseButton`）与 **换弹**（默认 R） |
| 2026-04-08 | 新增 `Combat`：`IDamageable`、`Health`（测试靶：场景里放 Cube + Collider + `Health`） |
| 2026-04-08 | 新增 `FpsHitscanWeapon`：相机射线、`LayerMask`、射速/弹匣/备弹/换弹 |
| 2026-04-08 | **`FpsHitscanWeapon`**：**`LayerMask` 默认含 Player**；**`RaycastAll`** 由近到远，**同根**不扣血（人机/他人可受伤） |
| 2026-04-08 | **`HitscanShotResolver`** + **`FpsAiHitscanWeapon`**（人机专用武器，与玩家武器分离）；**`FpsAiHitscanShooter`** |
| 2026-04-08 | **`HitscanShotResolver`**：`Health` 挂在子物体时父链找不到 `IDamageable`，改为根上 **`GetComponentInChildren`** 回退 |
| 2026-04-08 | **`HitscanShotResolver`**：自伤判断改为 **同一 `Health` 引用**，不再用 **`transform.root`**（同场景 `--Gameplay--` 父节点会导致 PvE 双方都不受伤） |
| 2026-04-08 | **`FpsHitscanWeapon.ResetAmmoToConfigDefaults`**；**`PlayerDeathRespawn`** 复活后恢复弹匣/备弹与槽位 |
| 2026-04-08 | 新增 `AmmoHub`：读武器弹药写入 **TMP** 文本（`Scripts/UI/`） |
| 2026-04-08 | README 对齐（第三阶段、Layer/UI）；音效改为 **`AudioManager`** 单例 + **`FpsHitscanWeapon`** 仅 **Clip** |
| 2026-04-08 | 武器 **`ShotFired` / `ReloadStarted`** 事件；**`FpsWeaponAudioObserver`** 订阅并播音；README 通讯图 |
| 2026-04-08 | **`AudioManager`** 移除 DDOL；新增 **`DontDestroyThisRoot`** 挂在场景根（符合 Unity 根物体要求） |
| 2026-04-08 | README **约定**：全局对象不在管理器内写 DDOL，统一顶层根 + **`DontDestroyThisRoot`** |
| 2026-04-08 | **`DontDestroyThisRoot`** 从 `Audio/` 移至 **`Scripts/Core/`**，命名空间 **`FpsDemo.Core`** |
| 2026-04-08 | **`ShotHitDamageable`** 事件；**`FpsCrosshairHitFeedback`** 准星命中变色 |
| 2026-04-08 | **`HitscanWeaponConfig`**（ScriptableObject）+ **`Data/Weapons/Hitscan_Rifle_Standard`**；**`FpsHitscanWeapon`** 数值改由 **`_config`** 提供 |
| 2026-04-08 | 命名：`HitscanWeaponStats`→**`HitscanWeaponConfig`**；默认资产 **`Hitscan_Rifle_Standard`**；字段 **`_config`** |
| 2026-04-08 | **`FpsHitscanWeapon`**：**`_configs`** 多槽 + **`1`/`2`** 切枪、每槽弹药；**`Hitscan_Pistol_Standard`**；**`FpsInput`** 增加切槽键 |
| 2026-04-08 | **`HitscanWeaponConfig`** 移至 **`Scripts/Data/`**，命名空间 **`FpsDemo.Data`**；Create 菜单 **FpsDemo → Data** |
| 2026-04-08 | 后座：**`HitscanWeaponConfig`**（`Recoil*`）、**`FpsRecoilController`**、场景 **`RecoilPivot`**；**`ShotFired`** 在 **`ShotResolved`** 之后；**`ShotHitInfo`** |
| 2026-04-08 | 撤销场景 **`RecoilPivot`**：武器挂点恢复为 **`CameraPivot`** 下与 **`MainCamera`** 并列；**`FpsRecoilController`** 仅挂 **`MainCamera`**（只改脚本、不动武器层级） |
| 2026-04-08 | 测试：**`FpsTestDummyEnemy`**（游荡 + 随机复活，勿与 **`Health`** 同挂） |
| 2026-04-08 | **`FpsTestDummyEnemy`**：游荡 **`CapsuleCast`** 防穿墙；复活 **`Linecast`** 重试 |
| 2026-04-08 | README：**近期计划**（玩法闭环 → 命中反馈 → 关卡/复活点 → 新输入 → 网络） |
| 2026-04-08 | README：**死斗单机第一版** UI/规则对齐（顶栏时间+排行、右上播报、左下血条、左上预留；人机视同真人计榜） |
| 2026-04-08 | 死斗需求：**结算暂停**（`timeScale=0`，UI 用 unscaled）；**复活**为**区域内随机**；备注未来可改**慢动作**终局 |
| 2026-04-08 | **击杀归因**：**`KillReport`**、**`CombatKillBus`**；**`Health`** 发布；**`FpsTestDummyEnemy`** 改为依赖 **`Health`** |
| 2026-04-08 | **`IDamageable`** 合并为**单接口 + `ApplyDamage` 两则重载**；删除 **`IDamageableWithInstigator`** |
| 2026-04-08 | **`MatchParticipant`** + **`MatchManager`**（`CombatKillBus` 记分、倒计时、目标击杀、结算占位） |
| 2026-04-08 | **`DeathmatchHudView`**：时间、排行、击杀条（5 条）、左下血量（需 **`Health`**） |
| 2026-04-08 | 移除 Editor **DeathmatchHudBuilder**（HUD 已落场景后不再需要一键生成） |
| 2026-04-08 | **`PlayerDeathRespawn`**：死亡灰幕、禁玩法输入；复活以 **`MatchSpawnPoints`**（可选 **`Fallback Respawn Point`**）；**`FpsPlayerMotor.ResetStateForRespawn`**；**`FpsInput.GameplayInputEnabled`** |

---

## 音效与事件

### 挂载

- **`AudioManager`**（单例）：可放在 **`--DDOL--`** 子物体上；拖 **`AudioSource`** → **`_oneShot2D`**，**Spatial Blend = 0**，**Play On Awake** 关。  
- **`DontDestroyOnLoad`**：Unity 要求只对 **Hierarchy 无父物体的根** 调用；**不要**在 `AudioManager` 子物体上 DDOL。在 **`--DDOL--`**（须为**顶层**空物体）上加 **`DontDestroyThisRoot`**，整棵子树（含 `AudioManager`）会随根保留。  
- **`FpsWeaponAudioObserver`**（与 **`FpsHitscanWeapon` 同物体** 或同 **Player**）：拖 **`FpsHitscanWeapon`**（空则同物体 **`GetComponent`**）、**开火/换弹/空枪 Clip**。未挂观察者则**无枪声**（武器仍正常射击）。  
- **`KillStreakTracker`**（**本地 `Player` 根**）：维护连杀数；订阅 **`CombatKillBus`**；**`Streak Window Seconds`**（**unscaled**）；死亡 / **`MatchEnded`** / 超时清零；**`StreakChanged(int)`** 供音效与 UI。**`KillStreakAudioFeedback`**（同物体）：订阅 **`StreakChanged`**，拖 **5 条** `AudioClip`（单杀→五杀），经 **`AudioManager.PlayOneShot2D`**。**`KillStreakHudPlaceholder`**（如 **`DeathmatchHUD`**）：拖 **`TMP_Text`**，格式 **`Format`** 默认「连杀 x{0}」，**`Tracker`** 可空（用 **`KillStreakTracker.Local`**）。  
- **以后**：脚步声、UI 等可再写 **观察者** 或继续调用 **`AudioManager`**。

### 通讯关系（观察者）

```
FpsInput → FpsHitscanWeapon（Update：扣弹、射线、换弹状态）
              ↓ 单发内顺序 Invoke
         ShotHitDamageable(bool) → ShotResolved(ShotHitInfo) → ShotFired
         ReloadStarted（换弹时）
              ↓ 订阅
FpsWeaponAudioObserver（ShotFired / ReloadStarted / DryFire）→ AudioManager.PlayOneShot2D(AudioClip)
FpsRecoilController（ShotFired）→ 读 ActiveConfig 累加后座，LateUpdate 恢复
FpsCrosshairHitFeedback（ShotHitDamageable）→ Image.color
              ↓
         弹孔/粒子等可订阅 ShotResolved（命中点、法线）
```

- **武器**：只发 **「发生了什么」**，**不**知道 Clip、**不**知道 `AudioManager`。  
- **观察者**：把事件 **映射成资源**，再交给 **`AudioManager`**；换音效只改观察者或 Clip。  
- **AmmoHub**：**不**走事件，直接 **每帧读武器公开属性**（与音频解耦）。  
- **后座**：**`FpsRecoilController`** 挂在 **`MainCamera`** 上，**`_recoilPivot`** 指向 **相机 Transform**；**`ShotFired`** 在 **射线与 `ShotResolved` 之后** 触发。  
- **层级**：`Player` → `CameraPivot`（`FpsPlayerLook`）→ **`MainCamera`** / **`WeaponSocket`**（**武器模型勿因后座脚本被挪层级**）。

### 战斗与击杀归因（数据流）

**职责**

| 组件 | 做什么 |
|------|--------|
| **`FpsHitscanWeapon`** | 射线命中后对 **`IDamageable`** 调用 **`ApplyDamage(伤害, 伤害来源)`**。伤害来源为**武器所在物体**（`DeathMatch` 中为 **`Player` 根**）。 |
| **`Health`** | 唯一扣血与死亡判定；实现 **`IDamageable`**（两则重载）；每次扣血后触发 **`Damaged(本次伤害, instigator)`**（含致死一击，先于 **`Died`**）；致死时再构造 **`KillReport`**，发布 **`CombatKillBus.KillCommitted`** 与 **`Died`**；可选 **`Destroy`**。 |
| **`CombatKillBus`** | 静态 **`KillCommitted`**：局内规则、击杀播报、排行榜等**只订阅此处**，不遍历场景找 `Health`。 |
| **`FpsTestDummyEnemy`** | **不**再实现受伤接口；**必须**同物体有 **`Health`**；只处理**游荡、死亡隐藏、区域内复活**，复活时调用 **`Health.ReviveFull()`**；若同物体有 **`FpsAiHitscanWeapon`**，再调用 **`RestoreStartingAmmo()`** 以免弹尽后无法开火。 |

**数据流（一发子弹打死目标）**

```
FpsHitscanWeapon.FireOnce
  → 命中 Collider → GetComponentInParent<IDamageable>
  → Health.ApplyDamage(数值, Player根)
  → 血量 ≤ 0 → KillReport(victim=本物体, killer=Player根)
  → CombatKillBus.Publish + Health.Died
  → （假人）FpsTestDummyEnemy 订阅 Died → 隐藏模型、启动复活协程
```

**不关心来源的物体**：实现 **`IDamageable`** 时，**带来源的重载**内转调 **`ApplyDamage(amount)`** 即可（武器始终调用两参版本）。

### 死斗局内规则（`Scripts/Match/`）

**职责**

| 组件 | 做什么 |
|------|--------|
| **`MatchParticipant`** | 挂在**参战单位根**（`Player`、每个人机根）；**显示名**、**是否本地玩家**；`OnEnable` 时进入静态列表 **`ActiveParticipants`**。 |
| **`MatchManager`** | **单例**（场景一个）；`Start` 用当前列表初始化击杀表；**订阅 `CombatKillBus`**：击杀者根上须有 **`MatchParticipant`** 才记分；**自杀**（`Killer==Victim`）不计；**倒计时**（`unscaled`）、**目标击杀**；有人达标 → **真实时间延迟** → **`timeScale=0`** → **`MatchEnded`** + 可选占位 UI；**时间到**按击杀比胜负、**允许并列**。 |

**数据流（击杀 → 记分）**

```
CombatKillBus.KillCommitted(KillReport)
  → MatchManager：Killer 上 GetComponent<MatchParticipant>
  → 有则对应参与者击杀 +1
  → 达目标击杀 或 时间归零 → MatchResult → 暂停 + 占位结算
```

**场景挂载**：在 **`DeathMatch`** 中空物体挂 **`MatchManager`**；**`Player` 与每个人机根**各挂 **`MatchParticipant`**（填显示名；本地玩家勾选 **`Is Local Player`**）。可选：拖 **占位 Panel + TMP_Text + Button** 到 **`MatchManager`** 的结算引用，**Restart** 调用 **`RestartMatch()`**（重载当前场景并恢复 **`timeScale`**）。

### 死斗 HUD（`DeathmatchHudView`）

搭建步骤：

1. 在 **`GamePlayCanvas`**（或主 Canvas）下建空物体 **`DeathmatchHUD`**，挂 **`DeathmatchHudView`**。  
2. **顶栏时间**：建 **TMP_Text**，锚点 **顶部居中**，拖入 **`Match Timer Text`**。  
3. **顶栏排行**：建 **TMP_Text**（多行、左对齐），锚点 **顶部偏左或全宽**，拖入 **`Leaderboard Text`**。  
4. **右上击杀条**：建 **5 个 TMP_Text**（竖排，上=new），按 **从上到下的顺序** 填入 **`Kill Feed Lines`** 数组（元素 0 = 最上一条）。  
5. **左下血条**：建 **Slider**（0～1）+ 可选血量数字 **TMP_Text**；**`Player` 需挂 `Health`** 才会更新（否则血条隐藏）。  
6. **`Kill Feed Hold Seconds`**：每条播报保留的**真实秒数**（`unscaled`）。**自己参与**的击杀用 **`Kill Feed Self Involved Color`** 高亮。  
7. **`Match Manager` / `Local Player Participant` / `Player Health`** 可留空，脚本会尝试 **`Instance`** 与 **`IsLocalPlayer`** 自动解析。

---

## 脚本说明

- `Assets/01_Project/Scripts/Fps/`：`FpsInput`、`FpsPlayerLook`、`FpsLadder`、**`FpsRecoilController`**（后座，一般挂 **Main Camera**）、**`FpsAdsWorldFov`**（主相机开镜 **FOV**）、**`FpsThirdPersonLocomotionAnimator`**（第三人称 **speed**）、**`FpsWeaponViewModelAnimator`**（订阅 **`FpsHitscanWeapon`**，内含 Layer/状态 **`CrossFade`**）、**`PlayerDeathRespawn`**（死亡灰幕、禁玩法输入；**`MatchSpawnPoints`** 随机复活）
- `Assets/01_Project/Scripts/Data/`：**`HitscanWeaponConfig`**（ScriptableObject 模板，与 `Assets/01_Project/Data/Weapons/` 下 `.asset` 对应）
- `Assets/01_Project/Scripts/Combat/`：`IDamageable`（**`ApplyDamage` 两则重载**）、**`KillReport`**、**`CombatKillBus`**、`Health`、`ShotHitInfo`、**`HitscanShotResolver`**、**`FpsHitscanWeapon`**、**`FpsTestDummyEnemy`**（测试：需同挂 **`Health`**，游荡 + 随机复活）
- `Assets/01_Project/Data/Weapons/`：**`Hitscan_Rifle_Standard`**、**`Hitscan_Pistol_Standard`** 等（**`Create → FpsDemo → Data → Hitscan Weapon Config`** 可再建）
- `Assets/01_Project/Scripts/UI/`：`AmmoHub`、`FpsCrosshairHitFeedback`、**`DeathmatchHudView`**（死斗：时间、排行、击杀条、血量）、**`KillStreakHudPlaceholder`**
- `Assets/01_Project/Scripts/Audio/`：`AudioManager`、`FpsWeaponAudioObserver`、`FpsHitscanSurfaceAudioFeedback`、**`KillStreakAudioFeedback`**
- `Assets/01_Project/Scripts/Core/`：`DontDestroyThisRoot`（通用：仅挂在场景根，用于 DDOL）
- `Assets/01_Project/Scripts/Match/`：**`MatchParticipant`**、**`MatchManager`**、**`MatchResult`** / **`MatchEndReason`**、**`KillStreakTracker`**
- `Assets/01_Project/Scripts/Ai/`：**`FpsAiHitscanWeapon`**（人机 Hitscan）、**`FpsAiHitscanShooter`**（行为：朝玩家开火）；联机可不挂
- `Assets/01_Project/Scripts/Fps/FpsPlayerMotor.cs`：`FpsPlayerMotor`（与 `Player` 同物体）

职责划分（便于以后换输入或加联机）：

| 脚本 | 职责 |
|------|------|
| `FpsInput` | Legacy 输入：移动轴、视角增量、跳/疾跑/蹲、**蹲按下沿**、**F 交互**、**开火**、**瞄准（按住）**、**换弹** |
| `FpsPlayerLook` | 身体 Yaw、`CameraPivot` Pitch、光标锁定 |
| `FpsPlayerMotor` | `CharacterController`：走跑蹲跳、**滑铲**、**梯子**、连跳惩罚、蹲/滑铲/站立时胶囊与相机高度；**开镜 / 按住开火时走路速**（Inspector 可关） |
| `FpsLadder` | 挂在梯子物体上（**Trigger 碰撞体**），玩家进入范围后由电机处理 **F** 攀爬 |
| `Health` | 可受伤物体：实现 **`IDamageable`**（单参转调两参）；致死发布 **`CombatKillBus`** 与 **`Died`**；**`ReviveFull` / `SetDestroyOnDeath`**；玩家复活流需 **关闭死亡 `Destroy`** |
| `PlayerDeathRespawn` | 与 **`Health`** 同挂 **`Player`**：仅 **`MatchParticipant.IsLocalPlayer`**（无则 **`Player` 标签**）才走全屏灰幕与复活；**勿**挂在假人上。订阅 **`Died`**；**`MatchSpawnPoints`**；可选 **`Fallback Respawn Point`**；**`Snap View To Spawn`** |
| `AudioManager` | 单例：拖 **`AudioSource`**（2D）；**`PlayOneShot2D`**；**不在此脚本上 DDOL** |
| `DontDestroyThisRoot`（`Core`） | 挂在**场景根**（如 **`--DDOL--`**），`Awake` 里 **`DontDestroyOnLoad`** 本物体；与音频无关 |
| `FpsWeaponAudioObserver` | 订阅 **`ShotFired` / `ReloadStarted` / `DryFire`**，拖 **Clip**，调 **`AudioManager`** |
| `FpsHitscanSurfaceAudioFeedback` | 仅 Player：订阅 **`ShotResolved`**，**可伤害体 / 环境** 各一 **Clip**，调 **`AudioManager`** |
| `HitscanWeaponConfig`（`Scripts/Data/`） | **ScriptableObject**：伤害、射速、弹药、射程、**后座**（`Recoil*`）；**勿在运行时改磁盘 asset**；菜单 **Create → FpsDemo → Data → Hitscan Weapon Config** |
| `FpsHitscanWeapon` | 玩家 Hitscan：拖 **`_configs`**、**`FpsInput`**、**`Main Camera`**；命中经 **`HitscanShotResolver`**；事件 **`ShotHitDamageable`** → **`ShotResolved`** → **`ShotFired`**；可选 **`DryFire`**、**`WeaponSlotChanged(int)`**；**`GetWeaponVisualRoot(int)`** |
| `FpsRecoilController` | 订阅 **`ShotFired`**，拖 **`FpsHitscanWeapon`**、**后座用 Transform**（一般为 **Main Camera**）；**`LateUpdate`** 恢复后座角 |
| `FpsWeaponViewModelAnimator` | 与 **`FpsHitscanWeapon` 同物体**：拖 **`FpsInput`**、**`FpsPlayerMotor`**（空则同物体 **`GetComponent`**）；**`Running`** ← **`ShouldDriveArmsSprintRunningPose`**；**`FireHeld` / `AimHeld` / `IsReloading`** 时关 **`Running`**（疾跑换弹只显换弹）；**`Aim` / `Aiming`** 同步 Infima 手臂与武器 **`Aiming`**；**`WeaponViewModelAnimRouting`**；切槽 **`RuntimeAnimatorController`** |
| `ShotHitInfo` | **readonly struct**：命中点、法线、是否可受伤等；**`ShotResolved`** 载荷 |
| `FpsCrosshairHitFeedback` | 准星：拖 **`Image`**、**`FpsHitscanWeapon`**；订阅 **`ShotHitDamageable`** |
| `FpsPlayerHurtOverlayFeedback` | **本地玩家**受伤：拖 **`Image`**、可选 **`Health`**；订阅 **`Health.Damaged`**，**`Peak` / `Fade`** 可调 |
| `AmmoHub` | 弹药 HUD：拖 **`FpsHitscanWeapon`**、**TMP_Text**；可改 **`_format`** 字符串 |
| `IDamageable` | **`ApplyDamage(float)`** 与 **`ApplyDamage(float, GameObject)`** 两则重载；不关心来源时第二则转调第一则 |
| `CombatKillBus` | 静态 **`KillCommitted(KillReport)`**，致死时由 **`Health`** 发布 |
| `FpsTestDummyEnemy` | **测试用**：**`[RequireComponent(Health)]`**；**`MatchSpawnPoints`** 开局/复活均在随机点，游荡圆心随落点更新，**复活后延迟一拍**再游荡（半径内置）；无全局点时圆内后备复活；**`CapsuleCast`**；订阅 **`Health.Died`** |
| `FpsAiHitscanWeapon`（`Ai/`） | 人机专用：拖 **`HitscanWeaponConfig`**、**`LayerMask`**；与 **`FpsHitscanWeapon`** 共用 **`HitscanShotResolver`** |
| `FpsAiHitscanShooter`（`Ai/`） | 单机 AI：拖 **`FpsAiHitscanWeapon`**、**`Aim Origin`**、**`Target`** 或 **`Player` Tag** |
| `MatchParticipant` | 参战者身份与显示名；进 **`MatchManager`** 记分表 |
| `MatchManager` | 订阅 **`CombatKillBus`**、倒计时与目标击杀、**`MatchEnded`**、占位结算、**`RestartMatch`** |
| `DeathmatchHudView` | 读 **`MatchManager`**、**`CombatKillBus`**、本地 **`Health`**；顶栏/右上/左下 HUD |
| `KillStreakTracker` | **本地 Player 根**：**`CombatKillBus`** + 时间窗；**`StreakChanged(int)`**；死亡 / **`MatchEnded`** / 超时清零 |
| `KillStreakAudioFeedback` | 同 **`Player`**：订阅 **`KillStreakTracker.StreakChanged`**，按档位 **`AudioManager.PlayOneShot2D`**（1～5） |
| `KillStreakHudPlaceholder` | **Canvas / `DeathmatchHUD`**：拖 **`TMP_Text`**，订阅 **`KillStreakTracker.Local`** 的 **`StreakChanged`** |

**挂载**：`FpsInput` / `FpsPlayerLook` / `FpsPlayerMotor` 均在 **`Player`** 上；梯子为场景内单独物体，加 **BoxCollider（Is Trigger）** + **`FpsLadder`**。`FpsHitscanWeapon` 挂在 **`Player` 根**（或空子物体）上，**不要**挂在电机上；**`_configs`** 填多份 **`HitscanWeaponConfig`**（如步枪 + 手枪）；**`1`/`2`** 切枪；若有两套武器模型，将根物体拖入 **`_weaponVisualRoots`** 与槽位对齐。另拖 **`FpsInput`**、**`MainCamera`**。第一人称手臂/枪骨骼：与 **`FpsHitscanWeapon` 同物体**加 **`FpsWeaponViewModelAnimator`**，拖手臂 **`Animator`**、每槽 **`RuntimeAnimatorController`**、**`WeaponViewModelAnimRouting`**（与所用 Controller 一致）。若动画片段含 **Animation Event**，需在 **`Animator`** 同物体上自行提供同名 **public** 方法或改资源。枪声：**`FpsWeaponAudioObserver`** + **`AudioManager`**。

- `FpsPlayerMotor`：拖入 **`FpsInput`**、**`CameraPivot`**；滑铲/梯子参数在 Inspector 可调；**Limit Speed To Walk While Aiming / While Firing** 默认开（开镜或按住开火时目标速度为走路，不按疾跑）。

**角色比例**：站立高约 **1.7m**（Y 缩放 **0.85**），半径 **0.35m**（XZ 缩放 **0.7**），略窄于常见门框，便于低模巷战场景通过门洞。`DeathMatch` 内 Player 已对齐。

执行顺序：`FpsInput`（-100）→ `FpsPlayerLook`（-50）→ `FpsHitscanWeapon`（-40）→ **`FpsWeaponViewModelAnimator`（-38）** → `FpsPlayerMotor`（0）。手臂 **`Running`** 在 **`FpsWeaponViewModelAnimator.LateUpdate`** 写入，故仍晚于本帧 **`FpsPlayerMotor.Update`**（与 **`isGrounded`** 对齐）。

---

## 运行方式

1. 使用 **Unity** 打开本仓库根目录（含 `Assets`、`ProjectSettings` 的工程文件夹）。
2. 打开对应场景，按 **Play** 运行。

（若需固定 Unity 版本或安装步骤，可在此补充。）
