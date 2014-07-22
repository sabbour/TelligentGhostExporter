using Sgml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace TelligentExporter
{
    /// <summary>
    /// Utility class for ensuring HTML is well-formed.
    /// </summary>
    internal static class HtmlCleaner
    {
        /// <summary>
        /// Ensures the specified HTML is well-formed.
        /// </summary>
        /// <param name="htmlInput">The content to convert to well-formed HTML.
        /// </param>
        /// <param name="errors">Errors detected during the "cleanup" of the
        /// HTML input.</param>
        /// <returns>A string containing the well-formed HTML based on the
        /// specified input.</returns>
        public static string Clean(string htmlInput,  out string errors)
        {
            errors = null;

            if (string.IsNullOrEmpty(htmlInput) == true)
            {
                return htmlInput;
            }

            using (StringReader sr = new StringReader(htmlInput))
            {
                using (StringWriter errorWriter = new StringWriter(CultureInfo.CurrentCulture))
                {
                    SgmlReader reader = new SgmlReader();
                    reader.DocType = "HTML";
                    reader.CaseFolding = CaseFolding.ToLower;
                    reader.InputStream = sr;
                    reader.ErrorLog = errorWriter;

                    using (StringWriter sw = new StringWriter(
                        CultureInfo.CurrentCulture))
                    {
                        using (XmlTextWriter w = new XmlTextWriter(sw))
                        {
                            reader.Read();
                            while (reader.EOF == false)
                            {
                                w.WriteNode(reader, true);
                            }
                        }

                        errorWriter.Flush();
                        errors = errorWriter.ToString();

                        string cleanedHtml = sw.ToString();

                        return cleanedHtml;
                    }
                }
            }
        }

        /// <summary>
        /// Ensures the specified HTML fragment is well-formed.
        /// </summary>
        /// <param name="htmlFragment">The content to convert to well-formed
        /// HTML.</param>
        /// <param name="errors">Errors detected during the "cleanup" of the
        /// HTML input.</param>
        /// <returns>A string containing the well-formed HTML based on the
        /// specified input.</returns>
        public static string CleanFragment(
            string htmlFragment,
            out string errors)
        {
            errors = null;

            if (string.IsNullOrEmpty(htmlFragment) == true)
            {
                return htmlFragment;
            }

            string cleanedHtml = Clean(htmlFragment, out errors);

            Debug.Assert(string.IsNullOrEmpty(cleanedHtml) == false);
            Debug.Assert(cleanedHtml.StartsWith(
                "<html>",
                StringComparison.OrdinalIgnoreCase) == true);

            Debug.Assert(cleanedHtml.EndsWith(
                "</html>",
                StringComparison.OrdinalIgnoreCase) == true);

            cleanedHtml = cleanedHtml.Substring(
                "<html>".Length,
                cleanedHtml.Length - "<html>".Length - "</html>".Length);

            return cleanedHtml;
        }
    }

}
