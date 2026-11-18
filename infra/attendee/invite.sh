#!/usr/bin/env bash
#
# Invite an attendee as a B2B guest and grant them access to exactly one
# fallback slot's resource group -- nothing else in the subscription.
#
# Run this only for attendees who cannot provision their own Azure resources.
# Create the slot first:   ./deploy.sh --slot 3 --mcp-url https://.../mcp
#
# Findings this script is built on: docs/spikes/foundry-agent-attendee-access.md
#
# This is a bash script; `sh invite.sh` (dash on Ubuntu) cannot parse it. Hand over to bash.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

EMAIL=""
SLOT=""
DISPLAY_NAME=""
DO_REVOKE="false"
PURGE_GUEST="false"
ASSUME_YES="false"

usage() {
  cat <<'EOF'
Invite an attendee to a fallback slot as a B2B guest.

USAGE
  ./invite.sh --slot <n> --email <address> [--name "Display Name"]
  ./invite.sh --slot <n> --email <address> --revoke [--purge-guest]

REQUIRED
  --slot <n>        The slot to grant access to. Must already exist --
                    create it with: ./deploy.sh --slot <n> --mcp-url ...
  --email <addr>    The attendee's email address.

OPTIONS
  --name "..."      Display name for the guest. Default: the email address.
  --revoke          Remove the role assignment for this attendee on this slot.
  --purge-guest     With --revoke: also delete the guest from the directory.
                    Off by default -- leaving a role-less guest costs nothing
                    and avoids re-doing invitation redemption next time.
  -y, --yes         Do not prompt for confirmation.
  -h, --help        Show this help.

WHAT THE ATTENDEE GETS
  Two role assignments, both scoped to that slot's resource group and nothing
  wider:
    - "Foundry User"         browse the project and chat with the agent.
    - "Log Analytics Reader" read the traces the agent writes.
  No subscription-scoped role is needed -- Foundry User already carries the
  control-plane reads. Log Analytics Reader is separate because Foundry User
  covers the Cognitive Services data plane only, so without it the portal's
  Traces tab returns 403 for a guest.

BEFORE THE WORKSHOP
  Attendees MUST redeem the invitation in advance. An unredeemed guest cannot
  sign in and the error message does not explain why. Send them:

      https://ai.azure.com/?tid=<your-tenant-id>

  (This script prints the exact link for you.)
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --slot)        SLOT="${2:?--slot needs a value}"; shift 2 ;;
    --email)       EMAIL="${2:?--email needs a value}"; shift 2 ;;
    --name)        DISPLAY_NAME="${2:?--name needs a value}"; shift 2 ;;
    --revoke)      DO_REVOKE="true"; shift ;;
    --purge-guest) PURGE_GUEST="true"; shift ;;
    -y|--yes)      ASSUME_YES="true"; shift ;;
    -h|--help)     usage; exit 0 ;;
    *) die "Unknown argument: $1" "Run './invite.sh --help' for usage." ;;
  esac
done

[[ -n "$SLOT" ]]  || die "--slot is required." "Run './invite.sh --help' for usage."
[[ -n "$EMAIL" ]] || die "--email is required." "Run './invite.sh --help' for usage."
[[ "$SLOT" =~ ^[0-9]+$ ]] || die "--slot must be a number (got '$SLOT')."
SLOT="$(printf '%02d' "$((10#$SLOT))")"

[[ "$EMAIL" == *@*.* ]] || die "'$EMAIL' does not look like an email address."
case "$EMAIL" in
  *+*) die "Microsoft Entra does not support '+' in email addresses." \
         "Plus-addressing causes delivery failures for B2B invitations." \
         "Ask the attendee for an address without a '+'." ;;
esac

[[ -n "$DISPLAY_NAME" ]] || DISPLAY_NAME="$EMAIL"

require_tools az jq
require_login

SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
SUBSCRIPTION_NAME="$(az account show --query name -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"

RG="rg-mcpws-slot${SLOT}"
RG_SCOPE="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RG}"
PORTAL_LINK="https://ai.azure.com/?tid=${TENANT_ID}"

# Roles the attendee needs on their slot, as "guid|label" pairs (bash 3.2 has no
# associative arrays). Granting and revoking both iterate this list, so the two
# paths cannot drift apart -- a revoke that forgets a role leaves access behind.
ATTENDEE_ROLES=(
  "${ROLE_FOUNDRY_USER}|Foundry User"
  "${ROLE_LOG_ANALYTICS_READER}|Log Analytics Reader"
)

if [[ "$(az group exists --name "$RG")" != "true" ]]; then
  die "Slot ${SLOT} does not exist (no resource group '${RG}')." \
    "Create it first:" \
    "" \
    "    ./deploy.sh --slot ${SLOT} --mcp-url https://<your-tunnel>/mcp"
fi

confirm() {
  [[ "$ASSUME_YES" == "true" ]] && return 0
  local reply
  printf '\n%s%s%s [y/N] ' "$C_BOLD" "$1" "$C_RESET" >&2
  read -r reply </dev/tty || reply=""
  [[ "$reply" == "y" || "$reply" == "Y" ]]
}

# ----------------------------------------------------------------------------
# Find an existing guest
#
# A B2B guest's UPN is mangled (alice_gmail.com#EXT#@tenant.onmicrosoft.com), so
# looking them up by email as a UPN fails. 'mail' is often unset on invited
# guests. 'otherMails' reliably holds the original address and is filterable
# without ConsistencyLevel -- which matters, because 'az ad user list' never
# sends that header, making every "advanced" filter unusable here.
# ----------------------------------------------------------------------------

# Restricted to userType eq 'Guest' deliberately. Without it this matches ANY
# directory user carrying that address in otherMails -- including a Member --
# and --purge-guest would then delete a real member account.
#
# Sets GUEST_OID. Returns non-zero if the lookup itself failed, so that a Graph
# outage or permission error is never mistaken for "no such guest".
find_guest_oid() {
  local out
  if ! out="$(az ad user list \
      --filter "userType eq 'Guest' and otherMails/any(m:m eq '${EMAIL}')" \
      --query "[0].id" -o tsv 2>&1)"; then
    printf '%s' "$out" >&2
    return 1
  fi
  GUEST_OID="$(printf '%s' "$out" | tr -d '[:space:]')"
  return 0
}

step "Looking up ${EMAIL}"
GUEST_OID=""
find_guest_oid || die "Could not query the directory for ${EMAIL}." \
  "The error above is from Microsoft Graph. Until this query succeeds we" \
  "cannot tell 'no such guest' apart from 'lookup failed', and acting on" \
  "that difference is how access gets left behind." \
  "" \
  "Most likely you lack directory read permission, or Graph is unavailable."

# ----------------------------------------------------------------------------
# Revoke
# ----------------------------------------------------------------------------

if [[ "$DO_REVOKE" == "true" ]]; then
  [[ -n "$GUEST_OID" ]] || { ok "No such guest in the directory; nothing to revoke."; exit 0; }
  ok "found guest ${GUEST_OID}"

  log ""
  log "About to revoke access for ${EMAIL} on slot ${SLOT} (${RG})."
  [[ "$PURGE_GUEST" == "true" ]] && log "The guest account will ALSO be deleted from the directory."
  confirm "Proceed?" || { log "Aborted."; exit 0; }

  # Order matters: remove the assignment before the principal. A role assignment
  # whose principal is gone shows as "Identity not found" and keeps granting
  # access for as long as an issued token remains valid.
  #
  # Every failure below is fatal. Reporting "Revoked." while an assignment
  # survives is the worst outcome this script can produce, so a failed lookup
  # must not degrade into an empty list and a failed delete must not be
  # swallowed by being the left-hand side of '&&' (set -e does not fire there).
  step "Removing role assignments"
  for entry in "${ATTENDEE_ROLES[@]}"; do
    role="${entry%%|*}"
    label="${entry##*|}"

    if ! ids="$(az role assignment list \
        --assignee-object-id "$GUEST_OID" --role "$role" --scope "$RG_SCOPE" \
        --fill-principal-name false --fill-role-definition-name false \
        --query "[].id" -o tsv 2>&1)"; then
      die "Could not list ${label} assignments on ${RG}." \
        "Service said: ${ids}" \
        "Access has NOT been revoked. Fix this and re-run before assuming it has."
    fi

    if [[ -z "${ids//[[:space:]]/}" ]]; then
      skip "no ${label} assignment on ${RG}"
      continue
    fi

    while IFS= read -r id; do
      [[ -z "$id" ]] && continue
      if ! az role assignment delete --ids "$id" -o none 2>/dev/null; then
        die "Failed to delete the ${label} assignment ${id}." \
          "Access has NOT been revoked. You need Owner or User Access" \
          "Administrator on ${RG}."
      fi
      ok "removed ${label}"
    done <<<"$ids"
  done

  if [[ "$PURGE_GUEST" == "true" ]]; then
    step "Deleting guest account"
    # Only reached once every assignment is gone, so a failure here cannot
    # leave an orphaned assignment pointing at a deleted principal.
    if ! az ad user delete --id "$GUEST_OID" -o none 2>/dev/null; then
      die "Removed the role assignment, but could not delete the guest account." \
        "Access is revoked, so this is not urgent -- but the account remains" \
        "in the directory. You likely lack directory write permission."
    fi
    ok "deleted ${EMAIL} from the directory"
  else
    detail "guest account left in place (use --purge-guest to remove it)"
  fi

  log ""
  ok "Revoked."
  exit 0
fi

# ----------------------------------------------------------------------------
# Invite
# ----------------------------------------------------------------------------

if [[ -n "$GUEST_OID" ]]; then
  skip "already a guest in this directory (${GUEST_OID})"
  REDEEM_URL=""
else
  step "Inviting ${EMAIL} as a guest"
  # Check-first rather than blind POST: what a plain re-POST does for an
  # already-redeemed guest is not documented, so we never find out.
  INVITE_BODY="$(jq -nc \
    --arg email "$EMAIL" \
    --arg name "$DISPLAY_NAME" \
    --arg redirect "$PORTAL_LINK" \
    '{
       invitedUserEmailAddress: $email,
       invitedUserDisplayName: $name,
       inviteRedirectUrl: $redirect,
       sendInvitationMessage: true,
       invitedUserMessageInfo: {
         messageLanguage: "en-US",
         customizedMessageBody: "You have been given access to an Azure AI Foundry project for the MCP workshop. Please accept this invitation BEFORE the session starts -- you will not be able to sign in on the day otherwise."
       }
     }')"

  if ! INVITE_RESULT="$(az rest --method POST \
      --url "https://graph.microsoft.com/v1.0/invitations" \
      --body "$INVITE_BODY" -o json 2>/tmp/invite_err.$$)"; then
    err="$(cat /tmp/invite_err.$$ 2>/dev/null || true)"; rm -f /tmp/invite_err.$$
    case "$err" in
      *Authorization_RequestDenied*|*Insufficient*privileges*)
        die "Your account is not allowed to invite guests." \
          "Service said: ${err}" \
          "" \
          "You need the 'Guest Inviter', 'Directory Writers' or 'User" \
          "Administrator' role -- or the tenant's External collaboration" \
          "settings have been narrowed to admins only." ;;
      *domain*|*blocked*|*denied*)
        die "The tenant's allow/deny list blocks this email domain." \
          "Service said: ${err}" ;;
      *) die "Could not send the invitation." "Service said: ${err}" ;;
    esac
  fi
  rm -f /tmp/invite_err.$$

  GUEST_OID="$(jq -r '.invitedUser.id // empty' <<<"$INVITE_RESULT")"
  REDEEM_URL="$(jq -r '.inviteRedeemUrl // empty' <<<"$INVITE_RESULT")"
  [[ -n "$GUEST_OID" ]] || die "The invitation succeeded but returned no user id." \
    "Response: ${INVITE_RESULT}"
  ok "invited (object id ${GUEST_OID})"
fi

# ----------------------------------------------------------------------------
# Grant access to the slot
# ----------------------------------------------------------------------------

step "Granting access to slot ${SLOT}"

for entry in "${ATTENDEE_ROLES[@]}"; do
  role="${entry%%|*}"
  label="${entry##*|}"

  existing="$(az role assignment list \
    --assignee-object-id "$GUEST_OID" --role "$role" --scope "$RG_SCOPE" \
    --fill-principal-name false --fill-role-definition-name false \
    --query "[].id" -o tsv 2>/dev/null || true)"

  if [[ -n "$existing" ]]; then
    skip "${label} on ${RG}"
  else
    # --assignee-object-id avoids a Graph lookup of the mangled #EXT# UPN, and
    # --assignee-principal-type avoids the documented PrincipalNotFound race when
    # assigning immediately after creating the principal.
    az role assignment create \
      --assignee-object-id "$GUEST_OID" \
      --assignee-principal-type User \
      --role "$role" \
      --scope "$RG_SCOPE" -o none \
      || die "Could not create the ${label} role assignment." \
           "You need Owner or User Access Administrator on ${RG}."
    ok "${label} on ${RG}"
  fi
done

# ----------------------------------------------------------------------------
# Done
# ----------------------------------------------------------------------------

step "Done"
log ""
log "  Attendee      ${EMAIL}"
log "  Object id     ${GUEST_OID}"
log "  Slot          ${SLOT}  (${RG})"
log "  Roles         Foundry User + Log Analytics Reader,"
log "                scoped to that resource group only"
log "  Subscription  ${SUBSCRIPTION_NAME}"
log ""
log "  Send them this link:"
log "    ${PORTAL_LINK}"
log ""
if [[ -n "${REDEEM_URL:-}" ]]; then
  detail "An invitation email has been sent. Direct redemption link, if it bounces:"
  detail "${REDEEM_URL}"
  log ""
fi
warn "They must redeem the invitation BEFORE the workshop."
detail "An unredeemed guest cannot sign in, and the error they get"
detail "(\"account does not exist in tenant\") does not say why."
log ""
