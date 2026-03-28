using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Runtime.InteropServices;
using WpfApplication1.Automation.IE;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Selectors;

namespace WpfApplication1
{
    public partial class ElementPickerWindow : Window
    {
        private const int PickPollIntervalMs = 250;
        private readonly IIeBrowserService _browserService;
        private readonly ObservableCollection<PageItem> _pages;
        private readonly ObservableCollection<ElementSummary> _elements;
        private readonly List<ElementSummary> _allElements;
        private readonly Action<string> _applySelectorAction;
        private readonly Action<LogLevel, string> _traceLogAction;
        private readonly bool _hideWindowDuringAutoPick;
        private readonly List<IIePage> _pickingPages;
        private readonly Dictionary<int, string> _lastLoggedStates;
        private readonly HashSet<int> _loggedPollErrors;
        private DispatcherTimer _pickPollTimer;
        private TaskCompletionSource<bool> _autoPickStartedSource;

        public ElementPickerWindow(Action<string> applySelectorAction = null, bool autoStartPicking = false, bool hideWindowDuringAutoPick = false, Action<LogLevel, string> traceLogAction = null)
        {
            InitializeComponent();
            _browserService = new IeBrowserService();
            _pages = new ObservableCollection<PageItem>();
            _elements = new ObservableCollection<ElementSummary>();
            _allElements = new List<ElementSummary>();
            _pickingPages = new List<IIePage>();
            _lastLoggedStates = new Dictionary<int, string>();
            _loggedPollErrors = new HashSet<int>();
            _applySelectorAction = applySelectorAction;
            _traceLogAction = traceLogAction;
            _hideWindowDuringAutoPick = hideWindowDuringAutoPick;
            PagesComboBox.ItemsSource = _pages;
            ElementsDataGrid.ItemsSource = _elements;
            Closed += OnClosed;
            ApplyWindowMode();
            LoadPages();
        }

        private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            StopPickSession();
            LoadPages();
        }

        private void PagesComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StopPickSession();
            LoadElements();
        }

        private void ElementsDataGrid_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CopySelectedSelector();
        }

        private void ElementsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HighlightSelectedElement();
        }

        private void CopySelectorButton_OnClick(object sender, RoutedEventArgs e)
        {
            CopySelectedSelector();
        }

        private void ApplySelectorButton_OnClick(object sender, RoutedEventArgs e)
        {
            ApplySelectedSelector();
        }

        private void FilterTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void LoadPages()
        {
            _pages.Clear();
            IList<IIePage> pages;
            try
            {
                pages = _browserService.GetAllPages();
                Trace(LogLevel.Info, "枚举到 " + pages.Count + " 个 IE 页面。");
            }
            catch (Exception ex)
            {
                Trace(LogLevel.Error, "枚举 IE 页面失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "Element Picker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var page in pages)
            {
                Trace(LogLevel.Info, "发现页面：" + DescribePage(page));
                _pages.Add(new PageItem
                {
                    Page = page,
                    DisplayName = BuildPageDisplay(page)
                });
            }

            if (_pages.Count > 0)
            {
                PagesComboBox.SelectedIndex = 0;
                Trace(LogLevel.Info, "默认选中页面：" + DescribePage(_pages[0].Page));
            }
            else
            {
                _allElements.Clear();
                _elements.Clear();
                Trace(LogLevel.Warning, "当前没有可用于拾取的 IE 页面。");
            }
        }

        private void LoadElements()
        {
            _allElements.Clear();
            _elements.Clear();
            var item = PagesComboBox.SelectedItem as PageItem;
            if (item == null || item.Page == null)
            {
                return;
            }

            _allElements.AddRange(item.Page.ListElements(300));
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            _elements.Clear();
            var keyword = FilterTextBox != null ? FilterTextBox.Text : null;
            var filtered = string.IsNullOrWhiteSpace(keyword)
                ? _allElements
                : _allElements.Where(item => ContainsKeyword(item, keyword)).ToList();

            foreach (var element in filtered)
            {
                _elements.Add(element);
            }
        }

        private void CopySelectedSelector()
        {
            var element = ElementsDataGrid.SelectedItem as ElementSummary;
            if (element == null || string.IsNullOrWhiteSpace(element.Selector))
            {
                return;
            }

            Clipboard.SetText(element.Selector);
            MessageBox.Show(this, "Selector copied: " + element.Selector, "Element Picker", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplySelectedSelector()
        {
            var element = ElementsDataGrid.SelectedItem as ElementSummary;
            if (element == null || string.IsNullOrWhiteSpace(element.Selector))
            {
                return;
            }

            ApplySelectorAndClose(element.Selector);
        }

        private void HighlightSelectedElement()
        {
            var element = ElementsDataGrid.SelectedItem as ElementSummary;
            var pageItem = PagesComboBox.SelectedItem as PageItem;
            if (element == null || pageItem == null || pageItem.Page == null || string.IsNullOrWhiteSpace(element.Selector))
            {
                return;
            }

            try
            {
                var selector = SelectorParser.Parse(element.Selector);
                pageItem.Page.HighlightElement(selector);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Highlight failed: " + ex.Message, "Element Picker", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StartPickSession()
        {
            var pageItems = ResolvePickPageItems();
            Trace(LogLevel.Info, "本次准备注入拾取脚本的页面数：" + pageItems.Count);
            foreach (var pageItem in pageItems)
            {
                Trace(LogLevel.Info, "候选拾取页面：" + DescribePage(pageItem.Page));
            }

            if (pageItems.Count == 0)
            {
                Trace(LogLevel.Warning, "未找到任何可拾取的 IE 页面，启动中止。");
                MessageBox.Show(this, "请先选择一个已经打开的 IE 页面。", "Element Picker", MessageBoxButton.OK, MessageBoxImage.Information);
                SignalAutoPickStarted(false);
                if (IsHiddenAutoPickMode)
                {
                    Close();
                }
                return;
            }

            StopPickSession();

            try
            {
                _pickingPages.Clear();
                _lastLoggedStates.Clear();
                _loggedPollErrors.Clear();
                foreach (var pageItem in pageItems)
                {
                    try
                    {
                        pageItem.Page.ExecuteScript(BuildPickScript());
                        _pickingPages.Add(pageItem.Page);
                        Trace(LogLevel.Info, "已注入拾取脚本：" + DescribePage(pageItem.Page));
                    }
                    catch (Exception ex)
                    {
                        Trace(LogLevel.Warning, "注入拾取脚本失败：" + DescribePage(pageItem.Page) + "，原因：" + ex.Message);
                    }
                }

                if (_pickingPages.Count == 0)
                {
                    throw new InvalidOperationException("没有可用的 IE 页面可供拾取。");
                }

                _pickPollTimer = new DispatcherTimer();
                _pickPollTimer.Interval = TimeSpan.FromMilliseconds(PickPollIntervalMs);
                _pickPollTimer.Tick += PickPollTimer_OnTick;
                _pickPollTimer.Start();
                SignalAutoPickStarted(true);
                Trace(LogLevel.Info, "页面拾取轮询已启动，当前挂载页面数：" + _pickingPages.Count);

                if (!IsHiddenAutoPickMode)
                {
                    MessageBox.Show(this,
                        "请切换到目标 IE 页面，直接点击要操作的元素。\r\n程序会自动计算 XPath；按 Esc 可以取消拾取。",
                        "开始拾取",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    WindowState = WindowState.Minimized;
                }
            }
            catch (Exception ex)
            {
                StopPickSession();
                SignalAutoPickStarted(false);
                Trace(LogLevel.Error, "启动页面拾取失败：" + ex.Message);
                MessageBox.Show(this, "无法启动页面拾取：" + ex.Message, "Element Picker", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (IsHiddenAutoPickMode)
                {
                    Close();
                }
            }
        }

        private List<PageItem> ResolvePickPageItems()
        {
            var selectedItem = PagesComboBox.SelectedItem as PageItem;
            if (!IsHiddenAutoPickMode)
            {
                if (selectedItem != null && selectedItem.Page != null)
                {
                    return new List<PageItem> { selectedItem };
                }

                return new List<PageItem>();
            }

            var result = new List<PageItem>();
            var foregroundPage = ResolveForegroundPageItem();
            if (foregroundPage != null)
            {
                PagesComboBox.SelectedItem = foregroundPage;
                result.Add(foregroundPage);
                Trace(LogLevel.Info, "前台页面命中：" + DescribePage(foregroundPage.Page));
            }
            else
            {
                Trace(LogLevel.Warning, "当前前台窗口没有命中可识别的 IE 页面。");
            }

            foreach (var page in ResolveVisiblePageItemsByZOrder())
            {
                if (!result.Any(item => item.Page.WindowHandle == page.Page.WindowHandle))
                {
                    result.Add(page);
                }
            }

            if (selectedItem != null && selectedItem.Page != null
                && !result.Any(item => item.Page.WindowHandle == selectedItem.Page.WindowHandle))
            {
                result.Add(selectedItem);
            }

            foreach (var page in _pages.Where(item => item != null && item.Page != null))
            {
                if (!result.Any(item => item.Page.WindowHandle == page.Page.WindowHandle))
                {
                    result.Add(page);
                }
            }

            if (result.Count == 0)
            {
                var lastPage = _pages.LastOrDefault(item => item != null && item.Page != null);
                if (lastPage != null)
                {
                    result.Add(lastPage);
                }
            }

            return result;
        }

        private PageItem ResolveForegroundPageItem()
        {
            var foregroundHandle = NormalizeWindowHandle(GetForegroundWindow());
            if (foregroundHandle == IntPtr.Zero)
            {
                return null;
            }

            return _pages.FirstOrDefault(item =>
                item != null
                && item.Page != null
                && NormalizeWindowHandle(new IntPtr(item.Page.WindowHandle)) == foregroundHandle);
        }

        private IEnumerable<PageItem> ResolveVisiblePageItemsByZOrder()
        {
            return _pages
                .Where(item => item != null && item.Page != null)
                .Select(item => new
                {
                    Item = item,
                    WindowHandle = NormalizeWindowHandle(new IntPtr(item.Page.WindowHandle))
                })
                .Where(entry => entry.WindowHandle != IntPtr.Zero && IsWindowVisible(entry.WindowHandle))
                .OrderBy(entry => GetWindowZOrder(entry.WindowHandle))
                .Select(entry => entry.Item)
                .ToList();
        }

        private static int GetWindowZOrder(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return int.MaxValue;
            }

            var rank = 0;
            for (var current = GetTopWindow(IntPtr.Zero); current != IntPtr.Zero; current = GetWindow(current, GwHwndNext))
            {
                if (NormalizeWindowHandle(current) == windowHandle)
                {
                    return rank;
                }

                rank++;
            }

            return int.MaxValue;
        }

        private static IntPtr NormalizeWindowHandle(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var rootHandle = GetAncestor(windowHandle, GaRoot);
            return rootHandle != IntPtr.Zero ? rootHandle : windowHandle;
        }

        private void PickPollTimer_OnTick(object sender, EventArgs e)
        {
            if (_pickingPages.Count == 0)
            {
                Trace(LogLevel.Warning, "轮询时发现没有挂载中的拾取页面，会话结束。");
                StopPickSession();
                return;
            }

            var hasAlivePage = false;
            try
            {
                foreach (var page in _pickingPages.ToList())
                {
                    string state;
                    try
                    {
                        state = page.EvaluateScript("window.__ieRpaPickerState || ''");
                        LogStateIfChanged(page, state);
                    }
                    catch (Exception ex)
                    {
                        LogPollError(page, ex);
                        continue;
                    }

                    hasAlivePage = true;
                    if (string.Equals(state, "picked", StringComparison.OrdinalIgnoreCase))
                    {
                        var picked = new ElementSummary
                        {
                            TagName = page.EvaluateScript("window.__ieRpaPickedTag || ''"),
                            Id = page.EvaluateScript("window.__ieRpaPickedId || ''"),
                            Name = page.EvaluateScript("window.__ieRpaPickedName || ''"),
                            Text = page.EvaluateScript("window.__ieRpaPickedText || ''"),
                            Selector = page.EvaluateScript("window.__ieRpaPickedSelector || ''")
                        };

                        Trace(LogLevel.Info, "页面已拾取成功：" + DescribePage(page) + "，Selector=" + (picked.Selector ?? string.Empty));
                        StopPickSession();
                        if (!IsHiddenAutoPickMode)
                        {
                            RestorePickerWindow();
                            AddOrSelectPickedElement(picked);
                        }
                        ApplySelectorAndClose(picked.Selector);
                        return;
                    }

                    if (string.Equals(state, "cancelled", StringComparison.OrdinalIgnoreCase))
                    {
                        Trace(LogLevel.Warning, "页面拾取被取消：" + DescribePage(page));
                        StopPickSession();
                        if (!IsHiddenAutoPickMode)
                        {
                            RestorePickerWindow();
                        }
                        else
                        {
                            Close();
                        }
                        return;
                    }
                }

                if (!hasAlivePage)
                {
                    Trace(LogLevel.Warning, "所有挂载页面都无法返回拾取状态，会话关闭。");
                    StopPickSession();
                    if (IsHiddenAutoPickMode)
                    {
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Trace(LogLevel.Error, "轮询拾取状态时发生异常：" + ex.Message);
                StopPickSession();
                if (!IsHiddenAutoPickMode)
                {
                    RestorePickerWindow();
                }
                else
                {
                    Close();
                }
            }
        }

        private void AddOrSelectPickedElement(ElementSummary picked)
        {
            if (picked == null || string.IsNullOrWhiteSpace(picked.Selector))
            {
                return;
            }

            _allElements.RemoveAll(item => string.Equals(item.Selector, picked.Selector, StringComparison.OrdinalIgnoreCase));
            _allElements.Insert(0, picked);
            ApplyFilter();
            ElementsDataGrid.SelectedItem = _elements.FirstOrDefault(item => string.Equals(item.Selector, picked.Selector, StringComparison.OrdinalIgnoreCase));
            if (ElementsDataGrid.SelectedItem != null)
            {
                ElementsDataGrid.ScrollIntoView(ElementsDataGrid.SelectedItem);
            }
        }

        private void ApplySelectorAndClose(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                Trace(LogLevel.Warning, "本次拾取没有返回有效的 Selector。");
                return;
            }

            if (_applySelectorAction == null)
            {
                Trace(LogLevel.Warning, "当前没有选中步骤，已改为把 Selector 复制到剪贴板：" + selector);
                Clipboard.SetText(selector);
                MessageBox.Show(this, "No step is selected. The selector was copied to clipboard instead.", "Element Picker", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Trace(LogLevel.Info, "准备把拾取结果写回当前步骤：" + selector);
            _applySelectorAction(selector);
            if (!IsHiddenAutoPickMode)
            {
                DialogResult = true;
            }
            Close();
        }

        private void StopPickSession()
        {
            if (_pickPollTimer != null)
            {
                _pickPollTimer.Stop();
                _pickPollTimer.Tick -= PickPollTimer_OnTick;
                _pickPollTimer = null;
            }

            foreach (var page in _pickingPages.ToList())
            {
                try
                {
                    page.ExecuteScript("if (window.__ieRpaCleanupPicker) { window.__ieRpaCleanupPicker(); }");
                    Trace(LogLevel.Info, "已清理拾取脚本：" + DescribePage(page));
                }
                catch (Exception ex)
                {
                    Trace(LogLevel.Warning, "清理拾取脚本失败：" + DescribePage(page) + "，原因：" + ex.Message);
                }
            }

            _pickingPages.Clear();
            _lastLoggedStates.Clear();
            _loggedPollErrors.Clear();
        }

        private void RestorePickerWindow()
        {
            WindowState = WindowState.Normal;
            Activate();
        }

        private bool IsHiddenAutoPickMode
        {
            get { return _hideWindowDuringAutoPick; }
        }

        private void ApplyWindowMode()
        {
            if (!IsHiddenAutoPickMode)
            {
                return;
            }

            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Opacity = 0;
            Width = 1;
            Height = 1;
            Left = -10000;
            Top = -10000;
        }

        public Task<bool> BeginAutoPickAsync()
        {
            _autoPickStartedSource = new TaskCompletionSource<bool>();
            Trace(LogLevel.Info, "已进入隐藏拾取准备阶段。");
            Dispatcher.BeginInvoke(new Action(StartPickSession), DispatcherPriority.ApplicationIdle);
            return _autoPickStartedSource.Task;
        }

        private void SignalAutoPickStarted(bool started)
        {
            if (_autoPickStartedSource != null && !_autoPickStartedSource.Task.IsCompleted)
            {
                _autoPickStartedSource.SetResult(started);
            }

            Trace(started ? LogLevel.Info : LogLevel.Warning, "隐藏拾取启动结果：" + (started ? "成功" : "失败"));
        }

        private void LogStateIfChanged(IIePage page, string state)
        {
            var key = GetPageLogKey(page);
            var normalizedState = string.IsNullOrWhiteSpace(state) ? "(empty)" : state;
            string previousState;
            if (_lastLoggedStates.TryGetValue(key, out previousState) && string.Equals(previousState, normalizedState, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _lastLoggedStates[key] = normalizedState;
            Trace(LogLevel.Info, "页面状态变化：" + DescribePage(page) + " -> " + normalizedState);
        }

        private void LogPollError(IIePage page, Exception ex)
        {
            var key = GetPageLogKey(page);
            if (_loggedPollErrors.Contains(key))
            {
                return;
            }

            _loggedPollErrors.Add(key);
            Trace(LogLevel.Warning, "页面轮询失败：" + DescribePage(page) + "，原因：" + ex.Message);
        }

        private void Trace(LogLevel level, string message)
        {
            if (_traceLogAction != null && !string.IsNullOrWhiteSpace(message))
            {
                _traceLogAction(level, message);
            }
        }

        private static int GetPageLogKey(IIePage page)
        {
            return page != null ? page.WindowHandle : 0;
        }

        private static string DescribePage(IIePage page)
        {
            if (page == null)
            {
                return "(null)";
            }

            return string.Format("Handle={0}, Title={1}, Url={2}",
                page.WindowHandle,
                SafeText(page.Title),
                SafeText(page.Url));
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }

        private static string BuildPickScript()
        {
            return @"
(function () {
    try {
        if (window.__ieRpaCleanupPicker) {
            window.__ieRpaCleanupPicker();
        }

        window.__ieRpaPickerState = 'pending';
        window.__ieRpaPickedSelector = '';
        window.__ieRpaPickedTag = '';
        window.__ieRpaPickedId = '';
        window.__ieRpaPickedName = '';
        window.__ieRpaPickedText = '';

        var doc = document;
        var lastElement = null;

        function normalizeText(text) {
            text = text || '';
            text = String(text).replace(/\s+/g, ' ').replace(/^\s+|\s+$/g, '');
            return text.length > 60 ? text.substring(0, 60) : text;
        }

        function setHighlight(el) {
            if (!el || !el.style) {
                return;
            }
            if (typeof el.__ieRpaPrevOutline === 'undefined') {
                el.__ieRpaPrevOutline = el.style.outline;
                el.__ieRpaPrevBackground = el.style.backgroundColor;
            }
            el.style.outline = '2px solid #ff4d4f';
            el.style.backgroundColor = '#fff2b8';
        }

        function clearHighlight(el) {
            if (!el || !el.style) {
                return;
            }
            if (typeof el.__ieRpaPrevOutline !== 'undefined') {
                el.style.outline = el.__ieRpaPrevOutline;
                el.style.backgroundColor = el.__ieRpaPrevBackground;
                el.__ieRpaPrevOutline = undefined;
                el.__ieRpaPrevBackground = undefined;
            }
        }

        function getSameTagIndex(el) {
            var index = 1;
            var sibling = el.previousSibling;
            while (sibling) {
                if (sibling.nodeType === 1 && sibling.nodeName === el.nodeName) {
                    index++;
                }
                sibling = sibling.previousSibling;
            }
            return index;
        }

        function buildXPath(el) {
            var parts = [];
            while (el && el.nodeType === 1) {
                parts.unshift('/' + String(el.nodeName).toLowerCase() + '[' + getSameTagIndex(el) + ']');
                if (String(el.nodeName).toLowerCase() === 'html') {
                    break;
                }
                el = el.parentNode;
            }
            return parts.join('');
        }

        function getTarget(evt) {
            return evt.target || evt.srcElement;
        }

        function stopEvent(evt) {
            if (!evt) {
                return false;
            }
            if (evt.preventDefault) {
                evt.preventDefault();
            }
            if (evt.stopPropagation) {
                evt.stopPropagation();
            }
            evt.returnValue = false;
            evt.cancelBubble = true;
            return false;
        }

        function onMove(evt) {
            evt = evt || window.event;
            var target = getTarget(evt);
            if (!target || target === doc.documentElement) {
                return;
            }
            if (lastElement !== target) {
                clearHighlight(lastElement);
                setHighlight(target);
                lastElement = target;
            }
        }

        function cleanup() {
            clearHighlight(lastElement);
            if (doc.removeEventListener) {
                doc.removeEventListener('mousemove', onMove, true);
                doc.removeEventListener('click', onClick, true);
                doc.removeEventListener('keydown', onKeyDown, true);
            } else if (doc.detachEvent) {
                doc.detachEvent('onmousemove', onMove);
                doc.detachEvent('onclick', onClick);
                doc.detachEvent('onkeydown', onKeyDown);
            }
            window.__ieRpaCleanupPicker = null;
        }

        function onClick(evt) {
            evt = evt || window.event;
            var target = getTarget(evt);
            if (!target || target === doc.documentElement) {
                return stopEvent(evt);
            }
            window.__ieRpaPickedSelector = 'xpath=' + buildXPath(target);
            window.__ieRpaPickedTag = String(target.nodeName || '').toLowerCase();
            window.__ieRpaPickedId = target.id || '';
            window.__ieRpaPickedName = target.name || '';
            window.__ieRpaPickedText = normalizeText(target.innerText || target.value || '');
            window.__ieRpaPickerState = 'picked';
            cleanup();
            return stopEvent(evt);
        }

        function onKeyDown(evt) {
            evt = evt || window.event;
            if ((evt.keyCode || 0) === 27) {
                window.__ieRpaPickerState = 'cancelled';
                cleanup();
                return stopEvent(evt);
            }
        }

        if (doc.addEventListener) {
            doc.addEventListener('mousemove', onMove, true);
            doc.addEventListener('click', onClick, true);
            doc.addEventListener('keydown', onKeyDown, true);
        } else if (doc.attachEvent) {
            doc.attachEvent('onmousemove', onMove);
            doc.attachEvent('onclick', onClick);
            doc.attachEvent('onkeydown', onKeyDown);
        }

        window.__ieRpaCleanupPicker = cleanup;
    } catch (ex) {
        window.__ieRpaPickerState = 'cancelled';
    }
})();";
        }

        private static bool ContainsKeyword(ElementSummary element, string keyword)
        {
            return MatchText(element.TagName, keyword)
                   || MatchText(element.Id, keyword)
                   || MatchText(element.Name, keyword)
                   || MatchText(element.Text, keyword)
                   || MatchText(element.Value, keyword)
                   || MatchText(element.Selector, keyword);
        }

        private static bool MatchText(string source, string keyword)
        {
            return !string.IsNullOrWhiteSpace(source)
                   && source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildPageDisplay(IIePage page)
        {
            var title = !string.IsNullOrWhiteSpace(page.Title) ? page.Title : "Untitled";
            return title + " | " + page.Url;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            StopPickSession();
        }

        private class PageItem
        {
            public string DisplayName { get; set; }

            public IIePage Page { get; set; }
        }

        private const uint GaRoot = 2;
        private const uint GwHwndNext = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
    }
}

