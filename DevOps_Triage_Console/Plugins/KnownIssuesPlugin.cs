using DevOps_Triage_Console.Data;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace DevOps_Triage_Console.Plugins;

public sealed class KnownIssuesPlugin
{
	private readonly KnownIssueRepository _repository;

	public KnownIssuesPlugin(KnownIssueRepository repository)
	{
		_repository = repository;
	}

	[KernelFunction]
	[Description("Searches the local known-issues knowledge base for incidents related to the reported problem.")]
	public async Task<string> SearchKnownIssuesAsync(
		[Description("A concise search query containing symptoms, product area, error information, or suspected cause.")]
	string query,
		[Description("Maximum records to return. Use a number from 1 to 5.")]
	int limit = 5)
	{
		limit = Math.Clamp(limit, 1, 5);
		var issues = await _repository.SearchAsync(query, limit);

		return issues.Count == 0
			? "No matching known issues were found."
			: JsonSerializer.Serialize(issues, new JsonSerializerOptions { WriteIndented = true });
	}
}
