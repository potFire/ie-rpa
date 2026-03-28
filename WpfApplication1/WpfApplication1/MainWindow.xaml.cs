using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WpfApplication1.Models;
using WpfApplication1.ViewModels;

namespace WpfApplication1
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;
        private DesignerCanvasNodeViewModel _draggingNode;
        private Point _dragStartPoint;
        private double _dragOriginX;
        private double _dragOriginY;

        public MainWindow()
            : this(new MainWindowViewModel())
        {
        }

        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            AttachViewModel(viewModel);
        }

        private void AttachViewModel(MainWindowViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            }

            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            }
        }

        private void ViewModel_OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "DesignerLeftPaneWidth"
                || e.PropertyName == "DesignerRightPaneWidth"
                || e.PropertyName == "LogDrawerHeight"
                || e.PropertyName == "IsLogDrawerExpanded"
                || e.PropertyName == "DesignerCanvasViewportOffsetX"
                || e.PropertyName == "DesignerCanvasViewportOffsetY")
            {
                ApplyShellLayoutState();
            }
        }

        private void Window_OnLoaded(object sender, RoutedEventArgs e)
        {
            AttachViewModel(DataContext as MainWindowViewModel);
            ApplyShellLayoutState();
        }

        private void ApplyShellLayoutState()
        {
            if (_viewModel == null)
            {
                return;
            }

            DesignerLeftPaneColumn.Width = new GridLength(_viewModel.DesignerLeftPaneWidth);
            DesignerRightPaneColumn.Width = new GridLength(_viewModel.DesignerRightPaneWidth);
            LogDrawerRow.Height = _viewModel.IsLogDrawerExpanded ? new GridLength(_viewModel.LogDrawerHeight) : new GridLength(0);

            if (DesignerCanvasScrollViewer != null)
            {
                DesignerCanvasScrollViewer.ScrollToHorizontalOffset(_viewModel.DesignerCanvasViewportOffsetX);
                DesignerCanvasScrollViewer.ScrollToVerticalOffset(_viewModel.DesignerCanvasViewportOffsetY);
            }
        }

        private void DesignerGridSplitter_OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            _viewModel.DesignerLeftPaneWidth = DesignerLeftPaneColumn.ActualWidth;
            _viewModel.DesignerRightPaneWidth = DesignerRightPaneColumn.ActualWidth;
        }

        private void LogDrawerSplitter_OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (_viewModel == null || !_viewModel.IsLogDrawerExpanded)
            {
                return;
            }

            _viewModel.LogDrawerHeight = LogDrawerRow.ActualHeight;
        }

        private void LogsDataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var entry = LogsDataGrid.SelectedItem as ExecutionLogEntry;
            if (entry == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("时间: " + entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("级别: " + entry.Level);
            builder.AppendLine("步骤: " + (string.IsNullOrWhiteSpace(entry.StepName) ? "-" : entry.StepName));
            builder.AppendLine("消息: " + (entry.Message ?? string.Empty));
            if (!string.IsNullOrWhiteSpace(entry.ScreenshotPath))
            {
                builder.AppendLine("截图路径: " + entry.ScreenshotPath);
            }

            Clipboard.SetText(builder.ToString());
        }

        private void ToolboxListBox_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_viewModel == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var listBox = sender as ListBox;
            if (listBox == null)
            {
                return;
            }

            var item = listBox.SelectedItem as ToolboxStepDefinition;
            if (item == null)
            {
                return;
            }

            DragDrop.DoDragDrop(listBox, item, DragDropEffects.Copy);
        }

        private void ToolboxListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel != null && _viewModel.AddStepCommand != null && _viewModel.AddStepCommand.CanExecute(null))
            {
                _viewModel.AddStepCommand.Execute(null);
            }
        }

        private void WorkflowCatalogListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel != null && _viewModel.OpenSelectedWorkflowCommand != null && _viewModel.OpenSelectedWorkflowCommand.CanExecute(null))
            {
                _viewModel.OpenSelectedWorkflowCommand.Execute(null);
            }
        }

        private void DesignerCanvas_OnDrop(object sender, DragEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            var toolboxStep = e.Data.GetData(typeof(ToolboxStepDefinition)) as ToolboxStepDefinition;
            if (toolboxStep == null)
            {
                return;
            }

            var point = e.GetPosition(DesignerCanvas);
            _viewModel.HandleCanvasDrop(toolboxStep, point);
        }

        private void DesignerCanvasScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            _viewModel.DesignerCanvasViewportOffsetX = e.HorizontalOffset;
            _viewModel.DesignerCanvasViewportOffsetY = e.VerticalOffset;
        }

        private void DesignerNode_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            var node = border != null ? border.DataContext as DesignerCanvasNodeViewModel : null;
            if (_viewModel == null || node == null)
            {
                return;
            }

            _viewModel.SelectCanvasNode(node.StepId);
            _draggingNode = node;
            _dragStartPoint = e.GetPosition(DesignerCanvas);
            _dragOriginX = node.X;
            _dragOriginY = node.Y;
            border.CaptureMouse();
            e.Handled = true;
        }

        private void DesignerNode_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            var border = sender as Border;
            if (_viewModel == null || _draggingNode == null || border == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var current = e.GetPosition(DesignerCanvas);
            _viewModel.MoveCanvasNode(_draggingNode.StepId, _dragOriginX + (current.X - _dragStartPoint.X), _dragOriginY + (current.Y - _dragStartPoint.Y));
        }

        private void DesignerNode_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border != null)
            {
                border.ReleaseMouseCapture();
            }

            _draggingNode = null;
        }
    }
}
