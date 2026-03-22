using System.Collections.Generic;

namespace WpfApplication1.Models
{
    public class SelectorDefinition
    {
        public SelectorDefinition()
        {
            Attributes = new Dictionary<string, string>();
        }

        public string FramePath { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public string TagName { get; set; }

        public string Text { get; set; }

        public string XPath { get; set; }

        public Dictionary<string, string> Attributes { get; set; }

        public string DomPath { get; set; }

        public int? Index { get; set; }

        public bool UseCoordinatesFallback { get; set; }
    }
}