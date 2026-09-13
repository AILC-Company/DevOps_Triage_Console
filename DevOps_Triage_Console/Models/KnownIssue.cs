namespace DevOps_Triage_Console.Models;

public sealed record KnownIssue(
string Id,
string Title,
string ProductArea,
string Symptoms,
string RootCause,
string RecommendedFix,
string Severity);
