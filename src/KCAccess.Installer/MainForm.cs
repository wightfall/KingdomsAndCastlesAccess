using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KCAccess.Installer
{
    /// <summary>
    /// The setup / updater window. Built from standard Windows controls so screen readers can read it:
    /// a read-only status box (focused on start) and buttons with Alt shortcuts. Questions use Yes/No message boxes.
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly TextBox status = new TextBox();
        private readonly Button installButton = new Button();
        private readonly Button checkButton = new Button();
        private readonly Button folderButton = new Button();
        private readonly Button uninstallButton = new Button();
        private readonly Button readmeButton = new Button();
        private readonly Button closeButton = new Button();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly GitHubClient github = new GitHubClient();
        private readonly Options options;

        private string gameDir;
        private Release latest;
        private bool checkedOnline;
        private bool busy;

        /// <summary>Version of the mod packed inside this exe (same as the exe's version).</summary>
        public static Version EmbeddedVersion => Versions.Normalize(Assembly.GetExecutingAssembly().GetName().Version);

        public static bool HasPayload => Assembly.GetExecutingAssembly().GetManifestResourceInfo("payload.zip") != null;

        public MainForm(Options options)
        {
            this.options = options;
            Text = "KCAccess Setup " + Versions.Show(EmbeddedVersion);
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(620, 400);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(500, 360);
            KeyPreview = true;

            status.Multiline = true;
            status.ReadOnly = true;
            status.ScrollBars = ScrollBars.Vertical;
            status.TabIndex = 0;
            status.AccessibleName = "Status";
            status.SetBounds(12, 12, 596, 250);
            status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            status.BackColor = SystemColors.Window;

            progress.SetBounds(12, 270, 596, 18);
            progress.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            progress.AccessibleName = "Download progress";
            progress.Visible = false;

            int y = 300, x = 12;
            Setup(installButton, "&Install or update", ref x, y, 1, OnInstall);
            Setup(checkButton, "&Check for updates", ref x, y, 2, OnCheck);
            Setup(folderButton, "Choose game &folder", ref x, y, 3, OnChooseFolder);
            x = 12;
            y += 44;
            Setup(uninstallButton, "&Uninstall", ref x, y, 4, OnUninstall);
            Setup(readmeButton, "&Read me", ref x, y, 5, OnReadme);
            Setup(closeButton, "Cl&ose", ref x, y, 6, (s, e) => Close());
            CancelButton = closeButton;
            AcceptButton = installButton;

            Controls.Add(status);
            Controls.Add(progress);
            Shown += async (s, e) => await StartAsync();
        }

        private void Setup(Button b, string text, ref int x, int y, int tab, EventHandler click)
        {
            b.Text = text;
            b.TabIndex = tab;
            b.SetBounds(x, y, 190, 36);
            b.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            b.Click += click;
            Controls.Add(b);
            x += 200;
        }

        private ModInstaller Installer => gameDir != null ? new ModInstaller(gameDir) : null;

        // ------------------------------------------------------------------ start-up

        private async Task StartAsync()
        {
            status.Focus();
            gameDir = FindGameFolder();
            ShowStatus();
            if (options.InstallNow)
            {
                await InstallAsync();
                return;
            }
            await CheckOnlineAsync(interactive: false);
        }

        private string FindGameFolder()
        {
            if (options.GameDir != null && SteamLibrary.IsGameFolder(options.GameDir)) return options.GameDir;
            // The updater copy lives in the game folder.
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            if (SteamLibrary.IsGameFolder(exeDir)) return exeDir;
            return SteamLibrary.FindGame(SteamLibrary.SteamRoots());
        }

        private void ShowStatus(string extra = null)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (gameDir == null)
            {
                lines.Add("Kingdoms and Castles was not found. Press Choose game folder and select the folder that contains KingdomsAndCastles.exe.");
            }
            else
            {
                var inst = Installer;
                lines.Add("Game folder: " + gameDir);
                lines.Add("Installed KCAccess version: " + Versions.Show(inst.InstalledVersion) + ".");
                lines.Add("BepInEx: " + (inst.BepInExInstalled ? "installed." : "not installed, it will be installed together with the mod."));
            }
            lines.Add("This setup contains version " + Versions.Show(EmbeddedVersion) + (HasPayload ? "." : " (no offline package, downloads from GitHub)."));
            if (checkedOnline) lines.Add(latest != null ? "Newest version on GitHub: " + Versions.Show(latest.Version) + "." : "Could not reach GitHub to check for updates.");
            if (!string.IsNullOrEmpty(extra)) lines.Add(extra);
            lines.Add("Press Install or update to install, Check for updates to look for a newer version, Escape to close.");
            status.Text = string.Join(Environment.NewLine, lines.ToArray());
            installButton.Enabled = gameDir != null && !busy;
            uninstallButton.Enabled = gameDir != null && !busy && (Installer.InstalledVersion != null || Directory.Exists(Installer.PluginDir));
            checkButton.Enabled = folderButton.Enabled = !busy;
            readmeButton.Enabled = true;
        }

        private void Say(string text)
        {
            status.AppendText(Environment.NewLine + text);
            status.Focus();
            status.SelectionStart = status.TextLength;
            status.ScrollToCaret();
        }

        // ------------------------------------------------------------------ update check

        private async Task CheckOnlineAsync(bool interactive)
        {
            SetBusy(true, "Checking GitHub for updates...");
            try
            {
                latest = await github.GetLatestAsync();
            }
            catch (Exception ex)
            {
                latest = null;
                if (interactive) MessageBox.Show(this, "Could not check for updates: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            checkedOnline = true;
            SetBusy(false);
            ShowStatus();

            if (gameDir == null) return;
            Version installed = Installer.InstalledVersion;
            Version best = Versions.IsNewer(EmbeddedVersion, installed) ? EmbeddedVersion : installed;
            if (latest != null && Versions.IsNewer(latest.Version, best))
            {
                string have = installed == null ? "KCAccess is not installed yet" : "you have " + Versions.Show(installed);
                var answer = MessageBox.Show(this,
                    "KCAccess " + Versions.Show(latest.Version) + " is available (" + have + ").\n\n" + Shorten(latest.Body) + "\n\nDownload and install it now?",
                    "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes) await InstallAsync();
                else ShowStatus("Update postponed. Run this program again any time to install it.");
            }
            else if (installed != null && Versions.IsNewer(EmbeddedVersion, installed))
            {
                var answer = MessageBox.Show(this, "This setup contains KCAccess " + Versions.Show(EmbeddedVersion) + " and you have " + Versions.Show(installed) + ". Install it now?",
                    "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes) await InstallAsync();
            }
            else if (interactive)
            {
                MessageBox.Show(this, installed == null ? "KCAccess is not installed. Press Install or update." : "You have the newest version, " + Versions.Show(installed) + ".",
                    "No updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static string Shorten(string notes)
        {
            if (string.IsNullOrEmpty(notes)) return string.Empty;
            notes = notes.Replace("**", "").Replace("\r", "");
            return notes.Length > 600 ? notes.Substring(0, 600) + "…" : notes;
        }

        // ------------------------------------------------------------------ install

        private async Task InstallAsync()
        {
            if (gameDir == null || busy) return;
            while (ModInstaller.GameRunning())
            {
                var r = MessageBox.Show(this, "Kingdoms and Castles is running. Please close the game, then press Retry.", Text, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                if (r != DialogResult.Retry) return;
            }
            var inst = Installer;
            string tempZip = null;
            string tempSetup = null;
            try
            {
                Stream package;
                Version installing;
                bool useOnline = latest != null && Versions.IsNewer(latest.Version, EmbeddedVersion) || !HasPayload;
                if (useOnline)
                {
                    if (latest == null)
                    {
                        SetBusy(true, "Looking up the newest release...");
                        latest = await github.GetLatestAsync();
                    }
                    var asset = latest.FindAsset(inst.BepInExInstalled ? AssetKind.ModOnly : AssetKind.Bundle) ?? latest.FindAsset(AssetKind.Bundle);
                    if (asset == null) throw new InvalidOperationException("The release has no package to download.");
                    SetBusy(true, "Downloading " + asset.Name + "...");
                    progress.Visible = true;
                    tempZip = await github.DownloadAsync(asset, new Progress<int>(p => progress.Value = Math.Max(0, Math.Min(100, p))));
                    // Also fetch the new setup program so the updater in the game folder stays current.
                    var setupAsset = latest.FindAsset(AssetKind.Setup);
                    if (setupAsset != null)
                    {
                        try
                        {
                            tempSetup = await github.DownloadAsync(setupAsset, null);
                        }
                        catch
                        {
                            tempSetup = null;
                        }
                    }
                    package = File.OpenRead(tempZip);
                    installing = latest.Version;
                }
                else
                {
                    package = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
                    installing = EmbeddedVersion;
                }

                SetBusy(true, "Installing KCAccess " + Versions.Show(installing) + "...");
                int files;
                using (package)
                {
                    files = await Task.Run(() => inst.Install(package));
                }
                string updater = inst.InstallUpdaterCopy(tempSetup ?? Application.ExecutablePath);
                SetBusy(false);
                ShowStatus("Installed KCAccess " + Versions.Show(installing) + " (" + files + " files). " +
                           "The updater was copied to " + updater + ". Run it any time to check for updates.");
                MessageBox.Show(this,
                    "KCAccess " + Versions.Show(installing) + " is installed.\n\nStart Kingdoms and Castles from Steam. After a few seconds you will hear \"Main menu\". Press F1 in the game for help.\n\n" +
                    "To check for updates later, run KCAccess-Updater.exe in the game folder.",
                    "Installation complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException)
            {
                SetBusy(false);
                OfferElevation();
            }
            catch (Exception ex)
            {
                SetBusy(false);
                ShowStatus("Installation failed: " + ex.Message);
                MessageBox.Show(this, "Installation failed: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progress.Visible = false;
                TryDelete(tempZip);
                TryDelete(tempSetup);
            }
        }

        private void OfferElevation()
        {
            var r = MessageBox.Show(this, "Windows did not allow writing to the game folder. Restart setup as administrator?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--install --game \"" + gameDir + "\"") { Verb = "runas", UseShellExecute = true });
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not restart as administrator: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (path != null && File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Temporary file; Windows cleans the temp folder eventually.
            }
        }

        private void SetBusy(bool value, string message = null)
        {
            busy = value;
            UseWaitCursor = value;
            if (message != null) Say(message);
            installButton.Enabled = !value && gameDir != null;
            checkButton.Enabled = folderButton.Enabled = uninstallButton.Enabled = !value;
        }

        // ------------------------------------------------------------------ buttons

        private async void OnInstall(object sender, EventArgs e) => await InstallAsync();

        private async void OnCheck(object sender, EventArgs e) => await CheckOnlineAsync(interactive: true);

        private void OnChooseFolder(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog { Description = "Select the Kingdoms and Castles folder (it contains KingdomsAndCastles.exe)", ShowNewFolderButton = false })
            {
                if (gameDir != null) dlg.SelectedPath = gameDir;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (!SteamLibrary.IsGameFolder(dlg.SelectedPath))
                {
                    MessageBox.Show(this, "That folder does not contain KingdomsAndCastles.exe.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                gameDir = dlg.SelectedPath;
                ShowStatus();
            }
        }

        private void OnUninstall(object sender, EventArgs e)
        {
            if (gameDir == null) return;
            if (ModInstaller.GameRunning())
            {
                MessageBox.Show(this, "Please close Kingdoms and Castles first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var r = MessageBox.Show(this, "Remove KCAccess from the game?", "Uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            var loader = MessageBox.Show(this, "Also remove BepInEx? Choose No if you use other BepInEx mods.", "Uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            try
            {
                Installer.Uninstall(loader == DialogResult.Yes);
                ShowStatus("KCAccess was removed.");
                MessageBox.Show(this, "KCAccess was removed.", "Uninstall", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(this, "Windows did not allow changing the game folder. Run setup as administrator.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Uninstall failed: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnReadme(object sender, EventArgs e)
        {
            string local = gameDir != null ? Path.Combine(gameDir, ModInstaller.PluginRelative, "README.md") : null;
            try
            {
                if (local != null && File.Exists(local)) Process.Start(new ProcessStartInfo("notepad.exe", "\"" + local + "\""));
                else Process.Start(new ProcessStartInfo("https://github.com/" + GitHubClient.Owner + "/" + GitHubClient.Repo + "#readme") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the read me: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
