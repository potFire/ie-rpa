using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WpfApplication1.Models;

namespace WpfApplication1.Services
{
    public class HttpFileUploadService : IHttpFileUploadService
    {
        public async Task<HttpFileUploadResult> UploadAsync(string url, string filePath, int timeoutMs, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("上传 URL 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("待上传文件不存在。", filePath);
            }

            var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            request.KeepAlive = true;

            var fileName = Path.GetFileName(filePath);
            var header = string.Format(
                "--{0}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{1}\"\r\nContent-Type: application/octet-stream\r\n\r\n",
                boundary,
                fileName);
            var footer = "\r\n--" + boundary + "--\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);
            var footerBytes = Encoding.UTF8.GetBytes(footer);

            using (var requestStream = await request.GetRequestStreamAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await requestStream.WriteAsync(headerBytes, 0, headerBytes.Length, cancellationToken);
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await fileStream.CopyToAsync(requestStream, 81920, cancellationToken);
                }

                await requestStream.WriteAsync(footerBytes, 0, footerBytes.Length, cancellationToken);
            }

            try
            {
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                {
                    return new HttpFileUploadResult
                    {
                        IsSuccess = true,
                        StatusCode = (int)response.StatusCode,
                        ResponseText = await reader.ReadToEndAsync(),
                        FilePath = filePath,
                        Url = url
                    };
                }
            }
            catch (WebException ex)
            {
                var responseText = string.Empty;
                var statusCode = 0;
                if (ex.Response != null)
                {
                    using (var response = (HttpWebResponse)ex.Response)
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                    {
                        statusCode = (int)response.StatusCode;
                        responseText = reader.ReadToEnd();
                    }
                }

                return new HttpFileUploadResult
                {
                    IsSuccess = false,
                    StatusCode = statusCode,
                    ResponseText = string.IsNullOrWhiteSpace(responseText) ? ex.Message : responseText,
                    FilePath = filePath,
                    Url = url
                };
            }
        }
    }
}