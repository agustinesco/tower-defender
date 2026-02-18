#!/bin/bash
# Unity MCP mutex lock
# Prevents multiple Claude Code instances from issuing MCP commands simultaneously.
# Uses session_id from hook stdin to distinguish instances.
# Lock auto-expires after LOCK_TIMEOUT seconds of inactivity.

LOCK_FILE="$CLAUDE_PROJECT_DIR/.unity-mcp-lock"
LOCK_TIMEOUT=120

INPUT=$(cat)
SESSION_ID=$(echo "$INPUT" | python3 -c "import sys,json; print(json.load(sys.stdin).get('session_id',''))" 2>/dev/null)

if [ -z "$SESSION_ID" ]; then
    exit 0
fi

NOW=$(date +%s)

if [ -f "$LOCK_FILE" ]; then
    LOCK_SESSION=$(sed -n '1p' "$LOCK_FILE" 2>/dev/null)
    LOCK_TIME=$(sed -n '2p' "$LOCK_FILE" 2>/dev/null)
    LOCK_TIME=${LOCK_TIME:-0}
    AGE=$((NOW - LOCK_TIME))

    if [ "$LOCK_SESSION" = "$SESSION_ID" ]; then
        # Our lock — refresh timestamp
        printf '%s\n%s\n' "$SESSION_ID" "$NOW" > "$LOCK_FILE"
        exit 0
    elif [ "$AGE" -lt "$LOCK_TIMEOUT" ]; then
        # Another active session holds the lock
        echo "Unity MCP is locked by another Claude instance (${AGE}s ago). Retry when the other instance is idle or after ${LOCK_TIMEOUT}s timeout." >&2
        exit 2
    fi
    # Lock expired — fall through to acquire
fi

# Acquire lock
printf '%s\n%s\n' "$SESSION_ID" "$NOW" > "$LOCK_FILE"
exit 0
