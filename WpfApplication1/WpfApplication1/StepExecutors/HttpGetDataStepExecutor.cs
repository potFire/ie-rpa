using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using WpfApplication1.Enums;
using WpfApplication1.Models;
using WpfApplication1.Services;
using WpfApplication1.Workflow;

namespace WpfApplication1.StepExecutors
{
    public class HttpGetDataStepExecutor : IStepExecutor
    {
        private readonly IVariableResolver _variableResolver;
        private readonly JavaScriptSerializer _serializer;

        public HttpGetDataStepExecutor(IVariableResolver variableResolver)
        {
            _variableResolver = variableResolver;
            _serializer = new JavaScriptSerializer();
        }

        public StepType StepType
        {
            get { return StepType.HttpGetData; }
        }

        public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, IExecutionContext context)
        {
            string url;
            string dataVariableName;
            string emptyAsNoDataRaw;
            string hasDataVariableNameRaw;
            string noDataStatusValuesRaw;
            step.Parameters.TryGetValue("url", out url);
            step.Parameters.TryGetValue("dataVariableName", out dataVariableName);
            step.Parameters.TryGetValue("emptyAsNoData", out emptyAsNoDataRaw);
            step.Parameters.TryGetValue("hasDataVariableName", out hasDataVariableNameRaw);
            step.Parameters.TryGetValue("noDataStatusValues", out noDataStatusValuesRaw);

            url = _variableResolver.ResolveString(url, context);
            dataVariableName = _variableResolver.ResolveString(dataVariableName, context);
            emptyAsNoDataRaw = _variableResolver.ResolveString(emptyAsNoDataRaw, context);
            hasDataVariableNameRaw = _variableResolver.ResolveString(hasDataVariableNameRaw, context);
            noDataStatusValuesRaw = _variableResolver.ResolveString(noDataStatusValuesRaw, context);

            if (string.IsNullOrWhiteSpace(url))
            {
                return StepExecutionResult.Failure("未配置 GET 请求地址。");
            }

            if (string.IsNullOrWhiteSpace(dataVariableName))
            {
                return StepExecutionResult.Failure("未配置 dataVariableName。");
            }

            var employeeId = ResolveEmployeeId(context);
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return StepExecutionResult.Failure("当前工号为空，无法发起 GET 请求。");
            }

            var requestUrl = AppendOrReplaceQueryParameter(url, "jobNo", employeeId);
            var timeoutMs = step.TimeoutMs > 0 ? step.TimeoutMs : 10000;
            var request = (HttpWebRequest)WebRequest.Create(requestUrl);
            request.Method = "GET";
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            request.Accept = "application/json";

            try
            {
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                {
                    var responseText = await reader.ReadToEndAsync();
                    return ParseResponse(responseText, dataVariableName, emptyAsNoDataRaw, hasDataVariableNameRaw, noDataStatusValuesRaw, context);
                }
            }
            catch (WebException ex)
            {
                var message = ex.Message;
                if (ex.Response != null)
                {
                    using (var response = (HttpWebResponse)ex.Response)
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                    {
                        var responseText = reader.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(responseText))
                        {
                            message = message + "，响应：" + responseText;
                        }
                    }
                }

                return StepExecutionResult.Failure("GET 请求失败：" + message, ex);
            }
        }

        private StepExecutionResult ParseResponse(
            string responseText,
            string dataVariableName,
            string emptyAsNoDataRaw,
            string hasDataVariableNameRaw,
            string noDataStatusValuesRaw,
            IExecutionContext context)
        {
            object payload;
            try
            {
                payload = _serializer.DeserializeObject(responseText);
            }
            catch (Exception ex)
            {
                return StepExecutionResult.Failure("响应不是有效的 JSON：" + ex.Message, ex);
            }

            var payloadObject = payload as IDictionary<string, object>;
            if (payloadObject == null)
            {
                return StepExecutionResult.Failure("响应 JSON 不是对象结构。");
            }

            var emptyAsNoData = CompositeIeStepHelper.ResolveBoolean(emptyAsNoDataRaw, false);
            var hasDataVariableName = string.IsNullOrWhiteSpace(hasDataVariableNameRaw) ? "HasApiData" : hasDataVariableNameRaw;
            var noDataStatusValues = ParseNoDataStatusValues(noDataStatusValuesRaw);

            object rawData;
            payloadObject.TryGetValue("data", out rawData);
            var data = NormalizeData(rawData);
            var noData = DetectNoData(payloadObject, data, emptyAsNoData, noDataStatusValues);

            context.Variables[hasDataVariableName] = !noData;
            context.Variables[dataVariableName] = noData ? string.Empty : SerializeAsJson(data);

            if (noData)
            {
                context.Variables[dataVariableName + ".status"] = "NoData";
                return StepExecutionResult.Success("GET 请求完成，当前无待处理数据。");
            }

            var dataObject = data as IDictionary<string, object>;
            if (dataObject != null)
            {
                foreach (var pair in dataObject)
                {
                    context.Variables[dataVariableName + "." + pair.Key] = ConvertToVariableValue(pair.Value);
                }
            }

            return StepExecutionResult.Success("GET 请求完成，data 已写入变量：" + dataVariableName);
        }

        private static bool DetectNoData(
            IDictionary<string, object> payloadObject,
            object data,
            bool emptyAsNoData,
            ICollection<string> noDataStatusValues)
        {
            object statusValue;
            if (payloadObject != null)
            {
                if (payloadObject.TryGetValue("status", out statusValue) || payloadObject.TryGetValue("code", out statusValue))
                {
                    var statusText = Convert.ToString(statusValue) ?? string.Empty;
                    if (noDataStatusValues.Contains(statusText))
                    {
                        return true;
                    }
                }
            }

            if (!emptyAsNoData)
            {
                return false;
            }

            if (data == null)
            {
                return true;
            }

            var text = data as string;
            if (text != null)
            {
                return string.IsNullOrWhiteSpace(text);
            }

            var collection = data as ICollection;
            if (collection != null)
            {
                return collection.Count == 0;
            }

            var enumerable = data as IEnumerable;
            if (enumerable != null)
            {
                foreach (var _ in enumerable)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static ICollection<string> ParseNoDataStatusValues(string raw)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NO_DATA",
                "EMPTY",
                "204"
            };

            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            foreach (var token in raw.Split(new[] { ',', ';', '|'}, StringSplitOptions.RemoveEmptyEntries))
            {
                result.Add(token.Trim());
            }

            return result;
        }

        private object NormalizeData(object data)
        {
            var text = data as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                return data;
            }

            var trimmed = text.Trim();
            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                return data;
            }

            try
            {
                return _serializer.DeserializeObject(trimmed);
            }
            catch
            {
                return data;
            }
        }

        private static string ResolveEmployeeId(IExecutionContext context)
        {
            object value;
            if (context.Variables.TryGetValue("EmployeeId", out value) && value != null)
            {
                return Convert.ToString(value);
            }

            if (context.Variables.TryGetValue("JobNo", out value) && value != null)
            {
                return Convert.ToString(value);
            }

            return string.Empty;
        }

        private static string AppendOrReplaceQueryParameter(string url, string parameterName, string parameterValue)
        {
            var builder = new UriBuilder(url);
            var query = ParseQueryString(builder.Query);
            query[parameterName] = parameterValue ?? string.Empty;
            builder.Query = BuildQueryString(query);
            return builder.Uri.ToString();
        }

        private static IDictionary<string, string> ParseQueryString(string rawQuery)
        {
            var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rawQuery))
            {
                return query;
            }

            var trimmed = rawQuery.TrimStart('?');
            var pairs = trimmed.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var separatorIndex = pair.IndexOf('=');
                if (separatorIndex < 0)
                {
                    query[Uri.UnescapeDataString(pair)] = string.Empty;
                    continue;
                }

                var key = Uri.UnescapeDataString(pair.Substring(0, separatorIndex));
                var value = Uri.UnescapeDataString(pair.Substring(separatorIndex + 1));
                query[key] = value;
            }

            return query;
        }

        private static string BuildQueryString(IDictionary<string, string> query)
        {
            var parts = new List<string>();
            foreach (var pair in query)
            {
                parts.Add(Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value ?? string.Empty));
            }

            return string.Join("&", parts.ToArray());
        }

        private string SerializeAsJson(object value)
        {
            return _serializer.Serialize(value);
        }

        private string ConvertToVariableValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is string || value.GetType().IsPrimitive || value is decimal)
            {
                return Convert.ToString(value);
            }

            return _serializer.Serialize(value);
        }
    }
}
