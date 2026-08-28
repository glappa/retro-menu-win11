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

        public static List<StartItem> Query(string text, int max)
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

                string sql =
                    "SELECT TOP " + max + " System.ItemNameDisplay, System.ItemPathDisplay " +
                    "FROM SystemIndex " +
                    "WHERE CONTAINS(System.FileName, '\"" + term + "*\"') " +
                    "ORDER BY System.Search.Rank DESC";

                recordset = db.Execute(sql);
                dynamic rows = recordset;

                while (!rows.EOF && results.Count < max)
                {
                    string name = rows.Fields[0].Value as string;
                    string path = rows.Fields[1].Value as string;
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
