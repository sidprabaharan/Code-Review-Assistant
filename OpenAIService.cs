using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using CodeReviewAssistant.Core.Enums;
using CodeReviewAssistant.Core.Interfaces;
using CodeReviewAssistant.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace CodeReviewAssistant.Infrastructure.Services
{
    public class OpenAIService : IOpenAIService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly OpenAISettings _settings;
        private readonly ILogger<OpenAIService> _logger;

        public OpenAIService(
            OpenAIClient openAIClient,
            IOptions<OpenAISettings> settings,
            ILogger<OpenAIService> logger)
        {
            _openAIClient = openAIClient ?? throw new ArgumentNullException(nameof(openAIClient));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<CodeIssue>> AnalyzeCodeAsync(string code, string language, double complexityScore)
        {
            try
            {
                var prompt = BuildAnalysisPrompt(code, language, complexityScore);
                
                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _settings.DeploymentName,
                    MaxTokens = _settings.MaxTokens,
                    Temperature = 0.0f,  // Use 0 for deterministic results
                    PresencePenalty = 0.0f,
                    FrequencyPenalty = 0.0f
                };

                // System message with instructions
                chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(
                    "You are a senior software engineer specializing in code review and security analysis. " +
                    "Your task is to analyze code samples for potential issues including syntax errors, " +
                    "security vulnerabilities, performance problems, and best practice violations. " +
                    "Provide your analysis in a structured JSON format for easy processing."
                ));
                
                // User message with code to analyze
                chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(prompt));

                var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                var content = response.Value.Choices[0].Message.Content;

                // Extract JSON from the response (it might be wrapped in markdown code blocks)
                var jsonContent = ExtractJsonFromResponse(content);
                
                return DeserializeIssues(jsonContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI for code analysis: {Message}", ex.Message);
                return new List<CodeIssue>();
            }
        }

        public async Task<string> GenerateFixSuggestionAsync(string code, CodeIssue issue)
        {
            try
            {
                var prompt = $@"
I have the following {issue.Language} code with an issue:

```{issue.Language}
{code}
```

The issue is:
- Type: {issue.Type}
- Description: {issue.Description}
- Location: Line {issue.LineNumber}, Column {issue.ColumnNumber}
- Severity: {issue.Severity}

Please provide a brief explanation of the issue and a specific, concrete suggestion on how to fix it. 
Only include the code that needs to be changed, not the entire file.
";

                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _settings.DeploymentName,
                    MaxTokens = _settings.MaxTokens,
                    Temperature = 0.1f,  // Slightly more creative for suggestions
                };

                chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(
                    "You are an expert software engineer specializing in fixing code issues. " +
                    "Provide concise, actionable fixes with clear explanations."
                ));
                
                chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(prompt));

                var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                return response.Value.Choices[0].Message.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating fix suggestion: {Message}", ex.Message);
                return "Unable to generate a fix suggestion at this time.";
            }
        }

        private string BuildAnalysisPrompt(string code, string language, double complexityScore)
        {
            var promptBuilder = new StringBuilder();
            
            promptBuilder.AppendLine($"Please analyze the following {language} code for issues:");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine($"```{language}");
            promptBuilder.AppendLine(code);
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("The code complexity score is: " + complexityScore);
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Perform a thorough analysis focusing on:");
            promptBuilder.AppendLine("1. Syntax errors and logical bugs");
            promptBuilder.AppendLine("2. Security vulnerabilities (especially OWASP Top 10)");
            promptBuilder.AppendLine("3. Performance issues");
            promptBuilder.AppendLine("4. Code style and best practices");
            promptBuilder.AppendLine("5. Design patterns and architecture");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Provide your findings in the following JSON format:");
            promptBuilder.AppendLine(@"{
  ""issues"": [
    {
      ""type"": ""security|performance|style|error|warning"",
      ""description"": ""Detailed description of the issue"",
      ""lineNumber"": 123,
      ""columnNumber"": 10,
      ""severity"": ""critical|high|medium|low"",
      ""ruleId"": ""CWE-79"",
      ""suggestion"": ""Brief suggestion for fixing the issue""
    }
  ]
}");

            return promptBuilder.ToString();
        }

        private string ExtractJsonFromResponse(string content)
        {
            // Check if the content is wrapped in markdown code blocks
            var jsonStartIndex = content.IndexOf("```json");
            var jsonEndIndex = content.LastIndexOf("```");
            
            if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
            {
                // Extract the JSON content between the code blocks
                jsonStartIndex = content.IndexOf('\n', jsonStartIndex) + 1;
                return content.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex).Trim();
            }
            
            // If no code blocks found, assume the entire content is JSON
            return content;
        }

        private List<CodeIssue> DeserializeIssues(string jsonContent)
        {
            try
            {
                var result = JsonConvert.DeserializeObject<OpenAIAnalysisResult>(jsonContent);
                if (result?.Issues == null)
                {
                    _logger.LogWarning("Failed to deserialize OpenAI response to issues list. Response: {Response}", jsonContent);
                    return new List<CodeIssue>();
                }
                
                var issues = new List<CodeIssue>();
                foreach (var issue in result.Issues)
                {
                    // Parse and validate the issue data
                    if (Enum.TryParse<IssueType>(issue.Type, true, out var issueType) &&
                        Enum.TryParse<IssueSeverity>(issue.Severity, true, out var severity) &&
                        issue.LineNumber > 0)
                    {
                        issues.Add(new CodeIssue
                        {
                            Id = Guid.NewGuid(),
                            Type = issueType,
                            Description = issue.Description,
                            LineNumber = issue.LineNumber,
                            ColumnNumber = issue.ColumnNumber,
                            Severity = severity,
                            RuleId = issue.RuleId,
                            Suggestion = issue.Suggestion,
                            Source = IssueSource.AI,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                
                return issues;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing OpenAI response: {Response}", jsonContent);
                return new List<CodeIssue>();
            }
        }

        private class OpenAIAnalysisResult
        {
            [JsonProperty("issues")]
            public List<OpenAIIssue> Issues { get; set; }
        }

        private class OpenAIIssue
        {
            [JsonProperty("type")]
            public string Type { get; set; }
            
            [JsonProperty("description")]
            public string Description { get; set; }
            
            [JsonProperty("lineNumber")]
            public int LineNumber { get; set; }
            
            [JsonProperty("columnNumber")]
            public int ColumnNumber { get; set; }
            
            [JsonProperty("severity")]
            public string Severity { get; set; }
            
            [JsonProperty("ruleId")]
            public string RuleId { get; set; }
            
            [JsonProperty("suggestion")]
            public string Suggestion { get; set; }
        }
    }

    public class OpenAISettings
    {
        public string ApiKey { get; set; }
        public string Endpoint { get; set; }
        public string DeploymentName { get; set; }
        public int MaxTokens { get; set; } = 8000;
    }
}
