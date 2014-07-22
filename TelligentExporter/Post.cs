using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelligentExporter
{
    public class TelligentPost
    {
        public string Title { get; set; }
        public string FullUrl { get; set; }
        public string RelativeUrl { get; set; }
        public string Slug { get; set; }
        public DateTime Date { get; set; }
        public string Author { get; set; }
        public string Content { get; set; }
        public string Summary { get; set; }
        public List<string> Tags { get; set; }

    }
}
