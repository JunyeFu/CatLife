# Blender MCP Setup

This project uses `ahujasid/blender-mcp`.

Architecture:

```text
Claude Code / MCP client
  -> uvx blender-mcp
  -> TCP 127.0.0.1:9876
  -> Blender addon
```

## Current Local Setup

The Claude Code project MCP server has been registered from this directory:

```powershell
claude mcp add blender uvx blender-mcp
```

The Blender addon has been downloaded here:

```text
tools/blender-mcp/blender_mcp_addon.py
```

## Blender-Side Install

1. Open Blender.
2. Go to `Edit > Preferences > Add-ons`.
3. Click `Install...`.
4. Select:

```text
D:\Agent\AIGC innovation\tools\blender-mcp\blender_mcp_addon.py
```

5. Enable the addon named `Interface: Blender MCP`.
6. In the 3D View, press `N` to open the sidebar.
7. Open the `BlenderMCP` tab.
8. Click `Connect to Claude`.

After that, port `127.0.0.1:9876` should be open and Claude/Codex-side tooling can call Blender MCP.

## Verify

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tools\blender-mcp\check-blender-mcp.ps1
```

Expected final line when Blender is connected:

```text
OK: Blender MCP addon is listening on 127.0.0.1:9876
```

If it says `NOT READY`, the client-side MCP is installed but Blender has not started the addon socket yet.

## Safety

`execute_code` runs arbitrary Python inside Blender with your user permissions. Save a `.blend` checkpoint before destructive scene edits or batch asset operations.
