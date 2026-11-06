using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KCAccess.Core;
using Microsoft.CSharp;
using Mono.Cecil;
using Xunit;

namespace KCAccess.Tests
{
    /// <summary>
    /// A small "game" is compiled twice, a mod is compiled against the first version, and the mod is then repaired for the
    /// second version and really run. Every kind of change a game update made in November 2026 is in here.
    /// </summary>
    public sealed class SelfRepairFixture : IDisposable
    {
        private const string Before = @"
public class Bag<T>
{
    public T[] data = new T[4];
    public int Count;
    public void Add(T item) { data[Count++] = item; }
}
public class Thing
{
    public int TreeAmount = 7;
    public int Gold = 3;
    public int Removed = 5;
    public bool visible { get { return true; } }
    public string Name { get { return ""thing""; } }
    public int Moved = 1;
}
public struct Spot { public int X; public int Z; }
public enum Mood { Calm, Angry }
public class Gone { public int Value = 5; public static Gone Make() { return new Gone(); } }
public class Keeper { public Keeper(int a) { A = a; } public int A; }
public static class Paths
{
    public static int Cost(int a, int b) { return a * 10 + b; }
    public static int Dropped(int a) { return a; }
    public static Spot Where() { var s = new Spot(); s.X = 9; return s; }
    public static Mood Feeling() { return Mood.Angry; }
    public static long Big() { return 5; }
    public static string Label() { return ""label""; }
    public static void Note() { Notes++; }
    public static int Notes;
    public static Bag<int> Numbers() { var b = new Bag<int>(); b.Add(4); return b; }
}
public class Manager
{
    public static Manager inst = new Manager();
    public int Refreshed;
    public void Refresh(bool force) { Refreshed++; }
    public int Shifted(int a, string b) { return a + b.Length; }
    public void Vanish() { }
    public Thing Pick() { return new Thing(); }
    public static T Find<T>() where T : class { return null; }
}
";

        private const string After = @"
public class Bag<T>
{
    public T[] data = new T[4];
    private int count;
    public int Count { get { return count; } }
    public void Add(T item) { data[count++] = item; }
}
public class BagRef<T> where T : class { public int Count { get { return 0; } } }
public class Thing
{
    public bool _visible = true; // public: the runtime of these tests checks access, the game's Mono does not
    public int Gold { get; set; }
    public string Name;
    public int moved = 1;
    public Thing() { Gold = 3; Name = ""thing""; }
    public bool Seen() { return _visible; }
}
public struct Spot { public int X; public int Z; }
public enum Mood { Calm, Angry }
public class Keeper { public Keeper(int a, int b) { A = a + b; } public int A; }
public static class Paths
{
    public static int Cost(int a, int b, int c) { return a * 10 + b + c; }
    public static int Notes;
    public static void Note(string why = ""none"") { Notes += why.Length; }
    public static BagRef<string> Numbers() { return new BagRef<string>(); }
}
public class Manager
{
    public static Manager inst = new Manager();
    public int Refreshed;
    public void Refresh() { Refreshed++; }
    public int Shifted(string b, int extra, int a) { return a + b.Length + extra; }
    public Thing Pick(int which = 2) { return new Thing(); }
    public static T Find<T>() where T : class { return null; }
}
";

        private const string Mod = @"
using System;
using System.Collections.Generic;
namespace HarmonyLib { public class HarmonyPatch : Attribute { } }
public class Holder { public Gone Lost; public int Kept = 2; }
public static class Native
{
    [System.Runtime.InteropServices.DllImport(""fakenative.dll"")] public static extern int Speak();
    [System.Runtime.InteropServices.DllImport(""kernel32"")] public static extern int GetTickCount();
}
[HarmonyLib.HarmonyPatch] public static class SomePatch
{
    public static bool Prefix() { var g = new Gone(); return g.Value == 0; }
    public static bool NotAPrefix() { var g = new Gone(); return g.Value == 5; }
}
public static class TheMod
{
    public static int ListCount() { var b = new Bag<string>(); b.Add(""a""); b.Add(""b""); return b.Count; }
    public static int FieldGone() { var t = new Thing(); return t.TreeAmount + t.Gold; }
    public static int FieldWrite() { var t = new Thing(); t.Gold = 8; t.Removed = 9; return t.Gold; }
    public static int FieldByRef() { var t = new Thing(); Bump(ref t.Gold); return t.Gold; }
    private static void Bump(ref int v) { v += 100; }
    public static bool PropertyToPrivateField() { return new Thing().visible; }
    public static string PropertyToField() { return new Thing().Name; }
    public static int RenamedCase() { return new Thing().Moved; }
    public static int ParamAdded() { return Paths.Cost(1, 2); }
    public static int ParamRemoved() { var m = new Manager(); m.Refresh(true); m.Refresh(false); return m.Refreshed; }
    public static int ParamsShuffled() { return new Manager().Shifted(1, ""ab""); }
    public static int MethodGone() { return Paths.Dropped(3) + 1; }
    public static void VoidGone() { new Manager().Vanish(); }
    public static int OptionalKept() { Paths.Notes = 0; Paths.Note(); return Paths.Notes; }
    public static int OptionalAddedInt() { return new Manager().Pick() != null ? 1 : 0; }
    public static int StructGone() { return Paths.Where().X; }
    public static int EnumGone() { return (int)Paths.Feeling(); }
    public static long LongGone() { return Paths.Big(); }
    public static string StringGone() { return Paths.Label() ?? ""nothing""; }
    public static int ReturnTypeChanged() { var b = Paths.Numbers(); return b == null ? -1 : b.Count; }
    public static int CtorChanged() { return new Keeper(5).A; }
    public static bool Branches(bool first)
    {
        int n = first ? new Thing().TreeAmount : 1;
        for (int i = 0; i < 300; i++) { if (i == 299) n += new Thing().Gold; else n += Paths.Dropped(i); }
        return n > 0;
    }
    public static int InTry()
    {
        try { return new Thing().Removed + 1; }
        catch (NullReferenceException) { return -1; }
        finally { Paths.Dropped(1); }
    }
    public static int TypeGoneLocal() { Gone g = Gone.Make(); return g.Value + 1; }
    public static int GoneInSignature(Gone g) { return g.Value; }
    public static int CallsGoneSignature() { return GoneInSignature(null) + 1; }
    public static int OwnFieldOfGoneType() { var h = new Holder(); h.Lost = null; return h.Kept; }
    public static int Unaffected(int a) { return a * 2; }
    public static List<int> UsesBcl() { return new List<int> { 1 }; }
    public static string Lambda() { Func<int, int> f = x => x + new Thing().Gold; return f(1).ToString(); }
}
";

        internal readonly RepairReport Report;
        internal readonly byte[] RepairedImage;
        internal readonly string NativeLibrary;
        internal readonly Assembly Repaired;
        private readonly string dir;
        private readonly Assembly newGame;

        public SelfRepairFixture()
        {
            dir = Path.Combine(Path.GetTempPath(), "kcaccess-repair-" + Guid.NewGuid().ToString("N"));
            string v1 = Directory.CreateDirectory(Path.Combine(dir, "v1")).FullName;
            string v2 = Directory.CreateDirectory(Path.Combine(dir, "v2")).FullName;
            string game1 = Compile(Before, Path.Combine(v1, "FakeGame.dll"));
            string game2 = Compile(After, Path.Combine(v2, "FakeGame.dll"));
            string mod = Compile(Mod, Path.Combine(v1, "FakeMod.dll"), game1);
            File.WriteAllBytes(Path.Combine(v1, "fakenative.dll"), new byte[1]);

            var clr = Path.GetDirectoryName(typeof(object).Assembly.Location);
            Report = SelfRepair.Repair(File.ReadAllBytes(mod), new[] { v2, clr }, "FakeMod.Repaired", v1, out byte[] repaired);
            Assert.NotNull(repaired);
            RepairedImage = repaired;
            NativeLibrary = Path.Combine(v1, "fakenative.dll");

            newGame = Assembly.LoadFrom(game2);
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            Repaired = Assembly.Load(repaired);
        }

        private Assembly Resolve(object sender, ResolveEventArgs args) => args.Name.StartsWith("FakeGame,", StringComparison.Ordinal) ? newGame : null;

        private static string Compile(string source, string output, params string[] references)
        {
            using (var compiler = new CSharpCodeProvider())
            {
                var options = new CompilerParameters { OutputAssembly = output, GenerateInMemory = false, CompilerOptions = "/target:library" };
                options.ReferencedAssemblies.Add("System.dll");
                options.ReferencedAssemblies.Add("System.Core.dll");
                options.ReferencedAssemblies.AddRange(references);
                var result = compiler.CompileAssemblyFromSource(options, source);
                var errors = result.Errors.Cast<CompilerError>().Where(e => !e.IsWarning).Select(e => e.ToString()).ToList();
                Assert.True(errors.Count == 0, string.Join("\n", errors));
                return output;
            }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
            try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } // the loaded game dll stays locked
        }
    }

    public class SelfRepairTests : IClassFixture<SelfRepairFixture>
    {
        private readonly SelfRepairFixture fixture;

        public SelfRepairTests(SelfRepairFixture fixture) => this.fixture = fixture;

        private object Call(string method, params object[] args) => fixture.Repaired.GetType("TheMod").GetMethod(method).Invoke(null, args);

        private object CallOn(string type, string method) => fixture.Repaired.GetType(type).GetMethod(method).Invoke(null, null);

        [Fact] public void FieldThatBecameAPropertyIsRead() => Assert.Equal(2, Call("ListCount"));
        [Fact] public void GoneFieldReadsAsZeroAndTheRestStillWorks() => Assert.Equal(3, Call("FieldGone"));
        [Fact] public void WritesGoToTheNewPropertyAndGoneFieldsAreSkipped() => Assert.Equal(8, Call("FieldWrite"));
        [Fact] public void FieldPassedByReferenceStillRuns() => Assert.Equal(3, Call("FieldByRef"));
        [Fact] public void PropertyRenamedToPrivateFieldIsRead() => Assert.Equal(true, Call("PropertyToPrivateField"));
        [Fact] public void PropertyThatBecameAFieldIsRead() => Assert.Equal("thing", Call("PropertyToField"));
        [Fact] public void FieldWhoseCaseChangedIsRead() => Assert.Equal(1, Call("RenamedCase"));
        [Fact] public void AddedParameterGetsZero() => Assert.Equal(12, Call("ParamAdded"));
        [Fact] public void RemovedParameterIsDropped() => Assert.Equal(2, Call("ParamRemoved"));
        [Fact] public void MovedParametersKeepTheirValues() => Assert.Equal(3, Call("ParamsShuffled"));
        [Fact] public void GoneMethodReturnsZero() => Assert.Equal(1, Call("MethodGone"));
        [Fact] public void GoneVoidMethodDoesNothing() => Call("VoidGone");
        [Fact] public void AddedOptionalParameterGetsItsDefault() => Assert.Equal(4, Call("OptionalKept"));
        [Fact] public void AddedOptionalIntegerStillCalls() => Assert.Equal(1, Call("OptionalAddedInt"));
        [Fact] public void GoneMethodReturningAStructGivesAnEmptyStruct() => Assert.Equal(0, Call("StructGone"));
        [Fact] public void GoneMethodReturningAnEnumGivesTheFirstValue() => Assert.Equal(0, Call("EnumGone"));
        [Fact] public void GoneMethodReturningALongGivesZero() => Assert.Equal(0L, Call("LongGone"));
        [Fact] public void GoneMethodReturningAStringGivesNull() => Assert.Equal("nothing", Call("StringGone"));
        [Fact] public void ChangedResultTypeIsNeverCalledWithTheOldType() => Assert.Equal(-1, Call("ReturnTypeChanged"));
        [Fact] public void ChangedConstructorIsCalled() => Assert.Equal(5, Call("CtorChanged"));
        [Fact] public void JumpsStayValidAroundLongerCode() => Assert.Equal(true, Call("Branches", true));
        [Fact] public void TryBlocksStayValid() => Assert.Equal(1, Call("InTry"));
        [Fact] public void MethodWithAGoneTypeInItsVariablesDoesNothing() => Assert.Equal(0, Call("TypeGoneLocal"));
        [Fact] public void CallerOfAMethodWithAGoneTypeKeepsRunning() => Assert.Equal(1, Call("CallsGoneSignature"));
        [Fact] public void ClassWithAFieldOfAGoneTypeStillLoads() => Assert.Equal(2, Call("OwnFieldOfGoneType"));
        [Fact] public void OtherMethodsAreUntouched() => Assert.Equal(42, Call("Unaffected", 21));
        [Fact] public void FrameworkCallsAreUntouched() => Assert.Single((List<int>)Call("UsesBcl"));
        [Fact] public void LambdasAreRepairedToo() => Assert.Equal("4", Call("Lambda"));
        [Fact] public void SilencedPrefixLetsTheGameMethodRun() => Assert.Equal(true, CallOn("SomePatch", "Prefix"));
        [Fact] public void OtherSilencedMethodsReturnFalse() => Assert.Equal(false, CallOn("SomePatch", "NotAPrefix"));

        [Fact]
        public void ReportSaysWhatChanged()
        {
            var report = fixture.Report;
            Assert.Contains(report.Adapted, a => a.StartsWith("Bag`1.Count", StringComparison.Ordinal));
            Assert.Contains(report.Adapted, a => a.StartsWith("Paths.Cost", StringComparison.Ordinal));
            Assert.Contains("Thing.TreeAmount", report.Missing);
            Assert.Contains("Paths.Dropped", report.Missing);
            Assert.Contains(report.Silenced, s => s.Contains("TypeGoneLocal"));
            Assert.True(report.Lost > 0);
            Assert.True(report.Changed);
        }

        [Fact]
        public void CopyFindsItsNativeLibrariesByFullPath()
        {
            // A copy loaded from memory has no folder: prism.dll, and so all speech, was lost this way in the first real test.
            using (var module = ModuleDefinition.ReadModule(new MemoryStream(fixture.RepairedImage)))
            {
                Assert.Contains(module.ModuleReferences, m => m.Name == fixture.NativeLibrary);
                Assert.Contains(module.ModuleReferences, m => m.Name == "kernel32"); // system libraries stay as they are
                Assert.Equal("FakeMod.Repaired", module.Assembly.Name.Name);
            }
        }

        [Fact]
        public void AModThatFitsIsNotRewritten()
        {
            // Nothing in this test assembly's own game-free references is missing.
            var dll = typeof(SelfRepair).Assembly.Location;
            var clr = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var report = SelfRepair.Repair(File.ReadAllBytes(dll), new[] { Path.GetDirectoryName(dll), clr }, null, null, out byte[] repaired);
            Assert.False(report.Changed);
            Assert.Null(repaired);
        }
    }

    public class TestedBuildTests
    {
        /// <summary>
        /// Fails when the game was updated after the last release: the new build must be tested with the mod, and the mod
        /// rebuilt against it, before CompatState.TestedGameBuild moves on.
        /// </summary>
        [Fact]
        public void ReleaseIsTestedOnTheInstalledGame()
        {
            string game = Path.Combine(GameDir, "KingdomsAndCastles_Data", "Managed", "Assembly-CSharp.dll");
            if (!File.Exists(game)) return;
            using (var module = ModuleDefinition.ReadModule(game))
                Assert.Equal(CompatState.TestedGameBuild, module.Mvid.ToString());
        }

        private static string GameDir => Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "", "Steam", "steamapps", "common", "Kingdoms and Castles");

        private static string BuiltPlugin([System.Runtime.CompilerServices.CallerFilePath] string here = "")
        {
#if DEBUG
            const string configuration = "Debug";
#else
            const string configuration = "Release";
#endif
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", "..", "src", "KCAccess", "bin", configuration, "net472", "KCAccess.dll"));
        }

        private static string[] GameDirs(string plugin) => new[]
        {
            Path.Combine(GameDir, "KingdomsAndCastles_Data", "Managed"), Path.Combine(GameDir, "BepInEx", "core"), Path.GetDirectoryName(plugin),
        };

        /// <summary>The compiler checks fields and methods; this is the same check self-repair makes in the player's game.</summary>
        [Fact]
        public void ReleaseFitsTheGame()
        {
            string plugin = BuiltPlugin();
            if (!File.Exists(plugin) || !Directory.Exists(GameDir)) return;
            var report = SelfRepair.Repair(File.ReadAllBytes(plugin), GameDirs(plugin), null, null, out byte[] repaired);
            Assert.True(!report.Changed, string.Join("\n", report.Adapted.Concat(report.Missing).Concat(report.Silenced)));
            Assert.Null(repaired);
        }

        /// <summary>A hook names its game method in a string, which the compiler cannot check.</summary>
        [Fact]
        public void EveryHookFindsItsGameMethod()
        {
            string plugin = BuiltPlugin();
            if (!File.Exists(plugin) || !Directory.Exists(GameDir)) return;
            var resolver = new DefaultAssemblyResolver();
            foreach (var dir in GameDirs(plugin)) resolver.AddSearchDirectory(dir);
            using (var module = ModuleDefinition.ReadModule(plugin, new ReaderParameters { AssemblyResolver = resolver }))
            {
                var missing = new List<string>();
                int hooks = 0;
                foreach (var type in module.GetTypes())
                {
                    TypeDefinition target = null;
                    string method = null;
                    foreach (var attribute in type.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))
                    {
                        foreach (var argument in attribute.ConstructorArguments)
                        {
                            if (argument.Value is TypeReference named) target = named.Resolve();
                            else if (argument.Value is string name && method == null) method = name;
                        }
                    }
                    if (target == null || method == null) continue;
                    hooks++;
                    bool found = false;
                    for (var t = target; t != null && !found; t = t.BaseType?.Resolve())
                        found = t.Methods.Any(m => m.Name == method) || t.Properties.Any(p => p.Name == method);
                    if (!found) missing.Add(target.Name + "." + method);
                }
                Assert.True(hooks > 30, "only " + hooks + " hooks seen");
                Assert.True(missing.Count == 0, string.Join(", ", missing));
            }
        }

        [Fact]
        public void RestGrowsAndStaysShort()
        {
            Assert.Equal(0.5f, CompatState.RestSeconds(1));
            Assert.Equal(1f, CompatState.RestSeconds(2));
            Assert.Equal(4f, CompatState.RestSeconds(4));
            Assert.Equal(30f, CompatState.RestSeconds(7));
            Assert.Equal(30f, CompatState.RestSeconds(5000));
            Assert.Equal(0.5f, CompatState.RestSeconds(0));
        }

        [Fact]
        public void NoticeTellsThePlayerWhatIsLost()
        {
            var report = new RepairReport();
            report.Missing.Add("Thing.Gold");
            report.Silenced.Add("Mod.Method");
            string build = CompatState.GameBuild;
            try
            {
                CompatState.GameBuild = "another build";
                CompatState.Report = report;
                CompatState.FailedHooks = 1;
                Assert.Contains("3 things", CompatState.Notice());
                report.Missing.Clear();
                report.Silenced.Clear();
                CompatState.FailedHooks = 0;
                CompatState.Repaired = true;
                CompatState.ToldBefore = true;
                Assert.Null(CompatState.Notice());
                CompatState.ToldBefore = false;
                Assert.Contains("adapted itself", CompatState.Notice());
                CompatState.Repaired = false;
                Assert.Contains("found nothing broken", CompatState.Notice());
                CompatState.GameBuild = CompatState.TestedGameBuild;
                Assert.Null(CompatState.Notice());
            }
            finally
            {
                CompatState.GameBuild = build;
                CompatState.Report = null;
                CompatState.Repaired = false;
                CompatState.ToldBefore = false;
                CompatState.FailedHooks = 0;
            }
        }
    }
}
