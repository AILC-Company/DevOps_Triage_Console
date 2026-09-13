namespace DevOps_Triage_Console.Models;

public sealed record OpenTicket(
	string Id,
	string Title,
	string Status,
	string Priority,
	string Team,
	string Summary);