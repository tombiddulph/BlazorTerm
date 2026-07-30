using System.Text.Json.Serialization;

namespace BlazorTerm;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextLine), "text")]
[JsonDerivedType(typeof(KeyValueLine), "key-value")]
[JsonDerivedType(typeof(LinkLine), "link")]
[JsonDerivedType(typeof(HelpLine), "help")]
[JsonDerivedType(typeof(TableLine), "table")]
[JsonDerivedType(typeof(RawLine), "raw")]
[JsonDerivedType(typeof(TraceLine), "trace")]
[JsonDerivedType(typeof(ChartLine), "chart")]
[JsonDerivedType(typeof(AsciiArtLine), "ascii-art")]
public abstract record OutputLine
{
    public string Style { get; init; } = string.Empty;
    public IReadOnlyList<TextRange> Highlights { get; init; } = [];

    public abstract string DisplayText { get; }
    public virtual string ToPlainText() => DisplayText;
    public abstract OutputLine WithHighlights(IReadOnlyList<TextRange> highlights);
}

public sealed record TextLine(string Text) : OutputLine
{
    public override string DisplayText => Text;
    public override OutputLine WithHighlights(IReadOnlyList<TextRange> highlights) => this with { Highlights = highlights };
}

public sealed record KeyValueLine(string Key, string Value) : OutputLine
{
    public override string DisplayText => $"{Key} {Value}";
    public override OutputLine WithHighlights(IReadOnlyList<TextRange> highlights) => this with { Highlights = highlights };
}

public enum LinkKind
{
    Command,
    Web
}

public sealed record LinkLine(string Label, string Target, LinkKind Kind, bool OpenInNewTab = false) : OutputLine
{
    public override string DisplayText => Label;
    public override OutputLine WithHighlights(IReadOnlyList<TextRange> highlights) => this with { Highlights = highlights };
}

public sealed record HelpLine(string Label, string Description, string Target = "", LinkKind? Kind = null) : OutputLine
{
    public IReadOnlyList<TextRange> DescriptionHighlights { get; init; } = [];
    public override string DisplayText => Label;
    public override string ToPlainText() => $"{Label}  {Description}";
    public override OutputLine WithHighlights(IReadOnlyList<TextRange> highlights) => this with { Highlights = highlights };
}

public sealed record TableLine(IReadOnlyList<string> Cells) : OutputLine
{
    public override string DisplayText => string.Join("  ", Cells);
    public override OutputLine WithHighlights(IReadOnlyList<TextRange> highlights) => this with { Highlights = highlights };
}

public enum RawLineKind
{
    Preformatted,
    Neofetch
}

public sealed record RawLine(string Text, RawLineKind Kind = RawLineKind.Preformatted) : OutputLine
{
    public override string DisplayText => Text;
    public override OutputLine WithHighlights(IReadOnlyList<TextRange> highlights) => this with { Highlights = highlights };
}

public sealed record TraceLine(string Label, string Bar, string DurationText) : OutputLine
{
    public override string DisplayText => $"{Label}  {Bar}  {DurationText}";
    public override OutputLine WithHighlights(IReadOnlyList<TextRange> highlights) => this with { Highlights = highlights };
}

public sealed record ChartLine(string Label, string Bar, string Value, string AccessibleText) : OutputLine
{
    public override string DisplayText => AccessibleText;
    public override OutputLine WithHighlights(IReadOnlyList<TextRange> highlights) => this with { Highlights = highlights };
}

public sealed record AsciiArtLine(string Text, string AccessibleText) : OutputLine
{
    public override string DisplayText => AccessibleText;
    public override string ToPlainText() => Text;
    public override OutputLine WithHighlights(IReadOnlyList<TextRange> highlights) => this with { Highlights = highlights };
}

public readonly record struct TextRange(int Start, int Length);

public sealed record CommandResult(IReadOnlyList<OutputLine> Lines, int ExitCode = 0)
{
    public static readonly CommandResult Empty = new([]);
}
