using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            var options = CliOptions.ParseArgs(args);

            if (options.ToolsError != null)
            {
                Console.Error.WriteLine(options.ToolsError);
                Environment.ExitCode = 2;
                return;
            }

            Engineering.TiaMajorVersion = options.TiaMajorVersion ?? 21;

            // Set before the --doctor branch below, which reports the write mode, and before the
            // host registers its tool types.
            WritePolicy.AllowWrite = options.AllowWrite;
            ToolSets.Enabled = options.ToolAreas;

            if (Engineering.TiaMajorVersion >= 20)
            {
                Openness.Initialize(Engineering.TiaMajorVersion);
            }

            // Fallback: the Siemens resolver does not cover every Siemens.Engineering.* satellite assembly.
            AppDomain.CurrentDomain.AssemblyResolve += Engineering.Resolver;

            // '--doctor' only reports the environment - it must run before the user group gate below,
            // because a missing group membership is one of the things it is meant to diagnose.
            if (options.Doctor)
            {
                RunDoctor();
                return;
            }

            // Ensure user is in user group 'Siemens TIA Openness'
            if (await Openness.IsUserInGroup())
            {
                await RunStdioHost(options);
            }
            else
            {
                Console.WriteLine("User is not in the required group. Exiting...");
            }
        }

        /// <summary>
        /// Prints the environment diagnostics of the '--doctor' command. Read-only: it neither
        /// connects to TIA Portal nor changes user group membership.
        /// </summary>
        public static void RunDoctor()
        {
            try
            {
                // Fully qualified: 'Diagnostics' alone would collide with the System.Diagnostics namespace.
                var report = TiaMcpServer.Siemens.Diagnostics.Run(new Portal(), WritePolicy.AllowWrite, ToolSets.Describe(ToolSets.Enabled));

                Console.WriteLine(report.Text);
            }
            catch (Exception ex)
            {
                // A diagnostics command must report a problem, not crash with a stack trace.
                Console.Error.WriteLine($"Diagnose failed: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }

        /// <summary>
        /// The read-only tools are always registered. The project-mutating tools are the
        /// McpServer methods marked [WriteTool]; they are left out with '--read-only',
        /// which is what keeps them out of 'tools/list' rather than merely refusing them when
        /// called. Tools are created one by one (the SDK's type-based registration would take
        /// every [McpServerTool] method of the class). The [DebugTool] methods are left out the same
        /// way unless '--debug-tools' was passed. '--tools' limits the tools to the named areas
        /// (<see cref="ToolSets"/>); the tools of connection, project and diagnostics stay.
        /// </summary>
        public static IEnumerable<global::ModelContextProtocol.Server.McpServerTool> BuildTools(bool allowWrite, bool debugTools = false, IReadOnlyCollection<string>? areas = null)
        {
            return typeof(McpServer)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<global::ModelContextProtocol.Server.McpServerToolAttribute>() != null)
                .Where(m => allowWrite || m.GetCustomAttribute<WriteToolAttribute>() == null)
                .Where(m => debugTools || m.GetCustomAttribute<DebugToolAttribute>() == null)
                .Select(m => global::ModelContextProtocol.Server.McpServerTool.Create(m))
                .Where(t => ToolSets.IsRegistered(t.ProtocolTool.Name, areas))
                .ToList();
        }

        /// <summary>
        /// The tools as a client sees them without '--full': <see cref="BuildTools"/> wrapped into the group tools of
        /// <see cref="ToolGroups"/>.
        /// </summary>
        public static IEnumerable<global::ModelContextProtocol.Server.McpServerTool> BuildCompactTools(bool allowWrite, bool debugTools = false, IReadOnlyCollection<string>? areas = null)
        {
            var writes = new HashSet<string>(typeof(McpServer)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<WriteToolAttribute>() != null)
                .Select(m => m.GetCustomAttribute<global::ModelContextProtocol.Server.McpServerToolAttribute>()?.Name)
                .Where(n => n != null)
                .Select(n => n!));

            return ToolGroups.Build(BuildTools(allowWrite, debugTools, areas).Select(t => (t, writes.Contains(t.ProtocolTool.Name))).ToList());
        }

        public static async Task RunStdioHost(CliOptions? options)
        {
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);
            if (builder != null)
            {
                if (options != null && options.Logging != null)
                {
                    switch (options.Logging)
                    {
                        case 1:
                            // ATTENTION: For STDIO, logs must go to stderr!
                            builder.Logging.AddConsole(options =>
                            {
                                options.LogToStandardErrorThreshold = LogLevel.Trace;
                            });
                            break;

                        case 2:
                            // Visual Studio Debug Output / Sysinternals.DebugView
                            builder.Logging.AddDebug();
                            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
                            builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Information);
                            builder.Logging.AddFilter("TiaMcpServer", LogLevel.Debug);

                            // Log Level for Debug Output
                            builder.Logging.SetMinimumLevel(LogLevel.Debug);
                            break;

                        case 3:
                            // Windows Event Log
                            builder.Logging.AddEventLog();
                            break;

                        default:
                            // no logging
                            break;
                    }
                }

                builder.Services
                    .AddMcpServer(serverOptions =>
                    {
                        serverOptions.ServerInfo = new global::ModelContextProtocol.Protocol.Implementation
                        {
                            Name = "TiaMcpServer",
                            Title = "TIA Portal MCP Server",
                            Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"
                        };

                        serverOptions.ServerInstructions =
                            "Exposes Siemens TIA Portal via Openness. Call 'open_tia_project' with an absolute .apXX " +
                            "project or .alsXX session path (or 'connect', then 'open_project'); TIA Portal must " +
                            "already be running. Use 'get_project_tree' and 'plc_get_software_tree' to discover the " +
                            "path strings the other tools expect; a '/' inside a name is written '%2F'. Export and " +
                            "import tools operate on the local file system of the machine running this server." +
                            (WritePolicy.AllowWrite
                                ? " Tools that create, rename or delete project objects are available. Their " +
                                  "changes stay in memory until 'save_project'."
                                : " The server runs read-only: tools that change the project are not available.") +
                            (options?.ToolAreas == null
                                ? string.Empty
                                : $" Only these tool areas are registered: {ToolSets.Describe(options.ToolAreas)} (and the tools for connection, " +
                                  "project and diagnostics); the other areas exist but were left out with '--tools'.") +
                            (options?.Full != true
                                ? " The tools named here and in the answers are called through the group tools - everything that only reads through 'tia_read', the rest through " +
                                  "plc_write, plc_delete, hw_write, ... - as {\"tool\": \"<name>\", \"arguments\": {...}}; each group tool lists its tools, and the tool 'tia_help' of 'tia_read' gives the parameters of a tool."
                                : string.Empty);
                    })
                    .WithStdioServerTransport()
                    .WithTools(options?.Full == true
                        ? BuildTools(WritePolicy.AllowWrite, options.DebugTools, options.ToolAreas)
                        : BuildCompactTools(WritePolicy.AllowWrite, options?.DebugTools ?? false, options?.ToolAreas))
                    .WithRequestFilters(filters => filters.AddCallToolFilter(ToolCallGate.Filter));

                // Register the Portal service for dependency injection
                builder.Services.AddSingleton<Portal>();

                var host = builder.Build();

                // Set the service provider for the MCP server, to retrieve Portal with injected logger
                McpServer.SetServiceProvider(host.Services);

                // Set the logger for the MCP server
                McpServer.Logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("McpServer");

                // log a bit of information about the server start
                if (options != null && options.Logging != null && options.Logging > 0)
                {
                    var logger = host.Services.GetRequiredService<ILogger<Program>>();

                    logger.LogInformation($"=== TIA Portal MCP Server '{DateTime.Now.ToShortTimeString()}' ===");

                    switch (options.Logging)
                    {
                        case 1:
                            logger.LogInformation("Logging to stderr");
                            break;
                        case 2:
                            logger.LogInformation("Logging to debug output");
                            break;
                        case 3:
                            logger.LogInformation("Logging to Windows event log");
                            break;
                    }
                }

                await host.RunAsync();
            }

        }
    }
}
