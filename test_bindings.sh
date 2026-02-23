#!/bin/bash
set -uo pipefail

SOLUTION_DIR="dotnet-tests"
OUTPUT=$(dotnet test -l "console;verbosity=normal" $SOLUTION_DIR 2>&1) || true

echo "$OUTPUT"

PASSED=$(echo "$OUTPUT" | grep -c "^  Passed " || true)
FAILED=$(echo "$OUTPUT" | grep -c "^  Failed " || true)

echo ""
echo "=== Test Summary ==="
echo "Passed: $PASSED"
echo "Failed: $FAILED"

if [ "$FAILED" -gt 0 ]; then
    echo "FAIL: $FAILED test(s) failed"
    exit 1
fi

if [ "$PASSED" -eq 0 ]; then
    echo "FAIL: No tests passed (likely build or discovery failure)"
    exit 1
fi

echo "OK: All $PASSED tests passed"
exit 0
