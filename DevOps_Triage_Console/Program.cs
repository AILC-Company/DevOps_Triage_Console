using DevOps_Triage_Console.Agents;
using DevOps_Triage_Console.Data;
using DevOps_Triage_Console.Plugins;
using DevOps_Triage_Console.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(logging =>
{
	logging.ClearProviders();
	logging.AddSimpleConsole(options =>
	{
		options.SingleLine = true;
		options.TimestampFormat = "HH:mm:ss ";
	});
	logging.SetMinimumLevel(LogLevel.Information);
});

var modelId = builder.Configuration["Ollama:ModelId"]
	?? throw new InvalidOperationException("Ollama:ModelId is missing in appsettings.json.");

var endpointText = builder.Configuration["Ollama:Endpoint"]
	?? "http://localhost:11434";

if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint))
{
	throw new InvalidOperationException("Ollama:Endpoint is not a valid absolute URI.");
}

var kernelBuilder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0070
kernelBuilder.AddOllamaChatCompletion(
	modelId: modelId,
	endpoint: endpoint);
#pragma warning restore SKEXP0070

builder.Services.AddSingleton(kernelBuilder.Build());
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<KnownIssueRepository>();
builder.Services.AddSingleton<MockTicketApiClient>();
builder.Services.AddSingleton<KnownIssuesPlugin>();
builder.Services.AddSingleton<TicketApiPlugin>();
builder.Services.AddSingleton<AgentFactory>();
builder.Services.AddSingleton<TriageWorkflow>();

using var host = builder.Build();

var initializer = host.Services.GetRequiredService<DatabaseInitializer>();
await initializer.InitializeAsync();

var workflow = host.Services.GetRequiredService<TriageWorkflow>();

Console.WriteLine("Semantic Kernel DevOps Triage Console — Ollama local mode");
Console.WriteLine($"Model: {modelId}");
Console.WriteLine($"Endpoint: {endpoint}");
Console.WriteLine("Enter an issue report. Submit an empty line to quit.");
Console.WriteLine();

while (true)
{
	Console.Write("> ");
	var report = Console.ReadLine();

	if (string.IsNullOrWhiteSpace(report))
	{
		break;
	}

	try
	{
		Console.WriteLine();
		Console.WriteLine("Processing locally...\n");

		var result = await workflow.RunAsync(report);

		Console.WriteLine(result);
		Console.WriteLine("\n" + new string('-', 80) + "\n");
	}
	catch (HttpRequestException ex)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"Cannot reach Ollama at {endpoint}. Start Ollama and confirm the endpoint. Details: {ex.Message}");
		Console.ResetColor();
	}
	catch (Exception ex)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"Processing failed: {ex.Message}");
		Console.ResetColor();
	}
}
