using System.Reflection;
using System.Text.RegularExpressions;
using ManuscriptPipeline.Models;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests.CoreSurface;

// The Friend scan (build 5, #420; plan section 2, point 5). Before v1.1 ships,
// every Friend type and member of the core that the interface calls becomes
// Public, moves behind the library session, or goes with the trial mode that
// calls it, so the C# interface does not depend on a friend grant. This lists
// them. It reads what the compiled assemblies reference, not names in the
// source: the source can pass two arguments where the compiled call goes to a
// four-parameter Friend constructor (DiskLibrarySession's ManuscriptRepository).
// It also keeps working once the app takes the assembly name PaperRouteTracker,
// which already holds the core's grant, so the compiler stops refusing Friend
// uses. It cannot see constants: the compiler copies a Const's value (an enum
// member's too) into the calling code, so no reference to a Friend Const is
// left to read. Only removing the core's grants to PaperRoute.ViewModels and
// the app, or a source check for Friend Const names, catches those.
// Nothing here makes anything Public; the issue named on each line does.
[TestClass]
public sealed partial class FriendUseTests
{
    private const string ListName = "friend-uses.txt";

    // DiskLibrarySession writes new ManuscriptRepository(data, managed); the
    // compiler fills the two optional parameters.
    private const string FourParameterRepository =
        "M:ManuscriptPipeline.Services.ManuscriptRepository.#ctor(System.String,System.String,ManuscriptPipeline.Services.ManagedLibraryService,System.Func{System.String,System.IO.Stream})";

    [TestMethod]
    public void EveryFriendUseOfTheInterfaceIsListed()
    {
        FriendUses found = FriendUseReader.Read(InterfaceAssemblies());

        Assert.IsEmpty(found.Unresolved,
            "References into the core that the loaded core does not have (is the build stale?):" +
            Environment.NewLine + string.Join(Environment.NewLine, found.Unresolved));

        var listed = ReadList().Select(line => line.Id).ToHashSet(StringComparer.Ordinal);

        // Both directions in one message, so one run shows every difference.
        var differences = new List<string>();
        differences.AddRange(found.Uses
            .Where(use => !listed.Contains(use.Key))
            .Select(use => $"not listed: {use.Key} (referenced by {string.Join(", ", use.Value)})"));
        differences.AddRange(listed
            .Where(id => !found.Uses.ContainsKey(id))
            .Order(StringComparer.Ordinal)
            .Select(id => $"listed, but not referenced as a Friend item: {id}"));
        Assert.IsEmpty(differences,
            ListName + " and the Friend core items the interface's compiled code references differ. " +
            "Add a new reference with its declaration, its fate and the issue that carries the fate out; " +
            "remove a line the interface no longer references or that is Public now:" +
            Environment.NewLine + string.Join(Environment.NewLine, differences));
    }

    [TestMethod]
    public void EveryListedFriendUseNamesItsDeclarationAndFate()
    {
        string root = RepositoryRoot();
        var problems = new List<string>();
        IReadOnlyList<ListedUse> lines = ReadList();

        foreach (ListedUse line in lines)
        {
            string where = $"line {line.Number} ({line.Id})";
            if (line.Fields.Length != 3)
            {
                problems.Add($"{where}: has {line.Fields.Length} field(s); it needs \"<documentation ID>;<file>:<line>;<fate> (#<issue>)\".");
                continue;
            }

            if (!(line.Id.StartsWith("T:", StringComparison.Ordinal) ||
                  line.Id.StartsWith("M:", StringComparison.Ordinal) ||
                  line.Id.StartsWith("F:", StringComparison.Ordinal)))
            {
                problems.Add($"{where}: the ID is neither T:, M: nor F:.");
            }

            if (!Fate().IsMatch(line.Fields[2]))
            {
                problems.Add($"{where}: the fate \"{line.Fields[2]}\" is not \"make Public\", \"behind the session\" or " +
                    "\"trial-only: goes with --<mode>\", followed by \" (#<issue>)\".");
            }

            if (DeclarationProblem(root, line.Id, line.Fields[1]) is { } problem)
            {
                problems.Add($"{where}: {problem}");
            }
        }

        for (int i = 1; i < lines.Count; i++)
        {
            if (string.CompareOrdinal(lines[i - 1].Id, lines[i].Id) >= 0)
            {
                problems.Add($"{lines[i].Id} is out of order or repeated (the list is sorted, ordinal).");
            }
        }

        Assert.IsEmpty(problems, ListName + " is malformed:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    // This depends on that constructor staying Friend. The list's fate for it
    // is "make Public (#421)"; when #421 does that, this test fails, and #421
    // points it at another Friend overload the interface reaches or replaces
    // it with an assembly built in memory for the purpose.
    [TestMethod]
    public void TheFriendReaderSeesAFriendConstructorOverload()
    {
        FriendUses found = FriendUseReader.Read([typeof(ILibrarySession).Assembly.Location]);

        string[] constructors = [.. found.Uses.Keys.Where(id =>
            id.StartsWith("M:ManuscriptPipeline.Services.ManuscriptRepository.#ctor", StringComparison.Ordinal))];
        Assert.HasCount(1, constructors,
            "The reader must report exactly one ManuscriptRepository constructor, the Friend one with four parameters " +
            "that the compiled call reaches; it reported: " + string.Join(", ", constructors));
        Assert.AreEqual(FourParameterRepository, constructors[0]);
    }

    // The declaration rule follows the reader's two kinds of Friend item: one
    // declared Friend (or Protected Friend, or Private Protected), whose line
    // holds that word, and a member that is Friend only because its type is,
    // whose line holds no such word. Each row points at a real line of the
    // core, found by its text so the rows do not hold line numbers.
    [TestMethod]
    // A Public property of a Friend class, at its own line.
    [DataRow("M:ManuscriptPipeline.Services.AssistantTestResult.get_Message",
        "PaperRoute.Core/Services/AssistantProviders.vb", "Public Property Message As String", true)]
    // The constructor the compiler adds to a Friend class, at the class's line.
    [DataRow("M:ManuscriptPipeline.Services.AssistantTrial.#ctor",
        "PaperRoute.Core/Services/OnlineAccess.vb", "Friend NotInheritable Class AssistantTrial", true)]
    // A Friend accessor of a Public property, at its own Friend Set line.
    [DataRow("M:ManuscriptPipeline.Services.PacketExportRow.set_SizeBytes(System.Nullable{System.Int64})",
        "PaperRoute.Core/Services/PacketExportService.vb", "Friend Set(value As Long?)", true)]
    // A Friend constructor, at a line that declares it Friend.
    [DataRow(FourParameterRepository,
        "PaperRoute.Core/Services/ManuscriptRepository.vb", "Friend Sub New(", true)]
    // A Friend constructor, at the Public constructor's line.
    [DataRow(FourParameterRepository,
        "PaperRoute.Core/Services/ManuscriptRepository.vb", "Public Sub New()", false)]
    // A Public property of a Friend class, at another property's line.
    [DataRow("M:ManuscriptPipeline.Services.AssistantTestResult.get_Message",
        "PaperRoute.Core/Services/AssistantProviders.vb", "Public Property Succeeded As Boolean", false)]
    public void TheDeclarationRuleFollowsTheReadersTwoKindsOfFriendItem(string id, string file, string text, bool passes)
    {
        string root = RepositoryRoot();
        string[] source = File.ReadAllLines(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar)));
        int index = Array.FindIndex(source, line => line.Trim().StartsWith(text, StringComparison.Ordinal));
        Assert.IsGreaterThanOrEqualTo(0, index, $"{file} has no line starting \"{text}\".");

        string? problem = DeclarationProblem(root, id, $"{file}:{index + 1}");
        if (passes)
        {
            Assert.IsNull(problem, $"{id} at \"{text}\" should pass; it gave: {problem}");
        }
        else
        {
            Assert.IsNotNull(problem, $"{id} at \"{text}\" should fail; it passed.");
        }
    }

    // What is wrong with a line's declaration field, or null if nothing is.
    // An item declared Friend: its line holds Friend (or Private Protected)
    // and its name, or for an accessor its Get, Set, AddHandler or
    // RemoveHandler keyword ("Friend Set(value As String)"). A member that is
    // Friend only through its type: its line holds its name; a constructor's
    // may be the line that declares its type, as the compiler adds one to a
    // class without Sub New.
    private static string? DeclarationProblem(string root, string id, string field)
    {
        FriendKind? kind = FriendUseReader.Classify(id);
        if (kind is null or FriendKind.Public)
        {
            return $"{id} is not a Friend item of the loaded core.";
        }

        Match declaration = Declaration().Match(field);
        if (!declaration.Success)
        {
            return $"the declaration \"{field}\" is not \"PaperRoute.Core/<path>.vb:<line>\".";
        }

        string file = Path.Combine(root, declaration.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(file))
        {
            return $"{declaration.Groups[1].Value} does not exist.";
        }

        string[] source = File.ReadAllLines(file);
        int number = int.Parse(declaration.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        string declared = number <= source.Length ? source[number - 1] : string.Empty;
        string name = DeclaredName(id);
        bool named = declared.Contains(name, StringComparison.Ordinal);
        if (kind == FriendKind.Declared)
        {
            named = named || (AccessorKeyword(id) is { } keyword &&
                Regex.IsMatch(declared, $@"\b{keyword}\b", RegexOptions.None, TimeSpan.FromSeconds(1)));
            return FriendWord().IsMatch(declared) && named
                ? null
                : $"{field} does not declare \"{name}\" Friend (the line must hold Friend or Private Protected and the name); " +
                  $"it reads \"{declared.Trim()}\".";
        }

        if (!named && name == "Sub New")
        {
            named = Regex.IsMatch(declared, $@"\b(Class|Structure)\s+{Regex.Escape(TypeOf(id))}\b", RegexOptions.None, TimeSpan.FromSeconds(1));
        }

        return named
            ? null
            : $"{field} does not declare \"{name}\", which is Friend because its type is not Public " +
              $"(the line must hold the name; a constructor the compiler adds, its type's declaration); it reads \"{declared.Trim()}\".";
    }

    // An accessor's keyword in VB ("Set" for set_Name), or null for other members.
    private static string? AccessorKeyword(string id)
    {
        string name = MemberName(id);
        return name.StartsWith("get_", StringComparison.Ordinal) ? "Get"
            : name.StartsWith("set_", StringComparison.Ordinal) ? "Set"
            : name.StartsWith("add_", StringComparison.Ordinal) ? "AddHandler"
            : name.StartsWith("remove_", StringComparison.Ordinal) ? "RemoveHandler"
            : null;
    }

    // The simple name of the type that declares a member, generic arity dropped.
    private static string TypeOf(string id)
    {
        int open = id.IndexOf('(', StringComparison.Ordinal);
        string head = open < 0 ? id : id[..open];
        head = head[..head.LastIndexOf('.')];
        string type = head[(head.LastIndexOf('.') + 1)..];
        int tick = type.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? type : type[..tick];
    }

    // The last segment of an ID before its parameters ("set_Name", "#ctor").
    private static string MemberName(string id)
    {
        int open = id.IndexOf('(', StringComparison.Ordinal);
        string head = open < 0 ? id : id[..open];
        return head[(head.LastIndexOf('.') + 1)..];
    }

    // The interface's two assemblies as built, found from their own loaded types.
    private static string[] InterfaceAssemblies() =>
    [
        typeof(ILibrarySession).Assembly.Location,
        typeof(App).Assembly.Location,
    ];

    // The name a declaration line must hold: "Sub New" for a constructor, the
    // property's name for an accessor, else the type's or member's own name.
    private static string DeclaredName(string id)
    {
        string name = MemberName(id);
        if (name == "#ctor")
        {
            return "Sub New";
        }

        foreach (string prefix in (string[])["get_", "set_", "add_", "remove_"])
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return name[prefix.Length..];
            }
        }

        int tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }

    private static IReadOnlyList<ListedUse> ReadList()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "CoreSurface", ListName);
        Assert.IsTrue(File.Exists(path), $"{path} was not copied to the test output.");

        var lines = new List<ListedUse>();
        int number = 0;
        foreach (string raw in File.ReadLines(path))
        {
            number++;
            string text = raw.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            string[] fields = [.. text.Split(';').Select(field => field.Trim())];
            lines.Add(new ListedUse(number, fields[0], fields));
        }

        return lines;
    }

    // The folder that holds PaperRoute.Desktop.slnx, above the test output.
    private static string RepositoryRoot()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "PaperRoute.Desktop.slnx")))
        {
            folder = folder.Parent;
        }

        Assert.IsNotNull(folder, $"No PaperRoute.Desktop.slnx above {AppContext.BaseDirectory}.");
        return folder.FullName;
    }

    // "PaperRoute.Core/<path>.vb:<line>", from the repository's root.
    [GeneratedRegex(@"^(PaperRoute\.Core/[^:;]+\.vb):([1-9][0-9]*)$")]
    private static partial Regex Declaration();

    // The fates of the design (shell design 2.7), each with the issue that carries it out.
    [GeneratedRegex(@"^(make Public|behind the session|trial-only: goes with --[a-z][a-z-]*) \(#[1-9][0-9]*\)$")]
    private static partial Regex Fate();

    [GeneratedRegex(@"\bFriend\b|\bPrivate\s+Protected\b")]
    private static partial Regex FriendWord();

    private sealed record ListedUse(int Number, string Id, string[] Fields);
}

// What FriendUseReader found: each Friend use with the assemblies that make
// it, and the references the loaded core could not resolve.
internal sealed record FriendUses(SortedDictionary<string, SortedSet<string>> Uses, IReadOnlyList<string> Unresolved);

// Public: needs no grant. Declared: the item's own access is Friend,
// Protected Friend or Private Protected. ThroughItsType: the item is itself
// Public (or Protected) but sits in a type that is not public, so its
// declaration holds no such word.
internal enum FriendKind
{
    Public,
    Declared,
    ThroughItsType,
}

// Reads assemblies' references into the core with CoreReferenceReader (from
// metadata, as compiled), resolves each in the loaded core by reflection and
// keeps the Friend ones: a type that is not public (or sits inside one that
// is not), and a member of such a type or with Friend, Protected Friend or
// Private Protected access, each of which needs the core's friend grant.
internal static class FriendUseReader
{
    private const BindingFlags Every =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Assembly Core => typeof(Manuscript).Assembly;

    public static FriendUses Read(IEnumerable<string> assemblyPaths)
    {
        string coreName = Core.GetName().Name
            ?? throw new InvalidOperationException("The core assembly has no name.");

        var uses = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string path in assemblyPaths)
        {
            Assert.IsTrue(File.Exists(path), $"The built assembly {path} was not found.");
            string assembly = Path.GetFileNameWithoutExtension(path);
            foreach (CoreReference reference in CoreReferenceReader.Read(path, coreName))
            {
                FriendKind? kind = Classify(reference.Id);
                if (kind is null)
                {
                    unresolved.Add(reference.Id);
                }
                else if (kind != FriendKind.Public)
                {
                    if (!uses.TryGetValue(reference.Id, out SortedSet<string>? users))
                    {
                        users = new SortedSet<string>(StringComparer.Ordinal);
                        uses.Add(reference.Id, users);
                    }

                    users.Add(assembly);
                }
            }
        }

        return new FriendUses(uses, [.. unresolved]);
    }

    // Which kind of item the ID names in the loaded core, or null when the
    // core has no such type or member.
    public static FriendKind? Classify(string id)
    {
        if (id.StartsWith("T:", StringComparison.Ordinal))
        {
            Type? type = ResolveType(id[2..]);
            if (type is null)
            {
                return null;
            }

            if (type.IsVisible)
            {
                return FriendKind.Public;
            }

            return type.IsNotPublic || type.IsNestedAssembly || type.IsNestedFamORAssem || type.IsNestedFamANDAssem
                ? FriendKind.Declared
                : FriendKind.ThroughItsType;
        }

        int open = id.IndexOf('(', StringComparison.Ordinal);
        string head = open < 0 ? id : id[..open];
        int tilde = head.IndexOf('~', StringComparison.Ordinal);
        head = tilde < 0 ? head : head[..tilde];
        int tick = head.IndexOf("``", StringComparison.Ordinal);
        head = tick < 0 ? head : head[..tick];
        int dot = head.LastIndexOf('.');
        Type? parent = ResolveType(head[2..dot]);
        if (parent is null)
        {
            return null;
        }

        string name = head[(dot + 1)..].Replace('#', '.');
        string tail = id[(dot + 1)..];
        MemberInfo? member = id[0] == 'F'
            ? Fields(parent).FirstOrDefault(field => field.Name == name)
            : Methods(parent, name).FirstOrDefault(method => Tail(method) == tail);
        if (member is null)
        {
            return null;
        }

        bool declared = member switch
        {
            FieldInfo field => field.IsAssembly || field.IsFamilyOrAssembly || field.IsFamilyAndAssembly,
            MethodBase method => method.IsAssembly || method.IsFamilyOrAssembly || method.IsFamilyAndAssembly,
            _ => false,
        };
        if (declared)
        {
            return FriendKind.Declared;
        }

        return !parent.IsVisible || !member.DeclaringType!.IsVisible ? FriendKind.ThroughItsType : FriendKind.Public;
    }

    // A documentation name ("Ns.Outer.Inner", generic arity included) to the
    // core's type: each dot from the right may stand for a nesting.
    private static Type? ResolveType(string name)
    {
        Type? type = Core.GetType(name);
        char[] chars = name.ToCharArray();
        for (int i = chars.Length - 1; type is null && i >= 0; i--)
        {
            if (chars[i] == '.')
            {
                chars[i] = '+';
                type = Core.GetType(new string(chars));
            }
        }

        return type;
    }

    private static IEnumerable<FieldInfo> Fields(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(Every | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    private static IEnumerable<MethodBase> Methods(Type type, string name)
    {
        if (name == ".ctor")
        {
            return type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        if (name == ".cctor")
        {
            return type.TypeInitializer is { } initializer ? [initializer] : [];
        }

        var methods = new List<MethodBase>();
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            methods.AddRange(current.GetMethods(Every | BindingFlags.DeclaredOnly).Where(method => method.Name == name));
        }

        return methods;
    }

    // A method's documentation ID after its type, written as CoreReferenceReader
    // writes a reference's: name, generic arity, parameters, conversion type.
    private static string Tail(MethodBase method)
    {
        string name = method.Name.Replace('.', '#');
        string arity = method.IsGenericMethodDefinition ? "``" + method.GetGenericArguments().Length : string.Empty;
        ParameterInfo[] parameters = method.GetParameters();
        string list = parameters.Length > 0
            ? "(" + string.Join(",", parameters.Select(parameter => TypeName(parameter.ParameterType))) + ")"
            : string.Empty;
        string conversion = method is MethodInfo info && name is "op_Implicit" or "op_Explicit"
            ? "~" + TypeName(info.ReturnType)
            : string.Empty;
        return name + arity + list + conversion;
    }

    // A type as DocIdTypeProvider writes it in a signature.
    private static string TypeName(Type type)
    {
        if (type.IsByRef)
        {
            return TypeName(type.GetElementType()!) + "@";
        }

        if (type.IsPointer)
        {
            return TypeName(type.GetElementType()!) + "*";
        }

        if (type.IsArray)
        {
            string element = TypeName(type.GetElementType()!);
            return type.IsSZArray
                ? element + "[]"
                : element + "[" + string.Join(",", Enumerable.Repeat("0:", type.GetArrayRank())) + "]";
        }

        if (type.IsGenericParameter)
        {
            return (type.IsGenericMethodParameter ? "``" : "`") +
                type.GenericParameterPosition.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (type.IsConstructedGenericType)
        {
            string open = PlainName(type.GetGenericTypeDefinition());
            int tick = open.LastIndexOf('`');
            open = tick < 0 ? open : open[..tick];
            return open + "{" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + "}";
        }

        return PlainName(type);
    }

    // "Ns.Outer.Inner", nesting written with dots.
    private static string PlainName(Type type) =>
        type.DeclaringType is { } outer && !type.IsGenericParameter
            ? PlainName(outer) + "." + type.Name
            : (string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name);
}
