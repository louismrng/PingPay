#!/bin/bash
set -e

BASE_URL="${BASE_URL:-http://localhost:5001}"
PHONE="${TEST_PHONE:-+1234567890}"
RECIPIENT_PHONE="${RECIPIENT_PHONE:-+0987654321}"
TOKEN="${TOKEN:-}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_header() {
    echo ""
    echo -e "${GREEN}=== $1 ===${NC}"
    echo ""
}

print_test() {
    echo -e "${YELLOW}> $1${NC}"
}

print_error() {
    echo -e "${RED}$1${NC}"
}

# Pretty print function that handles both JSON and plain text
pretty_print() {
    local input
    input=$(cat)
    if command -v jq &> /dev/null; then
        # Try to parse as JSON, fall back to plain output
        echo "$input" | jq . 2>/dev/null || echo "$input"
    else
        echo "$input"
    fi
}

print_header "PingPay API Test Suite"
echo "Base URL: $BASE_URL"
echo "Test Phone: $PHONE"
echo ""

# =============================================================================
# 1. Health Checks (No Auth Required)
# =============================================================================
print_header "1. Health Checks"

print_test "System health check"
curl -s "$BASE_URL/health" | pretty_print
echo ""

print_test "WhatsApp webhook health"
curl -s "$BASE_URL/api/whatsapp/health" | pretty_print
echo ""

# =============================================================================
# 2. Authentication Flow
# =============================================================================
print_header "2. Authentication Flow"

print_test "Request OTP (expecting error without real Twilio setup)"
curl -s -X POST "$BASE_URL/api/auth/request-otp" \
  -H "Content-Type: application/json" \
  -d "{\"phoneNumber\": \"$PHONE\"}" | pretty_print
echo ""

# Note: To complete auth flow, you would need real Twilio credentials
# print_test "Verify OTP (replace code with actual OTP received)"
# curl -s -X POST "$BASE_URL/api/auth/verify-otp" \
#   -H "Content-Type: application/json" \
#   -d "{\"phoneNumber\": \"$PHONE\", \"code\": \"123456\"}" | $JQ

# =============================================================================
# 3. Error Case Testing - Unauthorized Access
# =============================================================================
print_header "3. Error Case Testing - Unauthorized Access"

print_test "Balance without token (expecting 401)"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/wallet/balance")
echo "HTTP Status: $HTTP_CODE"
if [ "$HTTP_CODE" = "401" ]; then
    echo -e "${GREEN}PASS: Correctly returned 401 Unauthorized${NC}"
else
    print_error "FAIL: Expected 401, got $HTTP_CODE"
fi
echo ""

print_test "Balance with invalid token (expecting 401)"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/api/wallet/balance" \
  -H "Authorization: Bearer invalid_token")
echo "HTTP Status: $HTTP_CODE"
if [ "$HTTP_CODE" = "401" ]; then
    echo -e "${GREEN}PASS: Correctly returned 401 Unauthorized${NC}"
else
    print_error "FAIL: Expected 401, got $HTTP_CODE"
fi
echo ""

# =============================================================================
# 4. Error Case Testing - Validation
# =============================================================================
print_header "4. Error Case Testing - Validation"

print_test "Invalid phone number format (expecting 400)"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/auth/request-otp" \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber": "invalid"}')
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "HTTP Status: $HTTP_CODE"
echo "$BODY" | pretty_print
echo ""

# =============================================================================
# 5. WhatsApp Webhook Tests
# =============================================================================
print_header "5. WhatsApp Webhook Tests"

print_test "Help command"
curl -s -X POST "$BASE_URL/api/whatsapp/webhook" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "From=whatsapp:$PHONE&Body=help&MessageSid=TEST$(date +%s)001"
echo ""
echo ""

print_test "Register command"
curl -s -X POST "$BASE_URL/api/whatsapp/webhook" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "From=whatsapp:$PHONE&Body=register&MessageSid=TEST$(date +%s)002"
echo ""
echo ""

print_test "Balance command"
curl -s -X POST "$BASE_URL/api/whatsapp/webhook" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "From=whatsapp:$PHONE&Body=balance&MessageSid=TEST$(date +%s)003"
echo ""
echo ""

print_test "Send command"
curl -s -X POST "$BASE_URL/api/whatsapp/webhook" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "From=whatsapp:$PHONE&Body=send%20%2410%20to%20$RECIPIENT_PHONE&MessageSid=TEST$(date +%s)004"
echo ""
echo ""

print_test "Status callback"
curl -s -X POST "$BASE_URL/api/whatsapp/status" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "MessageSid=TEST123&MessageStatus=delivered&To=whatsapp:$PHONE"
echo ""
echo ""

# =============================================================================
# 6. Authenticated Endpoint Tests (requires valid TOKEN)
# =============================================================================
if [ -n "$TOKEN" ]; then
    print_header "6. Authenticated Endpoint Tests"

    print_test "Get wallet balance (cached)"
    curl -s "$BASE_URL/api/wallet/balance" \
      -H "Authorization: Bearer $TOKEN" | pretty_print
    echo ""

    print_test "Get wallet balance (force refresh)"
    curl -s "$BASE_URL/api/wallet/balance?refresh=true" \
      -H "Authorization: Bearer $TOKEN" | pretty_print
    echo ""

    print_test "Get payment history"
    curl -s "$BASE_URL/api/payments/history" \
      -H "Authorization: Bearer $TOKEN" | pretty_print
    echo ""

    print_test "Get payment history with pagination"
    curl -s "$BASE_URL/api/payments/history?limit=10&offset=0" \
      -H "Authorization: Bearer $TOKEN" | pretty_print
    echo ""

    # Uncomment to test actual payments (use with caution!)
    # print_test "Send payment"
    # curl -s -X POST "$BASE_URL/api/payments/send" \
    #   -H "Authorization: Bearer $TOKEN" \
    #   -H "Content-Type: application/json" \
    #   -d "{
    #     \"recipientPhone\": \"$RECIPIENT_PHONE\",
    #     \"amount\": 1.00,
    #     \"tokenType\": 0,
    #     \"idempotencyKey\": \"test-$(date +%s)\"
    #   }" | $JQ

    print_test "Missing required fields (expecting 400)"
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE_URL/api/payments/send" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d '{}')
    echo "HTTP Status: $HTTP_CODE"
    echo ""

    print_test "Amount exceeds limit (expecting 400)"
    curl -s -X POST "$BASE_URL/api/payments/send" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{
        \"recipientPhone\": \"$RECIPIENT_PHONE\",
        \"amount\": 99999.00,
        \"tokenType\": 0,
        \"idempotencyKey\": \"test-limit-$(date +%s)\"
      }" | pretty_print
    echo ""
else
    print_header "6. Authenticated Endpoint Tests (SKIPPED)"
    echo "Set TOKEN environment variable to run authenticated tests:"
    echo "  TOKEN=\"your_jwt_token\" ./scripts/test-api.sh"
    echo ""
fi

# =============================================================================
print_header "Tests Complete"
echo "For authenticated endpoint tests, set the TOKEN environment variable:"
echo "  TOKEN=\"your_jwt_token\" ./scripts/test-api.sh"
echo ""
echo "Environment variables:"
echo "  BASE_URL     - API base URL (default: http://localhost:5001)"
echo "  TEST_PHONE   - Phone number for testing (default: +1234567890)"
echo "  RECIPIENT_PHONE - Recipient phone for payments (default: +0987654321)"
echo "  TOKEN        - JWT token for authenticated requests"
