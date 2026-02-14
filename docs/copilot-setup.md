# GitHub Copilot Instructions Setup

This document explains the GitHub Copilot instructions configuration for the ElBruno.LocalEmbeddings repository.

## Overview

The repository uses GitHub Copilot's custom instructions feature to provide AI-powered coding assistance that understands the project's conventions, structure, and best practices.

## Files and Structure

### Primary Instructions

**Location:** `.github/copilot-instructions.md`

This file contains project-wide instructions that GitHub Copilot coding agent uses to understand:
- Project overview and tech stack
- Build and test workflows
- Naming conventions
- Code standards and patterns
- Project structure
- Security guidelines
- Boundaries and restrictions

### Squad Framework Integration

**Location:** `.ai-team-templates/copilot-instructions.md`

Additional instructions for the Squad AI team framework, providing guidance on:
- Team context and member roles
- Capability self-checks
- Branch naming conventions
- PR guidelines
- Decision tracking

**Custom Agent:** `.github/agents/squad.agent.md`

Defines a custom agent for Squad-specific workflows.

## What's Included

### 1. Project Context
- .NET 10.0 library for local text embeddings
- Uses ONNX Runtime and Microsoft.Extensions.AI
- Multi-targeting net8.0 and net10.0

### 2. Development Workflows
- Build command: `dotnet build`
- Test command: `dotnet test`
- Code style enforcement via `.editorconfig`
- Warnings treated as errors

### 3. Naming Conventions
- All projects, namespaces, and packages must start with `ElBruno.`
- Consistent naming across folders, projects, and namespaces

### 4. Code Standards
- File-scoped namespaces
- Nullable reference types enabled
- Explicit var usage rules
- xUnit for testing with Moq for mocking
- Table-driven tests for multiple scenarios

### 5. Boundaries and Restrictions
Clear guidance on files and directories that should not be modified:
- `.github/` configuration files
- `.devcontainer/` setup
- `.editorconfig` and `Directory.Build.props`
- License and Git configuration files
- Model files in user cache
- Squad framework directories

Protected patterns:
- Don't disable nullable reference type checks
- Don't suppress warnings globally
- Don't disable code style enforcement
- Avoid unsafe blocks without justification
- Prefer compile-time solutions over reflection

### 6. Code Examples
Concrete examples of preferred patterns:
- Service registration with dependency injection
- Async/await patterns with ConfigureAwait(false)
- Proper null handling with ArgumentNullException.ThrowIfNull
- Anti-patterns to avoid (var misuse, missing cancellation tokens, exception swallowing)

### 7. Security Guidelines
- Input validation requirements
- Path traversal prevention
- Resource limits
- Dependency security practices
- No hardcoded secrets
- Safe deserialization patterns

## Benefits

1. **Consistency**: Copilot generates code that follows project conventions
2. **Quality**: Reduces code review cycles by catching common issues early
3. **Onboarding**: New contributors (human or AI) understand project standards quickly
4. **Security**: Explicit security guidelines help prevent vulnerabilities
5. **Maintainability**: Code examples demonstrate preferred patterns

## Best Practices Followed

Based on [GitHub's official documentation](https://docs.github.com/en/copilot/how-tos/configure-custom-instructions/add-repository-instructions):

✅ Clear, actionable guidance with specific examples  
✅ Explicit boundaries and restrictions  
✅ Code snippets showing preferred patterns  
✅ Security considerations documented  
✅ Project-specific context that can't be inferred from code alone  
✅ Integration with Squad framework for team workflows  

## Maintenance

### When to Update

Update `.github/copilot-instructions.md` when:
- Adding new coding conventions or standards
- Changing build/test procedures
- Adding new project areas or structures
- Discovering security best practices specific to the codebase
- Team adopts new tools or frameworks

### How to Update

1. Edit `.github/copilot-instructions.md` directly
2. Keep changes focused and actionable
3. Add concrete examples for new patterns
4. Test that Copilot follows the updated instructions
5. Update this documentation if major changes are made

## References

- [GitHub Copilot Custom Instructions Documentation](https://docs.github.com/en/copilot/how-tos/configure-custom-instructions/add-repository-instructions)
- [Best Practices for Custom Instructions](https://design.dev/guides/copilot-instructions/)
- [Writing Great Agent Instructions](https://github.blog/ai-and-ml/github-copilot/how-to-write-a-great-agents-md-lessons-from-over-2500-repositories/)
- Project Contributing Guide: `docs/contributing.md`
- Squad Workflows: `docs/squad-workflows.md`
