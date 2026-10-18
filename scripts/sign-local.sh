#!/usr/bin/env bash
# Re-sign a todo binary with the fixed identity so the keychain ACL
# (designated requirement pinned to the certificate) never breaks.
# Credentials: ~/.macos_app_sign_keys (0600) — identity/password/p12(base64).
set -euo pipefail
BIN="${1:?usage: sign-local.sh <path-to-binary>}"
KEYS="$HOME/.macos_app_sign_keys"
[ -f "$BIN" ] || { echo "binary not found: $BIN" >&2; exit 1; }
[ -f "$KEYS" ] || { echo "signing keys file missing: $KEYS" >&2; exit 1; }
# shellcheck disable=SC1090
source "$KEYS"

ensure_identity() {
  if ! security find-certificate -c "$SIGNING_IDENTITY" >/dev/null 2>&1; then
    local tmp; tmp=$(mktemp /tmp/signer.XXXX.p12)
    printf '%s' "$SIGNING_P12_BASE64" | base64 -d > "$tmp"
    security import "$tmp" -k "$HOME/Library/Keychains/login.keychain-db" \
      -P "$SIGNING_PASSWORD" -T /usr/bin/codesign
    rm -f "$tmp"
  fi
}

ensure_identity
codesign --force --sign "$SIGNING_IDENTITY" "$BIN"
codesign -d -r- "$BIN" 2>&1 | tail -1
