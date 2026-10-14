using System.Reflection;
using UnoTP.Data;

namespace UnoTP.Tests;

/// <summary>
/// Every query in UnoTP.Data/Queries is one whole SQL statement, so any of them can be
/// read and changed on its own: none is built from another constant, from a piece
/// passed to it, or by a method.
/// </summary>
public class WholeQueryTests
{
    private const BindingFlags Declared = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Type[] QueryClasses =
        typeof(DmsPaths).Assembly.GetTypes().Where(type => type.Name.EndsWith("Queries", StringComparison.Ordinal)).ToArray();

    [Fact]
    public void The_query_classes_are_found()
    {
        Assert.True(QueryClasses.Length >= 16, $"Only {QueryClasses.Length} query classes were found.");
    }

    [Fact]
    public void A_query_class_holds_constants_and_nothing_else()
    {
        var notConstants = new List<string>();
        foreach (var type in QueryClasses)
        {
            notConstants.AddRange(type.GetFields(Declared)
                .Where(field => !(field.IsLiteral && field.FieldType == typeof(string)))
                .Select(field => $"{type.Name}.{field.Name} is not a string constant"));
            notConstants.AddRange(type.GetMethods(Declared).Select(method => $"{type.Name}.{method.Name} is a method"));
            notConstants.AddRange(type.GetProperties(Declared).Select(property => $"{type.Name}.{property.Name} is a property"));
        }

        Assert.True(notConstants.Count == 0, "Write each of these as a whole query in a constant:\n" + string.Join("\n", notConstants));
    }

    [Fact]
    public void A_query_is_public_so_no_piece_is_kept_to_build_others_from()
    {
        var hidden = QueryClasses
            .SelectMany(type => type.GetFields(Declared).Where(field => !field.IsPublic).Select(field => $"{type.Name}.{field.Name}"))
            .ToList();

        Assert.True(hidden.Count == 0, "A private constant is a piece of a query, not a query:\n" + string.Join("\n", hidden));
    }

    [Fact]
    public void No_query_file_builds_a_string_from_another()
    {
        var built = new List<string>();
        foreach (var file in Directory.GetFiles(QueriesFolder(), "*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (var at = 0; at < lines.Length; at++)
            {
                var line = lines[at];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                if (line.Contains("$\"", StringComparison.Ordinal) || line.Contains("string.", StringComparison.Ordinal))
                    built.Add($"{Path.GetFileName(file)}:{at + 1}: {line.Trim()}");
            }
        }

        Assert.True(built.Count == 0, "Write the SQL out in full instead:\n" + string.Join("\n", built));
    }

    // UnoTP.Data/Queries, found by walking up from where the tests run to the solution.
    private static string QueriesFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "UnoTP.sln"))) folder = folder.Parent;
        Assert.NotNull(folder);
        return Path.Combine(folder!.FullName, "UnoTP.Data", "Queries");
    }
}
