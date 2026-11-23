#!/usr/bin/env bash
set -euo pipefail

# This smoke test expects a fresh database populated with the Compose demo accounts.
: "${DEMO_NALEDI_PASSWORD:?Set the customer demo password}"
: "${DEMO_THABO_PASSWORD:?Set the second customer demo password}"
: "${DEMO_REVIEWER_PASSWORD:?Set the reviewer demo password}"

port="${1:-8443}"
[[ "$port" =~ ^[0-9]+$ ]]
base_url="https://localhost:$port"
cookie_dir="$(mktemp -d)"
customer="$cookie_dir/customer"
reviewer="$cookie_dir/reviewer"
other="$cookie_dir/other"
touch "$customer" "$reviewer" "$other"
trap 'rm -f -- "$customer" "$reviewer" "$other"; rmdir -- "$cookie_dir"' EXIT

request() {
  local jar="$1"
  shift
  curl -kfsS --max-time 15 --cookie "$jar" --cookie-jar "$jar" "$@"
}

sign_in() {
  local jar="$1" user="$2" password_variable="$3"
  jq -nc --arg user "$user" --arg variable "$password_variable" \
    '{userName:$user, password:env[$variable]}' |
    request "$jar" -H 'Content-Type: application/json' --data @- "$base_url/api/auth/login"
}

sign_out() {
  request "$1" -H 'Content-Type: application/json' --data '{}' "$base_url/api/auth/logout"
}

# Docker may publish the port before Kestrel is ready to negotiate TLS.
test "$(curl -kfsS --retry 20 --retry-all-errors --retry-delay 2 --max-time 5 "$base_url/health/ready")" = Healthy
test "$(curl -kfsS --max-time 5 "$base_url/health/live")" = Healthy
dashboard="$(curl -kfsS --retry 20 --retry-all-errors --retry-delay 2 --max-time 5 "$base_url/dashboard")"
grep -q '<app-root>' <<< "$dashboard"
test "$(curl -ksS -o /dev/null -w '%{http_code}' "$base_url/api/unknown")" = 404

printf '%s\n' 'Submitting a dispute as the customer'
sign_in "$customer" demo-naledi DEMO_NALEDI_PASSWORD
request "$customer" "$base_url/api/auth/session" | jq -e '.role == "Customer" and .displayName == "Naledi Mokoena"' >/dev/null
expired_id="$(request "$customer" "$base_url/api/transactions/page?search=Bookshop&filing=Closed" |
  jq -er '.items[] | select(.description == "Bookshop purchase" and .filingWindowExpired and .disputeDeadline != null) | .id')"
transaction_id="$(request "$customer" "$base_url/api/transactions/page?filing=Open&pageSize=1" | jq -er '.items[0].id')"
reason_code="$(request "$customer" "$base_url/api/disputes/reasons" | jq -er '.[0].code')"
printf '%s\n' 'Verifying the expired demo transaction is rejected'
expired_response="$(
  jq -nc --arg id "$expired_id" --arg reason "$reason_code" \
    '{transactionId:$id, reasonCode:$reason, details:"Expired transaction check"}' |
    curl -ksS --max-time 15 --cookie "$customer" -H 'Content-Type: application/json' \
      --data @- --write-out '\n%{http_code}' "$base_url/api/disputes"
)"
test "${expired_response##*$'\n'}" = 400
jq -e '.code == "filingWindowExpired"' <<< "${expired_response%$'\n'*}" >/dev/null
dispute_id="$(jq -nc --arg id "$transaction_id" --arg reason "$reason_code" \
  '{transactionId:$id, reasonCode:$reason, details:"Compose smoke test"}' |
  request "$customer" -H 'Content-Type: application/json' --data @- "$base_url/api/disputes" |
  jq -er 'select(.summary.status == "Submitted" and (.summary.referenceNumber | test("^DD-[0-9]{8}$"))) | .summary.id')"
request "$customer" "$base_url/api/disputes/page?pageSize=1&status=Submitted" |
  jq -e --arg id "$dispute_id" '.totalCount == 1 and .items[0].id == $id' >/dev/null
sign_out "$customer"

printf '%s\n' 'Recording a reviewer decision'
sign_in "$reviewer" demo-reviewer DEMO_REVIEWER_PASSWORD
request "$reviewer" "$base_url/api/auth/session" | jq -e '.role == "Reviewer"' >/dev/null
request "$reviewer" "$base_url/api/reviewer/disputes" |
  jq -e --arg id "$dispute_id" 'any(.[]; .id == $id and .status == "Submitted")' >/dev/null
request "$reviewer" -H 'Content-Type: application/json' --data '{}' \
  "$base_url/api/reviewer/disputes/$dispute_id/review"
request "$reviewer" -H 'Content-Type: application/json' \
  --data '{"status":"Approved","note":"Smoke test decision"}' \
  "$base_url/api/reviewer/disputes/$dispute_id/decision"
request "$reviewer" "$base_url/api/reviewer/disputes" |
  jq -e --arg id "$dispute_id" 'all(.[]; .id != $id)' >/dev/null
sign_out "$reviewer"

printf '%s\n' 'Verifying the timeline, unread notifications and persisted read state'
sign_in "$customer" demo-naledi DEMO_NALEDI_PASSWORD
request "$customer" "$base_url/api/disputes/$dispute_id" |
  jq -e '([.timeline[].status] == ["Submitted","Under review","Approved"]) and
    (.timeline[-1].note == "Smoke test decision")' >/dev/null
request "$customer" "$base_url/api/disputes?status=Approved" |
  jq -e --arg id "$dispute_id" 'any(.[]; .id == $id and .status == "Approved")' >/dev/null
request "$customer" "$base_url/api/dashboard" |
  jq -e '.disputesByStatus.Approved == 1 and .disputesByStatus.UnderReview == 0' >/dev/null
notification_id="$(request "$customer" "$base_url/api/dispute-notifications" |
  jq -er --arg id "$dispute_id" '[.[] | select(.disputeId == $id and .readAt == null)] |
    select((map(.status) | sort) == ["Approved","Under review"]) |
    .[] | select(.status == "Approved") | .id')"
request "$customer" -X PUT "$base_url/api/dispute-notifications/$notification_id/read"
sign_out "$customer"
sign_in "$customer" demo-naledi DEMO_NALEDI_PASSWORD
request "$customer" "$base_url/api/dispute-notifications" |
  jq -e --arg id "$dispute_id" --arg read "$notification_id" '
    [.[] | select(.disputeId == $id)] |
    length == 2 and
    any(.[]; .id == $read and .readAt != null) and
    any(.[]; .id != $read and .readAt == null)' >/dev/null

printf '%s\n' 'Verifying another customer cannot see the updates'
sign_in "$other" demo-thabo DEMO_THABO_PASSWORD
request "$other" "$base_url/api/dispute-notifications" |
  jq -e --arg id "$dispute_id" 'all(.[]; .disputeId != $id)' >/dev/null
test "$(curl -ksS --cookie "$other" -o /dev/null -w '%{http_code}' "$base_url/api/disputes/$dispute_id")" = 404
