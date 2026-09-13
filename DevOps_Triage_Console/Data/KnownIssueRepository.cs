using DevOps_Triage_Console.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace DevOps_Triage_Console.Data;

public sealed class KnownIssueRepository
{
	private readonly string _connectionString;

	public KnownIssueRepository(IConfiguration configuration)
	{
		_connectionString = configuration.GetConnectionString("KnowledgeBase")
			?? throw new InvalidOperationException("KnowledgeBase connection string is missing.");
	}

	public async Task<IReadOnlyList<KnownIssue>> SearchAsync(string query, int limit = 5)
	{
		var terms = query
			.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(term => term.Length >= 3)
			.Take(8)
			.ToArray();

		if (terms.Length == 0)
		{
			return Array.Empty<KnownIssue>();
		}

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync();

		var command = connection.CreateCommand();
		var conditions = new List<string>();

		for (var index = 0; index < terms.Length; index++)
		{
			var parameter = $"$term{index}";
			conditions.Add($"(Title LIKE {parameter} OR ProductArea LIKE {parameter} OR Symptoms LIKE {parameter} OR RootCause LIKE {parameter})");
			command.Parameters.AddWithValue(parameter, $"%{terms[index]}%");
		}

		command.CommandText = $"""
            SELECT Id, Title, ProductArea, Symptoms, RootCause, RecommendedFix, Severity
            FROM KnownIssues
            WHERE {string.Join(" OR ", conditions)}
            LIMIT $limit;
            """;
		command.Parameters.AddWithValue("$limit", limit);

		var results = new List<KnownIssue>();
		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			results.Add(new KnownIssue(
				reader.GetString(0),
				reader.GetString(1),
				reader.GetString(2),
				reader.GetString(3),
				reader.GetString(4),
				reader.GetString(5),
				reader.GetString(6)));
		}

		return results;
	}
}