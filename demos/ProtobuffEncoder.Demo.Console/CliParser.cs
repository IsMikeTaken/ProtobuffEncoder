public static class CliParser
{
    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

        foreach (var arg in args)
        {
            switch (arg.ToLowerInvariant())
            {
                case "-v":
                case "--verbose":
                    options.Verbose = true;
                    break;
                case "-s":
                case "--silent":
                    options.Silent = true;
                    break;
            }
        }

        return options;
    }
}