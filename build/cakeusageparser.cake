
public static void WriteAllText(this ICakeContext context, FilePath targetPath, string text)
{
    using(var stream = context.FileSystem.GetFile(targetPath).OpenWrite())
    {
        using(var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            writer.Write(text);
        }
    }
}

public static string ConverCakeUsageToMarkdown(this ICakeContext context)
{
    FilePath cakePath = context.Tools.Resolve("Cake.exe") ?? throw new System.IO.FileNotFoundException("Failed to find Cake.exe.", "Cake.exe");

    IEnumerable<string> helpLines = (context.StartProcess(
                                     cakePath,
                                     new ProcessSettings {
                                         Arguments = "--help",
                                         RedirectStandardOutput = true
                                     },
                                     out IEnumerable<string> redirectedStandardOutput
                                 ) == 0
                                ? redirectedStandardOutput
                                : Enumerable.Empty<string>());

    var usageLines = helpLines
                        .SkipWhile(line => !line.StartsWith("Usage:"))
                        .TakeWhile(line => !string.IsNullOrWhiteSpace(line))
                        .Where(line => line.Length > 7)
                        .Select(line => line.Substring(7))
                        .ToList();

    var exampleLines = helpLines
                        .TakeWhile(line => !line.StartsWith("Options"))
                        .Where(line => line.Length > 9)
                        .Select(line => line.Substring(9))
                        .ToList();

    var optionLines =  helpLines
                        .ParseOptions()
                        .ToList();

    return context.TransformText(@"# Usage

```powershell
<%usage%>
```

# Options

| Option                | Description                                                  |
|-----------------------|--------------------------------------------------------------|
<%options%>

# Examples

<%examples%>")
    .WithToken("usage",
        usageLines.Join())
    .WithToken("options",
        optionLines.Join(option => $"| {option.Option,-21} | {option.Descriptions.Join(separator:"<br>"), -60} |"))
    .WithToken("examples",
        exampleLines.Join(example => $@"```powershell
{example}
```
"))
    .ToString();
}

public static string Join<T>(this IEnumerable<T> lines, Func<T, string> selector = null, string separator = null)
{
    string defaultSelector<TValue>(TValue value) => $"{value}";

    return string.Join(separator ?? System.Environment.NewLine, lines.Select(selector ?? defaultSelector));
}

public static IEnumerable<CakeUsageOption> ParseOptions(this IEnumerable<string> lines)
{
    var stack = new Stack<CakeUsageOption>();
    foreach(var line in lines.Where(line => line?.Length > 25))
    {
        var key = line.Substring(0, 25).Trim();
        var description = line.Substring(25);

        if (key.StartsWith("--"))
        {
            stack.Push(new CakeUsageOption(key, description));
        }
        else if(stack.Count>0)
        {
            stack.Peek().Descriptions.Add(description);
        }
    }
    return stack.Reverse();
}

public class CakeUsageOption
{
    public string Option { get; }
    public IList<string> Descriptions { get; }
    public CakeUsageOption(string option, string description)
    {
        Option = option;
        Descriptions = new List<string>{ description };
    }
}