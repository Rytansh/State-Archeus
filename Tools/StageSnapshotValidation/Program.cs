using NUnitLite;

internal static class Program
{
    private static int Main(string[] args) =>
        args.Length == 2 && args[0] == "--syntax-root"
            ? StaticSyntaxChecks.Run(args[1])
            : new AutoRun().Execute(args);
}
