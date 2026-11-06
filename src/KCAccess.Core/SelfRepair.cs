using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace KCAccess.Core
{
    /// <summary>What self-repair changed in a copy of the mod.</summary>
    public sealed class RepairReport
    {
        /// <summary>Game members that moved, with what the mod now uses instead.</summary>
        public readonly SortedSet<string> Adapted = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>Game members that are gone: the mod reads them as zero or nothing, and calls to them do nothing.</summary>
        public readonly SortedSet<string> Missing = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>Mod methods that cannot run at all any more, because a type they need is gone. They now do nothing.</summary>
        public readonly SortedSet<string> Silenced = new SortedSet<string>(StringComparer.Ordinal);

        /// <summary>How many things the player loses until the mod is updated.</summary>
        public int Lost => Missing.Count + Silenced.Count;

        public bool Changed => Adapted.Count > 0 || Lost > 0;

        public override string ToString() => Adapted.Count + " adapted, " + Missing.Count + " missing, " + Silenced.Count + " methods silenced";
    }

    /// <summary>
    /// Makes the mod run on a game version it was not compiled for. A compiled mod names the game's fields and methods;
    /// when an update renames or changes one, Mono refuses the whole method that names it, the moment it is first called.
    /// That is how version 1.27.0 fell silent: one field of a list class became a property and nearly every method of the
    /// mod stopped working at once, so catching errors alone could not have kept it running.
    ///
    /// This reads the mod's own compiled code and checks everything it uses from the game against the game that is
    /// installed. What moved is rewritten to the new form (field to property and back, a parameter added or removed);
    /// what is gone reads as zero or nothing, so one missing fact costs that fact and not the whole feature. It works on a
    /// copy in memory: nothing on disk changes, and the next mod version, compiled for the new game, needs none of this.
    /// </summary>
    public static class SelfRepair
    {
        /// <param name="image">The mod's dll.</param>
        /// <param name="searchDirs">Where the game's assemblies are.</param>
        /// <param name="renameTo">Assembly name of the copy (Mono returns the loaded assembly for a name it knows), or null.</param>
        /// <param name="nativeDir">Folder of the mod's native libraries, or null. Mono looks for those next to the assembly, and a
        /// copy loaded from memory has no folder: without this it would run without prism.dll, so without speech.</param>
        /// <param name="repaired">The repaired dll, or null when nothing had to change.</param>
        public static RepairReport Repair(byte[] image, IEnumerable<string> searchDirs, string renameTo, string nativeDir, out byte[] repaired)
        {
            var report = new RepairReport();
            repaired = null;
            using (var resolver = new DefaultAssemblyResolver())
            {
                resolver.RemoveSearchDirectory(".");
                resolver.RemoveSearchDirectory("bin");
                foreach (var dir in searchDirs) if (Directory.Exists(dir)) resolver.AddSearchDirectory(dir);
                var parameters = new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate };
                using (var assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(image), parameters))
                {
                    new Repairer(assembly.MainModule, report).Run();
                    if (!report.Changed) return report;
                    if (renameTo != null)
                    {
                        assembly.Name.Name = renameTo;
                        assembly.MainModule.Name = renameTo + ".dll";
                    }
                    if (nativeDir != null)
                    {
                        foreach (var library in assembly.MainModule.ModuleReferences)
                        {
                            string path = Path.Combine(nativeDir, library.Name);
                            if (File.Exists(path)) library.Name = path;
                        }
                    }
                    using (var output = new MemoryStream())
                    {
                        assembly.Write(output);
                        repaired = output.ToArray();
                    }
                }
            }
            return report;
        }

        private sealed class Repairer
        {
            private readonly ModuleDefinition module;
            private readonly RepairReport report;
            private readonly Dictionary<string, bool> typesOk = new Dictionary<string, bool>(StringComparer.Ordinal);
            private readonly Dictionary<string, bool> membersOk = new Dictionary<string, bool>(StringComparer.Ordinal);

            /// <summary>The mod's own methods and fields whose signature named a type that is gone.</summary>
            private readonly HashSet<MemberReference> retyped = new HashSet<MemberReference>();

            internal Repairer(ModuleDefinition module, RepairReport report)
            {
                this.module = module;
                this.report = report;
            }

            internal void Run()
            {
                var types = module.GetTypes().ToList();
                foreach (var type in types) FixShape(type);
                foreach (var type in types)
                    foreach (var method in type.Methods)
                        if (method.HasBody) FixBody(method);
            }

            // ------------------------------------------------------------ what exists

            /// <summary>Only what comes with the game can change under the mod.</summary>
            private bool Guarded(IMetadataScope scope)
            {
                if (scope == null || scope is ModuleDefinition) return false;
                string name = scope.Name;
                return !(name == "mscorlib" || name == "netstandard" || name == "System" || name == "0Harmony" || name.StartsWith("System.", StringComparison.Ordinal)
                    || name.StartsWith("Microsoft.", StringComparison.Ordinal) || name.StartsWith("Mono.", StringComparison.Ordinal) || name.StartsWith("BepInEx", StringComparison.Ordinal)
                    || name.StartsWith("KCAccess", StringComparison.Ordinal));
            }

            private static TypeDefinition Find(TypeReference type)
            {
                try
                {
                    return type.Resolve();
                }
                catch (AssemblyResolutionException)
                {
                    return null;
                }
            }

            private bool TypeOk(TypeReference type)
            {
                if (type == null || type is GenericParameter || type is TypeDefinition) return true;
                if (type is GenericInstanceType generic)
                {
                    if (!TypeOk(generic.ElementType)) return false;
                    foreach (var argument in generic.GenericArguments) if (!TypeOk(argument)) return false;
                    return true;
                }
                if (type is TypeSpecification specification) return TypeOk(specification.ElementType);
                if (!Guarded(type.Scope)) return true;
                if (!typesOk.TryGetValue(type.FullName, out bool ok)) typesOk[type.FullName] = ok = Find(type) != null;
                return ok;
            }

            private bool FieldOk(FieldReference field)
            {
                if (field is FieldDefinition) return !retyped.Contains(field);
                var owner = field.DeclaringType.GetElementType();
                if (!Guarded(owner.Scope)) return true;
                if (!membersOk.TryGetValue(field.FullName, out bool ok))
                {
                    FieldDefinition found = null;
                    try { found = field.Resolve(); } catch (AssemblyResolutionException) { }
                    membersOk[field.FullName] = ok = found != null;
                }
                return ok;
            }

            private bool MethodOk(MethodReference method)
            {
                if (method is MethodDefinition) return !retyped.Contains(method);
                var element = method.GetElementMethod();
                if (element is MethodDefinition) return !retyped.Contains(element);
                var owner = method.DeclaringType.GetElementType();
                if (!Guarded(owner.Scope)) return true;
                if (method.DeclaringType.IsArray) return TypeOk(owner); // Foo[,]::Get and the like belong to the runtime
                if (!membersOk.TryGetValue(element.FullName, out bool ok))
                {
                    MethodDefinition found = null;
                    try { found = element.Resolve(); } catch (AssemblyResolutionException) { }
                    membersOk[element.FullName] = ok = found != null && found.IsStatic != element.HasThis;
                }
                return ok;
            }

            private static string ShortName(MemberReference member) => member.DeclaringType != null ? member.DeclaringType.Name + "." + member.Name : member.Name;

            // ------------------------------------------------------------ the mod's own classes

            /// <summary>Own signatures that name a type that is gone would stop the class from loading at all.</summary>
            private void FixShape(TypeDefinition type)
            {
                var obj = module.TypeSystem.Object;
                if (type.BaseType != null && !TypeOk(type.BaseType))
                {
                    report.Silenced.Add(type.FullName + " : " + type.BaseType.Name);
                    type.BaseType = obj;
                }
                for (int i = type.Interfaces.Count - 1; i >= 0; i--)
                    if (!TypeOk(type.Interfaces[i].InterfaceType)) type.Interfaces.RemoveAt(i);
                foreach (var field in type.Fields)
                {
                    if (TypeOk(field.FieldType)) continue;
                    report.Silenced.Add(field.FullName);
                    field.FieldType = obj;
                    retyped.Add(field);
                }
                foreach (var property in type.Properties)
                    if (!TypeOk(property.PropertyType)) property.PropertyType = obj;
                foreach (var method in type.Methods)
                {
                    bool bad = !TypeOk(method.ReturnType);
                    if (bad) method.ReturnType = obj;
                    foreach (var parameter in method.Parameters)
                    {
                        if (TypeOk(parameter.ParameterType)) continue;
                        parameter.ParameterType = obj;
                        bad = true;
                    }
                    for (int i = method.Overrides.Count - 1; i >= 0; i--)
                        if (!MethodOk(method.Overrides[i])) method.Overrides.RemoveAt(i);
                    if (bad) retyped.Add(method);
                }
            }

            private void FixBody(MethodDefinition method)
            {
                var body = method.Body;
                bool silence = retyped.Contains(method);
                bool repair = false;
                if (!silence)
                {
                    foreach (var variable in body.Variables) silence |= !TypeOk(variable.VariableType);
                    foreach (var handler in body.ExceptionHandlers) silence |= handler.CatchType != null && !TypeOk(handler.CatchType);
                    foreach (var instruction in body.Instructions)
                    {
                        switch (instruction.Operand)
                        {
                            case FieldReference field when !FieldOk(field):
                                repair = true;
                                silence |= instruction.OpCode == OpCodes.Ldtoken;
                                silence |= IsAddress(instruction) && !TypeOk(field.FieldType);
                                break;
                            case MethodReference call when !MethodOk(call):
                                repair = true;
                                silence |= instruction.OpCode == OpCodes.Ldftn || instruction.OpCode == OpCodes.Ldvirtftn || instruction.OpCode == OpCodes.Ldtoken;
                                break;
                            case TypeReference type:
                                silence |= !TypeOk(type);
                                break;
                        }
                    }
                }
                if (silence)
                {
                    Silence(method);
                    return;
                }
                if (!repair) return;
                body.SimplifyMacros();
                foreach (var instruction in body.Instructions.ToArray())
                {
                    if (instruction.Operand is FieldReference field && !FieldOk(field)) RepairField(method, instruction, field);
                    else if (instruction.Operand is MethodReference call && !MethodOk(call)) RepairCall(method, instruction, call);
                }
                body.OptimizeMacros();
            }

            /// <summary>The whole method does nothing. A prefix keeps letting the game's own method run.</summary>
            private void Silence(MethodDefinition method)
            {
                report.Silenced.Add(method.FullName);
                var body = method.Body;
                body.Instructions.Clear();
                body.ExceptionHandlers.Clear();
                body.Variables.Clear();
                var code = new List<Instruction>();
                if (method.ReturnType.MetadataType == MetadataType.Boolean && IsPrefix(method)) code.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                else if (!method.IsConstructor) PushDefault(code, method.ReturnType, method);
                code.Add(Instruction.Create(OpCodes.Ret));
                var il = body.GetILProcessor();
                foreach (var instruction in code) il.Append(instruction);
            }

            private static bool IsPrefix(MethodDefinition method)
            {
                bool named = method.Name == "Prefix" || method.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPrefix");
                return named && method.DeclaringType.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch");
            }

            // ------------------------------------------------------------ fields

            private static bool IsAddress(Instruction i) => i.OpCode == OpCodes.Ldflda || i.OpCode == OpCodes.Ldsflda;

            private void RepairField(MethodDefinition host, Instruction i, FieldReference field)
            {
                bool isStatic = i.OpCode == OpCodes.Ldsfld || i.OpCode == OpCodes.Stsfld || i.OpCode == OpCodes.Ldsflda;
                bool store = i.OpCode == OpCodes.Stfld || i.OpCode == OpCodes.Stsfld;
                bool address = IsAddress(i);
                var code = new List<Instruction>();
                var owner = field is FieldDefinition ? null : Find(field.DeclaringType);

                // A field that became a property (ArrayExt.Count in November 2026).
                var property = owner != null && !owner.IsValueType ? FindProperty(owner, field.Name, field.FieldType, isStatic) : null;
                var accessor = property == null ? null : store ? property.SetMethod : property.GetMethod;
                if (accessor != null)
                {
                    var call = Instruction.Create(isStatic ? OpCodes.Call : OpCodes.Callvirt, Reference(accessor, field.DeclaringType));
                    code.Add(call);
                    if (address)
                    {
                        var copy = Temp(host, field.FieldType);
                        code.Add(Instruction.Create(OpCodes.Stloc, copy));
                        code.Add(Instruction.Create(OpCodes.Ldloca, copy));
                    }
                    report.Adapted.Add(ShortName(field) + ": property");
                    Replace(host, i, code);
                    return;
                }

                // Renamed to its private spelling, as in visible and _visible.
                var alias = owner != null ? FindField(owner, field.Name, field.FieldType, isStatic) : null;
                if (alias != null)
                {
                    var moved = new FieldReference(alias.Name, field.FieldType, field.DeclaringType);
                    report.Adapted.Add(ShortName(field) + ": now " + alias.Name);
                    code.Add(Instruction.Create(i.OpCode, moved));
                    Replace(host, i, code);
                    return;
                }

                report.Missing.Add(ShortName(field));
                if (!isStatic) code.Add(Instruction.Create(OpCodes.Pop));
                if (store)
                {
                    code.Insert(0, Instruction.Create(OpCodes.Pop)); // the value, above the object
                }
                else if (address)
                {
                    var blank = Temp(host, field.FieldType);
                    code.Add(Instruction.Create(OpCodes.Ldloca, blank));
                    code.Add(Instruction.Create(OpCodes.Initobj, field.FieldType));
                    code.Add(Instruction.Create(OpCodes.Ldloca, blank));
                }
                else
                {
                    PushDefault(code, field.FieldType, host);
                }
                Replace(host, i, code);
            }

            private PropertyDefinition FindProperty(TypeDefinition owner, string name, TypeReference type, bool isStatic)
            {
                for (var t = owner; t != null; t = t.BaseType != null ? Find(t.BaseType) : null)
                    foreach (var property in t.Properties)
                    {
                        if (property.Name != name || property.PropertyType.FullName != type.FullName) continue;
                        var any = property.GetMethod ?? property.SetMethod;
                        if (any != null && any.IsStatic == isStatic) return property;
                    }
                return null;
            }

            /// <summary>The one field that differs from <paramref name="name"/> only in case or a leading _ or m_.</summary>
            private FieldDefinition FindField(TypeDefinition owner, string name, TypeReference type, bool isStatic)
            {
                string plain = Plain(name);
                FieldDefinition found = null;
                for (var t = owner; t != null; t = t.BaseType != null ? Find(t.BaseType) : null)
                    foreach (var field in t.Fields)
                    {
                        if (field.IsStatic != isStatic || field.FieldType.FullName != type.FullName || Plain(field.Name) != plain) continue;
                        if (found != null) return null; // two candidates: a guess could read the wrong one
                        found = field;
                    }
                return found;
            }

            private static string Plain(string name)
            {
                if (name.StartsWith("m_", StringComparison.Ordinal)) name = name.Substring(2);
                return name.TrimStart('_').ToLowerInvariant();
            }

            private MethodReference Reference(MethodDefinition method, TypeReference declaringType)
            {
                var reference = module.ImportReference(method);
                if (declaringType is GenericInstanceType) reference.DeclaringType = declaringType;
                return reference;
            }

            // ------------------------------------------------------------ calls

            private void RepairCall(MethodDefinition host, Instruction i, MethodReference call)
            {
                var owner = Find(call.DeclaringType);
                var code = new List<Instruction>();
                if (owner != null && !(call is GenericInstanceMethod) && !(call is MethodDefinition))
                {
                    if (i.OpCode != OpCodes.Newobj && AccessorToField(i, call, owner, code)
                        || Overload(host, i, call, owner, code))
                    {
                        Replace(host, i, code);
                        return;
                    }
                }

                report.Missing.Add(ShortName(call));
                int pops = call.Parameters.Count + (call.HasThis && i.OpCode != OpCodes.Newobj ? 1 : 0);
                for (int n = 0; n < pops; n++) code.Add(Instruction.Create(OpCodes.Pop));
                if (i.OpCode == OpCodes.Newobj) PushDefault(code, call.DeclaringType, host);
                else PushDefault(code, Closed(call.ReturnType, call), host);
                Replace(host, i, code);
            }

            /// <summary>A property that became a field: its reader or writer is now a plain field access.</summary>
            private bool AccessorToField(Instruction i, MethodReference call, TypeDefinition owner, List<Instruction> code)
            {
                bool get = call.Name.StartsWith("get_", StringComparison.Ordinal) && call.Parameters.Count == 0;
                bool set = call.Name.StartsWith("set_", StringComparison.Ordinal) && call.Parameters.Count == 1;
                if ((!get && !set) || owner.IsValueType) return false;
                var type = get ? call.ReturnType : call.Parameters[0].ParameterType;
                var field = FindField(owner, call.Name.Substring(4), type, !call.HasThis);
                if (field == null) return false;
                var target = new FieldReference(field.Name, type, call.DeclaringType);
                var op = call.HasThis ? (get ? OpCodes.Ldfld : OpCodes.Stfld) : (get ? OpCodes.Ldsfld : OpCodes.Stsfld);
                code.Add(Instruction.Create(op, target));
                report.Adapted.Add(ShortName(call) + ": field " + field.Name);
                return true;
            }

            /// <summary>
            /// The method with this name that best takes the same arguments: parameters added get zero or their default,
            /// parameters removed are dropped. Refused when more is invented than kept, or the result changes type.
            /// </summary>
            private bool Overload(MethodDefinition host, Instruction i, MethodReference call, TypeDefinition owner, List<Instruction> code)
            {
                if (HasOpenType(call)) return false;
                MethodDefinition best = null;
                int[] bestMap = null;
                int bestKept = -1, bestInvented = int.MaxValue;
                foreach (var candidate in Overloads(owner, call))
                {
                    var map = Map(call, candidate, out int kept);
                    int invented = 0; // parameters that need a made-up value: an optional one has its own
                    for (int p = 0; p < map.Length; p++) if (map[p] < 0 && !candidate.Parameters[p].HasConstant && !candidate.Parameters[p].IsOptional) invented++;
                    if (invented > kept || !ResultFits(call.ReturnType, candidate.ReturnType)) continue;
                    if (kept > bestKept || (kept == bestKept && invented < bestInvented))
                    {
                        best = candidate;
                        bestMap = map;
                        bestKept = kept;
                        bestInvented = invented;
                    }
                }
                if (best == null) return false;

                var arguments = new VariableDefinition[call.Parameters.Count];
                for (int a = arguments.Length - 1; a >= 0; a--)
                {
                    arguments[a] = Temp(host, call.Parameters[a].ParameterType);
                    code.Add(Instruction.Create(OpCodes.Stloc, arguments[a]));
                }
                for (int p = 0; p < bestMap.Length; p++)
                {
                    if (bestMap[p] >= 0) code.Add(Instruction.Create(OpCodes.Ldloc, arguments[bestMap[p]]));
                    else PushArgument(code, best.Parameters[p], host);
                }
                code.Add(Instruction.Create(i.OpCode, Reference(best, call.DeclaringType)));
                if (i.OpCode != OpCodes.Newobj)
                {
                    bool wasVoid = call.ReturnType.MetadataType == MetadataType.Void;
                    bool isVoid = best.ReturnType.MetadataType == MetadataType.Void;
                    if (wasVoid && !isVoid) code.Add(Instruction.Create(OpCodes.Pop));
                    else if (isVoid && !wasVoid) PushDefault(code, call.ReturnType, host);
                }
                report.Adapted.Add(ShortName(call) + ": now " + best.Parameters.Count + " parameters, was " + call.Parameters.Count);
                return true;
            }

            private bool HasOpenType(MethodReference call)
            {
                if (ContainsGeneric(call.ReturnType)) return true;
                foreach (var parameter in call.Parameters) if (ContainsGeneric(parameter.ParameterType)) return true;
                return false;
            }

            private static bool ContainsGeneric(TypeReference type)
            {
                if (type is GenericParameter) return true;
                if (type is GenericInstanceType generic && generic.GenericArguments.Any(ContainsGeneric)) return true;
                return type is TypeSpecification specification && ContainsGeneric(specification.ElementType);
            }

            private IEnumerable<MethodDefinition> Overloads(TypeDefinition owner, MethodReference call)
            {
                for (var t = owner; t != null; t = call.Name == ".ctor" || t.BaseType == null ? null : Find(t.BaseType))
                    foreach (var method in t.Methods)
                        if (method.Name == call.Name && method.IsStatic != call.HasThis && !method.HasGenericParameters && !method.Parameters.Any(p => ContainsGeneric(p.ParameterType)))
                            yield return method;
            }

            /// <summary>For each parameter of <paramref name="candidate"/> the argument of the old call it takes, or -1.</summary>
            private static int[] Map(MethodReference call, MethodDefinition candidate, out int kept)
            {
                kept = 0;
                var used = new bool[call.Parameters.Count];
                var map = new int[candidate.Parameters.Count];
                for (int p = 0; p < map.Length; p++)
                {
                    map[p] = -1;
                    for (int a = 0; a < used.Length; a++)
                    {
                        if (used[a] || call.Parameters[a].ParameterType.FullName != candidate.Parameters[p].ParameterType.FullName) continue;
                        used[a] = true;
                        map[p] = a;
                        kept++;
                        break;
                    }
                }
                return map;
            }

            private bool ResultFits(TypeReference was, TypeReference now)
            {
                if (was.FullName == now.FullName || was.MetadataType == MetadataType.Void || now.MetadataType == MetadataType.Void) return true;
                if (was.IsValueType || now.IsValueType || ContainsGeneric(was) || ContainsGeneric(now)) return false;
                for (var t = Find(now); t != null; t = t.BaseType != null ? Find(t.BaseType) : null)
                    if (t.FullName == was.FullName) return true;
                return false;
            }

            private void PushArgument(List<Instruction> code, ParameterDefinition parameter, MethodDefinition host)
            {
                var type = parameter.ParameterType;
                if (type is ByReferenceType reference)
                {
                    code.Add(Instruction.Create(OpCodes.Ldloca, Temp(host, reference.ElementType)));
                    return;
                }
                if (parameter.HasConstant && parameter.Constant is string text) code.Add(Instruction.Create(OpCodes.Ldstr, text));
                else if (parameter.HasConstant && parameter.Constant is bool flag) code.Add(Instruction.Create(flag ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
                else if (parameter.HasConstant && parameter.Constant is int number) code.Add(Instruction.Create(OpCodes.Ldc_I4, number));
                else if (parameter.HasConstant && parameter.Constant is float real) code.Add(Instruction.Create(OpCodes.Ldc_R4, real));
                else PushDefault(code, type, host);
            }

            // ------------------------------------------------------------ writing code

            private static TypeReference Closed(TypeReference type, MethodReference call)
            {
                if (type is GenericParameter parameter)
                {
                    if (parameter.Type == GenericParameterType.Method && call is GenericInstanceMethod method && parameter.Position < method.GenericArguments.Count)
                        return method.GenericArguments[parameter.Position];
                    if (parameter.Type == GenericParameterType.Type && call.DeclaringType is GenericInstanceType generic && parameter.Position < generic.GenericArguments.Count)
                        return generic.GenericArguments[parameter.Position];
                }
                return type;
            }

            private void PushDefault(List<Instruction> code, TypeReference type, MethodDefinition host)
            {
                switch (type.MetadataType)
                {
                    case MetadataType.Void:
                        return;
                    case MetadataType.Boolean:
                    case MetadataType.Char:
                    case MetadataType.SByte:
                    case MetadataType.Byte:
                    case MetadataType.Int16:
                    case MetadataType.UInt16:
                    case MetadataType.Int32:
                    case MetadataType.UInt32:
                        code.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                        return;
                    case MetadataType.Int64:
                    case MetadataType.UInt64:
                        code.Add(Instruction.Create(OpCodes.Ldc_I8, 0L));
                        return;
                    case MetadataType.Single:
                        code.Add(Instruction.Create(OpCodes.Ldc_R4, 0f));
                        return;
                    case MetadataType.Double:
                        code.Add(Instruction.Create(OpCodes.Ldc_R8, 0d));
                        return;
                    case MetadataType.IntPtr:
                    case MetadataType.UIntPtr:
                    case MetadataType.Pointer:
                    case MetadataType.ByReference:
                        code.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                        code.Add(Instruction.Create(OpCodes.Conv_I));
                        return;
                }
                bool open = type is GenericParameter;
                var definition = open || type.IsArray ? null : Find(type);
                if (definition != null && definition.IsEnum)
                {
                    PushDefault(code, definition.Fields.First(f => !f.IsStatic).FieldType, host);
                    return;
                }
                if (!open && (definition == null || !definition.IsValueType))
                {
                    code.Add(Instruction.Create(OpCodes.Ldnull));
                    return;
                }
                var blank = Temp(host, type);
                code.Add(Instruction.Create(OpCodes.Ldloca, blank));
                code.Add(Instruction.Create(OpCodes.Initobj, type));
                code.Add(Instruction.Create(OpCodes.Ldloc, blank));
            }

            private static VariableDefinition Temp(MethodDefinition host, TypeReference type)
            {
                var variable = new VariableDefinition(type);
                host.Body.Variables.Add(variable);
                host.Body.InitLocals = true;
                return variable;
            }

            /// <summary>
            /// Puts the code in the place of <paramref name="at"/>. That instruction object stays the first of the new code:
            /// jumps and try blocks point at it, so they keep working.
            /// </summary>
            private static void Replace(MethodDefinition host, Instruction at, List<Instruction> code)
            {
                if (code.Count == 0) code.Add(Instruction.Create(OpCodes.Nop));
                var il = host.Body.GetILProcessor();
                bool dropsCall = at.OpCode.Code != code[0].OpCode.Code && at.Previous != null && at.Previous.OpCode.OpCodeType == OpCodeType.Prefix;
                if (dropsCall) // constrained. and the like belong to the instruction that just left
                {
                    at.Previous.OpCode = OpCodes.Nop;
                    at.Previous.Operand = null;
                }
                at.OpCode = code[0].OpCode;
                at.Operand = code[0].Operand;
                var after = at;
                for (int n = 1; n < code.Count; n++)
                {
                    il.InsertAfter(after, code[n]);
                    after = code[n];
                }
            }
        }
    }
}
