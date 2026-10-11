# Starward Gacha — 独立抽卡记录应用（fork 说明）

本目录**不是**应用源码，而是这个 fork 的**全部增量**：构建脚本、同步脚本、诊断工具、备份与文档。
应用源码就是仓库根目录下的上游 Starward 源码树 + 我新增/挂钩的少量文件。

- 上游：<https://github.com/Scighost/Starward>（MIT）
- 快照基线：`main` @ 2026-10-08 的 `game management` 提交（见下文「上游已知破损」）
- 产物：`Starward.GachaOnly.exe`（同一份源码树也能编出原生 Starward：`-p:GachaOnly=false`）

---

## 1. 做法：屏蔽而非抽离

诉求是「把祈愿记录独立成一个应用，同时保证后续能持续合并上游的新功能/bug 修复」。
上游在 `Features/` 重构之后已经是**数据驱动的页面开关**架构：

```csharp
// Features/ViewHost/MainView.xaml.cs
NavigationViewItem_GachaLog.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GachaLogPage)).ToVisibility();
```

`CurrentGameFeatureConfig` 由 `Features/GameFeatureConfig.cs` 的 `FromGameId()` 按区服给出。
所以「只留抽卡」= 让这份 `SupportedPages` 只包含抽卡页 —— 一处配置级改动，而不是模块手术。
抽离成新工程需要切断 `Gacha → GameRecord / GameLauncher / ViewHost` 三条跨模块边（其中
`GameRecord` 有 1.3 万行，绝区零抽卡同步需要它的 Hoyolab 登录与角色列表），并且会把「合并上游」
变成「手工回放上游对 Gacha 的每一次改动」——与诉求二直接冲突。

**结论：屏蔽式方案，源码只增不改。**

---

## 2. 改动清单（这就是全部）

新增文件（上游永远不会碰，因此永不冲突）：

| 文件 | 作用 |
|---|---|
| `src/Starward/Features/GachaOnly/GachaOnlyProfile.cs` | 唯一开关 `GachaOnlyProfile.Enabled`（`#if GACHA_ONLY`）、默认落地页选择 `GetFallbackPage()` |
| `src/Starward/Features/GachaOnly/GameFeatureConfig.GachaOnly.cs` | `partial class GameFeatureConfig`：抽卡专区页面表 + 无抽卡游戏（崩坏3）的提示页配置 |
| `src/Starward/Features/GachaOnly/GachaOnlyDataFolder.cs` | 共用数据目录解析（见第 3 节） |
| `src/Starward/Features/GachaOnly/GachaOnlyUnsupportedPage.xaml(.cs)` | 「该游戏没有抽卡记录」提示页（中/日/英，不往 `Lang` 资源里加 key） |
| `src/Starward/Features/GachaOnly/GachaExportModels.cs` | 导出图片的数据模型 + 总览指标计算 + 分页 + 图标加载 |
| `src/Starward/Features/GachaOnly/GachaExportCard.xaml(.cs)` | 导出图片的卡片视觉（浅色、固定宽度、代码布局，见第 7 节） |
| `src/Starward/Features/GachaOnly/GachaExportWindow.xaml(.cs)` | 预览窗口 + 保存 PNG / 复制到剪贴板；分页渲染并拼接 |
| `src/Starward/Features/GachaOnly/GachaExportCli.cs` | 无头导出命令行 `--export-gacha-image` |
| `src/Starward.Core/HoYoPlay/HoYoPlayClientLegacyOverloads.cs` | 上游破损的兼容层，见第 6 节；上游修好后可整文件删除 |

上游文件各**一处**挂钩（每处 1~4 行，`GachaOnlyProfile.Enabled == false` 时行为与上游完全一致）：

| 文件 | 改动 |
|---|---|
| `Features/GameFeatureConfig.cs` | `return ApplyGachaOnlyProfile(config, gameId.GameBiz);` |
| `Features/ViewHost/MainView.xaml.cs` | ① `NavigateTo` 的两处 `typeof(GameLauncherPage)` 兜底 → `GachaOnlyProfile.GetFallbackPage(...)`；② 抽卡页在导航栏保持选中；③ 关闭上游更新检查 |
| `Features/ViewHost/MainWindow.xaml.cs` | 窗口可自由缩放 + 最大化（`presenter.IsResizable/IsMaximizable = GachaOnlyProfile.Enabled`；`Show()` 不再把窗口拉回 1200×676；`SC_MAXIMIZE` 只在 stock 构建里吞掉，见第 8 节）） |
| `Features/Gacha/GachaLogPage.xaml(.cs)` | 顶栏增加「导出为图片」「导出统计数量」两个按钮（同一样式，stock 构建下隐藏） |
| `App.xaml.cs` | ① `starward://` 不处理；② `starward://test/` 不处理；③ 跳过 `--hide` 托盘启动分支；④ 独立实例键 `StarwardGacha` + 窗口标题 `Starward Gacha`；⑤ 无头导出命令行 hook |
| `AppConfig.Configuration.cs` | `userDataFolder = GachaOnlyDataFolder.Resolve() ?? userDataFolder;` |
| `AppConfig.ServiceProvider.cs` | 日志文件名 `StarwardGacha_yyMMdd.log`（不与官方版抢同一个日志文件）+ 启动时记录数据目录 |
| `Features/UrlProtocol/UrlProtocolService.cs` | `RegisterProtocol()` 直接返回：不抢占 `starward://` |
| `Starward.csproj` | 文件末尾一个隔离块：`AssemblyName` / `Product` / `GACHA_ONLY` 常量 / 无 Windows SDK 时关闭 CsWinRT 与裁剪 |

**没有做**（为可合并性刻意不做）：改命名空间、移动/重命名 `Features/Gacha/**` 任何文件、改数据库
表名或 schema、往 `Starward.Language` 的 resx 里加 key、拆 `AppConfig` 分部类、换 DI 注册方式。

---

## 3. 数据目录：与现有 Starward 共用

解析顺序（第一个命中者生效，可诊断日志见 `%LOCALAPPDATA%\Starward\log\GachaOnly-data-folder.log`）：

1. 命令行 `--data-folder <path>` / `--data-folder=<path>`
2. 环境变量 `STARWARD_GACHA_DATA_FOLDER`
3. **exe 同目录的 `gacha-data.ini`**：`UserDataFolder=<path>`（推荐，`build.ps1 -DataFolder` 会生成）
4. 自动探测：`%LOCALAPPDATA%\Starward` → `<盘>:\Mihoyo\Starward` → `%LOCALAPPDATA%\MihoyoWishBook` → `<盘>:\Starward` → exe 所在目录（候选必须含 `StarwardDatabase.db`）
5. 都没有 → 回落到上游逻辑（注册表 / 便携 `config.ini` / 首次运行向导）

本机实测：`UserDataFolder=F:\Mihoyo\Starward`，与官方版 Starward 0.18.2 共用同一个
`StarwardDatabase.db`，抽卡记录直接可见。

共用带来的**相互影响**（都源于上游把设置也存进同一个 DB）：

- 设置项（`GachaLanguage`、`LastUid…`、`CloseWindowOption`、`CurrentGameBiz` …）是共享的，本应用改了官方版也会跟着变；
- 自动备份 `%LOCALAPPDATA%\Starward\DatabaseBackup\*.7z` 也是共享目录，两个应用都会往里写；
- 官方版与本应用可同时运行（SQLite WAL 支持多进程），但不要同时执行「导入/删除抽卡记录」这类写操作；
- 日志已分开（`StarwardGacha_*.log` vs `Starward_*.log`）。

想要完全隔离：把 `gacha-data.ini` 指到一个空目录即可（会新建自己的库）。

---

## 4. 构建

```powershell
# 正式打包（Release，x64，产物带 gacha-data.ini）
.\gacha-fork\build.ps1 -Version 0.1.0 -DataFolder 'F:\Mihoyo\Starward'

# 编原生 Starward（对照/排查用）
.\gacha-fork\build.ps1 -Stock

# 直接 dotnet（等价）
E:\A-Dev\deepseek-harness\.dotnet\dotnet.exe publish src\Starward\Starward.csproj `
    -c Release -r win-x64 -o out\StarwardGacha -p:Platform=x64 -p:GachaOnly=true -p:Version=0.1.0
```

环境要点（本机踩过）：

- **没有 Windows SDK / Visual Studio**：`CsWinRTGenerateProjection` / `CsWinRTAotOptimizerEnabled` 需要在
  隔离块里关掉（WinAppSDK 自带 C# projection）。机器上装了完整 SDK 时加 `-UseWindowsSdk`（即
  `-p:WindowsSdkAvailable=true`）恢复上游默认。
- **Release 打包必须关裁剪**：上游 `Properties/PublishProfiles/win-*.pubxml` 给 Release 开了
  `PublishTrimmed=True` + `PublishReadyToRun=True`，这两步依赖 CsWinRT AOT 优化器（同样要 Windows SDK）。
  缺 SDK 时照旧裁剪，程序会在 XAML 启动阶段以 `0xC000027B`（STATUS_STOWED_EXCEPTION）直接退出。
  `build.ps1` 因此在缺 SDK 时自动追加 `-p:PublishTrimmed=false -p:PublishReadyToRun=false`
  （产物约 270 MB / 937 文件，符合"不在意体积"的前提）；`Starward.csproj` 的隔离块里也写了同样的条件属性。
- .NET SDK 在 `E:\A-Dev\deepseek-harness\.dotnet`（`dotnet.exe` 默认不在 PATH）；
  项目 `global.json` 要求 `10.0.400`。
- 本机 GitHub 直连不通（`git clone` 超时），但 NuGet 与 `builds.dotnet.microsoft.com` 可用，
  所以 NuGet 还原与 SDK 安装正常；同步上游请在有 GitHub 网络的环境执行。

---

## 5. 与上游同步

```powershell
.\gacha-fork\sync-upstream.ps1          # fetch upstream/main 并 rebase
.\gacha-fork\sync-upstream.ps1 -Check   # 只预演，不改历史
.\gacha-fork\sync-upstream.ps1 -Merge   # 用 merge 代替 rebase
```

模型：fork 只有**一个**提交贴在 `upstream/main` 上面，冲突只可能出现在第 2 节表格里那 10 个文件、
且是 1~4 行级别。上游改 `Features/Gacha/**`（新卡池、新游戏、bug 修复）**零冲突**，直接继承。

`gacha-fork/gacha-only.patch`（`git am` 可用）与 `gacha-fork/gacha-only.diff`（纯 diff，便于审阅）
是同一份改动的两种导出，可应用到全新的上游克隆上：

```powershell
git clone https://github.com/Scighost/Starward.git && cd Starward
git am ..\gacha-fork\gacha-only.patch     # 或： git apply ..\gacha-fork\gacha-only.diff
```

---

## 6. 上游已知破损与兼容层（重要）

上游 `main` 的 `game management` 提交（`607a3077`，2026-10-08）把 `HoYoPlayClient` 的
「按启动器查询」重载移到了新的 `Starward.GameManagement` 项目，但 `Starward.RPC` 与
`Starward/Features/HoYoPlay/HoYoPlayService.cs` 仍在调用旧签名 —— **该提交上游 CI 就是红的**
（Actions run #2322，conclusion: failure），直接编这份快照必然失败（`GetGameInfoAsync` 等 19+ 处）。

处理方式：`src/Starward.Core/HoYoPlay/HoYoPlayClientLegacyOverloads.cs` 用**扩展方法**把这批旧重载补回来，
并且是**有实际语义**的实现：`LauncherId → LauncherConfig` 的映射在 Core 里是静态的
（`LauncherConfig.ChinaOfficial / GlobalOfficial / Bilibili*`），所以国服/国际服/B 服的游戏列表、
背景图、配置、分支都会打到各自正确的接口上，不是空壳。

上游修好后（恢复重载，或把 RPC/HoYoPlayService 迁到 `Starward.GameManagement`）：直接删除该文件即可
（实例方法优先于扩展方法，届时它只是死代码）。

---

## 7. 导出图片（独立应用新增）

入口：抽卡记录页**顶栏**两个按钮 —— **导出为图片** / **导出统计数量**（和「更新记录」「设置」同一行、同一
样式；stock 构建下隐藏）。两者都会打开预览窗口（可滚动查看整张卡片），底部工具栏提供 **保存为 PNG** 与
**复制到剪贴板**。

### 卡片结构

**一张图、一个页头、一个页脚，正文是双列瀑布（masonry）**：每个卡池是一整块（不可拆分），
按"事件池在前（抽数从多到少）、常驻池在最后"的顺序放进当前较矮的那一列 —— 原神是
角色池（左）/ 武器池（右上）/ 常驻池（右下），星铁是 角色活动（左）/ 光锥活动、角色联动、光锥联动（右）/
**群星跃迁（最下）**。常驻池靠"这一池有没有 UP 机制"来识别，不需要硬编码卡池类型数字。

- 顶部：游戏名 · UID · 记录区间 · 总抽数 · 卡池数 + 右上角游戏图标；
- 每个卡池块：标题 + 抽数 + 区间，数据总览指标，五星历史按年份分组；
- **五星标记**：左上角垫抽数按阈值配色 —— **1–10 黄、11–40 蓝、41–75 紫、76+ 红**（角色/武器/常驻一致）；
  右上角**只给 UP 的打绿色 UP 标签，歪的不做任何标注**（常驻池无 UP 概念，同样不标）；
- **指标 8 项（角色池与武器池一致）**：未出五星 / 五星平均 / 小保底不歪 / 最非 / 未出四星 / UP平均 /
  UP花费 / 最欧；常驻池没有 UP 机制，自动只显示 5 项；
- 指标宫格按数量选列数（5~6 个用 3 列、7~8 个用 4 列），避免"第二行只剩一个"；
- 只有 1 个卡池时自动退化为**单列**（每行 14 个图标，更矮）。

### 武器池的 UP 是怎么来的

上游的 `GachaNoUp` 表里只有 **hk4e301**，**没有 hk4e302**（星铁 11/12/21/22、绝区零 2/3 都有），
所以 Starward 本体在武器池上无法判断 UP，导出里原本连"小保底不歪"都出不来。我在
`GachaExportModels.cs` 里按同一机制补了一份**标准五星武器池**（天空之刃/天空之傲/天空之翼/天空之卷/
天空之脊/狼的末路/四风原典/阿莫斯之弓/和璞鸢/风鹰剑），该卡池里不在此列表的即为 UP。

- 该列表用本机数据验证过：武器池 26 个五星中 **7 个歪（27%，理论 25%）**，小保底不歪 **73.7%**（理论 75%）；
- 用"常驻池出过的五星"反推标准池是**错的**（会漏掉 4 把玩家没在常驻抽到过的标准武器，把不歪率抬到 91.7%）；
- 上游若哪天给 `GachaNoUp` 加上 hk4e302，删掉 `ExtraStandardPools` 里那一项即可。

### 数量统计导出（导出统计数量）

按物品统计抽到次数（对应侧边栏的"数量统计"面板）：五星、四星各一节，**先按数量从多到少，数量相同则角色在
前、武器在后**（识别方式：`ItemType` 落在武器类名表里的算武器 —— 武器/光锥/音擎及其英日写法），同类型再按
名称排序。卡片是单列宽版，每个格子是图标 + 次数药丸 + 名称，**角色次数用蓝底、武器用灰底**，一眼能看出
排序结果。条数超过 240 才会截断并标注。

```powershell
Starward.GachaOnly.exe --export-gacha-counts counts.png [--uid 110369802] [--game hk4e_cn]
```

### 为什么是这个布局（不是照抄参考图的三列）

| 方案 | 问题 |
|---|---|
| 每个卡池一列（参考图那种三列） | 真实数据里卡池大小极不均衡（角色池 90 个五星 vs 常驻池 16 个），矮的列下半截会留一大片空白；列变窄后每行只能放 5 个图标 |
| 纵向多张卡片再拼接 | **这就是上一版的问题**：每页自带边框/页头/页脚，拼起来中间出现"窄一块"的接缝，页头重复 |
| **双列瀑布（当前）** | 主池独占一列、小池叠在另一列，空白最少；宽 1600 DIP 让每行能放 7 个更大的图标；高度可控（原神整套 ~2650 DIP），**一次渲染**，天然没有接缝 |

### 实现要点（踩过的坑都在代码注释里）

- `RenderTargetBitmap` 单次渲染上限 4096 px：卡片按 2× 渲染，若布局高度超过 4000/2 DIP 就**整体按比例缩小**
  （实测原神 1.51×、星铁 1.97×），而不是分页拼接——分页就会留下上面说的接缝；
- 卡片根元素必须自带固定宽度并放在可横向滚动的预览里，否则会被窗口里的 `ScrollViewer` 压窄，
  渲染出来右边被裁（上一版 1600 的卡片实际只有 1192 宽）；
- 图标（`*_GachaInfo.Icon` 里的 URL）在**后台线程下载/命中缓存**，`BitmapImage` 必须在 **UI 线程**解码；
- 小保底不歪：一个卡池的五星序列里，只有「上一个五星是 UP」的那次才算 50/50（上一个非 UP 则为大保底）；
  序列按游戏自身记录 `Id` 排序（导入的时间戳常出现同分钟重复，按 Time 排序会乱序）；
- 命令行无头导出（便于自动化/定时备份）：
  ```powershell
  Starward.GachaOnly.exe --export-gacha-image out.png [--uid 110369802] [--game hk4e_cn]
  ```
  省略 `--uid` 则用上次在应用里选择的账号；省略 `--game` 则用当前选择的游戏；无记录时退出码 1。

---

## 8. 打包（源码包 / 便携包）

```powershell
powershell.exe -ExecutionPolicy Bypass -File gacha-fork\package.ps1 -Version 0.1.0 `
    -Architecture x64 -DataFolder 'F:\Mihoyo\Starward'
```

产出在 `gacha-fork/dist/`：

| 文件 | 内容 |
|---|---|
| `StarwardGacha_Portable_0.1.0_x64.zip`（约 118 MB） | 便携版，结构与官方 portable 包一致（见下） |
| `StarwardGacha_Source_0.1.0.zip`（约 1.8 MB） | 源码包：`git archive` 的源码树 + `gacha-fork/`（文档、脚本、启动器源码、补丁），**不含任何 bin/obj/out/backup** |

源码包里的入口文档以本 fork 为主：根 `README.md` = `gacha-fork/PACKAGE-README.md`（说明这是什么项目、怎么编、
改动面），上游原版另存为 `README.upstream.md`（仓库内的 `README.md` 保持上游原文不动，避免每次上游改 README
都冲突）。

### 在 GitHub Actions 上打包

先看清**哪个文件在什么时候跑** —— GitHub 会把所有 `on:` 匹配的工作流都跑一遍，它们是并存的：

| 工作流 | 触发 | 做什么 |
|---|---|---|
| `build.yml`（上游） | push 到 `main` / `dev/*`、PR | 编 C++ 启动器 + app → `build/Starward/` 上传 artifact，版本号 `0.0.1-build.<sha>`；**不看 tag、不发 Release** |
| `publish_preview.yml` / `_2` / `_3`（上游） | **任意 tag**（`tags: ["*"]`） | 需要上游的 SignPath / OSS / 签名密钥 —— fork 里必然失败，而且 `merge-publish` 会去创建 **draft release** |
| `convert_preview_to_stable.yml`、`publish_res_signpath.yml`（上游） | 手工 | 上游发布流程的后续步骤，fork 里用不到 |
| **`gacha-package.yml`（本 fork 新增）** | `workflow_dispatch` 手工，或 `push` 标签 `gacha-*` | 调 `package.ps1` 出两个 zip；**tag 触发时自动建 GitHub Release 并把两个 zip 挂成资产** |

所以：

- **推 `main`** → 只有 `build.yml`（artifact，不发 Release）；它现在能出**配套可用**的包：C++ 启动器 +
  `app-build.<sha>\Starward.exe` + `version.ini=build.<sha>` 正好对得上（见上一节的坑与修法）。
- **想要 Release** → 推 `gacha-*` 标签，或手工触发 `Gacha Package`（可勾选顺便发 Release）：
  ```bash
  git tag gacha-0.1.0 && git push origin gacha-0.1.0
  ```

> ⚠️ **推 tag 前先在 fork 里禁用上游那三个 tag 工作流**，否则它们会同时被触发：既报一堆没密钥的失败，又可能先建出一个
> **draft release**，而 GitHub Release 是按 tag 定位的 —— 我的发布步骤会往那个 draft 上挂资产，结果发布停在草稿态。
> ```bash
> gh workflow disable "Publish Preview"   --repo <owner>/<repo>
> gh workflow disable "Publish Preview 2" --repo <owner>/<repo>
> gh workflow disable "Publish Preview 3" --repo <owner>/<repo>
> ```
> 等价操作：仓库 Settings → Actions → General → 或在 Actions 列表里逐个 Disable workflow。禁用是仓库级设置，
> **不需要改任何文件**（所以上游合并依然干净）。想改用 `v*` 之类更常规的标签，把 `gacha-package.yml` 的
> `tags:` 加一项即可。

### 坑：CI 上编出来的包为什么曾是坏的（已修）

第一次在 GitHub Actions 上编出来的 artifact：启动器弹 "Starward files not found"，`Starward.GachaOnly.exe`
双击也起不来（`0xC000027B`）。两个根因都不是 CI 的错：

1. **`-p:DefineConstants=...` 是全局 MSBuild 属性，会静默盖掉项目内的赋值。**
   `build.yml` 传了 `-p:DefineConstants=DONOT_CHECK_UPDATE`，而我在 csproj 里用
   `<DefineConstants>$(DefineConstants);GACHA_ONLY</DefineConstants>` 追加常量 —— 项目文件对**全局**属性的赋值
   会被忽略，`-define:` 里只剩 `DONOT_CHECK_UPDATE`。于是 `GachaOnlyProfile.Enabled` 恒为 false：编出来的是
   "行为完全是原生 Starward、名字却叫 GachaOnly"的东西；它还会注册**上游的单实例键**，本机官方 Starward 一旦
   开着，它启动即静默退出（"打不开"的观感）。
   修法：开关改成 **MSBuild 生成的编译期常量**（`GenerateGachaOnlyMarker` target 写
   `obj/…/GachaOnlyMarker.g.cs`，`GachaOnlyProfile.Enabled` 读它）。命令行全局属性盖不掉它，Release 裁剪也不会
   把它裁掉（我先用的是 `AssemblyMetadata`，靠反射读，担心裁剪所以换掉了）。实测带
   `-p:DefineConstants=DONOT_CHECK_UPDATE` 编译后日志仍是 `Standalone gacha build` ✔
2. **发布配置在项目块之后导入，会把 `PublishTrimmed`/`PublishReadyToRun` 重新打开。**
   csproj 第 13 行 `<PublishProfile>win-$(Platform).pubxml</PublishProfile>` 自动套用
   `Properties/PublishProfiles/win-x64.pubxml`，其中写死 `PublishTrimmed=True`、`TrimMode=partial`、
   `PublishReadyToRun=True`（Release）。我原来在 csproj 块里把这两个属性设成 false 是在**配置导入之前**，
   于是被覆盖 → CI 上成了「trim ON + CsWinRT AOT 优化器 OFF」这个必崩组合（XAML 类型被裁掉，启动即
   `0xC000027B`）。
   修法：无 SDK 的机器**直接把 `PublishProfile` 清空**（并自己补 `SelfContained=true`），而不是去覆盖那两个属性；
   有 SDK 的机器（CI、装了 VS 的开发机）走**上游原样配置**（裁剪 + 投影 + AOT 优化器全开）。

顺带把 app 的 `AssemblyName` 固定成 `Starward`：上游和本 fork 的启动器都去找
`app-<版本>\Starward.exe`，这样 CI 不加任何参数就是配套的包。`WindowsSdkAvailable` 改为**自动探测**
（`Exists('$(WindowsSdkDir)UnionMetadata\Windows.winmd')`），也可显式 `-p:WindowsSdkAvailable=true|false` 覆盖；
这组"机器能力"属性对 `-p:GachaOnly=false` 的原生构建同样生效，两种模式都能在本机编出来（实测：原生构建窗口标题
`Starward`、抽卡构建标题 `Starward Gacha` 且日志有 `Standalone gacha build`）。

便携包结构（和官方 `Starward_Portable_*.7z` 一样，只是按你的要求用 zip）：

```
StarwardGacha/
    Starward.exe         启动器（读 version.ini → 起 app-<版本>\Starward.exe，透传命令行参数，
                         并清理更新后遗留的旧 app-* 目录）
    version.ini          version=0.1.0
    gacha-data.ini       UserDataFolder=F:\Mihoyo\Starward（-DataFolder 决定，可删）
    app-0.1.0/           实际应用（发布时用 -p:PortableLayout=true 把 exe 命名为 Starward.exe）
```

两个实现细节：

- **启动器是 C# 写的**（`gacha-fork/launcher/`）：官方的 `src/Starward.Launcher` 是 vcxproj，需要 MSVC 工具链，
  本机没有 cl/msbuild，所以按同样的行为重写了等价实现（单文件自包含，约 10 MB）。它不参与 `Starward.slnx`，
  上游合并不会碰到。
- **`PortableLayout=true`** 只在我的 csproj 隔离块里生效：把 app 项目发布成 `Starward.exe`。没有直接用命令行
  `-p:AssemblyName=Starward`，那是全局属性，会把所有被引用项目一起改名（NuGet 会报 `Ambiguous project name`）。

顺手修掉的一个真 bug：便携版里配置在包根目录、而进程工作目录是 `app-0.1.0\`，原先只查 exe 同级目录，
所以 `gacha-data.ini` 会被忽略（日志显示 "resolved from probe"）。现在 `GachaOnlyDataFolder` 会同时查
`app-<版本>` 的上一级目录 ✔（实测日志变为 "resolved from config file gacha-data.ini"）。

---

## 9. 验证记录

1. `dotnet build -c Debug -p:GachaOnly=true`：`Starward.Language / Starward.Core / Starward.Setup.Core /
   Starward.RPC / Starward` 全部通过，产出 `Starward.GachaOnly.dll`。
2. `build.ps1 -Version 0.1.0 -DataFolder F:\Mihoyo\Starward`：Release 自包含打包成功，
   产出 `Starward.GachaOnly.exe` + `gacha-data.ini`（270.5 MB / 937 文件）。
3. 实机运行（Debug 与 Release 两个产物都跑过）：日志
   `Standalone gacha build; user data folder: F:\Mihoyo\Starward (resolved from config file gacha-data.ini)`。
4. 界面（`out/gacha-app.png`、`out/gacha-app-printwindow.png`、`out/gacha-app-release.png`）：
   导航栏只剩「抽卡记录」与设置齿轮，左上角游戏切换器保留，主页面为完整祈愿记录
   （常驻/角色活动/武器活动三栏统计、5★/4★ 明细、uid 选择、更新/删除/导出/UIGF 全部在），
   共用数据目录里的 1120 / 5453 / 1348 条记录正常显示。
5. 图片导出（本次新增功能）：
   - 原神 → `2415×4000`（布局 1600×2650，1.51×，3.81 MB）；星铁 → `3010×4000`（1600×2126，1.88×，3.5 MB）；
     绝区零（该账号无记录）→ 退出码 1、不产生文件；
   - 版式：单页（一个页头 + 双列瀑布 + 一个页脚），武器池在常驻池上方、群星跃迁沉到最下，
     无接缝、无重复页头、无右侧裁切（布局日志 `card=1600x2650 body=1542x2490 columnWidths=[763,763]`）；
   - 武器池已带 8 项指标：`flips=19 wins=14 avgUp=54.2` → 小保底不歪 **73.7%**、UP 平均 54.2 抽、UP 花费 8665 原石；
   - 角色池指标与参考图（另一工具、同一批记录）交叉核对一致：五星平均 60.2 抽、小保底不歪 45.8%、
     最非 82 抽、UP 平均 93.4 抽、UP 花费 1.49w 原石、最欧 1 抽（参考图：45.3% / 93 抽 / 1.49w / 1 抽）。
6. 窗口限制移除：`MainWindow` 的 `OverlappedPresenter.IsResizable/IsMaximizable` 实测为 True，
   窗口样式含 `WS_THICKFRAME` / `WS_MAXIMIZEBOX`（可拖边缩放、可点最大化），
   日志 `[GachaOnly] window presenter: resizable=True maximizable=True`。
   **最大化按钮修复**：上游在 `WindowSubclassProc` 里无条件吞掉 `SC_MAXIMIZE`（规避"双击标题栏最大化"的
   WinAppSDK bug），导致最大化按钮、贴靠布局点击、`Win+↑` 全部无效（鼠标悬停仍会弹出 Win11 的快捷布局
   浮窗，看起来像"按钮坏了"）。现在只在 stock 构建里吞，GachaOnly 放行；实测真实点击最大化按钮后
   窗口变 `2576×1408`、日志 `state=Maximized`。
7. 数量统计导出：原神 `3200×3604`（6.35 MB）、星铁 `3200×2596`（4.98 MB），排序与角色/武器分组逐项核对通过。
8. 数据库热备份：`tools/db_backup.py` 用 SQLite 在线备份 API（避免裸拷 WAL 的不一致），
   备份落在 `backup/<时间戳>-<名称>/`，附 `integrity_check=ok` 与各抽卡表行数：
   `GenshinGachaItem=10088`、`StarRailGachaItem=6195`、`ZZZGachaItem=0`。
9. 补丁面：`git log` = 1 个 pristine 基线 + 1 个 fork 提交（相对上游约 +2500 行，
   其中上游文件 10 个、每处 1~6 行）。
10. 打包：`package.ps1` 产出的便携包解压后直接运行启动器
    `Starward.exe --export-gacha-image out.png --game hk4e_cn` → 导出成功（2415×4000），
    日志确认数据目录来自包内 `gacha-data.ini`；源码包 899 项、0 个构建产物。

---

## 10. 工具

| 脚本 | 用途 |
|---|---|
| `build.ps1` | 打包（默认 GachaOnly，`-Stock` 编原生 Starward） |
| `package.ps1` | 打包源码包与便携包（见第 8 节，含启动器与 zip 产出） |
| `launcher/` | 便携包启动器（C# 等价实现，单文件自包含） |
| `sync-upstream.ps1` | rebase/merge 上游，列出补丁面与冲突文件 |
| `tools/db_backup.py` | Starward 家族数据库热备份 + 抽卡表统计（`--src <目录> --label <名称>`） |
| `tools/screenshot.ps1` | 截取窗口用于验收。用 `PrintWindow(PW_RENDERFULLCONTENT)`，所以即使被游戏/IDE 挡在前面也能截到窗口本身（用 `CopyFromScreen` 会截到最上层窗口）。**注意**：本机执行策略禁止直接运行 .ps1，需 `powershell.exe -ExecutionPolicy Bypass -File tools\screenshot.ps1 ...` 或在会话内联执行 |

---

## 11. 仍未处理 / 需要决策

- `CloseWindowOption=Hide` 时关窗仍会最小化到托盘（只禁用了 `--hide` 启动分支）。想彻底不要托盘，
  需要在 `MainWindow`/`SystemTrayWindow` 里再挂钩。
- 启动器/截图/工具箱/账号记录等页面仍在程序里（只是不可达），符合「不在意体积」的取舍；
  将来若要瘦身，是「在 `GachaOnlyProfile` 开关下逐目录裁剪」，不必重写。
- 未改名命名空间、未改 `RootNamespace`，所以日志/异常里的类型名仍是 `Starward.*`（刻意为之，利于合并）。
- 若要对外发布：需保留上游 MIT 许可与署名，并按自己的版本号管理（本 fork 已禁用上游更新检查）。
