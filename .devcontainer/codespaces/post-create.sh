#!/bin/sh
set -eu

export DEBIAN_FRONTEND=noninteractive

dotnet tool restore

sudo apt-get update && sudo apt-get install -y jq protobuf-compiler bubblewrap socat

# Aspire CLI (Lab 0+). The native CLI from aspire.dev, pinned to the version of the
# `aspire.cli` local tool in .config/dotnet-tools.json so `aspire` and `dotnet aspire` agree.
# It lands in ~/.aspire/bin; the symlink puts it on PATH for every shell, interactive or not.
aspire_version=$(jq -r '.tools["aspire.cli"].version' .config/dotnet-tools.json)
if [ "$(aspire --version 2>/dev/null | cut -d+ -f1)" = "$aspire_version" ]; then
  echo "Aspire CLI $aspire_version already installed, skipping."
else
  echo "Installing Aspire CLI $aspire_version..."
  curl -sSL https://aspire.dev/install.sh | bash -s -- --version "$aspire_version"
  sudo ln -sf /home/vscode/.aspire/bin/aspire /usr/local/bin/aspire
fi

# Azure Developer CLI (Lab 6). Pinned, not latest: azd ships often, and the version the
# labs were dry-run against is the one attendees should deploy with. Lab 6 names the same
# version as its fallback install command; change both together.
azd_version=1.38.0
if [ "$(azd version 2>/dev/null | awk '{print $3}')" = "$azd_version" ]; then
  echo "Azure Developer CLI $azd_version already installed, skipping."
else
  echo "Installing Azure Developer CLI $azd_version..."
  curl -fsSL https://aka.ms/install-azd.sh | sudo bash -s -- --version "$azd_version"
fi

# MCP Inspector (Lab 1+). Installed globally so `npx @modelcontextprotocol/inspector`
# (the command the labs use) runs this copy instead of downloading one mid-workshop.
# npx reuses a global install only when it matches the registry's latest version, so
# install latest rather than a pin. Idempotent: npm upgrades in place or does nothing.
echo "Installing MCP Inspector..."
npm install -g @modelcontextprotocol/inspector@latest

# HTTPS dev certificate. On Linux, trust only takes effect for OpenSSL clients when
# $HOME/.aspnet/dev-certs/trust is listed in SSL_CERT_DIR, which devcontainer.json sets.
echo "Trusting the HTTPS development certificate..."
dotnet dev-certs https --trust
