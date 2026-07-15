from __future__ import annotations

from .models import ProgressSnapshot


def has_substantive_progress(previous: ProgressSnapshot, current: ProgressSnapshot) -> bool:
    return any(
        [
            previous.git_diff_hash != current.git_diff_hash,
            previous.changed_file_count != current.changed_file_count,
            previous.log_size != current.log_size,
            previous.test_total != current.test_total,
            previous.test_passed != current.test_passed,
            previous.test_failed != current.test_failed,
            previous.tool_call_count != current.tool_call_count,
            previous.diagnostic_hash != current.diagnostic_hash,
            previous.subtask_state != current.subtask_state,
        ]
    )

