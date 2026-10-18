#!/usr/bin/env bash
# Re-sign a locally published todo binary with the fixed identity so the
# keychain ACL (designated requirement pinned to the cert) never breaks.
# Assets: ~/.todo-cli/signing/{ms-todo-cli-signer.p12, ms-todo-cli-signer.password}
set -euo pipefail
BIN="${1:?usage: sign-local.sh <path-to-todo-binary>}"
DIR="$HOME/.todo-cli/signing"
[ -f "$BIN" ] || { echo "binary not found: $BIN" >&2; exit 1; }
[ -f "$DIR/ms-todo-cli-signer.p12" ] || { echo "signing assets missing in $DIR" >&2; exit 1; }
PW="$(cat "$DIR/ms-todo-cli-signer.password")"
codesign --force --sign "ms-todo-cli-signing" "$BIN"
codesign -d -r- "$BIN" 2>&1 | tail -1
