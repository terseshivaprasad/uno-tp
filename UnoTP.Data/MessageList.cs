using System.Reflection;
using System.Text.Json;

namespace UnoTP;

/// <summary>Names the page a group of messages belongs to, as the partner knows it.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PageAttribute(string title) : Attribute
{
    public string Title { get; } = title;
}

/// <summary>Says which field a message belongs to and when it is shown.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
public sealed class ShownAttribute(string field, string when) : Attribute
{
    public string Field { get; } = field;

    public string When { get; } = when;

    /// <summary>True when a script in the browser shows this message too.</summary>
    public bool InBrowser { get; set; }
}

/// <summary>One message of the master, as a row of the list.</summary>
public sealed record MessageRow(string PageName, string PageTitle, string Name, string Field, string When, string Text, bool InBrowser);

/// <summary>
/// Reads the master (Messages.cs) as a list: for docs/VALIDATIONS.md, and for the
/// scripts, which are handed the messages they show. Nothing here is edited to
/// change a message; the words are all in Messages.cs.
/// </summary>
public static class MessageList
{
    /// <summary>Every message, page by page in the master's order, a page's messages by field.</summary>
    public static IReadOnlyList<MessageRow> All { get; } = Read();

    /// <summary>
    /// The messages the scripts show, as JSON: "Page.Name" against its words. A
    /// message with a value in it carries the value's name in braces.
    /// </summary>
    public static string BrowserJson { get; } = JsonSerializer.Serialize(
        All.Where(row => row.InBrowser).ToDictionary(row => row.PageName + "." + row.Name, row => row.Text));

    private static List<MessageRow> Read()
    {
        var rows = new List<MessageRow>();
        var pages = typeof(Messages).GetNestedTypes().OrderBy(page => page.MetadataToken);
        foreach (var page in pages)
        {
            var title = page.GetCustomAttribute<PageAttribute>()?.Title ?? page.Name;
            var ofPage = new List<MessageRow>();

            foreach (var field in page.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var shown = field.GetCustomAttribute<ShownAttribute>();
                if (shown is null)
                {
                    continue;
                }
                var text = (string)field.GetRawConstantValue()!;
                ofPage.Add(new MessageRow(page.Name, title, field.Name, shown.Field, shown.When, text, shown.InBrowser));
            }

            foreach (var method in page.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var shown = method.GetCustomAttribute<ShownAttribute>();
                if (shown is null)
                {
                    continue;
                }
                // Called with each value's own name in braces, a message says where its values go.
                var names = method.GetParameters().Select(parameter => (object?)("{" + parameter.Name + "}")).ToArray();
                var text = (string)method.Invoke(null, names)!;
                ofPage.Add(new MessageRow(page.Name, title, method.Name, shown.Field, shown.When, text, shown.InBrowser));
            }

            rows.AddRange(ofPage.OrderBy(row => row.Field, StringComparer.Ordinal).ThenBy(row => row.Name, StringComparer.Ordinal));
        }
        return rows;
    }
}
