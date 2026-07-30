namespace BlazorTerm.Tests;

public sealed class CommandCatalogTests
{
    [Fact]
    public void Catalog_CoversEveryExecutableCommandAndFilter()
    {
        string[] expectedCommands =
        [
            "help", "neofetch", "man", "tour", "about", "resume", "stack", "hosting", "timeline", "status",
            "experience", "education", "skills", "projects", "project", "contributions", "contact", "github",
            "linkedin", "open", "history", "ls", "cat", "cd", "pwd", "tree", "whoami", "who", "date",
            "uptime", "version", "git", "kubectl", "rides", "clear", "theme", "gui", "telemetry", "trace",
            "coffee", "fortune", "cowsay", "sudo", "vim"
        ];
        string[] expectedFilters = ["grep", "head", "tail", "wc", "sort", "uniq"];

        Assert.Equal(expectedCommands.Order(), CommandCatalog.CommandNames.Order());
        Assert.Equal(expectedFilters.Order(), CommandCatalog.FilterNames.Order());
        Assert.Equal(CommandCatalog.Entries.Select(entry => entry.Name), CommandCatalog.CompletionNames);
        Assert.Equal(CommandCatalog.Entries.Count, CommandCatalog.Entries.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Catalog_DrivesCompleteBoundedHelpGroupsAndManualMetadata()
    {
        Assert.Equal(6, CommandCatalog.HelpCategories.Count);
        Assert.All(CommandCatalog.HelpCategories, group =>
        {
            var entries = CommandCatalog.Entries.Where(entry => entry.Category == group.Category).ToArray();
            Assert.NotEmpty(entries);
            Assert.InRange(entries.Length, 1, 16);
        });
        Assert.All(CommandCatalog.Entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Synopsis));
            Assert.False(string.IsNullOrWhiteSpace(entry.Description));
            Assert.Contains(entry.Name, CommandCatalog.ManualNames);
        });
        Assert.All(CommandCatalog.Aliases, alias => Assert.Equal(alias.Value, CommandCatalog.Find(alias.Key)?.Name));
    }
}
