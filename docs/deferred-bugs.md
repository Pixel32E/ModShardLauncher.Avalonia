# 迁移期暂缓修复的历史缺陷

本次重构以**功能对等迁移**为第一目标。下列问题来自重构前分析报告第 5.1 节，除特别说明外均**按原样保留**，留待后续单独修复。

| 编号 | 位置（新代码） | 问题 | 当前状态 |
|---|---|---|---|
| B1 | `GameDataService.LoadAsync` + `MainWindowViewModel.RunBusyAsync` | 旧版：加载失败时 `LoadingDialog.Hide()` 不执行、`ShowDialog()` 永久阻塞。 | **架构层面已消失**：不再有嵌套模态循环，失败会 `Reset()` 并弹出错误对话框。未做额外的业务逻辑修改。 |
| B2 | `PatchRunner.Patch` → `GetFailingMod` | 失败发生在日志注入阶段（尚无模组被标记 `Patching`）时，`First()` 会抛 `InvalidOperationException`，掩盖原始异常。 | **原样保留**；UI 层会捕获并显示二次异常。 |
| B3 | `ModFile.GetFile`、`TextureSourceScanner`、`SmlPackageReader` | `.sml` 内部路径使用 `\` 分隔，在非 Windows 上会失效。 | **Windows-only 目标下不构成问题**，未修改格式。 |
| B4 | `ModInterfaceServer.AcceptCallback` | `while (Client.Connected) if (Available == 0) continue;` 忙等，占满一个核心。 | **原样保留**，代码中有 `TODO` 注释指向本条。 |
| B5 | `PatchRunner.ApplyMods` | 每次打补丁都向 `Main.Settings.EnabledMods` 追加模组名，从不去重。 | **原样保留**。 |
| B6 | `GameDataService.LoadCore` | 每次加载 `data.win` 都调用 `data.Export(DataType.Items)`，删除并重建工作目录下的 `export/`。 | **原样保留**（旧行为）。 |
| B7 | — | 旧版 `Main.Refresh()` 把控件从旧 `ContentControl` 重挂到新控件，可能抛“元素已有父级”。 | **不适用**：MVVM 导航不再重挂可视元素。 |
| B8 | `SmlPackageReader.ReadBytes` | 使用 `Stream.Read` 且不校验返回值，存在短读可能；失败时在读取函数内部关闭流。 | **原样保留**。 |
| B9 | `ModCatalog.Reload` | 仅对 `.sml` 读取做 try/catch；`GetTypes()`/`Activator.CreateInstance` 异常会中断整批加载。 | **原样保留**。 |
| B10 | `ModInterfaceServer.Stop` | 旧版关闭窗口时直接 `Server.Close()`，可能 NRE。 | 为满足 nullable 编译做了**空安全最小加固**，行为不变。 |

## 其他已知风险（未在分析报告中列为 5.1）

- `UndertaleModLib` 引用了 `Magick.NET`，但以 `<Reference>` 方式引入时不会携带传递依赖；当前发布目录没有 Magick.NET。若某个代码路径触发 ImageMagick 转换，会抛 `FileNotFoundException`。
- `Serilog` 的日志模板沿用了旧版的 `{{{0}}}` 写法，日志中会显示为 `{v0.13.2.0}` 这类带花括号的文本；仅影响观感。
- 旧版存在“设置里保存 `Русский`、读取时判断 `Russian`”的不一致；新实现同时接受两种取值，属于兼容性处理而非功能修复。

修复这些问题时，请同步更新本文件并把对应行标记为“已修复”。
