namespace TiaMcpServer
{
    public class CliOptions
    {
        public int? TiaMajorVersion { get; set; }
        public int? Logging { get; set; } // "stdio" or "http"
        public bool Doctor { get; set; } // print environment diagnostics and exit
        // The project-mutating tools are registered unless '--read-only' is passed. '--allow-write'
        // is still accepted, so client configurations written for the earlier opt-in keep working.
        public bool AllowWrite { get; set; } = true;
        public bool DebugTools { get; set; } // register the server-development tools ([DebugTool])

        public static CliOptions ParseArgs(string[] args)
        {
            var options = new CliOptions();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-tia-major-version":
                    case "--tia-major-version":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int v))
                        {
                            options.TiaMajorVersion = v;
                            i++;
                        }
                        break;

                    case "-doctor":
                    case "--doctor":
                        options.Doctor = true;
                        break;

                    case "-allow-write":
                    case "--allow-write":
                        options.AllowWrite = true;
                        break;

                    case "-read-only":
                    case "--read-only":
                        options.AllowWrite = false;
                        break;

                    case "-debug-tools":
                    case "--debug-tools":
                        options.DebugTools = true;
                        break;

                    case "-logging":
                    case "--logging":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int l))
                        {
                            options.Logging = l;
                            i++;
                        }
                        break;
                }
            }
            return options;
        }
    }
}
