using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pinlist.Core;
using Pinlist.Integration.Mcp;
using Pinlist.Storage.SQLite;

var dataDirectory = Environment.GetEnvironmentVariable("PINLIST_DATA_DIRECTORY");
if (string.IsNullOrWhiteSpace(dataDirectory))
    dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pinlist");
var databasePath = Path.Combine(dataDirectory, "pinlist.db");

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton<IPinTaskRepository>(_ => new SqlitePinTaskRepository(databasePath));
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<PinlistTools>();

await builder.Build().RunAsync();
