using System.Text;
using System.Text.RegularExpressions;

namespace UnoTP.Tests;

/// <summary>
/// Keeps the list of validations and error messages (docs/VALIDATIONS.md) in step
/// with the master they are written in (UnoTP.Data/Messages.cs), and keeps the
/// messages in the master: a script may only ask for one the master has, and no
/// message's words are spelt out again anywhere else.
/// </summary>
public class ValidationListTests
{
    private const string WriteAgain = "UPDATE_VALIDATIONS=1 dotnet test UnoTP.sln --filter ValidationListTests";

    [Fact]
    public void The_list_is_the_one_the_master_gives()
    {
        var expected = ListAsMarkdown();
        var file = Path.Combine(RepoFolder(), "docs", "VALIDATIONS.md");

        // The list is never typed by hand: this writes it from the master when asked to.
        if (Environment.GetEnvironmentVariable("UPDATE_VALIDATIONS") == "1")
        {
            File.WriteAllText(file, expected);
        }

        var onFile = File.Exists(file) ? File.ReadAllText(file) : "";
        Assert.True(onFile == expected,
            "docs/VALIDATIONS.md is not in step with UnoTP.Data/Messages.cs. Write it again with:\n  " + WriteAgain);
    }

    [Fact]
    public void Every_message_says_its_field_and_when_it_is_shown()
    {
        Assert.NotEmpty(MessageList.All);
        foreach (var message in MessageList.All)
        {
            var name = message.PageName + "." + message.Name;
            Assert.True(message.Field.Length > 0, name + " has no field.");
            Assert.True(message.When.Length > 0, name + " does not say when it is shown.");
            Assert.True(message.Text.Length > 0, name + " has no words.");
        }
    }

    [Fact]
    public void A_script_asks_only_for_a_message_the_master_gives_the_browser()
    {
        var given = MessageList.All.Where(message => message.InBrowser).Select(message => message.PageName + "." + message.Name).ToHashSet();
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AppFolder(), "wwwroot", "js"), "*.js", SearchOption.AllDirectories))
        {
            foreach (Match asked in Regex.Matches(File.ReadAllText(file), @"\bmessage\('([\w.]+)'"))
            {
                if (!given.Contains(asked.Groups[1].Value))
                {
                    missing.Add(Path.GetFileName(file) + ": " + asked.Groups[1].Value);
                }
            }
        }

        Assert.True(missing.Count == 0,
            "A script asks for a message the master does not give the browser (add InBrowser = true to its [Shown] line in Messages.cs):\n" + string.Join("\n", missing));
    }

    [Fact]
    public void A_messages_words_are_written_only_in_the_master()
    {
        // Short words ("This bank") can turn up anywhere; a sentence cannot by chance.
        var sentences = MessageList.All.Where(message => message.Text.Length >= 20 && !message.Text.Contains('{')).ToList();
        var again = new List<string>();
        foreach (var file in CodeViewsAndScripts())
        {
            var text = File.ReadAllText(file);
            foreach (var message in sentences)
            {
                if (text.Contains("\"" + message.Text + "\"") || text.Contains("'" + message.Text + "'") || text.Contains(">" + message.Text + "<"))
                {
                    again.Add($"{Path.GetRelativePath(RepoFolder(), file)}: {message.PageName}.{message.Name}");
                }
            }
        }

        Assert.True(again.Count == 0,
            "These messages are spelt out again outside the master. Use Messages.Page.Name there instead:\n" + string.Join("\n", again));
    }

    /// <summary>The master as the table docs/VALIDATIONS.md holds: a section a page, a row a message.</summary>
    private static string ListAsMarkdown()
    {
        var list = new StringBuilder();
        list.Append("# Uno TP: validations and error messages\n\n");
        list.Append("Every validation and error message the app shows, page by page: the field it\n");
        list.Append("belongs to, when it is shown, and its words.\n\n");
        list.Append("**This file is written from the master, `UnoTP.Data/Messages.cs`. Do not edit it.**\n\n");
        list.Append("To change what a message says:\n\n");
        list.Append("1. Find it below, and note its page and name (for example `BankDetails.AccountInvalid`).\n");
        list.Append("2. Open `UnoTP.Data/Messages.cs`, go to that page and name, and change the words.\n");
        list.Append("   Keep any `{value}` in braces: the app puts the value there.\n");
        list.Append("3. Write this file again: `" + WriteAgain + "`\n");
        list.Append("4. Build, and replace `UnoTP.dll` and `UnoTP.Data.dll`.\n\n");
        list.Append("\"Browser: yes\" marks a message a script shows as the partner types. It is still\n");
        list.Append("written only in the master, which hands it to the page.\n\n");
        list.Append($"{MessageList.All.Count} messages.\n");

        foreach (var page in MessageList.All.GroupBy(message => message.PageTitle))
        {
            list.Append("\n## " + page.Key + "\n\n");
            list.Append("| Field | Shown when | Message | Name | Browser |\n");
            list.Append("|---|---|---|---|---|\n");
            foreach (var message in page)
            {
                var browser = message.InBrowser ? "yes" : "";
                list.Append($"| {Cell(message.Field)} | {Cell(message.When)} | {Cell(message.Text)} | `{message.PageName}.{message.Name}` | {browser} |\n");
            }
        }
        return list.ToString();
    }

    /// <summary>Text made safe for a table cell.</summary>
    private static string Cell(string text)
    {
        return text.Replace("|", "\\|");
    }

    /// <summary>Every code file, view and script of the app, the master itself left out.</summary>
    private static IEnumerable<string> CodeViewsAndScripts()
    {
        var files = new List<string>();
        files.AddRange(Directory.EnumerateFiles(AppFolder(), "*.cs", SearchOption.AllDirectories));
        files.AddRange(Directory.EnumerateFiles(Path.Combine(AppFolder(), "Views"), "*.cshtml", SearchOption.AllDirectories));
        files.AddRange(Directory.EnumerateFiles(Path.Combine(AppFolder(), "wwwroot", "js"), "*.js", SearchOption.AllDirectories));
        files.AddRange(Directory.EnumerateFiles(Path.Combine(RepoFolder(), "UnoTP.Data"), "*.cs", SearchOption.AllDirectories));

        var separator = Path.DirectorySeparatorChar;
        return files.Where(file => !file.Contains($"{separator}bin{separator}")
            && !file.Contains($"{separator}obj{separator}")
            && Path.GetFileName(file) != "Messages.cs");
    }

    private static string AppFolder()
    {
        return Path.Combine(RepoFolder(), "UnoTP");
    }

    /// <summary>The folder that holds UnoTP.sln, found by walking up from where the tests run.</summary>
    private static string RepoFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "UnoTP.sln")))
        {
            folder = folder.Parent;
        }
        if (folder is null)
        {
            throw new DirectoryNotFoundException("UnoTP.sln was not found above " + AppContext.BaseDirectory);
        }
        return folder.FullName;
    }
}
