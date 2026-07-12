---
name: git-worktree-guard
description: Verify the correct Xingyuan agent worktree and branch before writes, commits, or Git operations.
---

# Git worktree guard

Run before any write operation.

Expected workspaces:

- Lead: `D:\UntiyProject\XingyuanCovenant`
- Architect: `D:\AI-Worktrees\Xingyuan\architect`
- Gameplay: `D:\AI-Worktrees\Xingyuan\gameplay`
- UI/Tools: `D:\AI-Worktrees\Xingyuan\ui-tools`
- QA: `D:\AI-Worktrees\Xingyuan\qa`

Run:

```powershell
Get-Location
git rev-parse --show-toplevel
git branch --show-current
git status --short
git worktree list
```

Stop when the workspace does not match the agent, the branch is `main` for a write task, unrelated changes exist, another worktree owns the branch, or files are outside scope. Never use force push, hard reset, or destructive cleanup to bypass a failure.
