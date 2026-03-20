#!/bin/bash
# Test profile API (recent sessions) on hosted vs local backend
# Usage: ./scripts/test-profile-api.sh [WALLET_ADDRESS]
# Example: ./scripts/test-profile-api.sh 0x8EC9193Be33256812612E658C224A1F7C266B86C
# Requires: jq (brew install jq)

WALLET="${1:-0x8EC9193Be33256812612E658C224A1F7C266B86C}"
HOSTED="https://api-sushinads.cryptara.world"
LOCAL="http://localhost:8081"

echo "=== Profile API Test (wallet: $WALLET) ==="
echo ""

echo "--- Hosted backend ($HOSTED) ---"
curl -s "${HOSTED}/api/chef/profile?walletAddress=${WALLET}" | jq -r '
  if .error then "Error: \(.error)"
  else
    "Player: \(.player.name) | Energy: \(.player.energy)/\(.player.maxEnergy)",
    "Total sessions: \(.globalStats.totalSessionsPlayed)",
    "Recent sessions:",
    (.recentSessions // [] | map("  #\(.sessionId) | \(.score) PTS | \(.timestamp)") | .[]),
    (if .cached then "  (cached)" else "  (fresh)" end)
  end
'
echo ""

echo "--- Local backend ($LOCAL) ---"
curl -s "${LOCAL}/api/chef/profile?walletAddress=${WALLET}" | jq -r '
  if .error then "Error: \(.error)"
  else
    "Player: \(.player.name) | Energy: \(.player.energy)/\(.player.maxEnergy)",
    "Total sessions: \(.globalStats.totalSessionsPlayed)",
    "Recent sessions:",
    (.recentSessions // [] | map("  #\(.sessionId) | \(.score) PTS | \(.timestamp)") | .[]),
    (if .cached then "  (cached)" else "  (fresh)" end)
  end
'
echo ""

echo "=== Raw recentSessions (hosted) ==="
curl -s "${HOSTED}/api/chef/profile?walletAddress=${WALLET}" | jq '.recentSessions'
echo ""

echo "=== Raw recentSessions (local) ==="
curl -s "${LOCAL}/api/chef/profile?walletAddress=${WALLET}" | jq '.recentSessions'
