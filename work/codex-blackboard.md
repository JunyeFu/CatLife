# Codex Blackboard

## Current Task Goal

Enable a lightweight, project-local Codex SAPIEN-Lite workflow for CatLife without changing global Codex configuration, installing hooks, writing memory, or overwriting project rules.

## Constraints

- Project-local files only.
- No global configuration changes.
- No hooks.
- No long-term memory writes.
- Do not overwrite an existing `AGENTS.md`; append only if it exists.
- Keep all changes reversible and auditable.
- Verify file creation and structure before closing.

## Known Evidence

- Current project root: `D:\Agent\AIGC innovation`.
- No project-local `AGENTS.md` was present before this workflow setup.
- No project-local `work/` directory was present before this workflow setup.
- Git working tree was clean before creating these workflow files.

## Risks

- Accidentally creating workflow files in the wrong workspace.
- Overwriting existing project rules in future runs.
- Treating generated or pasted research as authoritative without local validation.
- Committing large ignored artifacts if `.gitignore` is bypassed.

## Next Step Queue

1. Maintain this blackboard for complex multi-step tasks.
2. Record meaningful checks in `work/codex-verification-log.md`.
3. Use `work/codex-evaluation-harness.md` to track quality across real tasks.
4. Keep `AGENTS.md` concise and project-local.
5. Commit and push completed workflow changes after verification.
