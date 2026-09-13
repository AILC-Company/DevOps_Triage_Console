using DevOps_Triage_Console.Agents;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DevOps_Triage_Console.Services;

public sealed class TriageWorkflow
{
	private readonly AgentFactory _agentFactory;
	private readonly ILogger<TriageWorkflow> _logger;

	public TriageWorkflow(AgentFactory agentFactory, ILogger<TriageWorkflow> logger)
	{
		_agentFactory = agentFactory;
		_logger = logger;
	}

	public async Task<string> RunAsync(string userReport, CancellationToken cancellationToken = default)
	{
		var intakeAgent = _agentFactory.CreateIntakeAgent();
		var researchAgent = _agentFactory.CreateResearchAgent();
		var resolverAgent = _agentFactory.CreateResolverAgent();

		_logger.LogInformation("Starting IntakeAgent.");
		var intake = await InvokeToTextAsync(intakeAgent, userReport, cancellationToken);

		var researchPrompt = $"""
            Original user report:
            ---
            {userReport}
            ---

            IntakeAgent analysis:
            ---
            {intake}
            ---

            Research the issue now.
            """;

		_logger.LogInformation("Starting ResearchAgent.");
		var research = await InvokeToTextAsync(researchAgent, researchPrompt, cancellationToken);

		var resolutionPrompt = $"""
            Original user report:
            ---
            {userReport}
            ---

            IntakeAgent analysis:
            ---
            {intake}
            ---

            ResearchAgent findings:
            ---
            {research}
            ---

            Produce the final triage response now.
            """;

		_logger.LogInformation("Starting ResolverAgent.");
		var resolution = await InvokeToTextAsync(resolverAgent, resolutionPrompt, cancellationToken);

		return $"""
            # Intake Analysis

            {intake}

            # Research Findings

            {research}

            # Resolution Plan

            {resolution}
            """;
	}

	private static async Task<string> InvokeToTextAsync(
		Microsoft.SemanticKernel.Agents.ChatCompletionAgent agent,
		string input,
		CancellationToken cancellationToken)
	{
		var output = new StringBuilder();

		await foreach (var update in agent.InvokeAsync(input, cancellationToken: cancellationToken))
		{
			var content = update.Message.Content;
			if (!string.IsNullOrWhiteSpace(content))
			{
				output.AppendLine(content);
			}
		}

		return output.Length == 0
			? "The agent returned no text. Inspect Ollama, model configuration, package compatibility, and logs."
			: output.ToString().Trim();
	}
}