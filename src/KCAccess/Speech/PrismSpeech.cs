using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using KCAccess.Core;

namespace KCAccess.Speech
{
    /// <summary>
    /// Speech through Prism (https://github.com/ethindp/prism): picks the best available backend
    /// (NVDA, JAWS, ZoomText, … and SAPI / OneCore as fallbacks) and sends text with braille.
    /// </summary>
    internal sealed class PrismSpeech : ISpeechBackend, IDisposable
    {
        private const string Dll = "prism.dll";
        private const int PRISM_OK = 0;
        private const int PRISM_ERROR_ALREADY_INITIALIZED = 15;

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string path);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr prism_init(IntPtr cfg);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void prism_shutdown(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr prism_registry_create_best(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr prism_registry_create(IntPtr ctx, ulong id);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool prism_registry_exists(IntPtr ctx, ulong id);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void prism_backend_free(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr prism_backend_name(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int prism_backend_initialize(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int prism_backend_output(IntPtr backend, byte[] text, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int prism_backend_speak(IntPtr backend, byte[] text, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int prism_backend_stop(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr prism_error_string(int error);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr prism_version_string();

        private const ulong PRISM_BACKEND_SAPI = 0x1D6DF72422CEEE66UL;

        private IntPtr ctx;
        private IntPtr backend;
        private int consecutiveFailures;

        public string Name { get; private set; } = "none";

        public string Version { get; private set; } = "unknown";

        public bool IsReady => backend != IntPtr.Zero;

        public string LastError { get; private set; }

        /// <summary>Loads prism.dll from the plugin folder and creates the best backend.</summary>
        public bool Initialize(string pluginDir)
        {
            try
            {
                string path = Path.Combine(pluginDir, Dll);
                if (File.Exists(path)) LoadLibraryW(path);
                Version = PtrToUtf8(prism_version_string()) ?? "unknown";
                ctx = prism_init(IntPtr.Zero);
                if (ctx == IntPtr.Zero)
                {
                    LastError = "prism_init returned null";
                    return false;
                }
                return CreateBackend(preferSapi: false);
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        private bool CreateBackend(bool preferSapi)
        {
            if (backend != IntPtr.Zero)
            {
                prism_backend_free(backend);
                backend = IntPtr.Zero;
            }
            IntPtr b = IntPtr.Zero;
            if (preferSapi && prism_registry_exists(ctx, PRISM_BACKEND_SAPI)) b = prism_registry_create(ctx, PRISM_BACKEND_SAPI);
            if (b == IntPtr.Zero) b = prism_registry_create_best(ctx);
            if (b == IntPtr.Zero)
            {
                LastError = "no Prism backend available";
                return false;
            }
            int err = prism_backend_initialize(b);
            if (err != PRISM_OK && err != PRISM_ERROR_ALREADY_INITIALIZED)
            {
                LastError = "backend initialize failed: " + ErrorString(err);
                prism_backend_free(b);
                return false;
            }
            backend = b;
            Name = PtrToUtf8(prism_backend_name(b)) ?? "unknown";
            consecutiveFailures = 0;
            return true;
        }

        /// <summary>Re-detect the screen reader (e.g. after the user starts NVDA while the game runs).</summary>
        public bool Redetect()
        {
            try
            {
                if (ctx == IntPtr.Zero) ctx = prism_init(IntPtr.Zero); // connection was closed: open it again
                if (ctx == IntPtr.Zero) return false;
                return CreateBackend(preferSapi: false);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return false;
            }
        }

        public bool Speak(string text, bool interrupt)
        {
            if (backend == IntPtr.Zero) return false;
            try
            {
                byte[] utf8 = ToUtf8(text);
                int err = prism_backend_output(backend, utf8, interrupt);
                if (err != PRISM_OK) err = prism_backend_speak(backend, utf8, interrupt);
                if (err == PRISM_OK)
                {
                    consecutiveFailures = 0;
                    return true;
                }
                LastError = ErrorString(err);
                // Screen reader may have been closed: fall back to the next best backend.
                if (++consecutiveFailures >= 2 && Redetect())
                {
                    return prism_backend_output(backend, utf8, interrupt) == PRISM_OK || prism_backend_speak(backend, utf8, interrupt) == PRISM_OK;
                }
                return false;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return false;
            }
        }

        public void Stop()
        {
            if (backend == IntPtr.Zero) return;
            try
            {
                prism_backend_stop(backend);
            }
            catch
            {
                // Stop is optional for some backends.
            }
        }

        public void Dispose()
        {
            try
            {
                if (backend != IntPtr.Zero) prism_backend_free(backend);
                if (ctx != IntPtr.Zero) prism_shutdown(ctx);
            }
            catch
            {
                // Shutting down the game; nothing useful to do.
            }
            backend = IntPtr.Zero;
            ctx = IntPtr.Zero;
        }

        private static byte[] ToUtf8(string s)
        {
            int n = Encoding.UTF8.GetByteCount(s);
            var bytes = new byte[n + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }

        private static string ErrorString(int err)
        {
            try
            {
                return PtrToUtf8(prism_error_string(err)) ?? ("error " + err);
            }
            catch
            {
                return "error " + err;
            }
        }

        private static string PtrToUtf8(IntPtr p)
        {
            if (p == IntPtr.Zero) return null;
            int len = 0;
            while (Marshal.ReadByte(p, len) != 0) len++;
            var buf = new byte[len];
            Marshal.Copy(p, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }
    }

    /// <summary>
    /// What the announcer speaks through: Prism whenever it has a backend, otherwise only the BepInEx log.
    /// Deciding per message means a screen reader found later (Ctrl+Shift+F5, Ctrl+Shift+F10) is used at once even when
    /// Prism had no backend at start-up; before, the announcer stayed bound to a log-only fallback for the whole session.
    /// </summary>
    internal sealed class SwitchingSpeech : ISpeechBackend
    {
        private readonly PrismSpeech prism;

        public SwitchingSpeech(PrismSpeech prism)
        {
            this.prism = prism;
        }

        public string Name => prism != null && prism.IsReady ? prism.Name : "log only";

        public bool Speak(string text, bool interrupt) => prism == null || !prism.IsReady || prism.Speak(text, interrupt);

        public void Stop() => prism?.Stop();
    }
}
