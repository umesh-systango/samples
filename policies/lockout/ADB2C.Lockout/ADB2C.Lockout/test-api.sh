#!/bin/bash

# Azure B2C Lockout API Test Script
# This script demonstrates the lockout functionality

API_BASE_URL="http://localhost:5000"
TEST_USER="test@example.com"

echo "=== Azure B2C Lockout API Test ==="
echo "API Base URL: $API_BASE_URL"
echo "Test User: $TEST_USER"
echo ""

# Function to make API calls and display results
make_request() {
    local method=$1
    local endpoint=$2
    local data=$3
    
    echo "Making $method request to $endpoint"
    if [ -n "$data" ]; then
        echo "Data: $data"
    fi
    
    if [ -n "$data" ]; then
        response=$(curl -s -w "\n%{http_code}" -X $method "$API_BASE_URL$endpoint" \
            -H "Content-Type: application/json" \
            -d "$data")
    else
        response=$(curl -s -w "\n%{http_code}" -X $method "$API_BASE_URL$endpoint")
    fi
    
    # Split response and status code
    http_code=$(echo "$response" | tail -n1)
    body=$(echo "$response" | head -n -1)
    
    echo "Status Code: $http_code"
    echo "Response: $body"
    echo "---"
}

# Test 1: Health Check
echo "1. Testing Health Check"
make_request "GET" "/api/monitoring/health"
echo ""

# Test 2: Initial Statistics
echo "2. Getting Initial Statistics"
make_request "GET" "/api/monitoring/stats"
echo ""

# Test 3: Simulate Failed Login Attempts
echo "3. Simulating Failed Login Attempts (should lock after 5 attempts)"
echo ""

for i in {1..6}; do
    echo "Attempt $i of 6:"
    make_request "POST" "/api/identity/signin" "{\"signInName\": \"$TEST_USER\"}"
    echo ""
    
    if [ "$i" -eq 5 ]; then
        echo "Account should now be locked after 5 failed attempts"
        echo "Waiting 2 seconds before next attempt..."
        sleep 2
    fi
done

# Test 4: Check Account Status
echo "4. Checking Account Status"
make_request "GET" "/api/identity/status/$TEST_USER"
echo ""

# Test 5: Try to login while locked
echo "5. Attempting to login while account is locked"
make_request "POST" "/api/identity/signin" "{\"signInName\": \"$TEST_USER\"}"
echo ""

# Test 6: Wait for lockout to expire (simulate 60 seconds)
echo "6. Waiting for lockout to expire (simulating 60 seconds)..."
echo "Note: In real scenario, this would be 60 seconds. For testing, we'll wait 5 seconds."
sleep 5

# Test 7: Try to login after lockout expires
echo "7. Attempting to login after lockout expires"
make_request "POST" "/api/identity/signin" "{\"signInName\": \"$TEST_USER\"}"
echo ""

# Test 8: Successful login (with objectId)
echo "8. Simulating Successful Login"
make_request "POST" "/api/identity/signin" "{\"signInName\": \"$TEST_USER\", \"objectId\": \"12345\"}"
echo ""

# Test 9: Check final account status
echo "9. Checking Final Account Status"
make_request "GET" "/api/identity/status/$TEST_USER"
echo ""

# Test 10: Final Statistics
echo "10. Getting Final Statistics"
make_request "GET" "/api/monitoring/stats"
echo ""

# Test 11: List all accounts
echo "11. Listing All Accounts"
make_request "GET" "/api/monitoring/accounts"
echo ""

echo "=== Test Complete ==="
echo ""
echo "Summary:"
echo "- The API should lock accounts after 5 failed attempts"
echo "- Accounts should be locked for 60 seconds (1 minute)"
echo "- Successful login should reset the counter"
echo "- All activities are logged and can be monitored" 