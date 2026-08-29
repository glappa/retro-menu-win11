using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace RetroMenu.Services
{
    /// <summary>
    /// Fetches RetroBar straight from its own releases and unpacks the portable
    /// build next to this program. The portable archive is used on purpose: nothing
    /// foreign is executed, no administrator is needed, and the checksum of exactly
    /// what was downloaded can be shown before anything is unpacked.
    /// </summary>
    public static class RetroBarInstaller
    {
        private const string ReleaseApi = "https://api.github.com/repos/dremin/RetroBar/releases/latest";
        private const string AssetName = "RetroBar.Portable.zip";

        public static string InstallDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "RetroBar");

        public static string ExecutablePath => Path.Combine(InstallDirectory, "RetroBar.exe");

        public static bool IsInstalled => File.Exists(ExecutablePath);

        public static async Task<bool> InstallAsync(Action<string> log)
        {
            string archive = null;

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("retro-menu-win11-setup");

                log("Suche die neueste RetroBar-Ausgabe…");
                string json = await http.GetStringAsync(ReleaseApi);

                using var document = JsonDocument.Parse(json);
                string tag = document.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : "?";

                var asset = document.RootElement.GetProperty("assets").EnumerateArray()
                    .FirstOrDefault(a => string.Equals(
                        a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase));

                if (asset.ValueKind != JsonValueKind.Object)
                {
                    log("In dieser Ausgabe fehlt " + AssetName + ".");
                    return false;
                }

                string url = asset.GetProperty("browser_download_url").GetString();
                log($"RetroBar {tag} wird geladen…");

                byte[] data = await http.GetByteArrayAsync(url);
                log($"{data.Length / 1024 / 1024} MB geladen von {url}");
                log("SHA-256: " + Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant());

                archive = Path.Combine(Path.GetTempPath(), "RetroBar.Portable.zip");
                await File.WriteAllBytesAsync(archive, data);

                Directory.CreateDirectory(InstallDirectory);
                log("Wird entpackt nach " + InstallDirectory);
                ZipFile.ExtractToDirectory(archive, InstallDirectory, true);

                if (!IsInstalled)
                {
                    // Some archives carry a single top level folder.
                    var inner = Directory.EnumerateFiles(InstallDirectory, "RetroBar.exe",
                        SearchOption.AllDirectories).FirstOrDefault();
                    if (inner != null)
                    {
                        string from = Path.GetDirectoryName(inner);
                        foreach (var file in Directory.EnumerateFiles(from))
                            File.Move(file, Path.Combine(InstallDirectory, Path.GetFileName(file)), true);
                    }
                }

                if (!IsInstalled)
                {
                    log("RetroBar.exe war im Archiv nicht zu finden.");
                    return false;
                }

                Installer.CreateShortcut(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                        "Programs", "RetroBar.lnk"),
                    ExecutablePath, "Taskleiste im Retro-Stil");

                log("RetroBar ist eingerichtet.");
                return true;
            }
            catch (Exception ex)
            {
                log("RetroBar konnte nicht eingerichtet werden: " + ex.Message);
                return false;
            }
            finally
            {
                try { if (archive != null && File.Exists(archive)) File.Delete(archive); }
                catch { }
            }
        }
    }
}
