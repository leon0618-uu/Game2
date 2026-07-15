import unittest

from src.secret_filter import REDACTION, redact, redact_text


class SecretFilterTests(unittest.TestCase):
    def test_redacts_sensitive_key_values(self):
        fake_token = "abc12345" + "678901234567890"
        payload = {"token": fake_token, "safe": "hello"}
        self.assertEqual(redact(payload)["token"], REDACTION)
        self.assertEqual(redact(payload)["safe"], "hello")

    def test_redacts_inline_secrets_and_private_ids(self):
        fake_token = "abc12345" + "678901234567890"
        fake_open_id = "ou_" + "FAKEPRIVATEID123456"
        text = f"Authorization: Bearer {fake_token} user={fake_open_id}"
        redacted = redact_text(text)
        self.assertNotIn(fake_token, redacted)
        self.assertNotIn(fake_open_id, redacted)


if __name__ == "__main__":
    unittest.main()
