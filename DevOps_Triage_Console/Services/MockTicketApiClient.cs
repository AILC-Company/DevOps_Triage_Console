using DevOps_Triage_Console.Models;

namespace DevOps_Triage_Console.Services;

public sealed class MockTicketApiClient
{
	private static readonly IReadOnlyList<OpenTicket> Tickets = new List<OpenTicket>
	{
		new(
			"T-1042",
			"Finance reports slow Excel export after version 2.8",
			"Open",
			"High",
			"Desktop Engineering",
			"Exporting large financial datasets causes a long UI freeze."),
		new(
			"T-1051",
			"Token refresh fails after idle timeout",
			"In Progress",
			"Medium",
			"Platform",
			"Desktop client does not recover after access-token expiration."),
		new(
			"T-1068",
			"Duplicate questionnaire answers after retry",
			"Open",
			"High",
			"API Team",
			"Network retry causes duplicate answer records in SQL Server.")
	};

	public Task<IReadOnlyList<OpenTicket>> SearchAsync(string query, int limit = 5)
	{
		var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		var results = Tickets
			.Where(ticket => terms.Any(term =>
				ticket.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
				ticket.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)))
			.Take(Math.Clamp(limit, 1, 5))
			.ToList();

		return Task.FromResult<IReadOnlyList<OpenTicket>>(results);
	}
}