#!/bin/sh
set -eu

certificate_path=/https/dotnet-devcert.pfx

dotnet dev-certs https --import "$certificate_path" --clean --password "$DOTNET_DEVCERT_PASSWORD" --quiet
dotnet dev-certs https --trust --verbose
