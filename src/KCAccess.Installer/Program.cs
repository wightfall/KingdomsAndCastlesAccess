using System;
using System.Windows.Forms;

namespace KCAccess.Installer
{
    /// <summary>
    /// Command line: [--install] [--quiet] [--game "folder"] [--mod-language code] [--start-game]. --quiet installs the
    /// built-in package without any window; --mod-language sets the mod's language (a language file code, en or auto);
    /// --start-game starts Kingdoms and Castles afterwards.
    /// </summary>
    public sealed class Options
    {
        public bool InstallNow;
        public bool Quiet;
        public string GameDir;
        public string ModLanguage;
        public bool StartGame;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--install" || a == "/install") o.InstallNow = true;
                else if (a == "--quiet" || a == "/quiet" || a == "/s") o.Quiet = true;
                else if ((a == "--game" || a == "/game") && i + 1 < args.Length) o.GameDir = args[++i].Trim('"');
                else if ((a == "--mod-language" || a == "/mod-language") && i + 1 < args.Length) o.ModLanguage = args[++i].Trim('"');
                else if (a == "--start-game" || a == "/start-game") o.StartGame = true;
            }
            return o;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var options = Options.Parse(args);
            if (options.Quiet)
            {
                Environment.Exit(QuietInstall(options));
                return;
            }
            try
            {
                string exeDir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                string game = options.GameDir != null && SteamLibrary.IsGameFolder(options.GameDir) ? options.GameDir
                    : SteamLibrary.IsGameFolder(exeDir) ? exeDir : SteamLibrary.FindGame(SteamLibrary.SteamRoots());
                InstallerLanguage.Init(game);
            }
            catch (Exception)
            {
                // English
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(options));
        }

        /// <summary>Exit codes: 0 installed, 2 game not found, 3 game running, 4 no built-in package, 1 other error.</summary>
        private static int QuietInstall(Options options)
        {
            try
            {
                string game = options.GameDir != null && SteamLibrary.IsGameFolder(options.GameDir) ? options.GameDir : SteamLibrary.FindGame(SteamLibrary.SteamRoots());
                if (game == null) return 2;
                if (ModInstaller.GameRunning()) return 3;
                var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
                if (stream == null) return 4;
                var inst = new ModInstaller(game);
                using (stream) inst.Install(stream);
                inst.InstallUpdaterCopy(Application.ExecutablePath);
                if (options.ModLanguage != null) ModConfig.WriteLanguage(game, options.ModLanguage);
                if (options.StartGame) GameLauncher.Start(game);
                return 0;
            }
            catch
            {
                return 1;
            }
        }
    }
}
