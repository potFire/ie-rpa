using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfApplication1.Automation.IE;
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
        private DispatcherTimer _pickPollTimer;
        private IIePage _pickingPage;

        public ElementPickerWindow(Action<string> applySelectorAction = null)
        {
            InitializeComponent();
            _browserService = new IeBrowserService();
            _pages = new ObservableCollection<PageItem>();
            _elements = new ObservableCollection<ElementSummary>();
            _allElements = new List<ElementSummary>();
            _applySelectorAction = applySelectorAction;
            PagesComboBox.ItemsSource = _pages;
            ElementsDataGrid.ItemsSource = _elements;
            Closed += OnClosed;
            LoadPages();
        }

        private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            StopPickSession();
            LoadPages();
        }

        private void PickButton_OnClick(object sender, RoutedEventArgs e)
        {
            StartPickSession();
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
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Element Picker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var page in pages)
            {
                _pages.Add(new PageItem
                {
                    Page = page,
                    DisplayName = BuildPageDisplay(page)
                });
            }

            if (_pages.Count > 0)
            {
                PagesComboBox.SelectedIndex = 0;
            }
            else
            {
                _allElements.Clear();
                _elements.Clear();
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
            var pageItem = PagesComboBox.SelectedItem as PageItem;
            if (pageItem == null || pageItem.Page == null)
            {
                MessageBox.Show(this, "请先选择一个已经打开的 IE 页面。", "Element Picker", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StopPickSession();

            try
            {
                pageItem.Page.ExecuteScript(BuildPickScript());
                _pickingPage = pageItem.Page;
                _pickPollTimer = new DispatcherTimer();
                _pickPollTimer.Interval = TimeSpan.FromMilliseconds(PickPollIntervalMs);
                _pickPollTimer.Tick += PickPollTimer_OnTick;
                _pickPollTimer.Start();

                MessageBox.Show(this,
                    "请切换到目标 IE 页面，直接点击要操作的元素。\r\n程序会自动计算 XPath；按 Esc 可以取消拾取。",
                    "开始拾取",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                StopPickSession();
                MessageBox.Show(this, "无法启动页面拾取：" + ex.Message, "Element Picker", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PickPollTimer_OnTick(object sender, EventArgs e)
        {
            if (_pickingPage == null)
            {
                StopPickSession();
                return;
            }

            try
            {
                var state = _pickingPage.EvaluateScript("window.__ieRpaPickerState || ''");
                if (string.Equals(state, "picked", StringComparison.OrdinalIgnoreCase))
                {
                    var picked = new ElementSummary
                    {
                        TagName = _pickingPage.EvaluateScript("window.__ieRpaPickedTag || ''"),
                        Id = _pickingPage.EvaluateScript("window.__ieRpaPickedId || ''"),
                        Name = _pickingPage.EvaluateScript("window.__ieRpaPickedName || ''"),
                        Text = _pickingPage.EvaluateScript("window.__ieRpaPickedText || ''"),
                        Selector = _pickingPage.EvaluateScript("window.__ieRpaPickedSelector || ''")
                    };

                    StopPickSession();
                    RestorePickerWindow();
                    AddOrSelectPickedElement(picked);
                    ApplySelectorAndClose(picked.Selector);
                    return;
                }

                if (string.Equals(state, "cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    StopPickSession();
                    RestorePickerWindow();
                }
            }
            catch
            {
                StopPickSession();
                RestorePickerWindow();
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
                return;
            }

            if (_applySelectorAction == null)
            {
                Clipboard.SetText(selector);
                MessageBox.Show(this, "No step is selected. The selector was copied to clipboard instead.", "Element Picker", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _applySelectorAction(selector);
            DialogResult = true;
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

            if (_pickingPage != null)
            {
                try
                {
                    _pickingPage.ExecuteScript("if (window.__ieRpaCleanupPicker) { window.__ieRpaCleanupPicker(); }");
                }
                catch
                {
                }

                _pickingPage = null;
            }
        }

        private void RestorePickerWindow()
        {
            WindowState = WindowState.Normal;
            Activate();
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
    }
}