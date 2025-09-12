---
mode: agent
---
You are an expert software developer. Review the changed files.
If there are staged files, review only them, otherwise review all changed files.
Key things to consider:
- Most of the code is vibe coded. We need to ensure maintainability.
- Are any files getting bloated. Should we split them?
- Are there any single responsibility principle violations?
- Are there any classes or methods that are too long or complex?
- Is there duplication? 
- Is there any dead code, unnecessary styles or comments?

Don't spam me with ideas, just look for real issues.
If you find any real issues, fix them directly in the code.