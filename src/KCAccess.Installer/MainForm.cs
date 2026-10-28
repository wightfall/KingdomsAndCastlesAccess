using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using KCAccess.Core;

namespace KCAccess.Installer
{
    /// <summary>
    /// The setup / updater window. Built from standard Windows controls so screen readers can read it:
    /// a read-only status box (focused on start), the language list, two check boxes and buttons with Alt shortcuts.
    /// Questions use Yes/No message boxes; an available update shows its notes in UpdateDialog.
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly TextBox status = new TextBox();
        private readonly Label languageLabel = new Label();
        private readonly ComboBox languageBox = new ComboBox();
        private readonly CheckBox useLanguageCheck = new CheckBox();
        private readonly CheckBox startGameCheck = new CheckBox();
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
        private bool fillingLanguages;

        /// <summary>Version of the mod packed inside this exe (same as the exe's version).</summary>
        public static Version EmbeddedVersion => Versions.Normalize(Assembly.GetExecutingAssembly().GetName().Version);

        public static bool HasPayload => Assembly.GetExecutingAssembly().GetManifestResourceInfo("payload.zip") != null;

        public MainForm(Options options)
        {
            this.options = options;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(620, 436);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(560, 500);
            KeyPreview = true;

            status.Multiline = true;
            status.ReadOnly = true;
            status.ScrollBars = ScrollBars.Vertical;
            status.TabIndex = 0;
            status.SetBounds(12, 50, 596, 200);
            status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            status.BackColor = SystemColors.Window;

            // Language of this window and, with the check box below, of the mod in the game.
            languageLabel.AutoSize = true;
            languageLabel.Location = new Point(12, 16);
            languageLabel.TabIndex = 1;
            languageBox.DropDownStyle = ComboBoxStyle.DropDownList;
            languageBox.SetBounds(160, 12, 448, 28);
            languageBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            languageBox.TabIndex = 2;
            languageBox.SelectedIndexChanged += OnLanguageChanged;

            progress.SetBounds(12, 256, 596, 18);
            progress.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            progress.Visible = false;

            // Both off by default: the player decides.
            SetupCheck(useLanguageCheck, 282, 3, options.ModLanguage != null);
            SetupCheck(startGameCheck, 310, 4, options.StartGame);

            int y = 344, x = 12;
            Setup(installButton, null, ref x, y, 5, OnInstall);
            Setup(checkButton, null, ref x, y, 6, OnCheck);
            Setup(folderButton, null, ref x, y, 7, OnChooseFolder);
            x = 12;
            y += 44;
            Setup(uninstallButton, null, ref x, y, 8, OnUninstall);
            Setup(readmeButton, null, ref x, y, 9, OnReadme);
            Setup(closeButton, null, ref x, y, 10, (s, e) => Close());
            CancelButton = closeButton;
            AcceptButton = installButton;

            Controls.Add(status);
            Controls.Add(languageLabel);
            Controls.Add(languageBox);
            Controls.Add(progress);
            Controls.Add(useLanguageCheck);
            Controls.Add(startGameCheck);
            if (options.ModLanguage != null) InstallerLanguage.Apply(options.GameDir ?? FindGameFolder(), Normalize(options.ModLanguage));
            ApplyTexts();
            Shown += async (s, e) => await StartAsync();
        }

        private void SetupCheck(CheckBox c, int y, int tab, bool on)
        {
            c.SetBounds(12, y, 596, 26);
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            c.TabIndex = tab;
            c.Checked = on;
        }

        /// <summary>All texts of the window in the current setup language (again after another language was chosen).</summary>
        private void ApplyTexts()
        {
            Text = Loc.F("KCAccess Setup {0}", Versions.Show(EmbeddedVersion));
            status.AccessibleName = Loc.T("Status");
            progress.AccessibleName = Loc.T("Download progress");
            languageLabel.Text = Loc.T("&Language:");
            languageBox.AccessibleName = Loc.T("Language");
            useLanguageCheck.Text = Loc.T("Use this language for the &mod in the game");
            startGameCheck.Text = Loc.T("&Start Kingdoms and Castles when setup finishes");
            installButton.Text = Loc.T("&Install or update");
            checkButton.Text = Loc.T("&Check for updates");
            folderButton.Text = Loc.T("Choose game &folder");
            uninstallButton.Text = Loc.T("&Uninstall");
            readmeButton.Text = Loc.T("&Read me");
            closeButton.Text = Loc.T("Cl&ose");
        }

        /// <summary>The language list: every language of the built-in package and of the game folder, the current one chosen.</summary>
        private void FillLanguages()
        {
            fillingLanguages = true;
            try
            {
                languageBox.Items.Clear();
                var list = InstallerLanguage.Available(gameDir);
                int selected = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    languageBox.Items.Add(list[i]);
                    if (string.Equals(list[i].Code, InstallerLanguage.Current, StringComparison.OrdinalIgnoreCase)) selected = i;
                }
                languageBox.SelectedIndex = list.Count > 0 ? selected : -1;
            }
            finally
            {
                fillingLanguages = false;
            }
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (fillingLanguages || !(languageBox.SelectedItem is InstallerLanguage.Choice choice)) return;
            if (!InstallerLanguage.Apply(gameDir, choice.Code)) return;
            ApplyTexts();
            ShowStatus();
            languageBox.Focus();
        }

        private string SelectedLanguageName => languageBox.SelectedItem is InstallerLanguage.Choice c ? c.Name : "English";

        private static string Normalize(string code) => string.Equals(code, "en", StringComparison.OrdinalIgnoreCase) ? null : code;

        private void Setup(Button b, string text, ref int x, int y, int tab, EventHandler click)
        {
            if (text != null) b.Text = text;
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
            // A previous self-update renames the running updater to *.old; remove it now.
            try
            {
                string old = Application.ExecutablePath + ".old";
                if (File.Exists(old)) File.Delete(old);
            }
            catch
            {
                // Still locked; removed next time.
            }
            gameDir = FindGameFolder();
            FillLanguages();
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
                lines.Add(Loc.T("Kingdoms and Castles was not found. Press Choose game folder and select the folder that contains KingdomsAndCastles.exe."));
            }
            else
            {
                var inst = Installer;
                lines.Add(Loc.F("Game folder: {0}", gameDir));
                lines.Add(Loc.F("Installed KCAccess version: {0}.", Versions.Show(inst.InstalledVersion)));
                lines.Add(inst.BepInExInstalled ? Loc.T("BepInEx: installed.") : Loc.T("BepInEx: not installed, it will be installed together with the mod."));
            }
            lines.Add(HasPayload ? Loc.F("This setup contains version {0}.", Versions.Show(EmbeddedVersion)) : Loc.F("This setup contains version {0} (no offline package, downloads from GitHub).", Versions.Show(EmbeddedVersion)));
            if (checkedOnline) lines.Add(latest != null ? Loc.F("Newest version on GitHub: {0}.", Versions.Show(latest.Version)) : Loc.T("Could not reach GitHub to check for updates."));
            if (!string.IsNullOrEmpty(extra)) lines.Add(extra);
            lines.Add(Loc.T("Press Install or update to install, Check for updates to look for a newer version, Escape to close."));
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
            SetBusy(true, Loc.T("Checking GitHub for updates..."));
            try
            {
                latest = await github.GetLatestAsync();
            }
            catch (Exception ex)
            {
                latest = null;
                if (interactive) MessageBox.Show(this, Loc.F("Could not check for updates: {0}", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            checkedOnline = true;
            SetBusy(false);
            ShowStatus();

            if (gameDir == null) return;
            Version installed = Installer.InstalledVersion;
            Version best = Versions.IsNewer(EmbeddedVersion, installed) ? EmbeddedVersion : installed;
            if (latest != null && Versions.IsNewer(latest.Version, best))
            {
                string have = installed == null ? Loc.T("KCAccess is not installed yet") : Loc.F("you have {0}", Versions.Show(installed));
                // What is new goes in a read-only box of its own window, not into the question.
                if (UpdateDialog.Ask(this, Loc.F("KCAccess {0} is available ({1}).", Versions.Show(latest.Version), have), latest.Body)) await InstallAsync();
                else ShowStatus(Loc.T("Update postponed. Run this program again any time to install it."));
            }
            else if (installed != null && Versions.IsNewer(EmbeddedVersion, installed))
            {
                // The notes of the built-in version when GitHub has them.
                string body = latest != null && Versions.Normalize(latest.Version) == EmbeddedVersion ? latest.Body : null;
                if (UpdateDialog.Ask(this, Loc.F("This setup contains KCAccess {0} and you have {1}.", Versions.Show(EmbeddedVersion), Versions.Show(installed)), body)) await InstallAsync();
            }
            else if (interactive)
            {
                MessageBox.Show(this, installed == null ? Loc.T("KCAccess is not installed. Press Install or update.") : Loc.F("You have the newest version, {0}.", Versions.Show(installed)),
                    Loc.T("No updates"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ------------------------------------------------------------------ install

        private async Task InstallAsync()
        {
            if (gameDir == null || busy) return;
            while (ModInstaller.GameRunning())
            {
                var r = MessageBox.Show(this, Loc.T("Kingdoms and Castles is running. Please close the game, then press Retry."), Text, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
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
                        SetBusy(true, Loc.T("Looking up the newest release..."));
                        latest = await github.GetLatestAsync();
                    }
                    var asset = latest.FindAsset(inst.BepInExInstalled ? AssetKind.ModOnly : AssetKind.Bundle) ?? latest.FindAsset(AssetKind.Bundle);
                    if (asset == null) throw new InvalidOperationException(Loc.T("The release has no package to download."));
                    SetBusy(true, Loc.F("Downloading {0}...", asset.Name));
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

                SetBusy(true, Loc.F("Installing KCAccess {0}...", Versions.Show(installing)));
                int files;
                using (package)
                {
                    files = await Task.Run(() => inst.Install(package));
                }
                string updater = inst.InstallUpdaterCopy(tempSetup ?? Application.ExecutablePath);
                SetBusy(false);
                string languageNote = null;
                if (useLanguageCheck.Checked)
                {
                    try
                    {
                        ModConfig.WriteLanguage(gameDir, InstallerLanguage.Current ?? "en");
                        languageNote = Loc.F("The mod will speak {0} in the game.", SelectedLanguageName);
                    }
                    catch (Exception ex)
                    {
                        languageNote = Loc.F("Could not save the mod's language: {0}", ex.Message);
                    }
                }
                bool startGame = startGameCheck.Checked;
                ShowStatus(Loc.F("Installed KCAccess {0} ({1} files).", Versions.Show(installing), files) + " " +
                           Loc.F("The updater was copied to {0}. Run it any time to check for updates.", updater) +
                           (languageNote != null ? " " + languageNote : string.Empty));
                MessageBox.Show(this,
                    Loc.F("KCAccess {0} is installed.", Versions.Show(installing)) + (languageNote != null ? " " + languageNote : string.Empty) + "\n\n" +
                    (startGame ? Loc.T("Kingdoms and Castles starts when you close this message. After a few seconds you will hear \"Main menu\". Press F1 in the game for help.")
                               : Loc.T("Start Kingdoms and Castles from Steam. After a few seconds you will hear \"Main menu\". Press F1 in the game for help.")) + "\n\n" +
                    Loc.T("To check for updates later, run KCAccess-Updater.exe in the game folder."),
                    Loc.T("Installation complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (startGame)
                {
                    try
                    {
                        GameLauncher.Start(gameDir);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, Loc.F("Could not start the game: {0}", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                SetBusy(false);
                OfferElevation();
            }
            catch (Exception ex)
            {
                SetBusy(false);
                ShowStatus(Loc.F("Installation failed: {0}", ex.Message));
                MessageBox.Show(this, Loc.F("Installation failed: {0}", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            var r = MessageBox.Show(this, Loc.T("Windows did not allow writing to the game folder. Restart setup as administrator?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            try
            {
                // The administrator copy keeps the player's choices.
                string args = "--install --game \"" + gameDir + "\"";
                if (useLanguageCheck.Checked) args += " --mod-language " + (InstallerLanguage.Current ?? "en");
                if (startGameCheck.Checked) args += " --start-game";
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, args) { Verb = "runas", UseShellExecute = true });
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Loc.F("Could not restart as administrator: {0}", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            using (var dlg = new FolderBrowserDialog { Description = Loc.T("Select the Kingdoms and Castles folder (it contains KingdomsAndCastles.exe)"), ShowNewFolderButton = false })
            {
                if (gameDir != null) dlg.SelectedPath = gameDir;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (!SteamLibrary.IsGameFolder(dlg.SelectedPath))
                {
                    MessageBox.Show(this, Loc.T("That folder does not contain KingdomsAndCastles.exe."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                gameDir = dlg.SelectedPath;
                FillLanguages();
                ShowStatus();
            }
        }

        private void OnUninstall(object sender, EventArgs e)
        {
            if (gameDir == null) return;
            if (ModInstaller.GameRunning())
            {
                MessageBox.Show(this, Loc.T("Please close Kingdoms and Castles first."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var r = MessageBox.Show(this, Loc.T("Remove KCAccess from the game? Your edited language files are removed too."), Loc.T("Uninstall"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            var loader = MessageBox.Show(this, Loc.T("Also remove BepInEx? Choose No if you use other BepInEx mods."), Loc.T("Uninstall"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            try
            {
                Installer.Uninstall(loader == DialogResult.Yes);
                ShowStatus(Loc.T("KCAccess was removed."));
                MessageBox.Show(this, Loc.T("KCAccess was removed."), Loc.T("Uninstall"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(this, Loc.T("Windows did not allow changing the game folder. Run setup as administrator."), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Loc.F("Uninstall failed: {0}", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show(this, Loc.F("Could not open the read me: {0}", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
