using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;

namespace OpenDownload.Desktop;

public sealed class BrowserSetupDialog : Window
{
    public BrowserSetupDialog()
    {
        var panel = DialogUi.Setup(this, "Connect Chrome or Edge", 630);
        DialogUi.Label(panel, "1. Open chrome://extensions or edge://extensions.\n2. Turn on Developer mode, choose Load unpacked and select this folder:");
        var folder = DialogUi.Input(panel, Path.Combine(AppContext.BaseDirectory, "browser-extension")); folder.IsReadOnly = true;
        DialogUi.Label(panel, "3. Copy the 32-letter extension ID shown by your browser and paste it here.");
        var id = DialogUi.Input(panel);
        DialogUi.Label(panel, "4. Click Register below, then open the extension and click Check connection.\n\nRegistration is for your Windows account only. IDM and other browser integrations are preserved. Repeat for another extension ID if Chrome and Edge show different IDs.");
        DialogUi.Buttons(panel, this, "Register browser helper", () =>
        {
            try
            {
                string value = id.Text.Trim(); if (!Regex.IsMatch(value, "^[a-p]{32}$")) throw new ArgumentException("Paste the 32-letter extension ID from your browser.");
                string host = Path.Combine(AppContext.BaseDirectory, "OpenDownload.NativeHost.exe");
                if (!File.Exists(host)) throw new IOException("The native helper is missing. Run the published app package.");
                string manifest = Path.Combine(App.DataDirectory, "native-host.json");
                var origins = new HashSet<string> { $"chrome-extension://{value}/" };
                if (File.Exists(manifest))
                {
                    using var old = JsonDocument.Parse(File.ReadAllText(manifest));
                    foreach (var origin in old.RootElement.GetProperty("allowed_origins").EnumerateArray())
                        if (Regex.IsMatch(origin.GetString() ?? "", "^chrome-extension://[a-p]{32}/$")) origins.Add(origin.GetString()!);
                }
                File.WriteAllText(manifest, JsonSerializer.Serialize(new { name = "org.opendownload.app", description = "OpenDownload browser integration", path = host, type = "stdio", allowed_origins = origins.ToArray() }));
                foreach (string browser in new[] { "Google\\Chrome", "Microsoft\\Edge", "Chromium" })
                {
                    using var key = Registry.CurrentUser.CreateSubKey($"Software\\{browser}\\NativeMessagingHosts\\org.opendownload.app");
                    key.SetValue("", manifest);
                }
                MessageBox.Show(this, "Registered. Open the browser extension and click Check connection.", "Browser integration ready"); DialogResult = true;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or JsonException) { MessageBox.Show(this, ex.Message, "Browser setup"); }
        });
    }
}
