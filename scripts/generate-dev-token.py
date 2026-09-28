#!/usr/bin/env python3
"""
Mint a signed development JWT for the NotificationMicroservice.

Why this script exists: the microservice is a resource server and never issues tokens.
In a real deployment the identity service signs them with its private key. This script
stands in for that identity service locally, so the demo is runnable without adding
token issuance to the microservice itself.

It reads the Jwt section from appsettings.json so the key can never drift from what the
microservice validates against.

Usage:
    python3 scripts/generate-dev-token.py
    python3 scripts/generate-dev-token.py --user-row-id 11111111-1111-1111-1111-111111111111
    python3 scripts/generate-dev-token.py --email demo@example.com --expires-minutes 120
    curl -s -H "Authorization: Bearer $(python3 scripts/generate-dev-token.py --quiet)" \
        http://localhost:5011/api/auth/me
"""

import argparse
import base64
import hashlib
import hmac
import json
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_USER_ROW_ID = "11111111-1111-1111-1111-111111111111"


def b64url(raw: bytes) -> str:
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode()


def load_jwt_settings() -> dict:
    settings_path = REPO_ROOT / "appsettings.json"
    if not settings_path.exists():
        sys.exit(f"appsettings.json not found at {settings_path}")
    with settings_path.open() as handle:
        return json.load(handle).get("Jwt", {})


def build_token(settings: dict, user_row_id: str, email: str | None, expires_minutes: int) -> str:
    key = settings.get("Key", "")
    if len(key.encode()) < 32:
        sys.exit("Jwt:Key in appsettings.json must be at least 32 bytes for HS256.")

    now = int(time.time())
    header = {"alg": "HS256", "typ": "JWT"}
    payload = {
        "iss": settings.get("Issuer"),
        "aud": settings.get("Audience"),
        "sub": user_row_id,
        "UserRowId": user_row_id,
        "jti": base64.urlsafe_b64encode(hashlib.sha256(str(now).encode()).digest())[:16]
        .decode()
        .rstrip("="),
        "iat": now,
        "nbf": now,
        "exp": now + (expires_minutes * 60),
    }
    if email:
        payload["email"] = email

    signing_input = (
        f"{b64url(json.dumps(header, separators=(',', ':')).encode())}."
        f"{b64url(json.dumps(payload, separators=(',', ':')).encode())}"
    )
    signature = hmac.new(key.encode(), signing_input.encode(), hashlib.sha256).digest()
    return f"{signing_input}.{b64url(signature)}"


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate a local development JWT.")
    parser.add_argument(
        "--user-row-id",
        default=DEFAULT_USER_ROW_ID,
        help=f"UserRowId claim (default: {DEFAULT_USER_ROW_ID})",
    )
    parser.add_argument("--email", default=None, help="Optional email claim")
    parser.add_argument(
        "--expires-minutes",
        type=int,
        default=None,
        help="Override Jwt:ExpiryMinutes from appsettings.json",
    )
    parser.add_argument("--quiet", action="store_true", help="Print only the token")
    args = parser.parse_args()

    settings = load_jwt_settings()
    expires_minutes = args.expires_minutes or settings.get("ExpiryMinutes", 60)
    token = build_token(settings, args.user_row_id, args.email, expires_minutes)

    if args.quiet:
        print(token)
        return

    print(f"Issuer    : {settings.get('Issuer')}")
    print(f"Audience  : {settings.get('Audience')}")
    print(f"UserRowId : {args.user_row_id}")
    print(f"Expires   : in {expires_minutes} minutes")
    print()
    print(token)
    print()
    print("Verify it:")
    print(f'  curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5011/api/auth/me')
    print("Register a device token:")
    print(
        "  curl -s -X POST http://localhost:5011/api/Notification/registerDeviceToken \\\n"
        '    -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" \\\n'
        f'    -d \'{{"deviceId":"demo-phone","deviceToken":"<FCM_TOKEN>","deviceModel":"Android","devicePlatform":"Android"}}\''
    )


if __name__ == "__main__":
    main()
