import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from src.supervisor import SupervisorOptions, SupervisorPaths, run_supervisor_scan


class SupervisorTests(unittest.TestCase):
    def test_run_supervisor_scan_writes_local_artifacts(self):
        fake_tasks = {"tasks": [{"taskId": "T1", "status": "failed", "agentId": "xingyuan-qa", "error": "compile failed"}]}
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp)
            paths = SupervisorPaths(base / "incidents", base / "interventions", base / "outbox", base / "audit")
            with patch("src.supervisor.OpenClawClient") as client_type:
                client_type.return_value.parsed_tasks_list.return_value = fake_tasks
                result = run_supervisor_scan(Path.cwd(), paths, SupervisorOptions(limit=1))
            self.assertEqual(result["count"], 1)
            self.assertTrue(list((base / "incidents").glob("*.json")))
            self.assertTrue(list((base / "interventions").glob("*.json")))
            self.assertTrue(list((base / "outbox").glob("*.json")))


if __name__ == "__main__":
    unittest.main()

