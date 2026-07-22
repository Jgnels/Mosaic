#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

python3 tools/static_verify.py
dotnet build Dagmay.Core/Dagmay.Core.csproj --configuration Release
dotnet build Dagmay.Providers/Dagmay.Providers.csproj --configuration Release
dotnet run --project Dagmay.Tests/Dagmay.Tests.csproj --configuration Release

echo "Core, providers, and executable contract tests completed."

