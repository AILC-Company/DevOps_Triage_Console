using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace DevOps_Triage_Console.Services;

public sealed class DatabaseInitializer
{
	private readonly string _connectionString;

	public DatabaseInitializer(IConfiguration configuration)
	{
		_connectionString = configuration.GetConnectionString("KnowledgeBase")
			?? throw new InvalidOperationException("KnowledgeBase connection string is missing.");
	}

	public async Task InitializeAsync()
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync();

		var command = connection.CreateCommand();
		command.CommandText = """
            CREATE TABLE IF NOT EXISTS KnownIssues (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                ProductArea TEXT NOT NULL,
                Symptoms TEXT NOT NULL,
                RootCause TEXT NOT NULL,
                RecommendedFix TEXT NOT NULL,
                Severity TEXT NOT NULL
            );
            """;

		await command.ExecuteNonQueryAsync();

		var countCommand = connection.CreateCommand();
		countCommand.CommandText = "SELECT COUNT(*) FROM KnownIssues;";
		var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

		if (count > 0)
		{
			return;
		}

		await InsertSeedAsync(connection,
			"KI-001",
			"Large export blocks the UI thread",
			"Export",
			"Desktop client becomes unresponsive during large exports. Window may show Not Responding.",
			"Export generation runs synchronously on the UI thread.",
			"Generate files in a background task; marshal only UI updates to the UI thread; add progress and cancellation.",
			"High");

		await InsertSeedAsync(connection,
			"KI-002",
			"Authentication fails after token expiry",
			"Authentication",
			"API requests return 401 after a long-running desktop session.",
			"Client does not refresh access tokens before the request.",
			"Implement token-refresh handling and retry one authenticated request after refresh.",
			"Medium");

		await InsertSeedAsync(connection,
			"KI-003",
			"Questionnaire save creates duplicate answers",
			"Questionnaire",
			"Saving the same response multiple times produces duplicate rows.",
			"API endpoint is not idempotent and no unique constraint protects the answer key.",
			"Add a unique constraint and use an upsert operation keyed by questionnaire, question, and respondent.",
			"High");

		await InsertSeedAsync(connection,
			"KI-004",
			"Export has high memory pressure",
			"Export",
			"Large exports allocate excessive memory and may fail with OutOfMemoryException.",
			"Application materializes all rows in a DataTable before writing the spreadsheet.",
			"Stream records in batches and write incrementally; avoid copying the entire result set into memory.",
			"High");
	}

	private static async Task InsertSeedAsync(
		SqliteConnection connection,
		string id,
		string title,
		string area,
		string symptoms,
		string rootCause,
		string fix,
		string severity)
	{
		var command = connection.CreateCommand();
		command.CommandText = """
            INSERT INTO KnownIssues
            (Id, Title, ProductArea, Symptoms, RootCause, RecommendedFix, Severity)
            VALUES
            ($id, $title, $area, $symptoms, $rootCause, $fix, $severity);
            """;

		command.Parameters.AddWithValue("$id", id);
		command.Parameters.AddWithValue("$title", title);
		command.Parameters.AddWithValue("$area", area);
		command.Parameters.AddWithValue("$symptoms", symptoms);
		command.Parameters.AddWithValue("$rootCause", rootCause);
		command.Parameters.AddWithValue("$fix", fix);
		command.Parameters.AddWithValue("$severity", severity);

		await command.ExecuteNonQueryAsync();
	}
}
