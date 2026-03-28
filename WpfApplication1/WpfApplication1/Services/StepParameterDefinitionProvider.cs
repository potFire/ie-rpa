using System;
using System.Collections.Generic;
using WpfApplication1.Enums;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class StepParameterDefinitionProvider : IStepParameterDefinitionProvider
    {
        private readonly IDictionary<StepType, IReadOnlyList<StepParameterDefinition>> _definitions;

        public StepParameterDefinitionProvider()
        {
            _definitions = BuildDefinitions();
        }

        public IReadOnlyList<StepParameterDefinition> GetDefinitions(StepType stepType)
        {
            IReadOnlyList<StepParameterDefinition> definitions;
            return _definitions.TryGetValue(stepType, out definitions)
                ? definitions
                : EmptyDefinitions;
        }

        private static IDictionary<StepType, IReadOnlyList<StepParameterDefinition>> BuildDefinitions()
        {
            return new Dictionary<StepType, IReadOnlyList<StepParameterDefinition>>
            {
                { StepType.LaunchIe, List(
                    Text("url", "启动地址", "启动 IE 后打开的目标地址。", StepParameterSection.Basic, true, "${TargetUrl}", 10)) },
                { StepType.AttachIe, EmptyDefinitions },
                { StepType.Navigate, List(
                    Text("url", "导航地址", "当前 IE 页面要跳转到的地址。", StepParameterSection.Basic, true, "http://intranet.example.local", 10)) },
                { StepType.HttpGetData, List(
                    Text("url", "请求地址", "GET 接口地址，系统会自动带上当前工号 jobNo。", StepParameterSection.Basic, true, "http://intranet.example.local/api/status", 10),
                    Text("dataVariableName", "数据变量名", "接口返回的 data 会写入该变量名。", StepParameterSection.Basic, true, "ApiData", 20),
                    Boolean("emptyAsNoData", "空结果视为无任务", "当 data 为空字符串、空集合或 null 时，按无任务处理。", StepParameterSection.Advanced, false, "false", 30),
                    Text("hasDataVariableName", "有数据标记变量", "写入 true/false 的变量名，供调度器判断是否继续执行申请流程。", StepParameterSection.Advanced, false, "HasApiData", 40),
                    MultiLine("noDataStatusValues", "无任务状态值", "逗号、分号或竖线分隔的状态码列表，命中后按无任务处理。", StepParameterSection.Advanced, false, "NO_DATA,EMPTY,204", 50)) },
                { StepType.WaitForElement, List(
                    XPath("selector", "等待目标 XPath", "等待出现的页面元素 XPath。", StepParameterSection.Picker, true, "xpath=/html[1]/body[1]/div[1]", 10),
                    Number("pollIntervalMs", "轮询间隔（毫秒）", "轮询元素出现的检查间隔。", StepParameterSection.Advanced, false, "500", 20)) },
                { StepType.WaitPageReady, EmptyDefinitions },
                { StepType.ClickElement, List(
                    XPath("selector", "点击目标 XPath", "要点击的按钮、链接或页面元素。", StepParameterSection.Picker, true, "xpath=/html[1]/body[1]/button[1]", 10)) },
                { StepType.ClickAndSwitchWindow, List(
                    XPath("clickSelector", "点击目标 XPath", "触发新窗口打开的页面元素。", StepParameterSection.Picker, true, "xpath=/html[1]/body[1]/a[1]", 10),
                    Text("targetWindowTitle", "目标窗口标题", "用于匹配新窗口标题的关键字。", StepParameterSection.Basic, true, "详情", 20),
                    Select("matchMode", "标题匹配方式", "标题匹配策略。", StepParameterSection.Basic, false, "contains", 30, WindowTitleMatchOptions()),
                    Number("pollIntervalMs", "轮询间隔（毫秒）", "等待新窗口出现的轮询间隔。", StepParameterSection.Advanced, false, "500", 40),
                    Boolean("excludeCurrent", "排除当前窗口", "查找新窗口时是否排除当前窗口。", StepParameterSection.Advanced, false, "true", 50)) },
                { StepType.InputText, List(
                    XPath("selector", "输入框 XPath", "要写入内容的输入框 XPath。", StepParameterSection.Picker, true, "xpath=/html[1]/body[1]/input[1]", 10),
                    Text("text", "输入内容", "支持变量占位符。", StepParameterSection.Basic, true, "${UserName}", 20)) },
                { StepType.ReadText, List(
                    XPath("selector", "读取目标 XPath", "要读取文本的页面元素 XPath。", StepParameterSection.Picker, true, "xpath=/html[1]/body[1]/div[1]", 10),
                    Text("variableName", "输出变量名", "读取到的文本会写入该变量。", StepParameterSection.Basic, true, "ResultText", 20)) },
                { StepType.SelectOption, List(
                    XPath("selector", "下拉框 XPath", "select 控件的 XPath。", StepParameterSection.Picker, true, "xpath=/html[1]/body[1]/select[1]", 10),
                    Text("option", "目标选项", "按文本或值匹配的选项内容。", StepParameterSection.Basic, true, "Shanghai", 20),
                    Select("matchMode", "匹配方式", "按文本或 value 匹配下拉项。", StepParameterSection.Basic, false, "text", 30, SelectOptionMatchOptions())) },
                { StepType.SwitchFrame, List(
                    Select("action", "Frame 动作", "进入、返回父级或回到根页面。", StepParameterSection.Basic, false, "enter", 10, FrameActionOptions()),
                    XPath("selector", "iframe XPath", "action 为 enter 时使用的 iframe XPath。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/iframe[1]", 20),
                    Text("framePath", "兼容 framePath", "兼容旧流程的 framePath 值；仅旧流程需要时填写。", StepParameterSection.Advanced, false, string.Empty, 30)) },
                { StepType.PageListLoop, List(
                    Select("mode", "处理模式", "列表项处理方式。", StepParameterSection.Business, false, "approve", 10, PageListModeOptions()),
                    XPath("filterSelector", "筛选字段 XPath", "筛选输入框或控件的 XPath。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/input[1]", 20),
                    Text("filterValue", "筛选值", "查询前填入的筛选值。", StepParameterSection.Business, false, "${BusinessName}", 30),
                    XPath("queryButtonSelector", "查询按钮 XPath", "点击后刷新列表的按钮 XPath。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/button[1]", 40),
                    XPath("listReadySelector", "列表就绪 XPath", "列表刷新完成后能稳定出现的元素 XPath。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/table[1]", 50),
                    Text("rowSelectorTemplate", "行 XPath 模板", "支持 ${RowIndex} 的行模板。", StepParameterSection.Business, true, "xpath=/html[1]/body[1]/table[1]/tbody[1]/tr[${RowIndex}]", 60),
                    Text("rowActionSelectorTemplate", "行内操作 XPath 模板", "支持 ${RowIndex} 的行内操作模板。", StepParameterSection.Business, true, "xpath=/html[1]/body[1]/table[1]/tbody[1]/tr[${RowIndex}]/td[8]/a[1]", 70),
                    Number("maxRows", "单轮最大扫描行数", "每轮最多扫描的行数。", StepParameterSection.Advanced, false, "50", 80),
                    Number("maxRounds", "最大轮次", "列表重新查询并从头检查的最大轮次。", StepParameterSection.Advanced, false, "100", 90),
                    Number("pollIntervalMs", "轮询间隔（毫秒）", "等待列表或窗口时的轮询间隔。", StepParameterSection.Advanced, false, "500", 100),
                    Text("targetWindowTitle", "详情窗口标题", "处理列表项后切换到详情窗口的标题关键字。", StepParameterSection.Business, false, "详情", 110),
                    Select("windowMatchMode", "窗口匹配方式", "详情窗口标题匹配方式。", StepParameterSection.Advanced, false, "contains", 120, WindowTitleMatchOptions()),
                    XPath("detailReadySelector", "详情页就绪 XPath", "进入详情页后等待出现的元素。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]", 130),
                    XPath("detailActionSelector", "详情页操作 XPath", "审批或处理详情页时要点击的按钮。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/button[1]", 140),
                    Select("returnMode", "返回策略", "处理完详情页后的恢复方式。", StepParameterSection.Business, false, "closeCurrentWindow", 150, ReturnModeOptions()),
                    XPath("returnSelector", "返回按钮 XPath", "返回策略需要点击按钮时使用。", StepParameterSection.Picker, false, string.Empty, 160),
                    Text("returnButtonText", "弹窗按钮文本", "处理成功弹窗时要点击的按钮文本。", StepParameterSection.Advanced, false, "确定", 170),
                    Text("returnTitleContains", "弹窗标题包含", "处理成功弹窗时的标题匹配关键字。", StepParameterSection.Advanced, false, string.Empty, 180),
                    XPath("popupReadySelector", "弹窗就绪 XPath", "查询报告弹窗出现后的稳定元素。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/div[1]", 190),
                    XPath("reportIframeSelector", "报告 iframe XPath", "报告内容所在 iframe。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/div[1]//iframe[1]", 200),
                    Directory("saveDirectory", "报告保存目录", "导出的报告 HTML 保存目录。", StepParameterSection.Advanced, false, @"C:\Temp\IeRpaReports", 210),
                    Text("fileNameTemplate", "报告文件名模板", "支持变量占位符的文件名模板。", StepParameterSection.Advanced, false, "report_${BusinessName}_${RowIndex}", 220),
                    Text("uploadUrl", "上传 URL", "导出后自动上传的接口地址。", StepParameterSection.Advanced, false, "http://intranet.example.local/api/upload", 230),
                    XPath("closePopupSelector", "关闭弹窗 XPath", "导出完成后关闭弹窗的按钮。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/div[1]//button[1]", 240),
                    Text("outputFileVariableName", "文件输出变量名", "保存导出文件路径的变量名。", StepParameterSection.Advanced, false, "LastReportFilePath", 250),
                    Text("uploadResponseVariableName", "上传响应变量名", "保存上传响应内容的变量名。", StepParameterSection.Advanced, false, "LastUploadResponse", 260),
                    Number("popupPollIntervalMs", "弹窗轮询间隔（毫秒）", "等待报告弹窗就绪的轮询间隔。", StepParameterSection.Advanced, false, "500", 270)) },
                { StepType.QueryAndExportReport, List(
                    XPath("queryButtonSelector", "查询按钮 XPath", "触发查询报告的按钮 XPath。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/table[1]/tbody[1]/tr[${RowIndex}]/td[8]/a[1]", 10),
                    XPath("popupReadySelector", "弹窗就绪 XPath", "报告弹窗出现后的稳定元素。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/div[1]", 20),
                    XPath("reportIframeSelector", "报告 iframe XPath", "报告内容所在 iframe。", StepParameterSection.Picker, true, "xpath=/html[1]/body[1]/div[1]//iframe[1]", 30),
                    Directory("saveDirectory", "报告保存目录", "导出的报告 HTML 保存目录。", StepParameterSection.Business, false, @"C:\Temp\IeRpaReports", 40),
                    Text("fileNameTemplate", "文件名模板", "支持变量占位符的导出文件名模板。", StepParameterSection.Business, false, "report_${BusinessName}_${RowIndex}", 50),
                    Text("uploadUrl", "上传 URL", "导出报告后自动上传的接口地址。", StepParameterSection.Business, false, "http://intranet.example.local/api/upload", 60),
                    XPath("closePopupSelector", "关闭弹窗 XPath", "导出完成后关闭弹窗的按钮 XPath。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/div[1]//button[1]", 70),
                    Text("outputFileVariableName", "文件输出变量名", "写入导出文件路径的变量名。", StepParameterSection.Advanced, false, "LastReportFilePath", 80),
                    Text("uploadResponseVariableName", "上传响应变量名", "写入上传响应内容的变量名。", StepParameterSection.Advanced, false, "LastUploadResponse", 90),
                    Number("popupPollIntervalMs", "弹窗轮询间隔（毫秒）", "等待报告弹窗就绪的轮询间隔。", StepParameterSection.Advanced, false, "500", 100)) },
                { StepType.HttpUploadFile, List(
                    Text("url", "上传 URL", "multipart 文件上传接口地址。", StepParameterSection.Basic, true, "http://intranet.example.local/api/upload", 10),
                    FilePath("filePath", "文件路径", "待上传的本地文件路径。", StepParameterSection.Basic, true, "${LastReportFilePath}", 20),
                    Text("responseVariableName", "响应变量名", "保存接口响应内容的变量名。", StepParameterSection.Advanced, false, "LastUploadResponse", 30)) },
                { StepType.UpdateBusinessState, List(
                    Text("name", "姓名", "业务记录中的姓名。", StepParameterSection.Business, false, "${ApiData.name}", 10),
                    Text("idCardNumber", "证件号", "业务记录中的证件号。", StepParameterSection.Business, false, "${ApiData.idCardNumber}", 20),
                    Text("reason", "原因", "业务原因说明。", StepParameterSection.Business, false, string.Empty, 30),
                    Text("sourceUrl", "来源 URL", "业务来源地址。", StepParameterSection.Business, false, "${TargetUrl}", 40),
                    Text("machineRole", "机器角色", "当前机器在业务流程中的角色标识。", StepParameterSection.Advanced, false, string.Empty, 50),
                    Text("lastStepName", "最后步骤名", "恢复时记录的最后一步名称。", StepParameterSection.Advanced, false, string.Empty, 60),
                    MultiLine("errorMessage", "错误信息", "失败或恢复时记录的错误摘要。", StepParameterSection.Advanced, false, string.Empty, 70),
                    FilePath("htmlFilePath", "报告文件路径", "业务对应的本地 HTML 报告路径。", StepParameterSection.Advanced, false, string.Empty, 80),
                    MultiLine("uploadResult", "上传结果", "上传接口返回的结果文本。", StepParameterSection.Advanced, false, string.Empty, 90),
                    Select("stage", "业务阶段", "业务当前所处阶段。", StepParameterSection.Business, false, "Fetched", 100, BusinessStageOptions()),
                    Text("fetchedAt", "获取时间", "可填写具体时间，留空时自动写入当前时间。", StepParameterSection.Advanced, false, string.Empty, 110),
                    Text("uploadedAt", "上传时间", "可填写具体时间，留空时自动写入当前时间。", StepParameterSection.Advanced, false, string.Empty, 120),
                    Number("retryCountDelta", "重试次数增量", "在当前重试次数基础上增加的值。", StepParameterSection.Advanced, false, string.Empty, 130),
                    Boolean("markCompleted", "标记为已完成", "勾选后把业务记录标记为完成。", StepParameterSection.Advanced, false, "false", 140),
                    MultiLine("resumeStages", "可恢复阶段", "逗号或换行分隔的恢复阶段列表。", StepParameterSection.Advanced, false, "Fetched", 150)) },
                { StepType.SwitchWindow, List(
                    Text("titleContains", "窗口标题包含", "按标题关键字匹配目标窗口。", StepParameterSection.Basic, false, string.Empty, 10),
                    Text("urlContains", "窗口地址包含", "按地址关键字匹配目标窗口。", StepParameterSection.Basic, false, string.Empty, 20),
                    Select("mode", "切换模式", "如何从匹配结果中选取目标窗口。", StepParameterSection.Basic, false, "last", 30, SwitchWindowModeOptions()),
                    Number("index", "索引位置", "mode 为 index 时使用的窗口索引。", StepParameterSection.Advanced, false, string.Empty, 40),
                    Boolean("waitForNewWindow", "等待新窗口出现", "未立即找到时是否继续等待。", StepParameterSection.Advanced, false, "true", 50),
                    Boolean("excludeCurrent", "排除当前窗口", "匹配时是否排除当前窗口。", StepParameterSection.Advanced, false, "true", 60)) },
                { StepType.ExecuteScript, List(
                    MultiLine("script", "执行脚本", "要在当前 IE 页面执行的 JavaScript。", StepParameterSection.Basic, true, "window.__ieRpaResult = document.title;", 10),
                    MultiLine("resultExpression", "结果表达式", "执行后读取结果的表达式，留空时默认读取 window.__ieRpaResult。", StepParameterSection.Advanced, false, "window.__ieRpaResult", 20),
                    Text("resultVariableName", "结果变量名", "脚本执行结果写入的变量名。", StepParameterSection.Basic, false, "ScriptResult", 30)) },
                { StepType.HandleAlert, List(
                    Select("action", "弹窗动作", "处理系统弹窗时的动作。", StepParameterSection.Basic, false, "accept", 10, AlertActionOptions()),
                    Text("buttonText", "按钮文本", "要点击的弹窗按钮文本；留空时按动作推断。", StepParameterSection.Basic, false, "确定", 20),
                    Text("titleContains", "标题包含", "用于筛选目标弹窗标题的关键字。", StepParameterSection.Advanced, false, string.Empty, 30)) },
                { StepType.UploadFile, List(
                    XPath("selector", "文件输入框 XPath", "能直接写入文件路径的 file input。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/input[1]", 10),
                    XPath("clickSelector", "上传按钮 XPath", "无法直接写入时，用于触发原生选择框。", StepParameterSection.Picker, false, "xpath=/html[1]/body[1]/button[1]", 20),
                    FilePath("filePath", "文件路径", "待上传的本地文件路径。", StepParameterSection.Basic, true, @"C:\Temp\example.txt", 30),
                    Number("dialogDelayMs", "对话框等待（毫秒）", "点击上传按钮后，等待原生文件对话框弹出的时间。", StepParameterSection.Advanced, false, "800", 40)) },
                { StepType.WaitDownload, List(
                    Directory("downloadDirectory", "下载目录", "要监控的下载目录。", StepParameterSection.Basic, false, @"C:\Users\Public\Downloads", 10),
                    Text("fileName", "文件名", "按精确文件名等待下载完成。", StepParameterSection.Business, false, string.Empty, 20),
                    Text("filePattern", "文件名包含", "按文件名关键字匹配下载文件。", StepParameterSection.Business, false, string.Empty, 30),
                    Number("stableMs", "稳定时间（毫秒）", "文件大小保持不变的时间阈值。", StepParameterSection.Advanced, false, "1200", 40),
                    Text("outputVariableName", "输出变量名", "写入最终下载文件路径的变量名。", StepParameterSection.Advanced, false, "DownloadedFilePath", 50)) },
                { StepType.SetVariable, List(
                    Text("name", "变量名", "要写入的流程变量名。", StepParameterSection.Basic, true, "VarName", 10),
                    MultiLine("value", "变量值", "支持填写文本或变量占位符。", StepParameterSection.Basic, true, "Value", 20)) },
                { StepType.Condition, List(
                    Text("left", "左值", "条件判断左侧值。", StepParameterSection.Basic, true, "${ResultText}", 10),
                    Select("operator", "比较运算符", "条件判断使用的比较方式。", StepParameterSection.Basic, false, "contains", 20, ConditionOperatorOptions()),
                    Text("right", "右值", "条件判断右侧值。", StepParameterSection.Basic, false, "success", 30),
                    Text("resultVariableName", "结果变量名", "保存条件判断结果的变量名。", StepParameterSection.Advanced, false, "ConditionResult", 40),
                    Number("whenTrueStepIndex", "命中时跳转步骤", "条件为真时跳转到的步骤索引。", StepParameterSection.Advanced, false, string.Empty, 50),
                    Number("whenFalseStepIndex", "未命中时跳转步骤", "条件为假时跳转到的步骤索引。", StepParameterSection.Advanced, false, string.Empty, 60)) },
                { StepType.Loop, List(
                    Text("loopKey", "循环标识", "旧版 Loop 的循环标识。", StepParameterSection.Basic, false, "mainLoop", 10),
                    Number("repeatFromStepIndex", "回跳步骤索引", "循环体重新开始的步骤索引。", StepParameterSection.Basic, true, "0", 20),
                    Number("times", "循环次数", "旧版 Loop 的总执行次数。", StepParameterSection.Basic, false, "3", 30),
                    Text("currentIterationVariable", "当前轮次变量", "写入当前执行轮次的变量名。", StepParameterSection.Advanced, false, "LoopIteration", 40)) },
                { StepType.LoopStart, List(
                    Text("loopKey", "循环标识", "开始循环与结束循环的配对标识。", StepParameterSection.Basic, true, "monitorLoop", 10),
                    MultiLine("requiredVariables", "必填变量列表", "逗号或换行分隔，缺失时跳过本轮。", StepParameterSection.Business, false, "ApiData.orderId", 20),
                    Text("iterationVariable", "轮次变量名", "写入当前轮次编号的变量名。", StepParameterSection.Advanced, false, "LoopIteration", 30)) },
                { StepType.LoopEnd, List(
                    Text("loopKey", "循环标识", "与开始循环配对的标识。", StepParameterSection.Basic, true, "monitorLoop", 10),
                    Select("mode", "循环模式", "无限循环或固定次数循环。", StepParameterSection.Basic, false, "infinite", 20, LoopModeOptions()),
                    Number("times", "循环次数", "mode 为 counted 时的总循环次数。", StepParameterSection.Business, false, "3", 30),
                    Number("intervalMs", "轮次间隔（毫秒）", "每轮循环之间的等待时间。", StepParameterSection.Advanced, false, "5000", 40)) },
                { StepType.Delay, List(
                    Number("durationMs", "等待时长（毫秒）", "当前步骤的固定等待时长。", StepParameterSection.Basic, false, "1000", 10)) },
                { StepType.Screenshot, List(
                    FilePath("outputPath", "输出文件路径", "填写完整输出路径时会优先使用。", StepParameterSection.Advanced, false, string.Empty, 10),
                    Directory("directory", "截图目录", "未提供完整路径时，截图保存目录。", StepParameterSection.Basic, false, @"C:\Temp\IeRpaScreenshots", 20),
                    Text("fileNamePrefix", "文件名前缀", "自动生成截图文件名时使用的前缀。", StepParameterSection.Basic, false, "ie_rpa", 30),
                    Text("outputVariableName", "输出变量名", "保存截图路径的变量名。", StepParameterSection.Advanced, false, "LastScreenshotPath", 40)) },
                { StepType.WriteLog, List(
                    MultiLine("message", "日志内容", "执行时要写入运行日志的内容。", StepParameterSection.Basic, true, "业务日志内容", 10)) }
            };
        }

        private static IReadOnlyList<StepParameterDefinition> EmptyDefinitions
        {
            get { return new StepParameterDefinition[0]; }
        }

        private static IReadOnlyList<StepParameterDefinition> List(params StepParameterDefinition[] definitions)
        {
            Array.Sort(definitions, (left, right) => left.Order.CompareTo(right.Order));
            return definitions;
        }

        private static StepParameterDefinition Text(string key, string displayName, string description, StepParameterSection section, bool isRequired, string defaultValue, int order)
        {
            return Create(key, displayName, description, StepParameterEditorKind.Text, section, isRequired, defaultValue, false, order, null);
        }

        private static StepParameterDefinition Number(string key, string displayName, string description, StepParameterSection section, bool isRequired, string defaultValue, int order)
        {
            return Create(key, displayName, description, StepParameterEditorKind.Number, section, isRequired, defaultValue, false, order, null);
        }

        private static StepParameterDefinition Boolean(string key, string displayName, string description, StepParameterSection section, bool isRequired, string defaultValue, int order)
        {
            return Create(key, displayName, description, StepParameterEditorKind.Boolean, section, isRequired, defaultValue, false, order, null);
        }

        private static StepParameterDefinition Select(string key, string displayName, string description, StepParameterSection section, bool isRequired, string defaultValue, int order, IList<StepParameterOption> options)
        {
            return Create(key, displayName, description, StepParameterEditorKind.Select, section, isRequired, defaultValue, false, order, options);
        }

        private static StepParameterDefinition MultiLine(string key, string displayName, string description, StepParameterSection section, bool isRequired, string defaultValue, int order)
        {
            return Create(key, displayName, description, StepParameterEditorKind.MultiLine, section, isRequired, defaultValue, false, order, null);
        }

        private static StepParameterDefinition FilePath(string key, string displayName, string description, StepParameterSection section, bool isRequired, string defaultValue, int order)
        {
            return Create(key, displayName, description, StepParameterEditorKind.FilePath, section, isRequired, defaultValue, false, order, null);
        }

        private static StepParameterDefinition Directory(string key, string displayName, string description, StepParameterSection section, bool isRequired, string defaultValue, int order)
        {
            return Create(key, displayName, description, StepParameterEditorKind.DirectoryPath, section, isRequired, defaultValue, false, order, null);
        }

        private static StepParameterDefinition XPath(string key, string displayName, string description, StepParameterSection section, bool isRequired, string defaultValue, int order)
        {
            return Create(key, displayName, description, StepParameterEditorKind.XPath, section, isRequired, defaultValue, true, order, null);
        }

        private static StepParameterDefinition Create(string key, string displayName, string description, StepParameterEditorKind editorKind, StepParameterSection section, bool isRequired, string defaultValue, bool supportsPicker, int order, IList<StepParameterOption> options)
        {
            return new StepParameterDefinition
            {
                Key = key,
                DisplayName = displayName,
                Description = description,
                EditorKind = editorKind,
                Section = section,
                IsRequired = isRequired,
                DefaultValue = defaultValue,
                SupportsPicker = supportsPicker,
                Order = order,
                Options = options
            };
        }

        private static IList<StepParameterOption> Options(params string[] values)
        {
            var result = new List<StepParameterOption>();
            for (var index = 0; index + 1 < values.Length; index += 2)
            {
                result.Add(new StepParameterOption { DisplayName = values[index], Value = values[index + 1] });
            }

            return result;
        }

        private static IList<StepParameterOption> WindowTitleMatchOptions()
        {
            return Options("包含", "contains", "完全匹配", "exact", "前缀匹配", "startswith", "后缀匹配", "endswith");
        }

        private static IList<StepParameterOption> SelectOptionMatchOptions()
        {
            return Options("按显示文本", "text", "按 value", "value");
        }

        private static IList<StepParameterOption> FrameActionOptions()
        {
            return Options("进入 iframe", "enter", "返回父级", "parent", "返回根页面", "root");
        }

        private static IList<StepParameterOption> PageListModeOptions()
        {
            return Options("审批处理", "approve", "查询报告", "queryReport", "仅点击处理", "click");
        }

        private static IList<StepParameterOption> ReturnModeOptions()
        {
            return Options("切回原窗口", "switchOriginal", "关闭当前窗口", "closeCurrentWindow", "点击返回按钮", "clickSelector", "处理成功弹窗", "alertConfirm", "点击按钮后处理弹窗", "clickSelectorAndAlert");
        }

        private static IList<StepParameterOption> SwitchWindowModeOptions()
        {
            return Options("最后一个窗口", "last", "第一个窗口", "first", "按索引选择", "index");
        }

        private static IList<StepParameterOption> AlertActionOptions()
        {
            return Options("确认 / 接受", "accept", "取消 / 关闭", "dismiss");
        }

        private static IList<StepParameterOption> ConditionOperatorOptions()
        {
            return Options(
                "等于", "equals",
                "不等于", "not_equals",
                "包含", "contains",
                "大于", "greater_than",
                "小于", "less_than",
                "为真", "is_true",
                "为假", "is_false",
                "存在值", "exists");
        }

        private static IList<StepParameterOption> LoopModeOptions()
        {
            return Options("无限循环", "infinite", "固定次数", "counted");
        }

        private static IList<StepParameterOption> BusinessStageOptions()
        {
            return Options(
                "未设置", "None",
                "已获取", "Fetched",
                "已申请", "Applied",
                "待审批", "PendingApproval",
                "已审批", "Approved",
                "可查询", "Queryable",
                "报告已保存", "ReportSaved",
                "已上传", "Uploaded",
                "失败", "Failed");
        }
    }
}
