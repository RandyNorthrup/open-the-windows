using System.CommandLine;

namespace OpenTheWindows.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        RootCommand root = CommandLineBuilder.Build();
        return root.Parse(args).Invoke();
    }
}
