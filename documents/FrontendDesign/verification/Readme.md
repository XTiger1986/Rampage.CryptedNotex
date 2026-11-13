# 编辑器与旧主题兼容性验证

在 Windows 上使用 .NET 8 或更新 SDK，从项目仓库根目录执行：

```powershell
dotnet run --project documents/FrontendDesign/verification/UiPreview.csproj -c Release -- --regression
```

`RichTextRegression.Run()` 只创建编辑器与主题管理器，不启动应用服务，不连接或修改任何 SQLite 数据库。宿主必须在 Windows STA 线程调用它。用于检验旧自定义 JSON、11 个主题 ID、原始 RTF/纯文本/Markdown 内容、混合换行、手动文字颜色、主题切换、只改标题、只读状态，以及实际编辑后产生新的内容。

富文本画布根据文档原有文字选择明暗，主题变化不会重着色文档。未经正文编辑时，保存请求取得的内容与加载字符串完全相同。验证过程中仅在该验证程序自己的输出目录临时写入一个随机名称的旧主题 JSON，结束后删除。

真实数据升级验收应使用已关闭应用后的完整数据副本；保留 `schema_history`、所有表以及 SQLite WAL 一致性。此验证不替代已有业务 E2E 控制台，也不需要运行用户正在使用的安装实例。

## 窗口与主题预览

```powershell
# 生成 11 个主题及登录、设置、编辑器的窗口截图，然后退出
dotnet run --project documents/FrontendDesign/verification/UiPreview.csproj -- --capture

# 使用 96 DPI 设计基线检验 100% 布局；默认按当前显示器 DPI 检验
dotnet run --project documents/FrontendDesign/verification/UiPreview.csproj -- --capture --dpi96

# 保留窗口，供手动检查交互
dotnet run --project documents/FrontendDesign/verification/UiPreview.csproj
```

窗口预览每次在其 `bin/.../runs/时间戳/` 创建独立测试库、虚构账户及笔记。它写入验证程序自己输出目录的配置，不打开已安装程序的数据；必须从 `verification/bin` 运行。截图与 `report.txt` 位于本次 runs 目录，失败详情写入 `error.txt`。验证包含主题切换后原始笔记密文保持一致，以及标题、元信息、编辑按钮、搜索框在父容器与窗口内可见。

Markdown 预览继续依赖本机 WebView2 Runtime；缺少运行时则验证源码编辑和可用的降级提示。截图使用 WinForms `DrawToBitmap`，原生占位文字及窗口非客户区可能与屏幕捕获不同，实际窗口应另行检查。
