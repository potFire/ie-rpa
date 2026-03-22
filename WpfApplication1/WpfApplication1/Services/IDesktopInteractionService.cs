using System;

namespace WpfApplication1.Services
{
    public interface IDesktopInteractionService
    {
        bool TryHandleDialog(string buttonText, string titleContains, int timeoutMs);

        string CaptureDesktop(string outputPath, string directory, string fileNamePrefix);
    }
}
