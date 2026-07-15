import unittest

from src.models import EscalationReason, ProgressSnapshot, TaskSnapshot
from src.progress_detector import has_substantive_progress
from src.task_supervisor import evaluate_task


class TaskSupervisorTests(unittest.TestCase):
    def test_failed_state_escalates_immediately(self):
        decision = evaluate_task(TaskSnapshot(task_id="TASK-1", state="failed", source_agent="xingyuan-qa"))
        self.assertTrue(decision.escalate)
        self.assertEqual(decision.reason, EscalationReason.FAILED)

    def test_three_no_progress_checks_escalates_as_stalled(self):
        decision = evaluate_task(
            TaskSnapshot(
                task_id="TASK-2",
                state="running",
                source_agent="xingyuan-lead",
                consecutive_no_progress_checks=3,
            )
        )
        self.assertTrue(decision.escalate)
        self.assertEqual(decision.reason, EscalationReason.STALLED)

    def test_progress_detector_detects_tool_call_change(self):
        previous = ProgressSnapshot(tool_call_count=1)
        current = ProgressSnapshot(tool_call_count=2)
        self.assertTrue(has_substantive_progress(previous, current))

    def test_no_progress_when_snapshots_equal(self):
        snapshot = ProgressSnapshot(git_diff_hash="abc", changed_file_count=2)
        self.assertFalse(has_substantive_progress(snapshot, snapshot))


if __name__ == "__main__":
    unittest.main()

