from __future__ import annotations

from pathlib import Path


def is_path_within_workspace(workspace: Path, target: Path) -> bool:
    workspace_resolved = workspace.resolve()
    target_resolved = target.resolve()
    try:
        target_resolved.relative_to(workspace_resolved)
        return True
    except ValueError:
        return False

