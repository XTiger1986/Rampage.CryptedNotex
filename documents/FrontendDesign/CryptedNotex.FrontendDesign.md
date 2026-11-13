# CryptedNotex 前端设计文档（主题与界面规范）

版本 2.0 · 2026-11-13 · 配套 [产品文档](../ProductDocuments/CryptedNotex.ProductDocument.md) §4.8 / §6。

本文记录现行实现。视觉规范以本文和 `Internal/Themes/*.json` 为准；历史金属外壳设计由独立的主题身份图形与安静的书写界面取代。

## 1. 设计立场

「秘密档案」是一款本地离线的私人笔记工具。界面围绕分类、找到笔记、阅读和编辑组织，主题负责表达个性，正文保留稳定的阅读空间。

- **身份与内容分区**：主题插画出现在登录侧区、主界面侧栏和主题卡片中。内置主题不再用整窗壁纸与重复拉丝纹理覆盖工作区；正文使用纯色纸面。
- **每套主题有独立语言**：不仅调整强调色，还改变明暗关系、图形构成、按钮轮廓与阅读纸面；11 套主题的身份见下表。
- **明确的操作层级**：新建与进入编辑使用主按钮；普通操作使用轻边界；彻底删除使用危险色；禁用、悬停、按下、键盘焦点和富文本格式启用状态都有区分。
- **阅读对比可验证**：内置主题按小字号正文标准要求文字配对至少 4.5:1。装饰图形不承载正文信息。分类还显示名称和锁定标志，不仅依赖颜色。
- **已有数据优先**：视觉升级不修改数据库结构、加密格式、原主题 ID 或历史笔记正文。

## 2. 主题令牌和 API

主题模型位于 `Rampage.CryptedNotex.WinForm/Internal/Themes/`，主题 JSON 内嵌于程序集。程序继续读取 exe 旁 `themes/*.json`，按 `id` 覆盖或追加；旧主题缺少新增可选字段时仍可使用。

| 段 | 字段 | 说明 |
|---|---|---|
| 顶级 | `id` / `name` / `description` | 稳定 ID、显示名、一句身份描述；旧 JSON 可不含 description |
| 顶级 | `darkTitleBar` | 双向设置 DWM 标题栏明暗 |
| 顶级 | `fonts.ui` / `fonts.listTitle` | UI / 列表字体；高对比主题分别为 9.5 / 10 pt |
| 顶级 | `categoryPalette` | 分类色板，按序号取模 |
| 可选 | `appearance.motif` | 身份图形名称；缺省按 id 选择，未知 id 使用通用几何图形 |
| 可选 | `appearance.styleName` | 视觉身份描述，由 GetStyleName 返回；缺省使用内置身份或主题名 |
| 可选 | `appearance.buttonShape` | `rounded` / `square` / `cut`；缺省 rounded |
| 兼容 | `chrome` | 旧自定义主题的 metal / wallpaper / glow 配置继续可读；新版内置主题不依赖这些资源 |

基础颜色仍为 21 键：

`windowBack`、`panelBack`、`panelAltBack`、`border`、`inputBack`、`inputBorder`、`text`、`textSecondary`、`accent`、`accentText`、`selectionBack`、`pinnedMark`、`trashTint`、`warning`、`danger`、`dangerText`、`success`、`link`、`editorBack`、`editorText`、`previewBack`。

新增两个可选语义底色：`sidebarBack` 缺省回退 panelBack；`cardBack` 缺省回退 panelAltBack。内置主题均给出完整 23 色键，颜色格式为 `#RRGGBB`。inputBorder 仍为保留令牌，原生 FixedSingle 输入边框由 Windows 绘制。

身份图形由 `ThemeArtwork` 使用 GDI+ 矢量绘制，不需要下载图片、联网或新增图片文件：

```csharp
ThemeArtwork.Draw(Graphics graphics, Rectangle bounds, Theme theme);
ThemeArtwork.GetStyleName(Theme theme);
```

身份图形可画在指定矩形中，绘制过程保存并恢复 Graphics 状态。认证页使用 `AuthArtworkPanel`，主侧栏使用 `WorkspaceIdentity`，设置页在微缩三栏预览中复用同一绘制器。

## 3. 11 套主题的视觉身份

| id | 名称 | 图形与阅读氛围 | 强调色 | 按钮轮廓 |
|---|---|---|---|---|
| light | 浅色 | 纸白、蓝色编辑网格、规整文稿 | #245BB8 | 圆角 |
| dark | 深色 | 石墨底、冷青斜光、低光专注 | #91D4CD | 圆角 |
| contrast | 高对比 | 黑白金、清晰实块与强边界 | #FFE640 | 方形 |
| forest | 墨绿 | 深松与苔绿、植物叶片、暖色文字 | #C3D597 | 圆角 |
| parchment | 羊皮纸 | 奶油纸、赭色叠页、灯下书房 | #80582A | 方形 |
| violet | 紫罗兰 | 暮紫底、薰衣草月弧、柔和层次 | #D6B7F5 | 柔和圆角 |
| invader | 入侵者 | 冰青底、坐标网格、侦察雷达 | #006A5B | 切角 |
| alienmorph | 异形形态 | 珠紫浅底、流体轮廓、有机曲面 | #704393 | 柔和圆角 |
| alx | ALX | 深海军蓝外壳、橙色建筑构成、浅色纸面 | #FFB66E | 方形 |
| darkstar | 暗星 | 酒红底、铜色星体轨道、深空观测 | #EFB39B | 圆角 |
| xenomorph | 异形 | 黑炭底、酸绿脊线、生物机械 | #B6F36A | 切角 |

新用户和未写入的全局默认继续使用 xenomorph。已保存的个人主题和全局默认不被重置。数据库列历史默认 light 与应用层默认 xenomorph 的差异保留，不为视觉改造变更历史 DDL。

## 4. 控件语义和交互

`ThemeManager.Apply` 递归应用配色，`ChromePainter` 统一绘制按钮、边界和焦点。事件钩子只注册一次，重绘读取当前主题。

| Tag | 用途 |
|---|---|
| `theme:sidebar` / `theme:brand` | sidebarBack；brand 兼容旧品牌条的徽记和 Text 绘制 |
| `theme:card` / `theme:cardEmblem` | cardBack 和细边界；cardEmblem 兼容旧徽记卡片 |
| `theme:chrome` / `theme:toolbar` / `theme:hero` | panelBack；hero 的身份图形由宿主调用 ThemeArtwork 绘制 |
| `theme:panelAlt` | 列表 / 次级内容底色 |
| `theme:editor` / `theme:editorTitle` | editorBack / editorText，标题输入框无原生边框 |
| `theme:document` | 用户富文档表面；主题完全保留 BackColor / ForeColor |
| `theme:accent` / `theme:danger` | 主按钮 / 危险操作按钮 |
| `theme:tool` | 紧凑格式按钮；启用状态由 SetButtonActive 独立记录 |
| `theme:muted` / `theme:link` | 次文本 / 链接 |
| `theme:rule` | 分隔线 |

| 状态 | 实现 |
|---|---|
| 普通按钮 | 纯色填充和细边界，轮廓由 appearance 决定 |
| 悬停 / 按下 | 填充向反馈色混合约 7% / 15%；按下文字下移 1 px |
| 键盘焦点 | 按钮内部虚线轮廓；主题卡片显示焦点框 |
| 禁用按钮 | 去除主强调填充，使用 panelBack / textSecondary |
| 富文本格式已启用 | 独立 Active 状态保持 selectionBack，切换主题后仍清晰可见 |
| 列表悬停 | selectionBack 的 72 alpha 洗版；仅重绘变化的行 |
| 输入框聚焦 | inputBack 向 accent 混合 8%；编辑器、标题和 document 不参与该变化 |
| 主题卡片选中 | 强调边框与「已选」文字；可使用 Tab、Enter、空格选择 |
| 滚动条 / 下拉 | 句柄建立和每次应用主题均更新 DarkMode_Explorer，浅色切回系统默认 |

ALX 等主题的外壳和编辑纸面明暗不同。编辑区内标签使用 editorText，次文本由纸面与正文色派生；链接色若在纸面上不足 4.5:1，则回退 editorText。LinkLabel 的 LinkColor / ActiveLinkColor / VisitedLinkColor 也随主题更新。

## 5. 布局和 DPI

所有手工布局以 **96 DPI** 设计值为基准。基类 `NotexFormBase.OnLoad` 在控件树创建完成后，首次设置 `AutoScaleDimensions = (96, 96)` 并执行 `PerformAutoScale()`。基类构造阶段不提前消费缩放，避免 150% 下只放大字体而漏缩放后创建的控件。

运行时手工定位、自绘尺寸与列表行高按 `DeviceDpi / 96` 换算；普通 Dock / TableLayout 交由 WinForms 布局。不要对同一尺寸重复手工和自动缩放。

- **登录 / 创建账户**：左侧主题插画、右侧可滚动表单；宽度不足 780 个设计像素时隐藏插画。插画区约占 42%。表单按文字实测高度逐项排版，清除旧滚动范围后重算宽度，保留滚动条余量；OnShown 限制到当前屏幕工作区并再次排版。
- **主工作区**：分类侧栏、笔记列表、编辑器三栏。初始分栏约 224 / 350 个设计像素，给编辑区保留约 400 个设计像素；可拖动调整。默认窗口不超过工作区约 90%，最大设计尺寸为 1440 × 900；最小尺寸随当前工作区限制。
- **分类与笔记列表**：分类行约 46、笔记行约 88 个设计像素；隐藏列头，以整行展示标题、类型、时间和字数。内部保留原 ListView 项与数据关联，不为新样式改变模型。
- **编辑区**：大标题、类型与修改信息、阅读 / 编辑入口、纯色书写面、附件区。无选中项时显示下一步引导；没有目标笔记时置顶和删除按钮禁用。
- **设置**：左侧主题画廊约 68%，右侧可滚动偏好约 32%，底部固定保存区。卡片按可用宽度自动排为 1–3 列，高约 194 个设计像素；预览展示真实三栏底色与身份图形。
- **保存反馈**：主窗体底部状态区显示忙碌与保存状态，右侧提示 Ctrl+N / Ctrl+F / Ctrl+S。提示条使用统一 ToastHost。

## 6. 三类编辑器与兼容性

纯文本和 Markdown 继续使用用户的编辑字体设置。Markdown 预览按 previewBack 的 GetBrightness 选择 light / dark CSS，并通过 `window.setThemeMode(mode, back, text)` 注入 previewBack 与 editorText；标记解析及预览资源继续本地打包。

富文本的颜色属于文档内容，不能当作界面主题颜色：

1. `RichNoteEditor` 保存加载时的原始 PlainContent。用户仅查看、改标题、切主题或切阅读模式时，`CollectContent()` 原样返回该字符串。
2. 只有实际编辑正文或格式后，才采用 RichTextBox 重新序列化的 RTF 并触发自动保存。
3. 文档纸面和 RichTextBox 均标记 `theme:document`，主题不改其前景、背景、字体或选区格式。
4. 加载时最多均匀采样 128 个字符的现有文字亮度：平均亮度大于 0.55 使用 #1E2229 深纸，否则用白纸。只改变显示背景，保留历史深色主题产生的浅色字及作者选色；之后切换主题不改变该纸面。
5. 无效 RTF 可退回纯文本显示；未编辑时仍保留原始字符串。富文本工具条和外部界面继续随主题更新。

自动锁定、保存队列、独立密码分类和加密会话仍沿用既有流程。设置保存使用现有字段，保留 LastNoteId、托盘偏好及自定义字体；主题即时保存失败时恢复原主题，不提交其他尚未保存的表单编辑。

本次改造没有新增数据库迁移，不修改密文版本、AAD、KDF、密码验证、附件内容或枚举值。升级分发仍须保留现有 `data/`、`config/` 和自定义 `themes/`。

系统 MessageBox、文件选择和颜色对话框继续使用 Windows 样式。RTF 作者自定义的任意文字颜色不纳入 UI 主题对比度保证；保留原文颜色优先。

## 7. 审计与验收

在仓库根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\documents\FrontendDesign\contrast-audit.ps1
```

脚本通过 PSScriptRoot 相对定位内置主题，不依赖开发者盘符。每主题检查 26 组文字 / 底色配对，共 286 项，包含正文、次文本、输入框、侧栏、卡片、选中行次文本、回收站选中行、强调 / 危险按钮、链接、预览和提示标题。缺字段、无效颜色、无主题文件及任何比值低于 4.5 均以退出码 1 失败，成功为 0。

可使用 `-ThemeDirectory <目录>` 审计另一组主题。sidebarBack / cardBack 缺省回退规则与运行时一致。静态令牌审计不替代焦点、悬停、长文本和实际 DPI 的界面验证。

2026-11-13 本次改造已完成业务回归 47 项、编辑器兼容检查 368 项，以及 100% / 150% 的界面截图和关键控件窗口边界检查。兼容性验证使用独立测试数据，未对现有运行实例的数据执行改造。

## 8. 维护清单

- 新主题保留稳定 id，提供独立 motif / 描述 / 按钮轮廓，验证其与其余主题的区别；可从现有完整 JSON 复制令牌结构。
- 改配色后运行审计，尤其检查 selectionBack 上的次文本和回收站标题；新增底色时同步配对。
- 新绘制统一放入 ThemeArtwork / ChromePainter，业务窗体使用语义 Tag；富文本正文继续使用 document。
- 改布局同时检查 100% / 150%、窄窗口、滚动、键盘导航及当前屏幕工作区；保留 OnLoad 的 96 DPI 基线处理。
- 数据与加密功能变更另走既有迁移规则；不要为了界面换肤改写历史数据库脚本或正文。
