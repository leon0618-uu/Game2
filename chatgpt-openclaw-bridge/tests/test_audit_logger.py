import json
import tempfile
import unittest
from pathlib import Path

from src.audit_logger import AuditLogger


class AuditLoggerTests(unittest.TestCase):
    def test_write_event_redacts_payload(self):
        with tempfile.TemporaryDirectory() as tmp:
            fake_token = "abc12345" + "678901234567890"
            path = AuditLogger(Path(tmp)).write_event("test", {"token": fake_token}, "REQ-1")
            data = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(data["payload"]["token"], "[REDACTED]")


if __name__ == "__main__":
    unittest.main()
