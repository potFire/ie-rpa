using System.Collections.Generic;
using WpfApplication1.Models;

namespace WpfApplication1.Selectors
{
    public static class SelectorParser
    {
        public static SelectorDefinition Parse(string raw)
        {
            var selector = new SelectorDefinition();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return selector;
            }

            var segments = raw.Split(';');
            foreach (var segment in segments)
            {
                var trimmed = segment.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = trimmed.Substring(0, separatorIndex).Trim().ToLowerInvariant();
                var value = trimmed.Substring(separatorIndex + 1).Trim();

                switch (key)
                {
                    case "id":
                        selector.Id = value;
                        break;
                    case "name":
                        selector.Name = value;
                        break;
                    case "tag":
                    case "tagname":
                        selector.TagName = value;
                        break;
                    case "text":
                        selector.Text = value;
                        break;
                    case "xpath":
                        selector.XPath = value;
                        break;
                    case "frame":
                    case "framepath":
                        selector.FramePath = value;
                        break;
                    case "index":
                        int index;
                        if (int.TryParse(value, out index))
                        {
                            selector.Index = index;
                        }
                        break;
                    default:
                        if (selector.Attributes == null)
                        {
                            selector.Attributes = new Dictionary<string, string>();
                        }

                        selector.Attributes[key] = value;
                        break;
                }
            }

            return selector;
        }
    }
}