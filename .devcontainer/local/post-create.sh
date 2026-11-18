#!/bin/sh
set -eu

export DEBIAN_FRONTEND=noninteractive

dotnet tool restore

sudo apt-get update && sudo apt-get install -y ripgrep protobuf-compiler bubblewrap socat

# MCP Inspector (Lab 1+). Installed globally so `npx @modelcontextprotocol/inspector`
# (the command the labs use) runs this copy instead of downloading one mid-workshop.
# npx reuses a global install only when it matches the registry's latest version, so
# install latest rather than a pin. Idempotent: npm upgrades in place or does nothing.
echo "Installing MCP Inspector..."
npm install -g @modelcontextprotocol/inspector@latest

# Check if the ASP.NET Core HTTPS development certificate is already trusted by ASP.NET Core and OpenSSL
dotnet dev-certs https --check --trust --verbose
