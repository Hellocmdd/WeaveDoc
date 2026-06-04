# WeaveDoc 前端重构任务清单

## 状态说明

- `[ ]` 未开始
- `[x]` 已完成

## 基线信息

- 当前分支：`main`
- 当前远端：`origin/main`
- 清理目标：`doc/软件设计/界面设计_demo/`
- 重构分支：`refactor/frontend-shell`
- 任务目标：先整理设计 demo 与当前文档变更，再创建独立分支实施 Avalonia 原生前端重构。

## 任务清单

### 1. 准备任务文档

- [x] 创建 `doc/task_doc/` 目录。
- [x] 新建 `doc/task_doc/frontend_refactor_tasks.md`。
- [x] 在任务文档中记录清理、提交、建分支、前端重构和验收标准。

### 2. 清理界面设计 demo

- [x] 清理 `doc/软件设计/界面设计_demo/` 中无关脚手架文件。
- [x] 删除未使用的 `components/`、`hooks/`、`lib/`、`components.json`、`styles/`、`pnpm-workspace.yaml`。
- [x] 删除未使用的占位资源 `public/placeholder*`。
- [x] 保留 `app/`、`public/icon*.png`、`public/icon.svg`、`public/apple-icon.png`、`.gitignore`、`package.json`、`pnpm-lock.yaml`、`next.config.mjs`、`postcss.config.mjs`、`tsconfig.json`、`next-env.d.ts`。
- [x] 精简 `package.json`，只保留 Next/React/Tailwind/lucide/TypeScript 相关依赖。
- [x] 更新 `pnpm-lock.yaml`。
- [x] 移除 `app/layout.tsx` 中的 Vercel Analytics 和 `generator: 'v0.app'` metadata。
- [x] 在 `doc/软件设计/界面设计_demo/` 下运行 `pnpm build`。

### 3. 验证并提交当前改动

- [x] 在仓库根目录运行 `dotnet build WeaveDoc.slnx --no-restore`。
- [x] 检查 `git status --short --branch`，确认将要提交的变更只包含预期文档整理内容。
- [x] 提交当前文档整理变更。
- [x] 推荐提交信息：`docs: add interface design demo and reorganize docs`。
- [x] 推送到远端：`git push origin main`。
- [x] 推送后再次检查 `git status --short --branch`。
- [x] 推送后检查 `git log -1 --oneline`。

### 4. 创建前端重构分支

- [ ] 确认本地 `main` 与 `origin/main` 对齐。
- [ ] 从干净的 `main` 创建新分支：`git switch -c refactor/frontend-shell`。
- [ ] 检查 `git branch --show-current`。
- [ ] 检查 `git status --short --branch`。

### 5. Avalonia 原生前端重构

- [ ] 新增 Avalonia 主题资源和通用 Shell 样式。
- [ ] 新增 Shell 状态类型：`ShellPanelKind`、`ShellDialogKind`、`ShellThemeKind`。
- [ ] 新增 `AppShellViewModel`，管理当前面板、弹窗、主题、状态栏和 Shell 状态。
- [ ] 用原生 Avalonia Shell 替换旧主窗口 `TabControl`。
- [ ] 实现标题栏、菜单栏、工具栏、三栏工作区、右侧面板和底部状态栏。
- [ ] 重组工作区：PDF/预览区域、Markdown 编辑器区域、AI/文献/快照侧栏。
- [ ] 接入 `RagTabViewModel`，保留现有 RAG 问答能力。
- [ ] 复用 Monaco 编辑器、Markdown 预览和 PDF Viewer 现有控件。
- [ ] 复用文档转换和模板管理现有业务逻辑。
- [ ] 实现导出文档弹窗。
- [ ] 实现设置弹窗。
- [ ] 实现首次配置向导弹窗。
- [ ] 实现关于弹窗。
- [ ] 为文献库和快照面板提供可用 UI 与空状态；持久化能力后续专项处理。

### 6. 测试与最终验收

- [ ] 更新 Avalonia headless UI 测试，覆盖 Shell 主结构。
- [ ] 更新右侧面板切换测试。
- [ ] 更新导出、设置、首次配置、关于弹窗入口测试。
- [ ] 保留并调整现有转换和模板管理回归测试。
- [ ] 运行 `dotnet build WeaveDoc.slnx --no-restore`。
- [ ] 运行 `dotnet test WeaveDoc.slnx --no-build`。
- [ ] 手动验收窗口默认尺寸和最小尺寸下的布局表现。
- [ ] 手动验收 WebView2 编辑器、Markdown 预览和 PDF Viewer 的可见性与叠放关系。
- [ ] 手动验收 RAG 初始化失败和成功两种状态下的界面反馈。

## 验收标准

### Demo 清理验收

- [x] `components/`、`hooks/`、`lib/`、`components.json`、`styles/`、`pnpm-workspace.yaml`、`public/placeholder*` 已删除。
- [x] `package.json` 不再包含未使用的 Radix/shadcn/form/chart/toast/analytics 等依赖。
- [x] `app/layout.tsx` 不再引入或渲染 Vercel Analytics。
- [x] `pnpm build` 在 `doc/软件设计/界面设计_demo/` 下成功。

### 提交与推送验收

- [x] `git status --short --branch` 在提交后显示 `main...origin/main` 且无未提交变更。
- [x] `git log -1 --oneline` 显示本次文档整理提交。
- [x] `git push origin main` 成功。
- [x] 远端 `origin/main` 与本地 `main` 对齐。

### 新分支验收

- [ ] `git branch --show-current` 输出 `refactor/frontend-shell`。
- [ ] 新分支创建后工作区干净。
- [ ] 新分支基于已推送的最新 `main`。

### 前端重构验收

- [ ] 主窗口视觉结构与 demo 一致：标题栏、菜单栏、工具栏、三栏工作区、右侧面板、底部状态栏齐全。
- [ ] 默认尺寸下无文字溢出、控件重叠、WebView2 遮挡或空白区域异常。
- [ ] 最小尺寸下无文字溢出、控件重叠、WebView2 遮挡或空白区域异常。
- [ ] Markdown 打开、保存、编辑和预览仍可用。
- [ ] PDF 打开和阅读仍可用。
- [ ] RAG 问答仍可用。
- [ ] 文档转换仍可用。
- [ ] 模板管理仍可用。
- [ ] 导出、设置、首次配置、关于弹窗可打开关闭，且不会破坏主工作区状态。
- [ ] `dotnet build WeaveDoc.slnx --no-restore` 成功。
- [ ] `dotnet test WeaveDoc.slnx --no-build` 成功，或明确记录失败原因和剩余风险。

## 假设与边界

- 该文档只记录任务和验收标准，不代表已经执行清理、提交、推送或分支创建。
- `doc/软件设计/界面设计_demo/` 只作为设计参考保留，不作为最终 Avalonia 应用运行时依赖。
- 当前已有 doc 删除和新增目录会在后续提交阶段一起处理，不擅自恢复。
- 文献库持久化、快照存储和公式 OCR 深度接入不纳入第一轮前端重构闭环。
