#!/usr/bin/env bash
# Shared helpers for the attendee provisioning scripts.
# Sourced by deploy.sh and invite.sh; not meant to be executed directly.

# ----------------------------------------------------------------------------
# Constants that the research in docs/spikes/foundry-agent-attendee-access.md
# established. Changing these without re-reading that document is a bad idea.
# ----------------------------------------------------------------------------

# ARM control plane. 2026-09-01 exists in the spec repo but is not on Learn and
# is not confirmed to be served; 2026-07-01 is the latest documented stable.
readonly ARM_API_VERSION="2026-07-01"

# Foundry data plane (agents, connections). Audience is ai.azure.com -- NOT
# cognitiveservices.azure.com (that is the inference plane) and NOT ARM.
readonly DATA_API_VERSION="v1"
readonly DATA_RESOURCE="https://ai.azure.com"

# Azure Monitor control plane. Both are the latest documented stable versions.
# 2020-02-02 is the first components version that requires WorkspaceResourceId,
# i.e. the workspace-based model -- classic App Insights is retired, so there is
# no older version worth using.
readonly LOG_ANALYTICS_API_VERSION="2023-09-01"
readonly APP_INSIGHTS_API_VERSION="2020-02-02"

# "Foundry User". The Foundry RBAC roles were renamed recently (it used to be
# "Azure AI User"), so we assign by GUID -- Learn explicitly recommends this to
# survive the rename rollout.
readonly ROLE_FOUNDRY_USER="53ca6127-db72-4b80-b1b0-d745d6d5456d"

# "Log Analytics Reader". Foundry User covers the Cognitive Services data plane
# and nothing else, so it does NOT let you read the trace store behind the
# portal's Traces tab. Learn names this role for exactly that.
readonly ROLE_LOG_ANALYTICS_READER="73c42c96-874c-492b-b04d-ab87d138a893"

# Resource providers. None of these are registered by default on a fresh
# subscription. The first seven are the documented Agent Service prerequisite;
# the last two carry Log Analytics and Application Insights, which this script
# provisions for tracing, and Container Apps in Lab 6.
readonly REQUIRED_PROVIDERS=(
  Microsoft.CognitiveServices
  Microsoft.KeyVault
  Microsoft.Storage
  Microsoft.MachineLearningServices
  Microsoft.Search
  Microsoft.App
  Microsoft.ContainerService
  Microsoft.OperationalInsights
  Microsoft.Insights
)

# Regions where Foundry Agent Service is supported. Narrower than the set of
# regions where the models themselves are available -- that is the trap.
readonly AGENT_SERVICE_REGIONS=(
  australiaeast brazilsouth canadacentral canadaeast centralus eastus eastus2
  francecentral germanywestcentral italynorth japaneast japanwest koreacentral
  northcentralus norwayeast polandcentral southafricanorth southcentralus
  southeastasia southindia spaincentral swedencentral switzerlandnorth
  switzerlandwest uaenorth uksouth westcentralus westeurope westus westus3
)

# ----------------------------------------------------------------------------
# Output
# ----------------------------------------------------------------------------

if [[ -t 2 ]]; then
  readonly C_RESET=$'\033[0m'  C_RED=$'\033[31m'  C_GREEN=$'\033[32m'
  readonly C_YELLOW=$'\033[33m' C_BLUE=$'\033[34m' C_DIM=$'\033[2m' C_BOLD=$'\033[1m'
else
  readonly C_RESET='' C_RED='' C_GREEN='' C_YELLOW='' C_BLUE='' C_DIM='' C_BOLD=''
fi

log()    { printf '%s\n' "$*" >&2; }
step()   { printf '\n%s==>%s %s%s%s\n' "$C_BLUE" "$C_RESET" "$C_BOLD" "$*" "$C_RESET" >&2; }
ok()     { printf '    %s/%s %s\n' "$C_GREEN" "$C_RESET" "$*" >&2; }
skip()   { printf '    %s=%s %s %s(already correct)%s\n' "$C_DIM" "$C_RESET" "$*" "$C_DIM" "$C_RESET" >&2; }
warn()   { printf '    %s!%s %s\n' "$C_YELLOW" "$C_RESET" "$*" >&2; }
detail() { printf '      %s%s%s\n' "$C_DIM" "$*" "$C_RESET" >&2; }

# die <message...> -- print a multi-line error and exit 1.
die() {
  printf '\n%sERROR%s %s\n' "$C_RED" "$C_RESET" "$1" >&2
  shift
  local line
  for line in "$@"; do printf '      %s\n' "$line" >&2; done
  printf '\n' >&2
  exit 1
}

# assumption <id> <text> -- announce that the next step rests on something the
# Microsoft docs do not actually guarantee. If the step then fails, the operator
# knows which unverified assumption to suspect. See the spike doc's
# "Empirical checks still outstanding" section.
assumption() {
  printf '    %s?%s %sassumes [%s]: %s%s\n' \
    "$C_YELLOW" "$C_RESET" "$C_DIM" "$1" "$2" "$C_RESET" >&2
}

# ----------------------------------------------------------------------------
# Prerequisites
# ----------------------------------------------------------------------------

require_tools() {
  local missing=()
  local t
  for t in "$@"; do
    command -v "$t" >/dev/null 2>&1 || missing+=("$t")
  done
  if (( ${#missing[@]} )); then
    die "Missing required tool(s): ${missing[*]}" \
      "In the workshop Codespace, 'az' and 'jq' should already be present." \
      "If 'az' is missing the devcontainer is out of date -- see infra/attendee/README.md." \
      "Install jq with: sudo apt-get update && sudo apt-get install -y jq"
  fi
}

require_login() {
  if ! az account show -o none 2>/dev/null; then
    die "Not signed in to Azure." \
      "Run this first (device code flow works in a browser-less Codespace):" \
      "" \
      "    az login --use-device-code" \
      "" \
      "Then re-run this script."
  fi
}

# ----------------------------------------------------------------------------
# Naming
#
# Foundry account names become a global DNS subdomain, so they must be globally
# unique AND stable across re-runs (otherwise re-running would orphan the old
# resource). We derive a short deterministic suffix rather than randomising.
# ----------------------------------------------------------------------------

short_hash() {
  local input="$1" len="${2:-8}"
  if command -v sha256sum >/dev/null 2>&1; then
    printf '%s' "$input" | sha256sum | cut -c1-"$len"
  else
    printf '%s' "$input" | shasum -a 256 | cut -c1-"$len"
  fi
}

# ----------------------------------------------------------------------------
# Foundry data-plane calls
#
# az rest exits non-zero on any 4xx and does not expose the status code, so we
# use curl with an explicit token when we need to branch on 404 vs 403.
# ----------------------------------------------------------------------------

data_token() {
  az account get-access-token --resource "$DATA_RESOURCE" --query accessToken -o tsv 2>/dev/null \
    || die "Could not acquire a token for $DATA_RESOURCE." \
         "This usually means the signed-in identity cannot access the Foundry data plane." \
         "Try: az login --use-device-code"
}

# data_call <method> <url> [json-body]
# Sets the globals DATA_STATUS and DATA_BODY. Never exits on its own -- callers
# decide what a given status means, because the same code means different things
# on different routes (404 on GET agent = "absent"; 404 on create = "bad model").
data_call() {
  local method="$1" url="$2" body="${3:-}"
  local token raw
  token="$(data_token)"

  local -a args=(
    -sS -X "$method" "$url"
    -H "Authorization: Bearer ${token}"
    -H "Content-Type: application/json"
    -w $'\n%{http_code}'
  )
  [[ -n "$body" ]] && args+=(-d "$body")

  raw="$(curl "${args[@]}" 2>/dev/null)" || {
    DATA_STATUS="000"; DATA_BODY=""
    return 0
  }
  DATA_STATUS="${raw##*$'\n'}"
  DATA_BODY="${raw%$'\n'*}"
}

# data_error_message -- best-effort extraction of the service's own message.
# Foundry publishes no error-code vocabulary for the agents routes, so the
# human-readable message is the only reliable signal. Branch on status, not code.
data_error_message() {
  jq -r '.error.message // .message // empty' <<<"${DATA_BODY:-}" 2>/dev/null \
    || printf '%s' "${DATA_BODY:-(no response body)}"
}

# die_on_data_error <context> -- shared interpretation of data-plane failures.
die_on_data_error() {
  local context="$1"
  local msg; msg="$(data_error_message)"
  case "$DATA_STATUS" in
    000) die "$context: could not reach the Foundry data plane." \
             "Check network access from this machine to *.services.ai.azure.com." ;;
    400) die "$context: the service rejected the request (400)." \
             "Service said: ${msg:-(no message)}" \
             "" \
             "Most likely causes, in order:" \
             "  - definition.model must be the DEPLOYMENT name, not the model name." \
             "  - a tool schema uses anyOf/allOf or a multi-type parameter, which" \
             "    Foundry rejects. A nullable C# parameter (string?) can cause this." \
             "  - the referenced project connection does not exist." ;;
    401) die "$context: unauthorized (401)." \
             "The token was missing, expired, or issued for the wrong audience." \
             "Re-run: az login --use-device-code" ;;
    403) die "$context: forbidden (403)." \
             "Authenticated, but the identity lacks a Foundry data-plane role." \
             "Assign 'Foundry User' ($ROLE_FOUNDRY_USER) on the project scope." ;;
    404) die "$context: not found (404)." \
             "Service said: ${msg:-(no message)}" \
             "If this was an agent create, the model deployment name is the usual culprit." ;;
    409) die "$context: conflict (409)." \
             "Service said: ${msg:-(no message)}" ;;
    429) die "$context: rate limited or out of quota (429)." \
             "Service said: ${msg:-(no message)}" \
             "Tier 0 subscriptions get 200 RPM on gpt-4.1-mini. If a room full of" \
             "attendees is provisioning at once, wait a minute and re-run -- this" \
             "script is idempotent and will pick up where it left off." ;;
    5*)  die "$context: service error ($DATA_STATUS)." \
             "Service said: ${msg:-(no message)}" \
             "This is usually transient. Re-run the script." ;;
    *)   die "$context: unexpected HTTP $DATA_STATUS." \
             "Service said: ${msg:-(no message)}" ;;
  esac
}

# ----------------------------------------------------------------------------
# Foundry portal playground deep link
#
# UNDOCUMENTED. Derived from Microsoft's own azd extension source
# (Azure/azure-dev, service_target_agent.go) and cross-checked against a real
# emitted URL. The subscription GUID is base64url-encoded from its 16 raw bytes
# with padding stripped. The empty third comma-slot is intentional; its purpose
# is unknown. Microsoft can change this without notice, so treat it as a
# convenience and never as a contract.
# ----------------------------------------------------------------------------

encode_subscription_id() {
  local hex="${1//-/}"
  if command -v python3 >/dev/null 2>&1; then
    python3 -c 'import base64,sys; print(base64.urlsafe_b64encode(bytes.fromhex(sys.argv[1])).decode().rstrip("="))' "$hex"
  elif command -v xxd >/dev/null 2>&1; then
    printf '%s' "$hex" | xxd -r -p | base64 | tr '+/' '-_' | tr -d '=\n'
  else
    return 1
  fi
}

playground_url() {
  local sub="$1" rg="$2" account="$3" project="$4" agent="$5" version="${6:-1}"
  local enc
  if ! enc="$(encode_subscription_id "$sub")"; then
    return 1
  fi
  printf 'https://ai.azure.com/nextgen/r/%s,%s,,%s,%s/build/agents/%s/build?version=%s' \
    "$enc" "$rg" "$account" "$project" "$agent" "$version"
}
