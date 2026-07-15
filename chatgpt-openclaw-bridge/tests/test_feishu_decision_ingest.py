import unittest

from src.approval_request import build_approval_request, validate_approval_decision
from src.feishu_decision_ingest import (
    build_feishu_approval_decision,
    build_feishu_callback_response,
    extract_feishu_payload_fields,
    validate_feishu_approval_decision,
    verify_feishu_signature,
)


class FeishuDecisionIngestTests(unittest.TestCase):
    def build_request(self):
        return build_approval_request(
            item="Send Feishu summary",
            reason="A real Feishu send affects an external channel.",
            recommendation="Approve after dry-run preview.",
            risk="high",
            impact="Message is sent to the project channel.",
            rollback="Post correction and disable sender.",
            action_type="other",
            command=["python", "-m", "src.main", "feishu-send", "--execute"],
        )

    def test_extracts_nested_feishu_decision_fields(self):
        fields = extract_feishu_payload_fields(
            {
                "event": {
                    "source": "feishu",
                    "approval_request_id": "APPROVAL-1",
                    "action": "approved",
                    "user": {"name": "Leon"},
                    "comment": "ok",
                    "message_id": "om_123",
                }
            }
        )
        self.assertEqual(fields["decision"], "approve")
        self.assertEqual(fields["decided_by"], "Leon")
        self.assertEqual(fields["message_id"], "om_123")

    def test_extracts_real_card_action_value_fields(self):
        fields = extract_feishu_payload_fields(
            {
                "schema": "2.0",
                "header": {"event_type": "card.action.trigger"},
                "event": {
                    "operator": {"operator_id": {"open_id": "ou_operator"}},
                    "action": {
                        "value": {
                            "approval_request_id": "APPROVAL-1",
                            "decision": "approve",
                            "notes": "ship it",
                        }
                    },
                    "context": {"open_message_id": "om_card"},
                },
            }
        )
        self.assertEqual(fields["source"], "feishu")
        self.assertEqual(fields["request_id"], "APPROVAL-1")
        self.assertEqual(fields["decision"], "approve")
        self.assertEqual(fields["decided_by"], "ou_operator")
        self.assertEqual(fields["notes"], "ship it")
        self.assertEqual(fields["message_id"], "om_card")

    def test_verifies_feishu_signature(self):
        body = b'{"source":"feishu"}'
        timestamp = "1700000000"
        nonce = "nonce-1"
        key = "unit-test-key"
        import hashlib

        signature = hashlib.sha256(f"{timestamp}{nonce}{key}".encode("utf-8") + body).hexdigest()
        self.assertTrue(verify_feishu_signature(timestamp=timestamp, nonce=nonce, encrypt_key=key, body=body, signature=signature))
        self.assertFalse(verify_feishu_signature(timestamp=timestamp, nonce=nonce, encrypt_key="bad-key", body=body, signature=signature))

    def test_builds_url_verification_challenge_response(self):
        response = build_feishu_callback_response({"type": "url_verification", "challenge": "challenge-token", "token": "verify"}, verification_token="verify")
        self.assertEqual(response, {"valid": True, "challenge": "challenge-token", "decision_source": "feishu"})

    def test_builds_valid_approval_decision_from_payload(self):
        request = self.build_request()
        payload = {
            "source": "feishu",
            "request_id": request["request_id"],
            "decision": "approve",
            "operator": "Leon",
            "notes": "dry-run checked",
            "message_id": "om_123",
        }
        decision = build_feishu_approval_decision(request, payload)
        self.assertFalse(validate_feishu_approval_decision(request, payload, decision))
        self.assertFalse(validate_approval_decision(decision, request=request))
        self.assertEqual(decision["decision_source"], "feishu")

    def test_rejects_mismatched_request_id(self):
        request = self.build_request()
        payload = {"source": "feishu", "request_id": "APPROVAL-other", "decision": "approve", "operator": "Leon"}
        decision = build_feishu_approval_decision(request, payload)
        errors = validate_feishu_approval_decision(request, payload, decision)
        self.assertIn("feishu payload request_id must match approval request", errors)

    def test_rejects_non_feishu_source(self):
        request = self.build_request()
        payload = {"source": "local", "request_id": request["request_id"], "decision": "approve", "operator": "Leon"}
        decision = build_feishu_approval_decision(request, payload)
        errors = validate_feishu_approval_decision(request, payload, decision)
        self.assertIn("feishu payload source must identify Feishu", errors)

    def test_rejects_unknown_decision(self):
        request = self.build_request()
        payload = {"source": "feishu", "request_id": request["request_id"], "decision": "maybe", "operator": "Leon"}
        decision = build_feishu_approval_decision(request, payload)
        errors = validate_feishu_approval_decision(request, payload, decision)
        self.assertIn("feishu payload decision must be one of ['approve', 'reject', 'pause_and_inspect']", errors)


if __name__ == "__main__":
    unittest.main()
