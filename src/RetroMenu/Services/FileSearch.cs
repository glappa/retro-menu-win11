using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetroMenu.Model;

namespace RetroMenu.Services
{
    /// <summary>
    /// Searches files through the Windows Search index — the same catalogue Explorer
    /// uses, so results are instant and cover whatever the user has told Windows to
    /// index. Reached over the Search.CollatorDSO OLE DB provider through late bound
    /// ADO, which keeps the project free of extra packages.
    /// </summary>
    public static class FileSearch
    {
        private const string ConnectionString =
            "Provider=Search.CollatorDSO;Extended Properties='Application=Windows'";

        /// <summary>False once a query has shown that the index is not answering.</summary>
        public static bool IsAvailable { get; private set; } = true;

        public static List<StartItem> Query(string text, int max) => Query(text, max, null);

        public static List<StartItem> Query(string text, int max, FileFilter filter)
        {
            var results = new List<StartItem>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            string term = Sanitise(text);
            if (term.Length < 2) return results;

            object connection = null;
            object recordset = null;

            try
            {
                var type = Type.GetTypeFromProgID("ADODB.Connection");
                if (type == null) { IsAvailable = false; return results; }

                connection = Activator.CreateInstance(type);
                dynamic db = connection;
                db.Open(ConnectionString);

                // System.ItemUrl carries the path as it is on disk; ItemPathDisplay
                // would hand back the localised one — C:\Benutzer\… on a German
                // Windows — which no file API and no icon can resolve.
                string sql =
                    "SELECT TOP " + max + " System.ItemNameDisplay, System.ItemUrl " +
                    "FROM SystemIndex " +
                    "WHERE CONTAINS(System.FileName, '\"" + term + "*\"')" +
                    Conditions(filter) + " " +
                    "ORDER BY System.Search.Rank DESC";

                recordset = db.Execute(sql);
                dynamic rows = recordset;

                while (!rows.EOF && results.Count < max)
                {
                    string name = rows.Fields[0].Value as string;
                    string path = FromUrl(rows.Fields[1].Value as string);
                    rows.MoveNext();

                    if (string.IsNullOrWhiteSpace(path)) continue;
                    if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileName(path);

                    results.Add(new StartItem
                    {
                        Name = name,
                        Subtext = ShortFolder(path),
                        ParsingName = path,
                        Target = path,
                        Kind = StartItemKind.Shortcut
                    });
                }

                IsAvailable = true;
            }
            catch
            {
                // No index, service stopped, or the provider is missing.
                IsAvailable = false;
            }
            finally
            {
                Release(recordset);
                Release(connection);
            }

            return results;
        }

        /// <summary>
        /// Turns the index's "file:C:/Users/…" into a path Windows can open. Items
        /// that are not files at all — mail, OneNote pages — are dropped.
        /// </summary>
        private static string FromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            const string prefix = "file:";
            if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;

            string path = url.Substring(prefix.Length).Replace('/', '\\');
            while (path.StartsWith("\\\\", StringComparison.Ordinal) && path.Length > 2)
                path = path.Substring(1);   // file://C:/… on some builds

            return path;
        }

        /// <summary>
        /// The extra WHERE clauses of the advanced search. Everything that reaches
        /// the query string is either a fixed word from <see cref="FileFilter"/> or a
        /// path with its quotes doubled, so nothing the user types can break out.
        /// </summary>
        private static string Conditions(FileFilter filter)
        {
            if (filter == null) return string.Empty;
            var clauses = new System.Text.StringBuilder();

            // System.Kind is a multi-value property, hence the ARRAY form. Folders
            // are the exception: they are matched on the item type instead, which
            // the index fills for every directory.
            if (!string.IsNullOrEmpty(filter.Kind))
            {
                clauses.Append(filter.Kind == "folder"
                    ? " AND System.ItemType = 'Directory'"
                    : " AND System.Kind = SOME ARRAY['" + filter.Kind + "']");
            }

            if (!string.IsNullOrWhiteSpace(filter.Folder))
                clauses.Append(" AND SCOPE = 'file:" + filter.Folder.Replace("'", "''") + "'");

            if (filter.Within.HasValue)
            {
                string since = DateTime.Now.Subtract(filter.Within.Value)
                                       .ToString("yyyy-MM-dd HH:mm:ss");
                clauses.Append(" AND System.DateModified >= '" + since + "'");
            }

            return clauses.ToString();
        }

        private static void Release(object com)
        {
            if (com == null) return;
            try { System.Runtime.InteropServices.Marshal.ReleaseComObject(com); }
            catch { }
        }

        /// <summary>
        /// The query goes into a quoted CONTAINS term, so anything that could close
        /// the quote or confuse the parser has to go.
        /// </summary>
        private static string Sanitise(string text)
        {
            var clean = new System.Text.StringBuilder(text.Length);
            foreach (char c in text.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '.')
                    clean.Append(c);
            }
            return clean.ToString().Trim();
        }

        private static string ShortFolder(string file)
        {
            try
            {
                string folder = Path.GetDirectoryName(file);
                if (string.IsNullOrEmpty(folder)) return null;

                var parts = folder.Split(Path.DirectorySeparatorChar);
                return parts.Length <= 2 ? folder : string.Join("\\", parts.Skip(parts.Length - 2));
            }
            catch { return null; }
        }
    }
}
