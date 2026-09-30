using CodeQuality.Cli;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: codequality <summary|methods|clones|comments> <path> [--json] [--config PATH] [--min-lines N] [--verdict V]");
    return 2;
}

var command = args[0];
var path = args[1];

// eip walks the semantic model instead of a directory: codequality eip 8038 <root> [<root>...]
if (command == "eip")
{
    var tail = args.Skip(2).ToList();
    var eipRoots = tail
        .Where((a, i) => !a.StartsWith("--")
                          && !(i > 0 && (tail[i - 1] == "--depth" || tail[i - 1] == "--sequence")))
        .ToList();
    if (eipRoots.Count == 0)
    {
        Console.Error.WriteLine("usage: codequality eip <number> <root> [<root>...] [--depth N] [--json]");
        return 2;
    }
    var depthIndex = Array.IndexOf(args, "--depth");
    var eipDepth = depthIndex >= 0 && depthIndex + 1 < args.Length ? int.Parse(args[depthIndex + 1]) : 2;
    var seqIndex = Array.IndexOf(args, "--sequence");
    var seqOf = seqIndex >= 0 && seqIndex + 1 < args.Length ? args[seqIndex + 1] : null;
    return EipCommand.Run(path, eipRoots, eipDepth, args.Contains("--json"), seqOf);
}

if (command == "comments-move")
{
    var outRoot = args.Length > 2 && !args[2].StartsWith("--") ? args[2] : null;
    if (outRoot is null)
    {
        Console.Error.WriteLine("usage: codequality comments-move <sourceRoot> <outRoot> [--apply]");
        return 2;
    }
    return CommentsCommand.Run(path, outRoot, Directory.GetCurrentDirectory(), args.Contains("--apply"));
}

try
{
    var options = ArgumentParser.Parse(args);

    if (!Directory.Exists(path))
    {
        Console.Error.WriteLine($"not a directory: {path}");
        return 2;
    }

    if (options.Config is not null && !File.Exists(options.Config))
    {
        Console.Error.WriteLine($"config not found: {options.Config}");
        return 2;
    }

    return command switch
    {
        "summary" => Commands.Summary(path, options.Json, options.Config),
        "methods" => Commands.Methods(path, options.MinLines, options.Json, options.Config),
        "clones" => Commands.Clones(path, options.Json, options.Config),
        "comments" => Commands.Comments(path, options.Verdict, options.Json, options.Config),
        _ => Unknown(command),
    };
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"unknown command: {command}");
    return 2;
}
