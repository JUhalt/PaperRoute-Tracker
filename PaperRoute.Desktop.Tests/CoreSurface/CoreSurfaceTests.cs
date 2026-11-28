using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ManuscriptPipeline.Models;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests.CoreSurface;

// Lock 3 from the other side (d1 1.3). BannedSymbols.txt names the core members
// that save, back up, restore or delete; a list of what is forbidden cannot be
// shown complete. This test reads the trial's compiled assemblies and checks
// that every reference they make into the core is on a checked-in list of what
// is allowed, each line with its reason. The list is exact both ways: a
// reference that is not listed fails, and so does a line nothing references.
[TestClass]
public sealed class CoreSurfaceTests
{
    private const string ListName = "allowed-core-references.txt";

    // A whole-type line's reason starts with this, after the type was read.
    private const string WholeTypePrefix = "in memory only: ";

    [TestMethod]
    public void TrialReferencesOnlyTheListedCoreSurface()
    {
        string coreName = typeof(Manuscript).Assembly.GetName().Name
            ?? throw new InvalidOperationException("The core assembly has no name.");

        // The two assemblies as built, found from their own loaded types.
        string[] trialAssemblies =
        [
            typeof(ILibrarySession).Assembly.Location,
            typeof(App).Assembly.Location,
        ];

        // Each reference, and the assemblies that make it.
        var references = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (string path in trialAssemblies)
        {
            Assert.IsTrue(File.Exists(path), $"The built assembly {path} was not found.");
            string assembly = Path.GetFileNameWithoutExtension(path);
            foreach (CoreReference reference in CoreReferenceReader.Read(path, coreName))
            {
                if (!references.TryGetValue(reference.Id, out SortedSet<string>? users))
                {
                    users = new SortedSet<string>(StringComparer.Ordinal);
                    references.Add(reference.Id, users);
                }

                users.Add(assembly);
            }
        }

        Assert.IsNotEmpty(references, "The reader found no reference into the core: the reader is broken.");

        IReadOnlyList<AllowedLine> lines = ReadList();
        var wholeTypes = lines.Where(line => line.IsWholeType).Select(line => line.Id).ToHashSet(StringComparer.Ordinal);
        var members = lines.Where(line => !line.IsWholeType).Select(line => line.Id).ToHashSet(StringComparer.Ordinal);

        // A type named on a member line is accounted for by that line: as the
        // member's own type, or as a type in its signature (naming a type
        // there runs none of its code).
        var typesWithListedMembers = members.Select(MemberTypeId).ToHashSet(StringComparer.Ordinal);
        var typesInListedSignatures = members.SelectMany(SignatureTypeIds).ToHashSet(StringComparer.Ordinal);

        var unlisted = new List<string>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string id, SortedSet<string> users) in references)
        {
            string? allowedBy = Allow(id);
            if (allowedBy is null)
            {
                unlisted.Add($"{id} (referenced by {string.Join(", ", users)})");
            }
            else
            {
                used.Add(allowedBy);
            }
        }

        string? Allow(string id)
        {
            if (id.StartsWith("T:", StringComparison.Ordinal))
            {
                if (wholeTypes.Contains(id))
                {
                    return id;
                }

                if (typesWithListedMembers.Contains(id) || typesInListedSignatures.Contains(id))
                {
                    // Counted as used through its member lines.
                    return string.Empty;
                }

                // The enclosing type of an allowed nested type.
                return wholeTypes.Concat(typesWithListedMembers).Any(other => other.StartsWith(id + ".", StringComparison.Ordinal) &&
                        references.ContainsKey(other))
                    ? string.Empty
                    : null;
            }

            if (members.Contains(id))
            {
                return id;
            }

            string type = MemberTypeId(id);
            return wholeTypes.Contains(type) ? type : null;
        }

        Assert.IsEmpty(unlisted,
            "The trial's compiled code references core members that " + ListName + " does not list. " +
            "Read each one in PaperRoute.Core, and add it with a reason only if it cannot write outside the isolated session:" +
            Environment.NewLine + string.Join(Environment.NewLine, unlisted));

        string[] stale = [.. lines.Select(line => line.Id).Where(id => !used.Contains(id))];
        Assert.IsEmpty(stale,
            ListName + " lists core references the trial no longer makes; remove them so the list stays true:" +
            Environment.NewLine + string.Join(Environment.NewLine, stale));
    }

    // The save ban (BannedSymbols.txt, RS0030) is lifted only where lock 3
    // allows a write: TrialStartup.cs (the example's seed) and
    // DiskLibrarySession.cs (the settings save, shell design 2.5), each with
    // its reason in a comment on the line above. A pragma cannot hide a new
    // banned call, because TrialReferencesOnlyTheListedCoreSurface pins every
    // core reference. A bare "#pragma warning disable" lifts the ban too.
    [TestMethod]
    public void OnlyTheSessionDisablesTheSaveBan()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "ViewModelSources");
        Assert.IsTrue(Directory.Exists(folder), $"{folder} was not copied to the test output.");
        string[] allowed = ["Session/TrialStartup.cs", "Session/DiskLibrarySession.cs"];
        string[] files = [.. Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];
        Assert.IsTrue(files.Any(file => Path.GetRelativePath(folder, file).Replace('\\', '/') == "Session/DiskLibrarySession.cs"),
            "The view models' sources were not copied to the test output.");

        var problems = new List<string>();
        int found = 0;
        foreach (string file in files)
        {
            string name = Path.GetRelativePath(folder, file).Replace('\\', '/');
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!LiftsTheSaveBan(lines[i]))
                {
                    continue;
                }

                found++;
                if (!allowed.Contains(name, StringComparer.Ordinal))
                {
                    problems.Add($"{name}:{i + 1} lifts the save ban (RS0030) outside {string.Join(" and ", allowed)}.");
                }
                else if (!HasReasonAbove(lines, i))
                {
                    problems.Add($"{name}:{i + 1} lifts the save ban (RS0030) with no reason in a comment on the line above.");
                }
            }
        }

        Assert.IsEmpty(problems, string.Join(Environment.NewLine, problems));
        Assert.IsGreaterThanOrEqualTo(allowed.Length, found,
            "The scan found fewer pragmas than the two allowed writes have: the scan is broken.");
    }

    private static bool LiftsTheSaveBan(string line) => IsSaveBanPragma(line, "disable");

    // "#pragma warning <verb>" naming RS0030, or naming no code at all.
    internal static bool IsSaveBanPragma(string line, string verb)
    {
        string text = line.Trim();
        string pragma = "#pragma warning " + verb;
        if (!text.StartsWith(pragma, StringComparison.Ordinal))
        {
            return false;
        }

        string codes = text[pragma.Length..];
        int comment = codes.IndexOf("//", StringComparison.Ordinal);
        codes = (comment < 0 ? codes : codes[..comment]).Trim();
        return codes.Length == 0 || codes.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Contains("RS0030", StringComparer.Ordinal);
    }

    // The nearest line above that is not blank is a // comment.
    internal static bool HasReasonAbove(string[] lines, int index)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            string text = lines[i].Trim();
            if (text.Length > 0)
            {
                return text.StartsWith("//", StringComparison.Ordinal);
            }
        }

        return false;
    }

    // "M:Ns.Type.Member(args)" or "F:Ns.Type.Field" to "T:Ns.Type".
    private static string MemberTypeId(string memberId)
    {
        int open = memberId.IndexOf('(', StringComparison.Ordinal);
        string head = open < 0 ? memberId : memberId[..open];
        int tilde = head.IndexOf('~', StringComparison.Ordinal);
        head = tilde < 0 ? head : head[..tilde];
        int dot = head.LastIndexOf('.');
        return "T:" + head[2..dot];
    }

    // The types a member's parameter list names, as "T:" IDs.
    private static IEnumerable<string> SignatureTypeIds(string memberId)
    {
        int open = memberId.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
        {
            return [];
        }

        return memberId[(open + 1)..]
            .Split(['(', ')', '{', '}', '[', ']', ',', '@', '*', '~'], StringSplitOptions.RemoveEmptyEntries)
            .Where(name => !name.StartsWith('`') && !name.EndsWith(':'))
            .Select(name => "T:" + name);
    }

    private static IReadOnlyList<AllowedLine> ReadList()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "CoreSurface", ListName);
        Assert.IsTrue(File.Exists(path), $"{path} was not copied to the test output.");

        var lines = new List<AllowedLine>();
        var problems = new List<string>();
        int number = 0;
        foreach (string raw in File.ReadLines(path))
        {
            number++;
            string text = raw.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            int separator = text.IndexOf(';', StringComparison.Ordinal);
            string id = separator < 0 ? text : text[..separator].Trim();
            string reason = separator < 0 ? string.Empty : text[(separator + 1)..].Trim();
            bool whole = id.StartsWith("T:", StringComparison.Ordinal);
            if (!(whole || id.StartsWith("M:", StringComparison.Ordinal) || id.StartsWith("F:", StringComparison.Ordinal)))
            {
                problems.Add($"line {number}: \"{id}\" is neither T:, M: nor F:.");
            }
            else if (reason.Length == 0)
            {
                problems.Add($"line {number}: {id} has no reason.");
            }
            else if (whole && !reason.StartsWith(WholeTypePrefix, StringComparison.Ordinal))
            {
                problems.Add($"line {number}: the whole type {id} must say \"{WholeTypePrefix}\" and why.");
            }

            lines.Add(new AllowedLine(id, whole));
        }

        for (int i = 1; i < lines.Count; i++)
        {
            if (string.CompareOrdinal(lines[i - 1].Id, lines[i].Id) >= 0)
            {
                problems.Add($"{lines[i].Id} is out of order or repeated (the list is sorted, ordinal).");
            }
        }

        Assert.IsEmpty(problems, ListName + " is malformed:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
        return lines;
    }

    private sealed record AllowedLine(string Id, bool IsWholeType);
}

// One reference into the core, named the way BannedSymbols.txt names symbols
// (documentation IDs): T: a type, M: a method (property and event accessors
// included), F: a field.
internal sealed record CoreReference(string Id);

// Reads a built assembly's metadata, without loading it, for every type
// reference that resolves to the core assembly and every member reference
// whose parent is such a type or a generic instantiation of one.
internal static class CoreReferenceReader
{
    public static IReadOnlyList<CoreReference> Read(string path, string coreName)
    {
        using FileStream stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader reader = pe.GetMetadataReader();

        var result = new List<CoreReference>();
        var names = new DocIdTypeProvider();

        foreach (TypeReferenceHandle handle in reader.TypeReferences)
        {
            if (IsCore(reader, handle, coreName))
            {
                result.Add(new CoreReference("T:" + DocIdTypeProvider.TypeName(reader, handle)));
            }
        }

        foreach (MemberReferenceHandle handle in reader.MemberReferences)
        {
            MemberReference member = reader.GetMemberReference(handle);
            string? parent = ParentName(reader, member.Parent, coreName);
            if (parent is null)
            {
                continue;
            }

            string name = reader.GetString(member.Name).Replace('.', '#');
            if (member.GetKind() == MemberReferenceKind.Field)
            {
                result.Add(new CoreReference($"F:{parent}.{name}"));
                continue;
            }

            MethodSignature<string> signature = member.DecodeMethodSignature(names, genericContext: null);
            string arity = signature.GenericParameterCount > 0 ? "``" + signature.GenericParameterCount : string.Empty;
            string parameters = signature.ParameterTypes.Length > 0 ? "(" + string.Join(",", signature.ParameterTypes) + ")" : string.Empty;
            string conversion = name is "op_Implicit" or "op_Explicit" ? "~" + signature.ReturnType : string.Empty;
            result.Add(new CoreReference($"M:{parent}.{name}{arity}{parameters}{conversion}"));
        }

        return result;
    }

    // The documentation name of a member's parent when it is a core type, else null.
    private static string? ParentName(MetadataReader reader, EntityHandle parent, string coreName)
    {
        switch (parent.Kind)
        {
            case HandleKind.TypeReference:
                var reference = (TypeReferenceHandle)parent;
                return IsCore(reader, reference, coreName) ? DocIdTypeProvider.TypeName(reader, reference) : null;

            case HandleKind.TypeSpecification:
                // A generic instantiation of a core type: its members are named
                // on the open type, as documentation IDs name them.
                TypeSpecification specification = reader.GetTypeSpecification((TypeSpecificationHandle)parent);
                BlobReader blob = reader.GetBlobReader(specification.Signature);
                if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance)
                {
                    return null;
                }

                _ = blob.ReadSignatureTypeCode();
                EntityHandle generic = blob.ReadTypeHandle();
                return generic.Kind == HandleKind.TypeReference && IsCore(reader, (TypeReferenceHandle)generic, coreName)
                    ? DocIdTypeProvider.TypeName(reader, (TypeReferenceHandle)generic)
                    : null;

            default:
                return null;
        }
    }

    // True when the reference resolves to the core assembly, nested types
    // through their enclosing types.
    private static bool IsCore(MetadataReader reader, TypeReferenceHandle handle, string coreName)
    {
        EntityHandle scope = reader.GetTypeReference(handle).ResolutionScope;
        while (scope.Kind == HandleKind.TypeReference)
        {
            scope = reader.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
        }

        return scope.Kind == HandleKind.AssemblyReference &&
            string.Equals(
                reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name),
                coreName,
                StringComparison.OrdinalIgnoreCase);
    }
}

// Type names in signatures, as documentation IDs write them, so overloads differ.
internal sealed class DocIdTypeProvider : ISignatureTypeProvider<string, object?>
{
    public static string TypeName(MetadataReader reader, TypeReferenceHandle handle)
    {
        TypeReference type = reader.GetTypeReference(handle);
        string name = reader.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            return TypeName(reader, (TypeReferenceHandle)type.ResolutionScope) + "." + name;
        }

        string space = reader.GetString(type.Namespace);
        return space.Length == 0 ? name : space + "." + name;
    }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "System." + typeCode switch
    {
        PrimitiveTypeCode.IntPtr => "IntPtr",
        PrimitiveTypeCode.UIntPtr => "UIntPtr",
        _ => typeCode.ToString(),
    };

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        TypeDefinition type = reader.GetTypeDefinition(handle);
        string name = reader.GetString(type.Name);
        TypeDefinitionHandle declaring = type.GetDeclaringType();
        if (!declaring.IsNil)
        {
            return GetTypeFromDefinition(reader, declaring, rawTypeKind) + "." + name;
        }

        string space = reader.GetString(type.Namespace);
        return space.Length == 0 ? name : space + "." + name;
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
        TypeName(reader, handle);

    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetArrayType(string elementType, ArrayShape shape) =>
        elementType + "[" + string.Join(",", Enumerable.Repeat("0:", shape.Rank)) + "]";

    public string GetByReferenceType(string elementType) => elementType + "@";

    public string GetPointerType(string elementType) => elementType + "*";

    public string GetPinnedType(string elementType) => elementType;

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        int tick = genericType.LastIndexOf('`');
        string open = tick < 0 ? genericType : genericType[..tick];
        return open + "{" + string.Join(",", typeArguments) + "}";
    }

    public string GetGenericTypeParameter(object? genericContext, int index) => "`" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string GetGenericMethodParameter(object? genericContext, int index) => "``" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string GetFunctionPointerType(MethodSignature<string> signature) => "=FUNC:" + signature.ReturnType;
}
