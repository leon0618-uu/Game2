import json
import os
import unittest

from src.openai_client import OpenAIClient


class OpenAIClientTests(unittest.TestCase):
    def test_request_body_redacts_package(self):
        fake_token = "abc12345" + "678901234567890"
        request = OpenAIClient(model="test-model").build_intervention_request({"task_id": "T1", "error": f"token={fake_token}"})
        encoded = json.dumps(request)
        self.assertNotIn(fake_token, encoded)
        self.assertIn("json_schema", encoded)

    def test_missing_api_key_fails_closed(self):
        old_value = os.environ.pop("OPENAI_API_KEY", None)
        try:
            result = OpenAIClient().request_intervention({"task_id": "T1"})
        finally:
            if old_value is not None:
                os.environ["OPENAI_API_KEY"] = old_value
        self.assertFalse(result.ok)
        self.assertIn("OPENAI_API_KEY", result.error)


if __name__ == "__main__":
    unittest.main()

