# Project Instructions

## Unity MCP Connection

On session start, connect to the Unity MCP server at port 8081 using `npx mcpforunity@latest 8081`.

## Unity MCP Mutex Lock

A PreToolUse hook at `.claude/hooks/unity-mcp-lock.sh` prevents multiple Claude sessions from issuing MCP commands to Unity simultaneously. It uses a file lock (`.unity-mcp-lock`) with a 120-second timeout. This is already configured in `.claude/settings.json` and runs automatically before any `mcp__UnityMCP__*` tool call. If you see a "Unity MCP is locked by another Claude instance" error, wait and retry.

## Unity Safety

Always ask before executing operations that may freeze Unity (e.g., running editor menu items, executing migration scripts, heavy prefab operations, domain reloads with complex scripts). Explain what the operation will do and wait for confirmation.
