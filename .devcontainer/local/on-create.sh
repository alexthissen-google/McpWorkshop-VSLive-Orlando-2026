#!/bin/sh

# Change ownership of the .dotnet directory to the vscode user (to avoid permission errors)
sudo chown -R vscode:vscode /home/vscode/.dotnet

# Import and trust the ASP.NET Core HTTPS development certificate
# Run this command if the certificate has not been generated yet
# pwsh .devcontainer/local/prepare-devcert.ps1 -password $DOTNET_DEVCERT_PASSWORD
dotnet dev-certs https --import /https/dotnet-devcert.pfx --clean --password "$DOTNET_DEVCERT_PASSWORD" --verbose
dotnet dev-certs https --trust --verbose