using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeReviewAssistant.Core.Enums;
using CodeReviewAssistant.Core.Interfaces;
using CodeReviewAssistant.Core.Models;

namespace CodeReviewAssistant.Core.Services
{
    public class CodeAnalyzer : ICodeAnalyzer
    {
        private readonly IOpenAIService _openAIService;
        private readonly ICodeParser _codeParser;
        private readonly IIssueRepository _issueRepository;
        private readonly IRuleEngine _ruleEngine;

        public CodeAnalyzer(
            IOpenAIService openAIService,
            ICodeParser codeParser,
            IIssueRepository issueRepository,
            IRuleEngine ruleEngine)
        {
            _openAIService = openAIService ?? throw new ArgumentNullException(nameof(openAIService));
            _codeParser = codeParser ?? throw new ArgumentNullException(nameof(codeParser));
            _issueRepository = issueRepository ?? throw new ArgumentNullException(nameof(issueRepository));
            _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        }

        public async Task<AnalysisResult> AnalyzeCodeAsync(string code, string language, string filePath, Guid repositoryId)
        {
            // Parse the code to extract relevant structures (classes, methods, etc.)
            var parsedCode = _codeParser.Parse(code, language);
            
            // Apply custom rules based on the language and repository settings
            var ruleBasedIssues = _ruleEngine.ApplyRules(parsedCode, language, repositoryId);
            
            // Use OpenAI to analyze for more complex patterns and potential issues
            var aiBasedIssues = await _openAIService.AnalyzeCodeAsync(
                code, 
                language, 
                parsedCode.ComplexityScore);

            // Combine the issues and remove duplicates
            var combinedIssues = MergeIssues(ruleBasedIssues, aiBasedIssues);
            
            // Store the analysis results
            foreach (var issue in combinedIssues)
            {
                await _issueRepository.AddIssueAsync(issue);
            }

            return new AnalysisResult
            {
                FilePath = filePath,
                Language = language,
                RepositoryId = repositoryId,
                Issues = combinedIssues,
                ComplexityScore = parsedCode.ComplexityScore,
                AnalysisTimestamp = DateTime.UtcNow
            };
        }

        private List<CodeIssue> MergeIssues(List<CodeIssue> ruleBasedIssues, List<CodeIssue> aiBasedIssues)
        {
            var mergedIssues = new List<CodeIssue>(ruleBasedIssues);
            var existingLocations = new HashSet<string>(ruleBasedIssues.ConvertAll(i => $"{i.LineNumber}:{i.ColumnNumber}"));

            foreach (var aiIssue in aiBasedIssues)
            {
                var locationKey = $"{aiIssue.LineNumber}:{aiIssue.ColumnNumber}";
                
                // Only add if we don't already have an issue at this location
                if (!existingLocations.Contains(locationKey))
                {
                    mergedIssues.Add(aiIssue);
                    existingLocations.Add(locationKey);
                }
            }

            // Sort by severity (Critical first) and then by line number
            mergedIssues.Sort((a, b) => 
            {
                var severityComparison = b.Severity.CompareTo(a.Severity);
                return severityComparison != 0 ? severityComparison : a.LineNumber.CompareTo(b.LineNumber);
            });
            
            return mergedIssues;
        }
    }
}
