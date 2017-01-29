using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betterpad
{
    class FileExtensions
    {
        private static List<string> Extensions = new List<string>
        {
            "txt",
            "conf",
            "ini",
            "cnf",
            "diff",
            "patch",
            "cfg",
            "xml",
            "js",
            "ini",
            "reg",
            "svg",
            "css",
            "html",
            "htm",
            "log",
            "markdown",
            "md",
            "asc",
            "0",
            "nfo",
            "1st",
            "600",
            "ME",
        };

        public static string AsFilter
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var ext in Extensions)
                {
                    sb.AppendFormat("*.{0};", ext);
                }
                sb.Remove(sb.Length - 1, 1);

                return sb.ToString();
            }
        }
    }
}
