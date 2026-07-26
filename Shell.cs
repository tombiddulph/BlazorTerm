using System.Text;
using System.Text.RegularExpressions;

namespace BlazorTerm;

public sealed record CommandSegment(string Name, IReadOnlyList<string> Arguments);

public static class ShellParser
{
    public static bool TryParse(string input, out IReadOnlyList<CommandSegment> segments, out string error)
    {
        List<CommandSegment> parsed = [];
        List<string> words = [];
        var current = new StringBuilder();
        var tokenStarted = false;
        var escaped = false;
        char? quote = null;

        void FinishToken()
        {
            if (!tokenStarted)
                return;
            words.Add(current.ToString());
            current.Clear();
            tokenStarted = false;
        }

        bool FinishSegment()
        {
            FinishToken();
            if (words.Count == 0)
                return false;
            parsed.Add(new(words[0].ToLowerInvariant(), words.Skip(1).ToArray()));
            words.Clear();
            return true;
        }

        foreach (var character in input)
        {
            if (escaped)
            {
                current.Append(character);
                tokenStarted = true;
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                tokenStarted = true;
                continue;
            }

            if (quote is not null)
            {
                if (character == quote)
                    quote = null;
                else
                    current.Append(character);
                tokenStarted = true;
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                tokenStarted = true;
            }
            else if (character == '|')
            {
                if (!FinishSegment())
                {
                    segments = [];
                    error = "syntax error near unexpected token `|'";
                    return false;
                }
            }
            else if (char.IsWhiteSpace(character))
            {
                FinishToken();
            }
            else
            {
                current.Append(character);
                tokenStarted = true;
            }
        }

        if (escaped)
        {
            segments = [];
            error = "syntax error: trailing escape";
            return false;
        }

        if (quote is not null)
        {
            segments = [];
            error = "syntax error: unterminated quoted string";
            return false;
        }

        if (!FinishSegment())
        {
            segments = [];
            error = parsed.Count == 0 ? string.Empty : "syntax error: expected command after `|'";
            return parsed.Count == 0;
        }

        segments = parsed;
        error = string.Empty;
        return true;
    }
}

public sealed record CommandInvocation(IReadOnlyList<string> Arguments, CommandResult? Input = null);

public interface ICommand
{
    CommandResult Execute(CommandInvocation invocation);
}

public sealed class DelegateCommand(Func<CommandInvocation, CommandResult> execute) : ICommand
{
    public CommandResult Execute(CommandInvocation invocation) => execute(invocation);
}

public interface IFilter : ICommand
{
    string Name { get; }
}

public sealed class PipelineExecutor
{
    private readonly IReadOnlyDictionary<string, IFilter> _filters;

    public PipelineExecutor()
    {
        IFilter[] filters = [new GrepFilter(), new HeadFilter(), new TailFilter(), new WcFilter(), new SortFilter(), new UniqFilter()];
        _filters = filters.ToDictionary(filter => filter.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> FilterNames => _filters.Keys.ToArray();

    public CommandResult Execute(
        IReadOnlyList<CommandSegment> segments,
        Func<string, ICommand?> resolveCommand)
    {
        CommandResult? result = null;
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var command = _filters.GetValueOrDefault(segment.Name) ?? resolveCommand(segment.Name);
            if (command is null)
                return Error($"{segment.Name}: command not found", 127);
            if (index > 0 && command is not IFilter)
                return Error($"{segment.Name}: not a filter", 127);

            result = command.Execute(new(segment.Arguments, result));

            if (result.ExitCode != 0)
                return result;
        }

        return result ?? CommandResult.Empty;
    }

    private static CommandResult Error(string message, int exitCode = 2) =>
        new([new TextLine(message) { Style = "error" }], exitCode);

    private abstract class FilterBase(string name) : IFilter
    {
        public string Name { get; } = name;
        public abstract CommandResult Execute(CommandInvocation invocation);

        protected static IReadOnlyList<OutputLine> InputLines(CommandInvocation invocation) => invocation.Input?.Lines ?? [];
        protected static CommandResult Usage(string message) => Error(message);
    }

    private sealed class GrepFilter() : FilterBase("grep")
    {
        public override CommandResult Execute(CommandInvocation invocation)
        {
            var ignoreCase = false;
            var invert = false;
            string? pattern = null;

            foreach (var argument in invocation.Arguments)
            {
                if (pattern is null && argument.StartsWith('-') && argument.Length > 1)
                {
                    foreach (var flag in argument[1..])
                    {
                        if (flag == 'i') ignoreCase = true;
                        else if (flag == 'v') invert = true;
                        else return Usage($"grep: invalid option -- '{flag}'");
                    }
                }
                else if (pattern is null)
                {
                    pattern = argument;
                }
                else
                {
                    return Usage("grep: too many arguments");
                }
            }

            if (pattern is null)
                return Usage("grep: missing pattern");

            Regex expression;
            try
            {
                expression = new(pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None, TimeSpan.FromMilliseconds(100));
            }
            catch (ArgumentException exception)
            {
                return Usage($"grep: invalid pattern: {exception.Message}");
            }

            List<OutputLine> lines = [];
            try
            {
                foreach (var line in InputLines(invocation))
                {
                    var isMatch = expression.IsMatch(line.ToPlainText());
                    if (isMatch == invert)
                        continue;

                    if (!invert)
                    {
                        var highlights = expression.Matches(line.DisplayText)
                            .Where(match => match.Length > 0)
                            .Select(match => new TextRange(match.Index, match.Length))
                            .ToArray();
                        var highlightedLine = line.WithHighlights(highlights);
                        if (highlightedLine is HelpLine helpLine)
                        {
                            highlightedLine = helpLine with
                            {
                                DescriptionHighlights = expression.Matches(helpLine.Description)
                                    .Where(match => match.Length > 0)
                                    .Select(match => new TextRange(match.Index, match.Length))
                                    .ToArray()
                            };
                        }
                        lines.Add(highlightedLine);
                    }
                    else
                    {
                        lines.Add(line.WithHighlights([]));
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return Usage("grep: pattern evaluation timed out");
            }

            return new(lines, lines.Count == 0 ? 1 : 0);
        }
    }

    private sealed class HeadFilter() : FilterBase("head")
    {
        public override CommandResult Execute(CommandInvocation invocation)
        {
            if (!TryReadCount("head", invocation.Arguments, out var count, out var error))
                return Usage(error);
            return new(InputLines(invocation).Take(count).ToArray());
        }
    }

    private sealed class TailFilter() : FilterBase("tail")
    {
        public override CommandResult Execute(CommandInvocation invocation)
        {
            if (!TryReadCount("tail", invocation.Arguments, out var count, out var error))
                return Usage(error);
            return new(InputLines(invocation).TakeLast(count).ToArray());
        }
    }

    private sealed class WcFilter() : FilterBase("wc")
    {
        public override CommandResult Execute(CommandInvocation invocation)
        {
            if (invocation.Arguments.Count > 1 || (invocation.Arguments.Count == 1 && invocation.Arguments[0] != "-l"))
                return Usage("wc: only the -l option is supported");

            var lines = InputLines(invocation);
            if (invocation.Arguments.Count == 1)
                return new([new TextLine(lines.Count.ToString())]);

            var text = string.Join('\n', lines.Select(line => line.ToPlainText()));
            var words = Regex.Matches(text, @"\S+").Count;
            return new([new TextLine($"{lines.Count} {words} {text.Length}")]);
        }
    }

    private sealed class SortFilter() : FilterBase("sort")
    {
        public override CommandResult Execute(CommandInvocation invocation)
        {
            if (invocation.Arguments.Any(argument => argument != "-r"))
                return Usage("sort: only the -r option is supported");
            var descending = invocation.Arguments.Contains("-r");
            var ordered = descending
                ? InputLines(invocation).OrderByDescending(line => line.ToPlainText(), StringComparer.OrdinalIgnoreCase)
                : InputLines(invocation).OrderBy(line => line.ToPlainText(), StringComparer.OrdinalIgnoreCase);
            return new(ordered.ToArray());
        }
    }

    private sealed class UniqFilter() : FilterBase("uniq")
    {
        public override CommandResult Execute(CommandInvocation invocation)
        {
            if (invocation.Arguments.Count > 0)
                return Usage("uniq: no options are supported");

            List<OutputLine> lines = [];
            string? previous = null;
            foreach (var line in InputLines(invocation))
            {
                var text = line.ToPlainText();
                if (previous is null || !text.Equals(previous, StringComparison.Ordinal))
                    lines.Add(line);
                previous = text;
            }
            return new(lines);
        }
    }

    private static bool TryReadCount(string command, IReadOnlyList<string> arguments, out int count, out string error)
    {
        count = 10;
        error = string.Empty;
        if (arguments.Count == 0)
            return true;

        string? value = null;
        if (arguments.Count == 2 && arguments[0] == "-n")
            value = arguments[1];
        else if (arguments.Count == 1 && arguments[0].StartsWith("-n", StringComparison.Ordinal) && arguments[0].Length > 2)
            value = arguments[0][2..];
        else
        {
            error = $"{command}: usage: {command} [-n count]";
            return false;
        }

        if (!int.TryParse(value, out count) || count < 0)
        {
            error = $"{command}: invalid number of lines: '{value}'";
            return false;
        }
        return true;
    }
}
