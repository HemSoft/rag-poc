# GitHub Copilot Instructions for RAG POC Project

## Script Creation Policy
- **DO NOT** create any scripts (PowerShell, batch, shell, etc.) unless explicitly requested by the user
- Only create scripts when the user specifically asks for them
- Focus on code changes and direct implementation rather than automation scripts

## PowerShell Command Syntax
- When providing multiple PowerShell commands on a single line, use semicolon (`;`) as the separator
- **Correct**: `cd c:\path\to\directory ; dotnet build ; dotnet run`
- **Incorrect**: `cd c:\path\to\directory && dotnet build && dotnet run`
- The `&&` operator is for bash/cmd, not PowerShell

## General Guidelines
- Provide direct solutions and code changes
- Avoid creating helper scripts unless specifically requested
- Focus on the core functionality and implementation
- Use proper PowerShell syntax when providing command examples
- ALWAYS ask clarifying questions if the request is ambiguous or could lead to misunderstandings.