# Set password to environment variable or fallback to default
[CmdletBinding()]
param(
    [string]$password = $(
        if ($env:DOTNET_DEVCERT_PASSWORD) { $env:DOTNET_DEVCERT_PASSWORD } 
        else { "DefaultPassword!" }
    )
)

$certificateDirectory = Join-Path $HOME ".aspnet/https"
New-Item -ItemType Directory -Path $certificateDirectory -Force | Out-Null
 
# Export ASP.NET Core development certificate to PFX file in mount directory
dotnet dev-certs https --export-path (Join-Path $certificateDirectory "dotnet-devcert.pfx") --password $password --verbose