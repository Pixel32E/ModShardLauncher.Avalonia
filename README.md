# ModShardLauncher（Avalonia 重构版）

《石质碎片》（Stoneshard）的**模组管理器**与**模组制作工具**。本仓库是从 WPF 版本重构而来的 .NET 10 + Avalonia 桌面应用，同时提供无头 CLI。

![主界面](docs/assets/main-window.png)

## 功能

- 打开原版 `data.win`，扫描 `Mods/*.sml`，启用/禁用模组，打补丁并另存为新文件。
- 打开数据文件时，选择器默认定位到 Steam 安装目录 `C:\Program Files (x86)\Steam\steamapps\common\Stoneshard`；该目录不存在时回退到当前工作目录。
- 在 `ModSources/` 中编译 C# 模组工程为 `.sml`（运行时 Roslyn，保留原有流程）。
- 完整保留 `Msl` modding API（命名空间、类型、方法签名不变，已发布模组仍可加载）。
- 现代 IDE 风格界面：顶部菜单 + 工具栏 + 左侧导航 + 底部实时日志 + 状态栏。
- 中 / 英 / 俄三语，采用标准 `.resx` + satellite assemblies，可在运行时切换。
- 无头 CLI：批量打补丁、列出模组、编译模组、导出数据，便于 CI 和服务器使用。

## 解决方案结构

| 项目 | 程序集名 | 说明 |
|---|---|---|
| `src/ModShardLauncher.Core` | `ModShardLauncher` | Modding API + 补丁/加载/纹理/编译等全部领域逻辑，**无任何 UI 依赖** |
| `src/ModShardLauncher.Desktop` | `ModShardLauncher.Desktop` | Avalonia 桌面客户端（MVVM） |
| `src/ModShardLauncher.Cli` | `ModShardLauncher.Cli` | 无头命令行工具 |
| `tests/ModShardLauncher.Tests` | `ModShardLauncher.Tests` | xUnit 单元测试（跨平台） |

> **重要**：Core 的程序集名必须是 `ModShardLauncher`。已发布的 `.sml` 内部程序集引用了这个程序集名，改名会让所有历史模组失效。

详细设计与兼容性约定见 [`docs/architecture.md`](docs/architecture.md)。

## 环境要求

- Windows 10 / 11 x64（游戏本身仅支持 Windows）
- .NET SDK 10.0.4xx（`global.json` 已固定主版本）

## 构建、运行与测试

```powershell
# 还原 + 构建整个解决方案
dotnet build ModShardLauncher.Avalonia.slnx -c Release

# 运行桌面客户端
dotnet run --project src/ModShardLauncher.Desktop

# 运行单元测试
dotnet test tests/ModShardLauncher.Tests
```

> 便携模式：`Mods/`、`ModSources/`、`logs/`、`Settings.json` 都相对于**进程工作目录**。从发布目录直接启动 `ModShardLauncher.Desktop.exe` 即可得到和旧版一致的便携布局。旧版 `Settings.json` 可直接复用。

## 发布（多文件 self-contained）

```powershell
dotnet publish src/ModShardLauncher.Desktop/ModShardLauncher.Desktop.csproj `
  -c Release -r win-x64 --self-contained true -o publish/desktop

dotnet publish src/ModShardLauncher.Cli/ModShardLauncher.Cli.csproj `
  -c Release -r win-x64 --self-contained true -o publish/cli
```

发布目录需要保持**多文件**：

- 运行时 Roslyn 编译器需要从磁盘读取 `ModShardLauncher.dll` 的 `Location` 作为元数据引用；
- 运行时编译还依赖发布根目录下的 `netstandard.dll`、`System.Collections.dll`、`System.Runtime.dll`、`System.Linq.dll`、`System.ObjectModel.dll`（由 Core 的 `ModReference` 复制）。
- 因此请勿使用 `PublishSingleFile` / `PublishTrimmed`。

## CLI

```text
ModShardLauncher.Cli patch   --data <vanilla.win> --output <patched.win>
                             [--mods <dir>] [--enable name1,name2 | --all]
ModShardLauncher.Cli list    [--mods <dir>]
ModShardLauncher.Cli compile --source <project-dir> [--mods <dir>]
ModShardLauncher.Cli export  --data <vanilla.win> --type items|codes|presets

全局选项：
  --workdir <dir>   覆盖便携工作目录（默认当前目录）
  -h, --help        帮助
  -v, --version     版本
```

示例：

```powershell
# 打补丁：启用全部模组
ModShardLauncher.Cli.exe patch --data D:\Games\Stoneshard\data.win `
  --output .\modded.win --all

# 只启用指定模组
ModShardLauncher.Cli.exe patch --data .\data.win --output .\out.win --enable Quicksave,SpeedManager

# 编译 ModSources 下的工程
ModShardLauncher.Cli.exe compile --source .\ModSources\MyMod
```

退出码：`0` 成功，`1` 参数错误，`2` 编译/补丁失败，`3` 未处理异常。

## 目录与设置

```
<工作目录>/
├─ data.win              # 任意位置的原版数据文件
├─ Settings.json         # 与旧版同名字段：Language / EnableLogger / EnabledMods
├─ Mods/                 # *.sml 模组包
├─ ModSources/           # C# 模组工程（含 .csproj 的子目录）
└─ logs/                 # 文件日志（Serilog），界面底部日志面板同步显示
```

## 本地化

- 资源文件：`src/ModShardLauncher.Desktop/Resources/Strings.resx`（英文，中立资源）、`Strings.zh-Hans.resx`、`Strings.ru.resx`。
- 使用标准 `ResourceManager` + 卫星程序集，没有自研格式。
- XAML 中通过 `{loc:Tr Key}` 绑定到可观察的 `LocalizedText`，切换语言后所有界面文本即时刷新。
- 新增语言：添加 `Strings.<culture>.resx`，并在 `SettingsViewModel.Languages` 中注册。

## 已知问题

本次重构以**功能对等迁移**为第一目标，分析报告中 5.1 节列出的历史缺陷暂未修复（详见 [`docs/deferred-bugs.md`](docs/deferred-bugs.md)），包括：

- 补丁失败时优先定位“正在打补丁的模组”的 `First()` 逻辑；
- 每次打补丁都会向 `Settings.EnabledMods` 追加记录；
- 打开数据文件时自动导出 `export/` 的副作用；
- 模组接口服务器的忙等循环；
- `.sml` 读取时可能出现的短读。

此外，`UndertaleModTool` 的二进制依赖仍按原样冻结；其 `Magick.NET` 传递依赖未随发布输出，若走到需要 ImageMagick 的代码路径可能失败，后续单独处理。

## 许可证

GPL-3.0，见 [LICENSE](LICENSE)。本项目是社区项目，与 Ink Stains Games 无关。
