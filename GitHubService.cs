using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeReviewAssistant.Core.Interfaces;
using CodeReviewAssistant.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace CodeReviewAssistant.Infrastructure.Services
{
    public class GitHubService : IGitHubService
    {
        private readonly GitHubClient _gitHubClient;
        private readonly GitHubSettings _settings;
        private readonly ILogger<GitHubService> _logger;
        private readonly ICodeAnalyzer _codeAnalyzer;

        public GitHubService(
            IOptions<GitHubSettings> settings,
            ILogger<GitHubService> logger,
            ICodeAnalyzer codeAnalyzer)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _codeAnalyzer = codeAnalyzer ?? throw new ArgumentNullException(nameof(codeAnalyzer));

            _gitHubClient = new GitHubClient(new ProductHeaderValue("CodeReviewAssistant"))
            {
                Credentials = new Credentials(_settings.AccessToken)
            };
        }

        public async Task<PullRequestReviewResult> ReviewPullRequestAsync(string owner, string repository, int pullRequestNumber)
        {
            try
            {
                _logger.LogInformation("Starting review of PR #{PrNumber} in {Owner}/{Repo}", 
                    pullRequestNumber, owner, repository);
                
                // Get the PR details
                var pullRequest = await _gitHubClient.PullRequest.Get(owner, repository, pullRequestNumber);
                
                // Get the files changed in this PR
                var files = await _gitHubClient.PullRequest.Files(owner, repository, pullRequestNumber);
                
                var result = new PullRequestReviewResult
                {
                    PullRequestId = pullRequestNumber,
                    RepositoryFullName = $"{owner}/{repository}",
                    StartTime = DateTime.UtcNow,
                    Status = ReviewStatus.InProgress
                };

                // Process each file
                var reviewTasks = files
                    .Where(file => ShouldReviewFile(file.Filename))
                    .Select(file => ProcessFileAsync(owner, repository, file, pullRequest.Head.Sha))
                    .ToList();

                // Wait for all file reviews to complete
                var fileReviews = await Task.WhenAll(reviewTasks);
                
                // Add the results to our review result
                result.FileReviews.AddRange(fileReviews);
                
                // Submit review comments
                await SubmitReviewCommentsAsync(owner, repository, pullRequestNumber, result);
                
                // Update review status
                result.EndTime = DateTime.UtcNow;
                result.Status = ReviewStatus.Completed;
                result.IssueCount = result.FileReviews.Sum(fr => fr.Issues.Count);
                
                _logger.LogInformation("Completed review of PR #{PrNumber} in {Owner}/{Repo}, found {IssueCount} issues", 
                    pullRequestNumber, owner, repository, result.IssueCount);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reviewing pull request {Owner}/{Repo}#{PrNumber}", 
                    owner, repository, pullRequestNumber);
                
                throw;
            }
        }

        private bool ShouldReviewFile(string filename)
        {
            // Skip binary files, deleted files, etc.
            var extension = System.IO.Path.GetExtension(filename).ToLowerInvariant();
            
            return _settings.SupportedFileExtensions.Contains(extension);
        }

        private async Task<FileReview> ProcessFileAsync(string owner, string repository, PullRequestFile file, string commitSha)
        {
            try
            {
                // Get the file content
                var fileContent = await GetFileContentAsync(owner, repository, file.Filename, commitSha);
                
                // Determine language from file extension
                var extension = System.IO.Path.GetExtension(file.Filename).ToLowerInvariant();
                var language = MapExtensionToLanguage(extension);
                
                // Calculate a dummy repository ID - in a real system, this would come from the database
                var repositoryId = Guid.Parse(StringToGuid($"{owner}/{repository}"));
                
                // Analyze the code
                var analysisResult = await _codeAnalyzer.AnalyzeCodeAsync(
                    fileContent, 
                    language, 
                    file.Filename, 
                    repositoryId);
                
                return new FileReview
                {
                    FilePath = file.Filename,
                    Language = language,
                    Issues = analysisResult.Issues,
                    LinesAdded = file.Additions,
                    LinesRemoved = file.Deletions,
                    Status = file.Status,
                    AnalysisCompleted = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file {Filename}", file.Filename);
                
                return new FileReview
                {
                    FilePath = file.Filename,
                    Status = file.Status,
                    AnalysisCompleted = false,
                    Error = ex.Message
                };
            }
        }

        private async Task<string> GetFileContentAsync(string owner, string repository, string path, string commitSha)
        {
            try
            {
                var fileContent = await _gitHubClient.Repository.Content.GetAllContentsByRef(
                    owner, repository, path, commitSha);
                
                // There should only be one file
                if (fileContent.Count == 1)
                {
                    return fileContent[0].Content;
                }
                
                _logger.LogWarning("Expected 1 file at {Path}, but got {Count}", path, fileContent.Count);
                throw new InvalidOperationException($"Expected 1 file at {path}, but got {fileContent.Count}");
            }
            catch (NotFoundException)
            {
                _logger.LogWarning("File {Path} not found in {Owner}/{Repo} at commit {Sha}", 
                    path, owner, repository, commitSha);
                throw;
            }
        }

        private async Task SubmitReviewCommentsAsync(string owner, string repository, int pullRequestNumber, PullRequestReviewResult review)
        {
            try
            {
                // Create a new pull request review
                var newReview = new NewPullRequestReview
                {
                    Body = GenerateReviewSummary(review),
                    Event = PullRequestReviewEvent.Comment // or Approve/RequestChanges based on severity
                };
                
                // Add comments for each issue
                foreach (var fileReview in review.FileReviews)
                {
                    foreach (var issue in fileReview.Issues)
                    {
                        newReview.Comments.Add(new DraftPullRequestReviewComment
                        {
                            Path = fileReview.FilePath,
                            Body = FormatIssueComment(issue),
                            Position = issue.LineNumber // This is a simplification, GitHub needs diff position
                        });
                    }
                }
                
                // Submit the review
                await _gitHubClient.PullRequest.Review.Create(
                    owner, repository, pullRequestNumber, newReview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting review comments");
            }
        }

        private string GenerateReviewSummary(PullRequestReviewResult review)
        {
            // Count issues by severity
            int critical = 0, high = 0, medium = 0, low = 0;
            
            foreach (var fileReview in review.FileReviews)
            {
                foreach (var issue in fileReview.Issues)
                {
                    switch (issue.Severity)
                    {
                        case IssueSeverity.Critical: critical++; break;
                        case IssueSeverity.High: high++; break;
                        case IssueSeverity.Medium: medium++; break;
                        case IssueSeverity.Low: low++; break;
                    }
                }
            }
            
            var summary = $@"# AI Code Review Results

I've analyzed the changes and found the following issues:

- 🚨 Critical: {critical}
- ⚠️ High: {high}
- 🔔 Medium: {medium}
- 📝 Low: {low}

Total files analyzed: {review.FileReviews.Count}
Review time: {(review.EndTime - review.StartTime).TotalSeconds:F2} seconds

See inline comments for details on each issue.";

            return summary;
        }

        private string FormatIssueComment(CodeIssue issue)
        {
            var severityEmoji = issue.Severity switch
            {
                IssueSeverity.Critical => "🚨",
                IssueSeverity.High => "⚠️",
                IssueSeverity.Medium => "🔔",
                IssueSeverity.Low => "📝",
                _ => "ℹ️"
            };
            
            return $@"{severityEmoji} **{issue.Type}**: {issue.Description}

{issue.Suggestion}

Rule: {issue.RuleId ?? "CustomRule"}";
        }

        private string MapExtensionToLanguage(string extension)
        {
            return extension switch
            {
                ".cs" => "csharp",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".jsx" => "javascript",
                ".tsx" => "typescript",
                ".html" => "html",
                ".css" => "css",
                ".sql" => "sql",
                _ => "text"
            };
        }
        
        private string StringToGuid(string input)
        {
            // Create a deterministic GUID from a string
            using var md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            
            return new Guid(hashBytes).ToString();
        }
    }

    public class GitHubSettings
    {
        public string AccessToken { get; set; }
        public HashSet<string> SupportedFileExtensions { get; set; } = new HashSet<string>
        {
            ".cs", ".js", ".ts", ".jsx", ".tsx", ".html", ".css", ".sql"
        };
    }
}
