# Rampage.CryptedNotex

「秘密档案」项目。

- `Rampage.CryptedNotex.Net`：.NET 8 窗体程序。
- `documents`：产品设计、前端设计和数据库表设计。
- `installer`：单文件发布脚本与 Inno Setup 安装包脚本。

## 发布 / Publish

单文件发布（依赖框架；产物为单个 exe + `config\` + `Assets\Markdown\`，运行时再生成 `data\`、`__Logs\`）：

```powershell
cd installer
powershell -ExecutionPolicy Bypass -File .\publish.ps1
```

- 产物目录：`Rampage.CryptedNotex.Net\Rampage.CryptedNotex.WinForm\bin\Release\net8.0-windows\win-x64\publish\`。
- 目标机需已安装 **.NET 8 Desktop Runtime (x64)**；未安装 WebView2 运行时时仅 Markdown 预览不可用，其余功能不受影响。
- 便携使用时请将 exe 放在可写目录：`data\`（数据库）与 `__Logs\`（日志）会生成在 exe 旁，Program Files 等受保护目录下会写入失败；正式分发走 `installer\秘密档案.iss` 的 per-user 安装则无此问题。
- 发布约束：不得开启 `PublishTrimmed` / `PublishAot`（框架依赖运行时反射与 Reflection.Emit）。
