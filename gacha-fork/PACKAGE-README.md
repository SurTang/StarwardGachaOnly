# Starward Gacha — 源码包

[Starward](https://github.com/Scighost/Starward) 的独立抽卡记录应用（fork）源码。同一份源码树既能编出
`Starward Gacha`，也能编出原生 Starward。

- 上游基线：`Scighost/Starward` `main` @ 2026-10-08（`game management`，commit `607a3077`）
- 许可：MIT —— 上游 `LICENSE` 与署名原样保留
- 上游原始 README 保留在 `README.upstream.md`（本文件替换了它，便于一眼看出这是哪个项目）

---

## 怎么编

```powershell
# 独立抽卡应用（默认）
dotnet build src/Starward/Starward.csproj -c Debug -r win-x64 -p:Platform=x64 -p:GachaOnly=true

# 上游原生 Starward（同一份源码树，用于对照/回归）
dotnet build src/Starward/Starward.csproj -c Debug -r win-x64 -p:Platform=x64 -p:GachaOnly=false
```

- 没有 Windows SDK 的机器会自动走 `WindowsSdkAvailable=false`（关闭 CsWinRT 投影与裁剪，Release 才不会
  在 XAML 启动时崩 0xC000027B）；有完整 SDK 时加 `-p:WindowsSdkAvailable=true` 可启用上游默认行为。
- 直接出便携版：`powershell -File gacha-fork\package.ps1 -Version 0.1.0 -DataFolder <共用数据目录>`
- 旧版发布脚本：`powershell -File gacha-fork\build.ps1 -Version 0.1.0`

## 包内有什么

| 路径 | 说明 |
|---|---|
| `src/**` | 上游 Starward 全部源码 + 本 fork 的新增文件与挂钩 |
| `gacha-fork/README.md` | **本 fork 的完整文档**：为什么"屏蔽而非抽离"、改动清单、导出功能与版式、打包、验证记录、未处理项 |
| `gacha-fork/gacha-only.patch` / `.diff` | 相对上游的全部改动，`git am` 可直接打 |
| `gacha-fork/build.ps1` | 发布构建（自包含 Release） |
| `gacha-fork/package.ps1` | 打包源码包 / 便携包（含启动器编译） |
| `gacha-fork/sync-upstream.ps1` | 合并上游并列出补丁面与冲突文件 |
| `gacha-fork/launcher/` | 便携包启动器源码（C#；上游那个是 C++，本机无 MSVC 故重写等价实现） |
| `gacha-fork/tools/` | 数据库热备份脚本 + 若干数据核对脚本 |
| `README.upstream.md` | 上游原始 README |

## 改动面（这就是全部）

- **新增文件**（上游永远不会碰 → 永不冲突）
  - `src/Starward/Features/GachaOnly/**`：11 个文件（开关、页面表、数据目录解析、无抽卡提示页、
    导出模型 / 卡片 / 预览窗口 / 命令行）
  - `src/Starward.Core/HoYoPlay/HoYoPlayClientLegacyOverloads.cs`：上游快照破损的兼容层（上游修好后可删）
- **上游文件挂钩**：10 个文件、每处 1~6 行
  `Features/GameFeatureConfig.cs`、`Features/ViewHost/MainView.xaml.cs`、`Features/ViewHost/MainWindow.xaml.cs`、
  `Features/Gacha/GachaLogPage.xaml(.cs)`、`App.xaml.cs`、`AppConfig.Configuration.cs`、
  `AppConfig.ServiceProvider.cs`、`Features/UrlProtocol/UrlProtocolService.cs`、`Starward.csproj`
- **没有**改命名空间、**没有**移动文件、**没有**改数据库结构 —— 这样上游的新功能/bug 修复才能持续 `git merge`
  进来（细节与取舍见 `gacha-fork/README.md`）
