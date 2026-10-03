using System;
using System.Windows.Forms;

namespace KCAccess.Installer
{
    /// <summary>Command line: [--install] [--quiet] [--game "folder"]. --quiet installs the built-in package without any window.</summary>
    public sealed class Options
    {
        public bool InstallNow;
        public bool Quiet;
        public string GameDir;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--install" || a == "/install") o.InstallNow = true;
                else if (a == "--quiet" || a == "/quiet" || a == "/s") o.Quiet = true;
                else if ((a == "--game" || a == "/game") && i + 1 < args.Length) o.GameDir = args[++i].Trim('"');
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
                return 0;
            }
            catch
            {
                return 1;
            }
        }
    }
}
