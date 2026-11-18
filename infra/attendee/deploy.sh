#!/usr/bin/env bash
#
# Provision (or converge, or delete) one attendee's Azure AI Foundry setup:
# resource group -> Foundry account -> project -> capped model deployment ->
# RemoteTool connection -> agent with an MCP tool.
#
# Safe to re-run at any point. Every step checks before it acts, and the common
# case -- "my MCP URL changed" -- touches exactly one resource.
#
# Findings this script is built on: docs/spikes/foundry-agent-attendee-access.md
#
# This is a bash script. `sh deploy.sh` (dash on Ubuntu) chokes on the first [[ or <( below
# with "syntax error near unexpected token `<'", so hand over to bash before anything else.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

# ----------------------------------------------------------------------------
# Defaults
# ----------------------------------------------------------------------------

LOCATION="eastus2"
MODEL="gpt-4.1-mini"
MODEL_VERSION="2025-04-14"
MODEL_FORMAT="OpenAI"
SKU_NAME="GlobalStandard"
CAPACITY="50"            # 1 == 1,000 TPM, so 50 == 50K TPM. One playground turn that
                         # books a seat is ~5 model requests, each resending the whole
                         # thread plus the tool schemas; 10K throttles mid-turn.
APPROVAL="always"        # Lab 1 keeps approvals on; Lab 2+ pass --approval never.
APPROVAL_SET="false"     # when false, an existing agent's policy is preserved
MCP_URL=""
MCP_TOKEN=""             # Lab 5: bearer token for a secured /mcp endpoint.
MCP_TOKEN_SET="false"    # distinguishes "not passed" from "passed empty"
SLOT=""
DO_DELETE="false"
ALL_SLOTS="false"
ASSUME_YES="false"
APP_INSIGHTS="true"       # provision App Insights and connect it for tracing
WRITE_APPSETTINGS="true"  # store its connection string in the Host's appsettings.json

AGENT_NAME="travel-booking-agent"
CONNECTION_NAME="travel-mcp-conn"
TRACING_CONNECTION_NAME="travel-appinsights-conn"
SERVER_LABEL="travelbooking"

usage() {
  cat <<'EOF'
Provision an Azure AI Foundry agent that calls your Travel Booking MCP server.

USAGE
  ./deploy.sh --mcp-url <url> [options]
  ./deploy.sh --delete [--slot N | --all-slots]

REQUIRED (unless deleting)
  --mcp-url <url>        Public HTTPS URL of your MCP endpoint, including the
                         path. Your Codespace's public port 5197 in Labs 0-5,
                         the Container Apps URL in Lab 6. A URL that does
                         not end in /mcp gets /mcp appended.
                         e.g. https://$CODESPACE_NAME-5197.app.github.dev/mcp

OPTIONS
  --location <region>    Azure region. Default: eastus2
                         Must be a Foundry Agent Service region (narrower than
                         the set of regions where the models exist).
  --model <name>         Chat model. Default: gpt-4.1-mini
  --model-version <ver>  Default: 2025-04-14
  --capacity <n>         TPM cap in thousands. Default: 50 (= 50,000 TPM)
  --approval always|never
                         Whether the agent asks before each tool call.
                         Default: always (Lab 1). Labs 2+ use: --approval never
  --mcp-token <jwt>      Bearer token to present to the MCP server (Lab 5,
                         once /mcp requires authentication). Stored as an
                         Authorization header on the project connection.
                         Pass "" to clear it and go back to anonymous.
                         Note: readable by anyone with access to the project.
  --no-app-insights      Skip Application Insights entirely. Foundry then
                         records no agent traces and the portal's Traces tab
                         stays empty, so Lab 3 loses half its material.
  --no-appsettings       Still provision and connect Application Insights, but
                         leave src/TravelBooking.Host/appsettings.json alone.
                         The connection string is printed either way, so you
                         can configure it yourself.
  --slot <n>             Instructor fallback mode: provision an isolated,
                         numbered copy (slot 1, 2, 3...) instead of a
                         personal one. See README.md.
  --delete               Tear down. Respects --slot; add --all-slots to remove
                         every slot at once.
  --all-slots            With --delete: remove all slots found in this
                         subscription.
  -y, --yes              Do not prompt for confirmation on destructive actions.
  -h, --help             Show this help.

EXAMPLES
  # Make port 5197 reachable from Azure, then provision
  gh codespace ports visibility 5197:public -c "$CODESPACE_NAME"
  ./deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp"

  # Lab 2 onward: stop prompting for every tool call
  ./deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp" --approval never

  # Lab 6: point the same agent at Azure Container Apps
  ./deploy.sh --mcp-url https://travelbooking-host.x.eastus2.azurecontainerapps.io/mcp

  # Instructor: fallback slot 3 in Sweden
  ./deploy.sh --slot 3 --location swedencentral --mcp-url https://.../mcp

  # Instructor: clean up everything afterwards
  ./deploy.sh --delete --all-slots

  # Provision tracing but configure the connection string by hand
  ./deploy.sh --mcp-url "https://${CODESPACE_NAME}-5197.app.github.dev/mcp" --no-appsettings
EOF
}

# ----------------------------------------------------------------------------
# Arguments
# ----------------------------------------------------------------------------

while [[ $# -gt 0 ]]; do
  case "$1" in
    --location)      LOCATION="${2:?--location needs a value}"; shift 2 ;;
    --model)         MODEL="${2:?--model needs a value}"; shift 2 ;;
    --model-version) MODEL_VERSION="${2:?--model-version needs a value}"; shift 2 ;;
    --capacity)      CAPACITY="${2:?--capacity needs a value}"; shift 2 ;;
    --approval)      APPROVAL="${2:?--approval needs a value}"; APPROVAL_SET="true"; shift 2 ;;
    --mcp-url)       MCP_URL="${2:?--mcp-url needs a value}"; shift 2 ;;
    # Accepts an empty value on purpose: --mcp-token "" clears the credential.
    --mcp-token)     [[ $# -ge 2 ]] || die "--mcp-token needs a value (use \"\" to clear)."
                     MCP_TOKEN="$2"; MCP_TOKEN_SET="true"; shift 2 ;;
    --no-app-insights) APP_INSIGHTS="false"; shift ;;
    --no-appsettings)  WRITE_APPSETTINGS="false"; shift ;;
    --slot)          SLOT="${2:?--slot needs a value}"; shift 2 ;;
    --delete)        DO_DELETE="true"; shift ;;
    --all-slots)     ALL_SLOTS="true"; shift ;;
    -y|--yes)        ASSUME_YES="true"; shift ;;
    -h|--help)       usage; exit 0 ;;
    *) die "Unknown argument: $1" "Run './deploy.sh --help' for usage." ;;
  esac
done

[[ "$APPROVAL" == "always" || "$APPROVAL" == "never" ]] \
  || die "--approval must be 'always' or 'never' (got '$APPROVAL')."

if [[ -n "$SLOT" ]]; then
  [[ "$SLOT" =~ ^[0-9]+$ ]] || die "--slot must be a number (got '$SLOT')."
  SLOT="$(printf '%02d' "$((10#$SLOT))")"
fi

if [[ "$DO_DELETE" == "false" ]]; then
  [[ -n "$MCP_URL" ]] || die "--mcp-url is required." \
    "Make port 5197 public first, then pass its URL including the /mcp path:" \
    "" \
    "    gh codespace ports visibility 5197:public -c \"\$CODESPACE_NAME\"" \
    "    ./deploy.sh --mcp-url \"https://\${CODESPACE_NAME}-5197.app.github.dev/mcp\"" \
    "" \
    "Run './deploy.sh --help' for more."

  case "$MCP_URL" in
    https://*) : ;;
    http://*)  die "--mcp-url must be HTTPS." \
                 "Foundry requires TLS 1.2 or greater; a plain http:// endpoint will fail." ;;
    *)         die "--mcp-url does not look like a URL: '$MCP_URL'" ;;
  esac

  case "$MCP_URL" in
    *localhost*|*127.0.0.1*)
      die "--mcp-url points at localhost." \
        "The Foundry agent runs in Azure and cannot reach your Codespace directly." \
        "Make the port public first, then use the public URL:" \
        "" \
        "    gh codespace ports visibility 5197:public -c \"\$CODESPACE_NAME\"" \
        "    https://\${CODESPACE_NAME}-5197.app.github.dev/mcp" ;;
  esac

  # The server maps MCP at /mcp (app.MapMcp("/mcp")), so the URL has to end there. A
  # URL copied from the Ports panel or from azd's output is the site root, and an agent
  # pointed at that sees no tools; add the path rather than let that happen.
  while [[ "$MCP_URL" == */ ]]; do MCP_URL="${MCP_URL%/}"; done
  if [[ "$MCP_URL" != */mcp ]]; then
    MCP_URL="${MCP_URL}/mcp"
    warn "--mcp-url did not end in /mcp; using ${MCP_URL}"
  fi
fi

require_tools az jq curl
require_login

# ----------------------------------------------------------------------------
# Identity and naming
# ----------------------------------------------------------------------------

SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
SUBSCRIPTION_NAME="$(az account show --query name -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"
SIGNED_IN_OID="$(az ad signed-in-user show --query id -o tsv 2>/dev/null || true)"

# Foundry account names become a global DNS label, so they must be globally
# unique AND stable across re-runs. Derive them deterministically rather than
# randomising, so a second run converges instead of creating a twin.
if [[ -n "$SLOT" ]]; then
  NAME_SEED="slot${SLOT}:${SUBSCRIPTION_ID}"
  SUFFIX="$(short_hash "$NAME_SEED" 6)"
  RG="rg-mcpws-slot${SLOT}"
  ACCOUNT="mcpws-slot${SLOT}-${SUFFIX}"
  PROJECT="proj-mcpws-slot${SLOT}"
  MODE_LABEL="fallback slot ${SLOT}"
else
  NAME_SEED="${SIGNED_IN_OID:-$SUBSCRIPTION_ID}:${SUBSCRIPTION_ID}"
  SUFFIX="$(short_hash "$NAME_SEED" 8)"
  RG="rg-mcpws-${SUFFIX}"
  ACCOUNT="mcpws-${SUFFIX}"
  PROJECT="proj-mcpws"
  MODE_LABEL="personal"
fi

DEPLOYMENT_NAME="$MODEL"   # definition.model is the DEPLOYMENT name, so keeping
                           # them identical removes a whole class of confusion.

# Monitoring resource names are derived from the resource group rather than from
# the suffix directly, so that the teardown path can reconstruct them from a
# resource group name alone (--all-slots iterates over groups, not identities).
# These are resource-group-scoped names, so they need no global uniqueness.
WORKSPACE="log-mcpws-${RG#rg-mcpws-}"
APPINSIGHTS="appi-mcpws-${RG#rg-mcpws-}"

REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
APPSETTINGS_FILE="${REPO_ROOT}/src/TravelBooking.Host/appsettings.json"
APPSETTINGS_LABEL="${APPSETTINGS_FILE#"${REPO_ROOT}/"}"

# ----------------------------------------------------------------------------
# Local configuration
#
# The Host enables the Azure Monitor exporter whenever it finds a connection
# string in AzureMonitor:ConnectionString -- in addition to the OTLP export to
# the Aspire dashboard, not instead of it (see ServiceDefaults,
# AddOpenTelemetryExporters). Writing the value here is what puts the attendee's
# own spans in the same store as the agent's, where they join on trace id.
# ----------------------------------------------------------------------------

appsettings_connection_string() {
  [[ -f "$APPSETTINGS_FILE" ]] || return 0
  jq -r '.AzureMonitor.ConnectionString // empty' "$APPSETTINGS_FILE" 2>/dev/null || true
}

# set_appsettings_connection_string <value>
#
# Returns 0 only when the file was actually rewritten, so callers can stay quiet
# on a re-run. Pass "" to clear the setting, which switches the exporter off.
set_appsettings_connection_string() {
  local value="$1" tmp
  if [[ ! -f "$APPSETTINGS_FILE" ]]; then
    warn "no ${APPSETTINGS_LABEL} to update"
    return 1
  fi
  [[ "$(appsettings_connection_string)" == "$value" ]] && return 1

  # jq has no in-place mode, and redirecting onto the source file would truncate
  # it if jq failed halfway -- which would leave the attendee with no settings
  # file at all. Build it beside the original and move it into place.
  tmp="$(mktemp)" || { warn "could not create a temporary file"; return 1; }
  if ! jq --arg cs "$value" '.AzureMonitor.ConnectionString = $cs' \
       "$APPSETTINGS_FILE" >"$tmp" 2>/dev/null; then
    rm -f "$tmp"
    warn "could not rewrite ${APPSETTINGS_LABEL} -- is it still valid JSON?"
    return 1
  fi
  if ! mv "$tmp" "$APPSETTINGS_FILE"; then
    rm -f "$tmp"
    warn "could not replace ${APPSETTINGS_LABEL}"
    return 1
  fi
  return 0
}

# ----------------------------------------------------------------------------
# Teardown
# ----------------------------------------------------------------------------

confirm() {
  [[ "$ASSUME_YES" == "true" ]] && return 0
  local reply
  printf '\n%s%s%s [y/N] ' "$C_BOLD" "$1" "$C_RESET" >&2
  read -r reply </dev/tty || reply=""
  [[ "$reply" == "y" || "$reply" == "Y" ]]
}

# A connection string left pointing at a deleted resource is not harmless: the
# Host would keep trying to export telemetry to an endpoint that no longer
# accepts it, and because appsettings.json is tracked by git the dead value
# follows the attendee into their next session. Only clear it when it really is
# the instance we are about to delete -- an attendee may have pointed the file
# at some App Insights of their own.
clear_appsettings_for_rg() {
  local rg="$1"
  local component="appi-mcpws-${rg#rg-mcpws-}"
  local stored live

  stored="$(appsettings_connection_string)"
  [[ -n "$stored" ]] || return 0

  live="$(az rest --method GET \
    --url "https://management.azure.com/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${rg}/providers/Microsoft.Insights/components/${component}?api-version=${APP_INSIGHTS_API_VERSION}" \
    --query 'properties.ConnectionString' -o tsv 2>/dev/null || true)"

  if [[ -n "$live" && "$live" == "$stored" ]]; then
    if set_appsettings_connection_string ""; then
      ok "cleared the connection string from ${APPSETTINGS_LABEL}"
    fi
  fi
  return 0
}

delete_one_rg() {
  local rg="$1"
  if [[ "$(az group exists --name "$rg")" != "true" ]]; then
    skip "resource group ${rg} does not exist"
    return 0
  fi
  step "Deleting resource group ${rg}"
  # Read it out before the group goes, while the component still answers.
  clear_appsettings_for_rg "$rg"
  # Role assignments are not documented to cascade with the resource group, and
  # a stale assignment keeps granting access while its token is valid. Remove
  # them while the scope still exists.
  local scope="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${rg}"
  local ids
  ids="$(az role assignment list --scope "$scope" \
          --fill-principal-name false --fill-role-definition-name false \
          --query "[?scope=='${scope}'].id" -o tsv 2>/dev/null || true)"
  if [[ -n "$ids" ]]; then
    local id
    while IFS= read -r id; do
      [[ -z "$id" ]] && continue
      az role assignment delete --ids "$id" -o none 2>/dev/null \
        && ok "removed role assignment" \
        || warn "could not remove a role assignment (continuing)"
    done <<<"$ids"
  fi
  az group delete --name "$rg" --yes --no-wait -o none
  ok "deletion started for ${rg} (running in the background)"
}

if [[ "$DO_DELETE" == "true" ]]; then
  if [[ "$ALL_SLOTS" == "true" ]]; then
    # Deliberately not 'mapfile' -- that is bash 4+, and macOS ships bash 3.2.
    slot_rgs=()
    while IFS= read -r _rg; do
      [[ -n "$_rg" ]] && slot_rgs+=("$_rg")
    done < <(az group list --query "[?starts_with(name,'rg-mcpws-slot')].name" -o tsv)
    if (( ${#slot_rgs[@]} == 0 )); then
      log "No slot resource groups found in subscription '${SUBSCRIPTION_NAME}'."
      exit 0
    fi
    log ""
    log "About to delete ${#slot_rgs[@]} slot resource group(s) in '${SUBSCRIPTION_NAME}':"
    printf '  - %s\n' "${slot_rgs[@]}" >&2
    confirm "Delete all of these?" || { log "Aborted."; exit 0; }
    for rg in "${slot_rgs[@]}"; do delete_one_rg "$rg"; done
  else
    log ""
    log "About to delete the ${MODE_LABEL} setup in '${SUBSCRIPTION_NAME}':"
    log "  resource group: ${RG}"
    confirm "Delete it?" || { log "Aborted."; exit 0; }
    delete_one_rg "$RG"
  fi
  log ""
  ok "Teardown started. 'az group list -o table' will show progress."
  exit 0
fi

# ----------------------------------------------------------------------------
# Preflight
# ----------------------------------------------------------------------------

step "Preflight"

detail "subscription : ${SUBSCRIPTION_NAME} (${SUBSCRIPTION_ID})"
detail "mode         : ${MODE_LABEL}"
detail "region       : ${LOCATION}"
detail "model        : ${MODEL} ${MODEL_VERSION} (${SKU_NAME}, ${CAPACITY}K TPM cap)"

# --- Region must support Agent Service, not merely the model -----------------
region_supported="false"
for r in "${AGENT_SERVICE_REGIONS[@]}"; do
  [[ "$r" == "$LOCATION" ]] && { region_supported="true"; break; }
done
if [[ "$region_supported" != "true" ]]; then
  die "Region '${LOCATION}' does not support Foundry Agent Service." \
    "A model being available in a region does not mean agents are -- the" \
    "Agent Service region list is shorter. Supported regions:" \
    "" \
    "  ${AGENT_SERVICE_REGIONS[*]}" \
    "" \
    "Recommended: eastus2 (default) or swedencentral."
fi
ok "region ${LOCATION} supports Agent Service"

# --- Subscription offer type -------------------------------------------------
QUOTA_ID="$(az rest --method GET \
  --url "https://management.azure.com/subscriptions/${SUBSCRIPTION_ID}?api-version=2020-01-01" \
  --query "subscriptionPolicies.quotaId" -o tsv 2>/dev/null || true)"

case "$QUOTA_ID" in
  FreeTrial_*)
    warn "This is a FREE TRIAL subscription (${QUOTA_ID})."
    detail "Microsoft documents free trials at 0 TPM for all models, and free"
    detail "trials are NOT eligible for quota increases. The newer quota-tier"
    detail "system may supersede this, but Learn never says which tier a trial"
    detail "gets, so this may simply fail at the model-deployment step."
    detail ""
    detail "If it does: upgrade to pay-as-you-go (portal, self-service) and"
    detail "allow time for the change to propagate, or ask your instructor for"
    detail "a fallback slot."
    ;;
  AzureForStudents*)
    warn "This is an Azure for Students subscription (${QUOTA_ID})."
    detail "Documented at 1K TPM, which a single agent turn can exceed."
    detail "Expect throttling; a fallback slot may work better."
    ;;
  "")
    warn "This subscription reports no offer type (empty quotaId)."
    detail "Sponsorship, CSP and some EA subscriptions do this. They have no"
    detail "conventional billing history, which is the profile Azure's anti-abuse"
    detail "system flags on a FIRST Cognitive Services model deployment."
    detail "If the deployment step below fails with code 715-123420, that is why,"
    detail "and it needs a support request rather than a change to this script."
    ;;
  *)  ok "subscription offer: ${QUOTA_ID}" ;;
esac

# --- Quota tier (best effort; preview API, may not answer) -------------------
TIER="$(az rest --method GET \
  --url "https://management.azure.com/subscriptions/${SUBSCRIPTION_ID}/providers/Microsoft.CognitiveServices/quotaTiers?api-version=2025-10-01-preview" \
  -o json 2>/dev/null | jq -r '.. | .currentTierName? // empty' | head -1 || true)"
if [[ -n "$TIER" ]]; then
  ok "quota tier: ${TIER}"
  if [[ "$TIER" == *"Free"* || "$TIER" == *"Tier 0"* ]]; then
    detail "Tier 0 carries only gpt-4.1-mini, gpt-5-mini, o4-mini and"
    detail "text-embedding-3-small. gpt-4.1 (full) is not deployable here."
  fi
else
  detail "quota tier: not reported (preview API; not fatal)"
fi

# --- Resource providers ------------------------------------------------------
step "Registering resource providers"
pending=()
for ns in "${REQUIRED_PROVIDERS[@]}"; do
  state="$(az provider show --namespace "$ns" --query registrationState -o tsv 2>/dev/null || echo "Unknown")"
  case "$state" in
    Registered) skip "$ns" ;;
    *)
      az provider register --namespace "$ns" -o none 2>/dev/null || true
      pending+=("$ns")
      ok "$ns (registration requested)"
      ;;
  esac
done
if (( ${#pending[@]} )); then
  # Registration is per-region and incremental; Learn warns against blocking on
  # the global state. Proceed and let the real operation be the test.
  detail "${#pending[@]} provider(s) still registering -- continuing, since"
  detail "registration completes per-region and blocking here is discouraged."
fi

# ----------------------------------------------------------------------------
# Resource group
# ----------------------------------------------------------------------------

step "Resource group"
if [[ "$(az group exists --name "$RG")" == "true" ]]; then
  skip "$RG"
else
  az group create --name "$RG" --location "$LOCATION" \
    --tags workshop=mcp-vslive purpose=foundry-agent mode="$MODE_LABEL" -o none
  ok "created $RG"
fi

# ----------------------------------------------------------------------------
# Foundry account
# ----------------------------------------------------------------------------

step "Foundry resource"
if az cognitiveservices account show -n "$ACCOUNT" -g "$RG" -o none 2>/dev/null; then
  skip "$ACCOUNT"
else
  detail "creating ${ACCOUNT} (this also claims the global subdomain)"
  acct_err="$(mktemp)"; trap 'rm -f "$acct_err"' EXIT
  if ! az cognitiveservices account create \
      --name "$ACCOUNT" \
      --resource-group "$RG" \
      --kind AIServices \
      --sku S0 \
      --location "$LOCATION" \
      --custom-domain "$ACCOUNT" \
      --assign-identity \
      --allow-project-management true \
      --yes -o none 2>"$acct_err"; then
    err="$(cat "$acct_err" 2>/dev/null || true)"
    case "$err" in
      *CustomDomainInUse*)
        die "The subdomain '${ACCOUNT}' is already taken globally." \
          "The name is derived from your identity, so this is unlucky rather" \
          "than expected. Re-run with --slot to get a different name." ;;
      *InsufficientQuota*|*SpecialFeatureOrQuotaIdRequired*)
        die "Azure refused to create the Foundry resource on this subscription." \
          "Service said: ${err}" \
          "" \
          "This is the documented free-trial failure. Upgrade to pay-as-you-go" \
          "or ask your instructor for a fallback slot." ;;
      *) die "Could not create the Foundry resource." "Service said: ${err}" ;;
    esac
  fi
  rm -f "$acct_err"; trap - EXIT
  ok "created $ACCOUNT"
fi

# ----------------------------------------------------------------------------
# Project
# ----------------------------------------------------------------------------

step "Foundry project"
if az cognitiveservices account project show \
     --name "$ACCOUNT" -g "$RG" --project-name "$PROJECT" -o none 2>/dev/null; then
  skip "$PROJECT"
else
  az cognitiveservices account project create \
    --name "$ACCOUNT" \
    --resource-group "$RG" \
    --project-name "$PROJECT" \
    --location "$LOCATION" \
    --display-name "Travel Booking MCP Workshop" -o none \
    || die "Could not create the Foundry project." \
         "The usual cause is a missing managed identity on the account." \
         "Delete the account and re-run so it is recreated with --assign-identity."
  ok "created $PROJECT"
fi

# Read the data-plane endpoint rather than constructing it -- a custom subdomain
# would make the constructed form wrong.
PROJECT_ENDPOINT="$(az cognitiveservices account project show \
  --name "$ACCOUNT" -g "$RG" --project-name "$PROJECT" \
  --query 'properties.endpoints."AI Foundry API"' -o tsv 2>/dev/null || true)"
[[ -n "$PROJECT_ENDPOINT" && "$PROJECT_ENDPOINT" != "null" ]] \
  || die "The project exists but reported no 'AI Foundry API' endpoint." \
       "It may still be provisioning. Wait a minute and re-run."
PROJECT_ENDPOINT="${PROJECT_ENDPOINT%/}"
detail "endpoint: ${PROJECT_ENDPOINT}"

PROJECT_MI_OID="$(az cognitiveservices account project show \
  --name "$ACCOUNT" -g "$RG" --project-name "$PROJECT" \
  --query 'identity.principalId' -o tsv 2>/dev/null || true)"

# ----------------------------------------------------------------------------
# Role assignments
#
# Deploying from the portal auto-assigns Foundry User; deploying from the CLI
# does not. Without this the data-plane calls below return 403.
# ----------------------------------------------------------------------------

step "Role assignments"
RG_SCOPE="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RG}"

assign_foundry_user() {
  local oid="$1" label="$2" ptype="${3:-User}"
  [[ -z "$oid" || "$oid" == "null" ]] && { warn "no object id for ${label}; skipping"; return 0; }
  local existing
  existing="$(az role assignment list \
    --assignee-object-id "$oid" --role "$ROLE_FOUNDRY_USER" --scope "$RG_SCOPE" \
    --fill-principal-name false --fill-role-definition-name false \
    --query "[].id" -o tsv 2>/dev/null || true)"
  if [[ -n "$existing" ]]; then
    skip "Foundry User for ${label}"
  else
    # --assignee-object-id + --assignee-principal-type skips the Graph lookup
    # and avoids the documented PrincipalNotFound replication race.
    az role assignment create \
      --assignee-object-id "$oid" \
      --assignee-principal-type "$ptype" \
      --role "$ROLE_FOUNDRY_USER" \
      --scope "$RG_SCOPE" -o none 2>/dev/null \
      && ok "Foundry User for ${label}" \
      || warn "could not assign Foundry User to ${label} (you may lack permission)"
  fi
}

assign_foundry_user "$SIGNED_IN_OID" "you" "User"
assign_foundry_user "$PROJECT_MI_OID" "the project's managed identity" "ServicePrincipal"

# ----------------------------------------------------------------------------
# Model deployment
# ----------------------------------------------------------------------------

step "Model deployment"
# Compare capacity AND model version: matching capacity alone would skip the
# update after a --model-version change and silently leave the old version
# running. If a JMESPath ever stops matching we just re-issue the PUT, which is
# harmless because PUT is create-or-update.
existing_dep="$(az cognitiveservices account deployment show \
  --name "$ACCOUNT" -g "$RG" --deployment-name "$DEPLOYMENT_NAME" -o json 2>/dev/null || true)"
existing_capacity="$(jq -r '.sku.capacity // empty' <<<"${existing_dep:-{\}}" 2>/dev/null || true)"
existing_model="$(jq -r '.properties.model.name // empty' <<<"${existing_dep:-{\}}" 2>/dev/null || true)"
existing_version="$(jq -r '.properties.model.version // empty' <<<"${existing_dep:-{\}}" 2>/dev/null || true)"

if [[ "$existing_capacity" == "$CAPACITY" \
   && "$existing_model" == "$MODEL" \
   && "$existing_version" == "$MODEL_VERSION" ]]; then
  skip "${DEPLOYMENT_NAME} (${MODEL} ${MODEL_VERSION}, ${CAPACITY}K TPM)"
else
  if [[ -n "$existing_capacity" ]]; then
    [[ "$existing_capacity" != "$CAPACITY" ]] && detail "capacity ${existing_capacity} -> ${CAPACITY}"
    [[ "$existing_version" != "$MODEL_VERSION" ]] && detail "version ${existing_version:-none} -> ${MODEL_VERSION}"
    [[ "$existing_model" != "$MODEL" ]] && \
      warn "deployment '${DEPLOYMENT_NAME}' currently runs ${existing_model}, not ${MODEL}" && \
      detail "an in-place model change is not documented to work; if this fails," && \
      detail "delete the deployment and re-run."
  fi
  dep_err="$(mktemp)"; trap 'rm -f "$dep_err"' EXIT
  # PUT is create-or-update, so this is safe whether or not it already exists.
  if ! az cognitiveservices account deployment create \
      --name "$ACCOUNT" \
      --resource-group "$RG" \
      --deployment-name "$DEPLOYMENT_NAME" \
      --model-name "$MODEL" \
      --model-version "$MODEL_VERSION" \
      --model-format "$MODEL_FORMAT" \
      --sku-name "$SKU_NAME" \
      --sku-capacity "$CAPACITY" -o none 2>"$dep_err"; then
    err="$(cat "$dep_err" 2>/dev/null || true)"
    # Order matters: the anti-abuse message is matched first, because it is NOT
    # a quota problem and the quota advice below would send you the wrong way.
    case "$err" in
      *715-123420*|*"unusual activity"*)
        die "Azure blocked this deployment as 'unusual activity' (code 715-123420)." \
          "" \
          "This is an anti-abuse gate on the SUBSCRIPTION or billing account." \
          "It is not a quota problem, not a region problem, and not a problem" \
          "with ${MODEL} -- the request never got as far as validating the model," \
          "so 'az cognitiveservices model list' will tell you nothing here." \
          "" \
          "It usually means one of:" \
          "  - the subscription is brand new, or newly upgraded from a free trial," \
          "    and has no billing history yet (the classic first-deployment gate);" \
          "  - there is no verified payment method on the billing account;" \
          "  - Cognitive Services accounts or deployments were created and deleted" \
          "    repeatedly in quick succession." \
          "" \
          "Check these first, in order:" \
          "  1. az account show --query 'subscriptionPolicies.quotaId' -o tsv" \
          "     -> FreeTrial_* means upgrade to pay-as-you-go. Do that regardless;" \
          "        it often clears this gate as a side effect." \
          "  2. Azure portal -> Cost Management + Billing -> Payment methods." \
          "     Confirm a card is present AND verified." \
          "  3. Has this subscription ever deployed a Cognitive Services model" \
          "     successfully? If not, this is the first-deployment gate." \
          "" \
          "If all three look fine, this needs Microsoft support -- there is no" \
          "CLI flag or payload change that clears it. Open a support request," \
          "quote code 715-123420, and say you are deploying an Azure AI Foundry" \
          "chat model for a training workshop. These are typically cleared in a" \
          "day or two." \
          "" \
          "For the workshop itself: if this is an ATTENDEE subscription, use an" \
          "instructor fallback slot. If this IS the instructor subscription, the" \
          "slots are blocked too -- they live here -- so the support request is" \
          "on the critical path and needs raising well before the event." ;;
      *[Qq]uota*|*Capacity*|*capacity*)
        die "Not enough quota to deploy ${MODEL} in ${LOCATION}." \
          "Service said: ${err}" \
          "" \
          "This is the free-trial wall described in the spike doc. Options:" \
          "  - upgrade the subscription to pay-as-you-go, or" \
          "  - lower the cap:   --capacity 1   (1,000 TPM), or" \
          "  - ask your instructor for a fallback slot." ;;
      *)
        die "Could not create the model deployment." \
          "Service said: ${err}" \
          "" \
          "Check that ${MODEL} ${MODEL_VERSION} exists on ${SKU_NAME} in ${LOCATION}." \
          "The default table view only prints Kind and SkuName, so query the" \
          "model fields explicitly:" \
          "" \
          "    az cognitiveservices model list -l ${LOCATION} \\" \
          "      --query \"[?model.name=='${MODEL}'].{version:model.version, skus:join(',', model.skus[].name)}\" \\" \
          "      -o table" ;;
    esac
  fi
  rm -f "$dep_err"; trap - EXIT
  ok "deployed ${DEPLOYMENT_NAME} (${MODEL} ${MODEL_VERSION}, ${CAPACITY}K TPM)"
fi

# ----------------------------------------------------------------------------
# Application Insights
#
# Two independent consumers want this, which is why it is worth the two extra
# resources:
#
#   1. Foundry's own server-side agent tracing. It turns on by the mere act of
#      connecting an Application Insights resource to the project -- no code, no
#      SDK -- and Application Insights is the ONLY place those traces ever go.
#      There is no documented way to point Foundry at an OTLP endpoint.
#   2. The attendee's own server, via AzureMonitor:ConnectionString. Both halves
#      of a request then land in one store and join on the W3C trace id.
#
# Classic Application Insights is retired, so a component has to ingest into a
# Log Analytics workspace -- that is the only reason the workspace exists.
#
# Neither failure is fatal. The agent is perfectly usable without tracing, and
# dying here would strand an attendee whose subscription or region refuses Log
# Analytics with no agent at all.
# ----------------------------------------------------------------------------

APPINSIGHTS_CONNECTION_STRING=""

WORKSPACE_ID="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RG}/providers/Microsoft.OperationalInsights/workspaces/${WORKSPACE}"
APPINSIGHTS_ID="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RG}/providers/Microsoft.Insights/components/${APPINSIGHTS}"

setup_app_insights() {
  local ws_url="https://management.azure.com${WORKSPACE_ID}?api-version=${LOG_ANALYTICS_API_VERSION}"
  local ai_url="https://management.azure.com${APPINSIGHTS_ID}?api-version=${APP_INSIGHTS_API_VERSION}"
  local ws_state ws_body ai_json ai_body attempt

  # --- Log Analytics workspace ----------------------------------------------
  ws_state="$(az rest --method GET --url "$ws_url" \
    --query 'properties.provisioningState' -o tsv 2>/dev/null || true)"

  if [[ "$ws_state" == "Succeeded" ]]; then
    skip "workspace ${WORKSPACE}"
  else
    ws_body="$(jq -nc --arg loc "$LOCATION" --arg mode "$MODE_LABEL" \
      '{location: $loc,
        tags: {workshop: "mcp-vslive", purpose: "foundry-agent", mode: $mode},
        properties: {sku: {name: "PerGB2018"}, retentionInDays: 30}}')"

    if ! az rest --method PUT --url "$ws_url" --body "$ws_body" -o none 2>/dev/null; then
      warn "Could not create the Log Analytics workspace ${WORKSPACE}."
      detail "Application Insights cannot ingest without one, so tracing is off."
      return 1
    fi

    # The component PUT below is rejected while its workspace is still being
    # created, and workspace creation is not instant.
    for attempt in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
      ws_state="$(az rest --method GET --url "$ws_url" \
        --query 'properties.provisioningState' -o tsv 2>/dev/null || true)"
      [[ "$ws_state" == "Succeeded" ]] && break
      sleep 2
    done
    if [[ "$ws_state" != "Succeeded" ]]; then
      warn "workspace ${WORKSPACE} is still ${ws_state:-provisioning} after 30s"
      detail "Trying the component anyway; re-run if the next step fails."
    fi
    ok "created ${WORKSPACE}"
  fi

  # --- Application Insights component ---------------------------------------
  ai_json="$(az rest --method GET --url "$ai_url" -o json 2>/dev/null || true)"

  if [[ -n "$(jq -r '.properties.ConnectionString // empty' <<<"${ai_json:-{\}}" 2>/dev/null || true)" ]]; then
    skip "${APPINSIGHTS}"
  else
    # Flow_Type and Request_Source are the values Learn prescribes for a
    # component created through the REST API rather than the portal.
    ai_body="$(jq -nc --arg loc "$LOCATION" --arg ws "$WORKSPACE_ID" --arg mode "$MODE_LABEL" \
      '{location: $loc,
        kind: "web",
        tags: {workshop: "mcp-vslive", purpose: "foundry-agent", mode: $mode},
        properties: {Application_Type: "web",
                     Flow_Type: "Bluefield",
                     Request_Source: "rest",
                     WorkspaceResourceId: $ws,
                     IngestionMode: "LogAnalytics",
                     RetentionInDays: 30}}')"

    if ! az rest --method PUT --url "$ai_url" --body "$ai_body" -o none 2>/dev/null; then
      warn "Could not create the Application Insights component ${APPINSIGHTS}."
      return 1
    fi
    ok "created ${APPINSIGHTS}"
    ai_json="$(az rest --method GET --url "$ai_url" -o json 2>/dev/null || true)"
  fi

  APPINSIGHTS_CONNECTION_STRING="$(jq -r '.properties.ConnectionString // empty' \
    <<<"${ai_json:-{\}}" 2>/dev/null || true)"
  if [[ -z "$APPINSIGHTS_CONNECTION_STRING" ]]; then
    warn "${APPINSIGHTS} exists but reported no connection string."
    detail "It may still be provisioning. Wait a minute and re-run."
    return 1
  fi

  # --- Project connection: this is what switches Foundry's tracing on --------
  #
  # Deliberately re-PUT on every run instead of skipping when the target
  # matches. A stored credential is never returned by GET, so target equality
  # does not prove the key is current -- and if the component is ever deleted
  # and recreated, the resource id (the target) is identical while the
  # ingestion key is not. Foundry would then quietly post traces nowhere.
  local conn_url conn_body conn_file
  conn_url="https://management.azure.com/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RG}/providers/Microsoft.CognitiveServices/accounts/${ACCOUNT}/projects/${PROJECT}/connections/${TRACING_CONNECTION_NAME}?api-version=${ARM_API_VERSION}"
  conn_body="$(jq -nc --arg target "$APPINSIGHTS_ID" --arg key "$APPINSIGHTS_CONNECTION_STRING" \
    '{properties: {category: "AppInsights",
                   target: $target,
                   authType: "ApiKey",
                   credentials: {key: $key},
                   metadata: {ApiType: "Azure", ResourceId: $target}}}')"

  # Via a file so the ingestion key never appears in the process list.
  conn_file="$(mktemp)"; chmod 600 "$conn_file"
  printf '%s' "$conn_body" >"$conn_file"
  if ! az rest --method PUT --url "$conn_url" --body "@${conn_file}" -o none 2>/dev/null; then
    rm -f "$conn_file"
    warn "Could not connect ${APPINSIGHTS} to the project."
    detail "The resource exists, so your own server can still export to it, but"
    detail "Foundry will not record agent traces and the Traces tab stays empty."
    detail "If this is a 403, you may lack write access to the project."
    # The component and its connection string are fine, so keep the local half
    # of tracing alive instead of failing the whole step.
    return 0
  fi
  rm -f "$conn_file"
  ok "${TRACING_CONNECTION_NAME} -> ${APPINSIGHTS} (agent tracing on)"

  return 0
}

step "Application Insights"
if [[ "$APP_INSIGHTS" != "true" ]]; then
  skip "disabled with --no-app-insights"
  detail "Provisioning skip only: an existing project connection and a stored"
  detail "connection string in ${APPSETTINGS_LABEL} are left as they are."
elif ! setup_app_insights; then
  warn "Continuing without tracing -- the agent itself is unaffected."
  APPINSIGHTS_CONNECTION_STRING=""
fi

# ----------------------------------------------------------------------------
# Local configuration
# ----------------------------------------------------------------------------

step "Local telemetry configuration"
if [[ -z "$APPINSIGHTS_CONNECTION_STRING" ]]; then
  skip "nothing to configure (no Application Insights)"
elif [[ "$WRITE_APPSETTINGS" != "true" ]]; then
  skip "disabled with --no-appsettings"
elif set_appsettings_connection_string "$APPINSIGHTS_CONNECTION_STRING"; then
  ok "${APPSETTINGS_LABEL} now points at ${APPINSIGHTS}"
  detail "Restart the app for it to take effect."
else
  skip "${APPSETTINGS_LABEL}"
fi

# ----------------------------------------------------------------------------
# Project connection -- this is where the MCP URL actually lives
#
# Deliberately NOT using 'az cognitiveservices account project connection
# create': that command has no CustomKeys case, and it silently rewrites
# authType "None" to "AAD" for any category outside Git/PythonFeed. A RemoteTool
# connection made that way would be AAD-authenticated and fail against an
# unauthenticated endpoint, with nothing in the output saying so.
# ----------------------------------------------------------------------------

step "MCP connection"
CONN_ID="https://management.azure.com/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RG}/providers/Microsoft.CognitiveServices/accounts/${ACCOUNT}/projects/${PROJECT}/connections/${CONNECTION_NAME}"
CONN_URL="${CONN_ID}?api-version=${ARM_API_VERSION}"
CONN_SECRETS_URL="${CONN_ID}/listSecrets?api-version=${ARM_API_VERSION}"

conn_json="$(az rest --method GET --url "$CONN_URL" -o json 2>/dev/null || true)"
current_target="$(jq -r '.properties.target // empty' <<<"${conn_json:-{\}}" 2>/dev/null || true)"
current_auth="$(jq -r '.properties.authType // empty' <<<"${conn_json:-{\}}" 2>/dev/null || true)"

if [[ -n "$MCP_TOKEN" ]]; then
  desired_auth="CustomKeys"
elif [[ "$MCP_TOKEN_SET" == "true" ]]; then
  desired_auth="None"          # --mcp-token "" explicitly clears the credential
elif [[ "$current_auth" == "CustomKeys" ]]; then
  desired_auth="CustomKeys"    # a token was set on a previous run; don't silently drop it
else
  desired_auth="None"
fi

# A stored credential is never returned by GET, so when a token is supplied we
# cannot tell whether it changed. Always re-PUT in that case -- re-running with
# a freshly minted token is exactly what you do when the old one expires.
if [[ "$current_target" == "$MCP_URL" && "$current_auth" == "$desired_auth" && -z "$MCP_TOKEN" ]]; then
  skip "${CONNECTION_NAME} already points at ${MCP_URL} (auth: ${current_auth:-None})"
else
  if [[ -n "$current_target" && "$current_target" != "$MCP_URL" ]]; then
    detail "${current_target}"
    detail "  -> ${MCP_URL}"
  fi

  if [[ "$desired_auth" == "CustomKeys" && -n "$MCP_TOKEN" ]]; then
    conn_body="$(jq -nc --arg target "$MCP_URL" --arg auth "Bearer ${MCP_TOKEN}" \
      '{properties:{category:"RemoteTool", target:$target, authType:"CustomKeys",
                    credentials:{keys:{Authorization:$auth}}}}')"
  elif [[ "$desired_auth" == "CustomKeys" ]]; then
    # Keep the existing credential, change only the target. PUT replaces the whole
    # connection and the service rejects CustomKeys without credentials ("Credentials
    # Property can't be empty"), so the stored keys have to be read back and sent again.
    kept_keys="$(az rest --method POST --url "$CONN_SECRETS_URL" \
      --query 'properties.credentials.keys' -o json 2>/dev/null || true)"
    if [[ -z "$kept_keys" || "$kept_keys" == "null" || "$kept_keys" == "{}" ]]; then
      die "Could not read back the bearer token stored on ${CONNECTION_NAME}." \
        "An earlier run attached one with --mcp-token, and moving the connection to" \
        "a new URL means sending it again. Re-run with one of:" \
        "" \
        "    --mcp-token \"\$TOKEN\"   attach a fresh token (Labs 5 and 6)" \
        "    --mcp-token \"\"         go back to anonymous (Labs 0-4)"
    fi
    detail "keeping the bearer token from an earlier run (--mcp-token \"\" clears it)"
    conn_body="$(jq -nc --arg target "$MCP_URL" --argjson keys "$kept_keys" \
      '{properties:{category:"RemoteTool", target:$target, authType:"CustomKeys",
                    credentials:{keys:$keys}}}')"
  else
    conn_body="$(jq -nc --arg target "$MCP_URL" \
      '{properties:{category:"RemoteTool", target:$target, authType:"None"}}')"
  fi

  # Body is passed via a file so the token never appears in the process list.
  conn_file="$(mktemp)"; chmod 600 "$conn_file"
  trap 'rm -f "$conn_file"' EXIT
  printf '%s' "$conn_body" >"$conn_file"
  if ! conn_err="$(az rest --method PUT --url "$CONN_URL" --body "@${conn_file}" -o none 2>&1)"; then
    rm -f "$conn_file"; trap - EXIT
    die "Could not create or update the MCP connection." \
      "Service said: ${conn_err}" \
      "" \
      "If this is a 403, you may lack write access to the project."
  fi
  rm -f "$conn_file"; trap - EXIT

  if [[ "$desired_auth" == "CustomKeys" ]]; then
    ok "${CONNECTION_NAME} -> ${MCP_URL} (bearer token attached)"
  else
    ok "${CONNECTION_NAME} -> ${MCP_URL}"
  fi
fi

if [[ -n "$MCP_TOKEN" ]]; then
  warn "A static bearer token is now stored in the project connection."
  detail "Anyone with access to this Foundry project can read it, and it does"
  detail "not refresh. When it expires the agent fails in a way that looks like"
  detail "a broken config -- re-mint and re-run with the new --mcp-token."
fi

# ----------------------------------------------------------------------------
# Agent
#
# POST /agents/{name} is de-duplicating: it adds a version only if the
# definition actually changed, and returns the existing one otherwise. So this
# converges without churning versions on every run. The version selector
# defaults to "always use latest", and we never pin it, so the stable agent
# endpoint always serves what we just wrote.
# ----------------------------------------------------------------------------

step "Agent"

read -r -d '' INSTRUCTIONS <<'EOF' || true
You are a travel booking assistant for an airline.

Use the provided MCP tools to search for flights, inspect flight details, hold a
seat, and confirm or cancel bookings. Always search before you book, and confirm
a hold promptly -- seat holds expire after a few minutes and a confirmation that
arrives too late will fail.

When a tool returns an error, read it carefully and tell the user what actually
went wrong and what they can do about it. Do not invent flight numbers, booking
references, seat numbers or prices: every fact you state must come from a tool
result.
EOF

# The model has no clock. Without this it guesses the year for "November 21st" (and
# guesses the year of its training data), searches a date with no flights, and reports
# "no direct flights" for a route that exists. Re-running deploy.sh refreshes the date.
INSTRUCTIONS="Today is $(date -u +%Y-%m-%d) (UTC). When the user gives a date without a year,
use the next occurrence of that date on or after today. Pass dates to tools as YYYY-MM-DD.

${INSTRUCTIONS}"

# build_agent_body <server_url> <require_approval>
#
# allowed_tools is deliberately omitted. It is case-sensitive and must match
# tools/list exactly, and attendees add tools progressively through Lab 1 --
# pinning a list here would hide tools that do not exist yet.
build_agent_body() {
  jq -nc \
    --arg name "$AGENT_NAME" \
    --arg model "$DEPLOYMENT_NAME" \
    --arg instructions "$INSTRUCTIONS" \
    --arg label "$SERVER_LABEL" \
    --arg url "$1" \
    --arg conn "$CONNECTION_NAME" \
    --arg approval "$2" \
    '{
       name: $name,
       description: "Travel Booking MCP workshop agent",
       definition: {
         kind: "prompt",
         model: $model,
         instructions: $instructions,
         tools: [{
           type: "mcp",
           server_label: $label,
           server_url: $url,
           require_approval: $approval,
           project_connection_id: $conn
         }]
       },
       metadata: { enableVnextExperience: "true" }
     }'
}

AGENT_BODY="$(build_agent_body "$MCP_URL" "$APPROVAL")"

# What the agent ends up with, which is not always what was asked for: an
# existing approval policy is carried forward when --approval was not passed.
EFFECTIVE_APPROVAL="$APPROVAL"

assumption "agent-404" "GET on a missing agent returns 404 (confirmed only via azd source, not docs)"
data_call GET "${PROJECT_ENDPOINT}/agents/${AGENT_NAME}?api-version=${DATA_API_VERSION}"

if [[ "$DATA_STATUS" == "200" ]]; then
  # The agent exists, so converge it.
  #
  # The definition always carries the CURRENT --mcp-url, even though the
  # connection's target is what actually routes traffic (Foundry documents the
  # connection endpoint as winning, and it does). The inline server_url is not
  # dead weight: Foundry quotes it verbatim in MCP tool error messages. An agent
  # whose definition still names a stale host reports every failure against that
  # host -- so a Lab 6 attendee who has moved to Azure Container Apps reads
  # "cannot connect to <old Codespace URL>" and debugs the wrong machine.
  # Keeping the two in step costs a new immutable version per URL change, which
  # we then prune below.
  #
  # require_approval is different: if --approval was not passed, keep whatever
  # policy the agent already has, so Lab 6's URL-only command cannot silently
  # re-enable the prompts Lab 2 turned off.
  existing_tool="$(jq -c '.versions.latest.definition.tools[]?
                          | select(.type == "mcp" and .server_label == "'"$SERVER_LABEL"'")' \
                     <<<"$DATA_BODY" 2>/dev/null | head -1)"

  keep_approval="$APPROVAL"
  if [[ -n "$existing_tool" ]]; then
    prev_url="$(jq -r '.server_url // empty' <<<"$existing_tool" 2>/dev/null || true)"
    # Only a scalar "always"/"never" can be carried forward as-is; the object
    # form is a per-tool filter we never write, so leave it to the caller.
    prev_approval="$(jq -r 'select(.require_approval | type == "string") | .require_approval' \
                      <<<"$existing_tool" 2>/dev/null || true)"
    if [[ -n "$prev_url" && "$prev_url" != "$MCP_URL" ]]; then
      detail "agent definition ${prev_url}"
      detail "                -> ${MCP_URL} (cuts a new version)"
    fi
    if [[ "$APPROVAL_SET" != "true" && -n "$prev_approval" ]]; then
      keep_approval="$prev_approval"
      [[ "$keep_approval" != "$APPROVAL" ]] && detail "keeping existing approval policy: ${keep_approval}"
    fi
  fi

  EFFECTIVE_APPROVAL="$keep_approval"
  AGENT_BODY="$(build_agent_body "$MCP_URL" "$keep_approval")"
  data_call POST "${PROJECT_ENDPOINT}/agents/${AGENT_NAME}?api-version=${DATA_API_VERSION}" "$AGENT_BODY"
  [[ "$DATA_STATUS" == "200" || "$DATA_STATUS" == "201" ]] || die_on_data_error "Updating the agent"
  ok "converged ${AGENT_NAME}"
elif [[ "$DATA_STATUS" == "404" ]]; then
  data_call POST "${PROJECT_ENDPOINT}/agents?api-version=${DATA_API_VERSION}" "$AGENT_BODY"
  # Create is documented as 200, but Azure's own client accepts 201 too.
  [[ "$DATA_STATUS" == "200" || "$DATA_STATUS" == "201" ]] || die_on_data_error "Creating the agent"
  ok "created ${AGENT_NAME}"
else
  die_on_data_error "Looking up the agent"
fi

AGENT_VERSION="$(jq -r '.versions.latest.version // .version // 1' <<<"$DATA_BODY" 2>/dev/null || echo 1)"

# Prune superseded versions.
#
# Changing --mcp-url cuts a new immutable version (see above). Left alone those
# pile up, and every one of them is still openable in the portal's version
# dropdown with the old URL baked in -- an easy way to spend ten minutes
# chatting to a version that points at a host you shut down this morning. Keep
# exactly the version we just converged.
#
# Cleanup failing is not a reason to fail the deployment: the agent is already
# correct at this point, so warn and carry on.
prune_old_versions() {
  local keep="$1" listed version deleted=0

  data_call GET "${PROJECT_ENDPOINT}/agents/${AGENT_NAME}/versions?api-version=${DATA_API_VERSION}&limit=100"
  if [[ "$DATA_STATUS" != "200" ]]; then
    warn "Could not list agent versions (HTTP ${DATA_STATUS}); leaving them in place."
    return 0
  fi

  listed="$(jq -r '.data[]?.version // empty' <<<"$DATA_BODY" 2>/dev/null || true)"

  while IFS= read -r version; do
    [[ -z "$version" || "$version" == "$keep" ]] && continue
    data_call DELETE "${PROJECT_ENDPOINT}/agents/${AGENT_NAME}/versions/${version}?api-version=${DATA_API_VERSION}"
    case "$DATA_STATUS" in
      200|204|404) deleted=$((deleted + 1)) ;;
      *)
        warn "Could not delete agent version ${version} (HTTP ${DATA_STATUS})."
        detail "$(data_error_message)"
        ;;
    esac
  done <<<"$listed"

  if (( deleted > 0 )); then
    ok "removed ${deleted} superseded version(s); only version ${keep} remains"
  fi
  return 0
}

prune_old_versions "$AGENT_VERSION"

# The connection target is what actually routes traffic, so the agent calling
# the right host depends on this value and not on the definition we just wrote.
final_target="$(az rest --method GET --url "$CONN_URL" --query 'properties.target' -o tsv 2>/dev/null || true)"
if [[ -z "$final_target" || "$final_target" == "null" ]]; then
  warn "Could not read back the connection target; the agent may not route correctly."
elif [[ "$final_target" != "$MCP_URL" ]]; then
  die "The connection did not take the URL we asked for." \
    "wanted: ${MCP_URL}" \
    "stored: ${final_target}" \
    "The agent routes via the connection, so it would call the wrong host." \
    "Re-run; if it persists, check write access to the project."
fi

# ----------------------------------------------------------------------------
# Done
# ----------------------------------------------------------------------------

step "Ready"

log ""
log "  Resource group  ${RG}"
log "  Foundry account ${ACCOUNT}"
log "  Project         ${PROJECT}"
log "  Model           ${DEPLOYMENT_NAME} (${CAPACITY}K TPM cap)"
log "  Agent           ${AGENT_NAME} (version ${AGENT_VERSION})"
log "  MCP endpoint    ${MCP_URL}"
log "  Tool approval   ${EFFECTIVE_APPROVAL}"
if [[ -n "$APPINSIGHTS_CONNECTION_STRING" ]]; then
  log "  Tracing         ${APPINSIGHTS}"
else
  log "  Tracing         off"
fi
log ""

if URL="$(playground_url "$SUBSCRIPTION_ID" "$RG" "$ACCOUNT" "$PROJECT" "$AGENT_NAME" "$AGENT_VERSION")"; then
  printf '  %sOpen the playground:%s\n  %s\n\n' "$C_BOLD" "$C_RESET" "$URL" >&2
  detail "The link carries the version number, so use this one -- an older link"
  detail "points at a version that no longer exists."
  log ""
else
  warn "Could not build the playground link (needs python3 or xxd)."
  detail "Open https://ai.azure.com/?tid=${TENANT_ID} and pick project ${PROJECT}."
fi

log "  Then ask it something like:"
log "    \"Find me a flight from Amsterdam to London on November 21st, $(date -u +%Y) and book a window seat.\""
log ""

if [[ -n "$APPINSIGHTS_CONNECTION_STRING" ]]; then
  printf '  %sApplication Insights connection string:%s\n  %s\n\n' \
    "$C_BOLD" "$C_RESET" "$APPINSIGHTS_CONNECTION_STRING" >&2
  if [[ "$WRITE_APPSETTINGS" == "true" ]]; then
    detail "Already stored in ${APPSETTINGS_LABEL}, so a local run exports there"
    detail "as well as to the Aspire dashboard. Restart the app to pick it up."
  else
    detail "Not stored anywhere (--no-appsettings). To use it, set it as"
    detail "AzureMonitor:ConnectionString in ${APPSETTINGS_LABEL}, or export"
    detail "APPLICATIONINSIGHTS_CONNECTION_STRING before starting the app."
  fi
  detail "This string carries an ingestion key -- anyone holding it can write"
  detail "telemetry into the resource. Fine for a workshop; do not commit it."
  log ""
  detail "Agent traces appear under Traces in the playground, and both halves of"
  detail "a request meet in the portal's end-to-end transaction view."
  log ""
fi
if [[ "$EFFECTIVE_APPROVAL" == "always" ]]; then
  detail "Approval is ON: the playground asks before each tool call."
  detail "From Lab 2 onward, re-run with --approval never."
fi
detail "MCP URL changed? Re-run with the new --mcp-url. That cuts a fresh agent"
detail "version, deletes the old one, and prints a new playground link."
detail "Agent suddenly failing after a break? Your Codespace restarted and port"
detail "5197 reverted to private -- the agent then hits GitHub's sign-in page and"
detail "Foundry reports it as a 401. Re-run:"
detail "  gh codespace ports visibility 5197:public -c \"\$CODESPACE_NAME\""
log ""
