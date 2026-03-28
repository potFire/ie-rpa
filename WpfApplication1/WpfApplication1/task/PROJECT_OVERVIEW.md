# IE RPA 工程说明

## 项目定位
- 这是一个基于 WPF 的 IE RPA 设计器，用来编排、保存和执行 IE 自动化流程。
- 用户通过左侧步骤库组装流程，在中间流程区配置执行顺序，在右侧属性区编辑步骤参数。
- 流程执行时由 `WorkflowRunner` 驱动，各步骤由对应的 `StepExecutor` 实现。

## 主要结构
- `ViewModels/MainWindowViewModel.cs`
  - 主界面核心逻辑。
  - 管理流程编辑、步骤增删改、自动保存、工号、运行命令、元素拾取入口。
- `MainWindow.xaml`
  - 主设计器界面。
  - 包含步骤库、流程区、属性编辑区、执行日志区。
- `Workflow/WorkflowRunner.cs`
  - 流程执行主引擎。
  - 负责步骤调用、重试、异常处理、日志、跳转和循环配对。
- `StepExecutors/`
  - 每个步骤类型一个执行器。
  - 新增能力时通常要同时修改 `Enums/StepType.cs`、执行器注册、步骤库定义和属性编辑区。
- `Services/XmlApplicationStateStore.cs`
  - 本地 XML 状态持久化。
  - 保存工号、当前流程、当前选中步骤。
- `ElementPickerWindow.xaml.cs`
  - 页面元素拾取逻辑。
  - 当前支持隐藏式拾取，不再强依赖显示拾取器主界面。

## 当前重要功能
- 启动时检查本地 XML 状态；没有工号时，先弹工号输入框。
- 工号保存到本地，并在执行时写入系统变量 `EmployeeId` / `JobNo`。
- 流程编辑内容采用 XML 自动保存和启动恢复。
- 支持页面元素拾取，当前流程是：
  - 点击主界面拾取按钮。
  - 先弹 loading 动画窗。
  - 随后进入隐藏式页面拾取。
  - 用户直接在 IE 页面点击元素，选择器自动写回步骤。
- 支持 `HttpGetData` 步骤：
  - 自动把工号作为 `jobNo` 查询参数注入 URL。
  - 响应中的 `data` 写入系统变量。
- 支持新版循环块：
  - `LoopStart`
  - `LoopEnd`
  - 旧 `Loop` 仍保留兼容。

## 持久化与启动
- 状态文件位置：`bin/Debug/State/designer-state.xml`（运行目录下）。
- 应用启动逻辑在 `App.xaml.cs` 中。
- 为避免“工号输入窗关闭导致整个应用退出”，启动阶段会临时使用显式关闭模式，主窗口显示后再恢复为主窗口关闭即退出。

## 维护约定
- 每次新增功能或修复，优先同步更新：
  - 本文件：用于描述项目当前结构和运行方式。
  - `task/FEATURE_CHANGELOG.md`：用于记录每次功能调整。
- 如果后续再增加步骤类型，通常至少要同步这几处：
  - `Enums/StepType.cs`
  - `ViewModels/MainWindowViewModel.cs`
  - `StepExecutors/`
  - `Workflow/WorkflowRunner.cs` 或运行时上下文
  - `MainWindow.xaml`
