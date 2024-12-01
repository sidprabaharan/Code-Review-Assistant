#!/usr/bin/env python3
import os
import random
import subprocess
from datetime import datetime, timedelta
import time

# Configuration
START_DATE = datetime(2024, 12, 1)
END_DATE = datetime(2024, 12, 31)
REPOSITORY_PATH = "C:/Users/Owner/Code-Review-Assistant"  # Change this to your repository path
COMMIT_MIN = 1  # Minimum commits per active day
COMMIT_MAX = 5  # Maximum commits per active day
ACTIVE_DAYS_PERCENT = 70  # Percentage of days that will have commits
WEEKEND_COMMIT_PROBABILITY = 40  # Percentage chance of committing on weekends
DAILY_START_HOUR = 9  # Earliest hour to commit (24-hour format)
DAILY_END_HOUR = 23  # Latest hour to commit (24-hour format)

# List of common commit messages to randomly select from
COMMIT_MESSAGES = [
    "Initial project structure",
    "Add core analyzer service",
    "Implement OpenAI integration",
    "Add GitHub service for PR integration",
    "Create basic models and interfaces",
    "Setup database schema and migrations",
    "Add repository service implementation",
    "Create React dashboard component",
    "Implement issues table in dashboard",
    "Add statistics charts to dashboard",
    "Fix bug in code analyzer",
    "Improve error handling in GitHub service",
    "Add unit tests for core services",
    "Update documentation",
    "Refactor OpenAI service for better prompting",
    "Add authentication middleware",
    "Implement file parsing logic",
    "Add rule engine for pattern-based detection",
    "Create API endpoints for repositories",
    "Implement pull request review endpoints",
    "Add severity classification to issues",
    "Optimize database queries",
    "Add logging and error tracking",
    "Improve UI/UX of dashboard",
    "Configure CI/CD pipeline",
    "Update README with setup instructions",
    "Add pagination to API endpoints",
    "Implement caching for GitHub API calls",
    "Create Docker configuration",
    "Add support for more programming languages",
    "Fix security vulnerability in API",
    "Improve code comments and documentation",
    "Refactor frontend state management",
    "Add integration tests"
]

# File paths to modify (these should exist in your repository)
FILE_PATHS = [
    "README.md",
    "src/CodeReviewAssistant.Core/Services/CodeAnalyzer.cs",
    "src/CodeReviewAssistant.Infrastructure/Services/OpenAIService.cs",
    "src/CodeReviewAssistant.Infrastructure/Services/GitHubService.cs",
    "src/CodeReviewAssistant.API/Controllers/RepositoriesController.cs",
    "src/CodeReviewAssistant.Web/src/components/Dashboard.jsx",
    "docs/architecture.md",
    "docs/github-integration.md",
    ".github/workflows/build.yml",
    "database/schema.sql"
]

def make_random_change(file_path):
    """Make a small change to a file to create a reason for a commit"""
    if not os.path.exists(file_path):
        # Create directory if it doesn't exist
        os.makedirs(os.path.dirname(file_path), exist_ok=True)
        
        # Create file with some content if it doesn't exist
        with open(file_path, 'w') as f:
            f.write(f"# {os.path.basename(file_path)}\n\nInitial content for {file_path}")
        return
    
    # File exists, make a small modification
    with open(file_path, 'a') as f:
        f.write(f"\n// Modified at {datetime.now().isoformat()}\n")

def commit_with_date(date, message):
    """Create a commit with a specific date"""
    env = os.environ.copy()
    env['GIT_AUTHOR_DATE'] = date
    env['GIT_COMMITTER_DATE'] = date
    
    subprocess.run(['git', 'add', '.'], check=True)
    subprocess.run(['git', 'commit', '-m', message], env=env, check=True)

def main():
    # Move to the repository directory
    os.chdir(REPOSITORY_PATH)
    
    # Generate list of all days in the date range
    all_days = []
    current_date = START_DATE
    while current_date <= END_DATE:
        all_days.append(current_date)
        current_date += timedelta(days=1)
    
    # Determine which days will have commits
    active_days = []
    for day in all_days:
        # Lower probability of committing on weekends
        if day.weekday() >= 5:  # 5 = Saturday, 6 = Sunday
            if random.randint(1, 100) <= WEEKEND_COMMIT_PROBABILITY:
                active_days.append(day)
        else:
            if random.randint(1, 100) <= ACTIVE_DAYS_PERCENT:
                active_days.append(day)
    
    print(f"Will create commits on {len(active_days)}/{len(all_days)} days")
    
    # Create commits for each active day
    for day in active_days:
        # Determine number of commits for this day
        num_commits = random.randint(COMMIT_MIN, COMMIT_MAX)
        
        # Create each commit
        for i in range(num_commits):
            # Choose a random hour during the workday
            hour = random.randint(DAILY_START_HOUR, DAILY_END_HOUR)
            minute = random.randint(0, 59)
            second = random.randint(0, 59)
            
            commit_date = day.replace(hour=hour, minute=minute, second=second)
            formatted_date = commit_date.strftime('%Y-%m-%d %H:%M:%S')
            
            # Choose a random file to modify
            file_to_modify = random.choice(FILE_PATHS)
            make_random_change(file_to_modify)
            
            # Choose a random commit message
            commit_message = random.choice(COMMIT_MESSAGES)
            if random.random() < 0.3:  # 30% chance to add more detail
                commit_message += f" for {os.path.basename(file_to_modify)}"
            
            # Create the commit
            print(f"Creating commit on {formatted_date}: {commit_message}")
            commit_with_date(formatted_date, commit_message)
            
            # Add a small delay to prevent issues
            time.sleep(0.1)
    
    print("Done! Created commit history for December 2024")

if __name__ == "__main__":
    main()
