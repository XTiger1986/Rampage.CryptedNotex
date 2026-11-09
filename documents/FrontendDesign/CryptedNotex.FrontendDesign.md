# CryptedNotex 前端设计文档（主题与界面规范）

版本 1.0 · 2026-11-09 · 配套 [CryptedNotex.ProductDocument.md](../ProductDocuments/CryptedNotex.ProductDocument.md) §4.8/§6.4

## 1. 设计立场

产品灵魂词是「**秘密档案**」：本地离线的加密保险柜。视觉语言是**金属档案柜的照明**——

- **光 = 反馈动词**。悬停点亮辉光、按下光熄灭下沉、聚焦让输入框微微泛起强调色；光只回答操作，不做装饰动画。
- **材质分区**。壁纸只在窗体边缘与登录底；卡片/侧栏是拉丝金属；编辑区永远是纯色纸面——内容越安静越好。
- **每套主题一句身份**。色相距离拉开（藏蓝/靛蓝/青绿/赤/青辉），不靠同色系微调充数。
- **对比度是底线**。全部文字/底色配对 WCAG AA ≥4.5（2026-11-09 全量审计通过，含回收站行、提示条、按钮白字）。
- **扁平主题遵守产品红线**：不加载壁纸、不加质感，只换配色。

## 2. 主题令牌系统（Internal\Themes\*.json）

| 段 | 键 | 说明 |
|---|---|---|
| 顶级 | `id` / `name` / `description` | id 唯一；description 一句身份描述（设置窗画廊展示，旧 JSON 可缺省） |
| 顶级 | `darkTitleBar` | true 时 DWM 暗色标题栏；**切回浅色主题会写回 0**（双向） |
| 顶级 | `fonts.ui` / `fonts.listTitle` | 窗体基础字体 / 列表字体，随主题实时应用（contrast 主题 9.5/10.0 由此生效） |
| 顶级 | `categoryPalette[8]` | 分类色点色板，按分类序号取模 |
| 顶级 | `chrome` | 金属主题专属：`metal`(silver/black)、`wallpaper`(资源 id)、`glow`(辉光色) |
| colors | 21 键 | windowBack / panelBack / panelAltBack / border / inputBack / inputBorder(保留) / text / textSecondary / accent / accentText / selectionBack / pinnedMark / trashTint / warning / danger / **dangerText** / **success** / link / editorBack / editorText / previewBack |

新增主题要求：21 色键齐全、`success`/`dangerText` 给出、主要文字配对 ≥4.5、给出 `description`；放入 exe 旁 `themes\*.json` 按 id 覆盖或追加。

## 3. 主题家族（2026-11-09 版）

| id | 名称 | 身份 | 强调色 | 标题栏 |
|---|---|---|---|---|
| light | 浅色 | 明亮纸白 · 日常办公 | #007ACC | 浅 |
| dark | 深色 | 低光环境 · 护眼深色 | #007ACC | 深 |
| contrast | 高对比 | 黑白金 · 最大可读性 | #FFD700(配黑字) | 深 |
| forest | 墨绿 | 沉静墨绿 · 暗夜护眼 | #4CAF7D | 深 |
| parchment | 羊皮纸 | 暖黄纸感 · 灯下读写 | #8C6D3A | 浅 |
| violet | 紫罗兰 | 低光紫调 · 夜间记录 | #8C6FD4 | 深 |
| invader | 入侵者 | 拉丝银 · 侦察青绿 | #067A92 | 浅 |
| alienmorph | 异形形态 | 拉丝银 · 靛蓝机库 | #3D5FD0 | 浅 |
| alx | ALX | 拉丝银 · 公务藏蓝 | #2563EB | 浅 |
| darkstar | 暗星 | 拉丝黑 · 警报赤辉 | #E23B4A | 深 |
| xenomorph | 异形 | 拉丝黑 · 深空青辉（默认） | #3EC6FF | 深 |

对比度审计方法：对每主题计算 text/次文本/按钮字/链接/编辑器/回收站行/成功色 与对应底色的 WCAG 相对亮度比；XX(<3.0) 与 --(3.0~4.5) 必须清零。历史修复：dark/forest/violet 回收站行 1.37~1.41→≥4.9；invader 强调按钮 2.83→5.2；contrast 危险钮改黑字 2.81→7.2；light/parchment 次要文字提深至 ≥4.6。

## 4. 交互光影语言（ChromePainter）

| 状态 | 金属模式 | 扁平模式 |
|---|---|---|
| 按钮静止 | 填充+拉丝纹理+顶部高光 110α+底部阴影 | 填充+边框令牌 |
| 按钮悬停 | 普通**钮边缘点亮主题辉光** 1.4F；强调/危险辉光 1F→2F、高光 150α、填充提亮 8% | `ControlPaint.Light(fill,0.18)` |
| 按钮按下 | 填充压暗 8%、顶部高光熄灭、辉光转暗 | `ControlPaint.Dark(fill,0.12)` |
| 列表行悬停 | `selectionBack` 64α 洗版（选中行除外），MouseMove 命中变化只重绘新旧两行 | 同左 |
| 输入框聚焦 | 底色向 accent 混合 28%（GotFocus/LostFocus 钩子在 ChromePainter） | 同左 |
| 主题卡片 | 选中=辉光 2F / 悬停=辉光 1.5F / 常态=border 1F | 同左 |
| 附件 chip | 悬停边框转 accent 1.5F | 同左 |

滚动条/下拉：暗色主题对 ListView/ListBox/TextBox/TreeView/ComboBox 施加 `SetWindowTheme("DarkMode_Explorer")`，浅色写回默认；句柄创建后与应用时各处理一次，失败静默。

## 5. 布局规范

- **流式排版模式**（LoginForm/CreateUserForm/SettingsForm）：控件在 `BuildUi` 只创建，`LayoutCard`/构建流按**实测高度**自上而下累计定位；`OnShown` 重排一次。解决 150% 显示缩放下"字体放大而坐标不缩放"的叠字问题。
- 间距节奏 4 / 8 / 12 / 16 / 24；卡片内边距 28px 侧沟。
- 主题画廊：226×86 双列富卡片（壁纸条 22px 或强调条 6px、名称 9.5B、描述 8.25、右侧三色点=accent+色板前两色）。
- 空状态 = 引导而非说明：两行文案，第二行告诉用户下一步（"点上方「新建笔记」开始记录"）。
- 状态栏：左侧状态文本 + 左侧忙碌进度、右侧快捷键提示。

## 6. Markdown 预览联动

`previewBack` 令牌决定预览画布：亮度 <0.5 选 github-dark CSS，否则 github-light；画布底色=previewBack、正文色=editorText 注入页面（`window.setThemeMode(mode, back, text)`）。羊皮纸的预览是暖黄纸面、墨绿是深绿纸面，与主题家族一致。

## 7. 已知边界（不改的理由）

- MessageBox / 文件与颜色对话框保持系统样式：不重造系统对话框。
- `inputBorder` 令牌保留未接线：WinForms FixedSingle 边框为系统绘制，聚焦反馈改用底色辉光实现。
- `warning` 令牌保留：预留给未保存标记/锁定倒计时等未来场景。
- 主题 `fonts` 只覆盖 ui/listTitle 两档；其余控件显式字体（43 处）维持原设计稿字号。
- 历史分叉：user_setting.theme_id 列默认 'light'（SQL 注释），代码默认 'xenomorph'（UserSetting.cs 已注明）——不改库，文档标注。

## 8. 维护清单

改配色 → 同步审计算法（相对亮度配对，脚本存档见会话记录）；加令牌 → 全部 11 套 JSON 同步 + ThemeManager.DefaultColor 兜底；改布局 → 遵循流式模式，勿回写死坐标；新交互状态 → 进 ChromePainter 统一实现，勿在业务窗体散落绘制。
