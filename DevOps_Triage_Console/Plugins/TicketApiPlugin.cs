using DevOps_Triage_Console.Services;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace DevOps_Triage_Console.Plugins;

public sealed class TicketApiPlugin
{
	private readonly MockTicketApiClient _client;

	public TicketApiPlugin(MockTicketApiClient client)
	{
		_client = client;
	}

	[KernelFunction]
	[Description("Searches currently open support and engineering tickets for reports related to an issue.")]
	public async Task<string> SearchOpenTicketsAsync(
		[Description("A concise search query based on the user report and intake analysis.")]
		string query,
		[Description("Maximum tickets to return. Use a number from 1 to 5.")]
		int limit = 5)
	{
		limit = Math.Clamp(limit, 1, 5);
		var tickets = await _client.SearchAsync(query, limit);

		return tickets.Count == 0
			? "No related open tickets were found."
			: JsonSerializer.Serialize(tickets, new JsonSerializerOptions { WriteIndented = true });
	}
}