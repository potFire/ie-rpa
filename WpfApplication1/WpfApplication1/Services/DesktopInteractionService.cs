using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WpfApplication1.Services
{
    public class DesktopInteractionService : IDesktopInteractionService
    {
        private const int BmClick = 0x00F5;
        private const int WmClose = 0x0010;

        public bool TryHandleDialog(string buttonText, string titleContains, int timeoutMs)
        {
            var startedAt = DateTime.UtcNow;
            while ((DateTime.UtcNow - startedAt).TotalMilliseconds < timeoutMs)
            {
                var dialog = FindDialogWindow(titleContains);
                if (dialog != IntPtr.Zero)
                {
                    // 先尝试按按钮文本点击，这样可以区分“确定/取消”等不同动作。
                    // 如果没有找到对应按钮，再退回到直接关闭窗口，至少不让流程永久卡死。
                    if (TryClickDialogButton(dialog, buttonText))
                    {
                        return true;
                    }

                    SendMessage(dialog, WmClose, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }

                System.Threading.Thread.Sleep(150);
            }

            return false;
        }

        public string CaptureDesktop(string outputPath, string directory, string fileNamePrefix)
        {
            var finalPath = ResolveScreenshotPath(outputPath, directory, fileNamePrefix);
            var finalDirectory = Path.GetDirectoryName(finalPath);
            if (!string.IsNullOrWhiteSpace(finalDirectory))
            {
                Directory.CreateDirectory(finalDirectory);
            }

            var bounds = Screen.PrimaryScreen.Bounds;
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
                }

                bitmap.Save(finalPath, ImageFormat.Png);
            }

            return finalPath;
        }

        private static string ResolveScreenshotPath(string outputPath, string directory, string fileNamePrefix)
        {
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(outputPath);
            }

            var baseDirectory = string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots")
                : Path.GetFullPath(directory);
            var prefix = string.IsNullOrWhiteSpace(fileNamePrefix) ? "screenshot" : fileNamePrefix;
            var fileName = prefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff") + ".png";
            return Path.Combine(baseDirectory, fileName);
        }

        private static IntPtr FindDialogWindow(string titleContains)
        {
            IntPtr matchedWindow = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true;
                }

                var className = GetClassNameText(hWnd);
                if (!string.Equals(className, "#32770", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var title = GetWindowTextValue(hWnd);
                if (!string.IsNullOrWhiteSpace(titleContains)
                    && title.IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return true;
                }

                matchedWindow = hWnd;
                return false;
            }, IntPtr.Zero);

            return matchedWindow;
        }

        private static bool TryClickDialogButton(IntPtr dialogHandle, string buttonText)
        {
            var matchedButton = IntPtr.Zero;
            var buttonTexts = BuildButtonCandidates(buttonText);

            EnumChildWindows(dialogHandle, delegate(IntPtr childHandle, IntPtr lParam)
            {
                var className = GetClassNameText(childHandle);
                if (!string.Equals(className, "Button", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var text = GetWindowTextValue(childHandle);
                foreach (var candidate in buttonTexts)
                {
                    if (string.Equals(text, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedButton = childHandle;
                        return false;
                    }
                }

                return true;
            }, IntPtr.Zero);

            if (matchedButton == IntPtr.Zero)
            {
                return false;
            }

            SetForegroundWindow(dialogHandle);
            SendMessage(matchedButton, BmClick, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        private static IList<string> BuildButtonCandidates(string buttonText)
        {
            var result = new List<string>();
            if (!string.IsNullOrWhiteSpace(buttonText))
            {
                result.Add(buttonText);
            }

            // IE 弹窗和系统对话框的按钮文本在不同语言环境里不一样，
            // 这里预置一组常见候选项，尽量提高在中文/英文系统里的兼容性。
            result.Add("确定");
            result.Add("是");
            result.Add("否");
            result.Add("取消");
            result.Add("OK");
            result.Add("Yes");
            result.Add("No");
            result.Add("Cancel");
            return result;
        }

        private static string GetWindowTextValue(IntPtr handle)
        {
            var builder = new StringBuilder(256);
            GetWindowText(handle, builder, builder.Capacity);
            return builder.ToString();
        }

        private static string GetClassNameText(IntPtr handle)
        {
            var builder = new StringBuilder(256);
            GetClassName(handle, builder, builder.Capacity);
            return builder.ToString();
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
