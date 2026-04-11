#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT_DIR"

RUNSETTINGS="KanbanApi.Tests/coverlet.runsettings"
REPORT_DIR="KanbanApi.Tests/TestResults/CoverageReport"
LCOV_OUT="KanbanApi.Tests/TestResults/CoverageReport/lcov.info"

if ! command -v reportgenerator >/dev/null 2>&1; then
  echo "reportgenerator not found. Install once with: dotnet tool install -g dotnet-reportgenerator-globaltool"
  exit 1
fi

rm -rf "$REPORT_DIR"
mkdir -p "$REPORT_DIR"

echo "Running tests with coverage (migrations excluded)..."
dotnet test --collect:"XPlat Code Coverage" --settings "$RUNSETTINGS"

LATEST_RESULTS_DIR=$(find KanbanApi.Tests/TestResults -mindepth 1 -maxdepth 1 -type d -printf '%T@ %p\n' | sort -n | tail -n 1 | cut -d' ' -f2-)

if [[ -z "${LATEST_RESULTS_DIR:-}" ]]; then
  echo "No test result directory found in KanbanApi.Tests/TestResults"
  exit 1
fi

LATEST_COBERTURA="$LATEST_RESULTS_DIR/coverage.cobertura.xml"
LATEST_LCOV="$LATEST_RESULTS_DIR/coverage.info"

if [[ ! -f "$LATEST_COBERTURA" ]]; then
  echo "No coverage.cobertura.xml found in KanbanApi.Tests/TestResults"
  exit 1
fi

echo "Generating HTML report..."
reportgenerator \
  -reports:"$LATEST_COBERTURA" \
  -targetdir:"$REPORT_DIR" \
  -reporttypes:"Html;TextSummary;JsonSummary" \
  -filefilters:"-*/Migrations/*;-*/obj/*"

if [[ -f "$LATEST_LCOV" ]]; then
  cp "$LATEST_LCOV" "$LCOV_OUT"
  echo "LCOV report ready: $LCOV_OUT"
else
  echo "LCOV file not found at expected path: $LATEST_LCOV"
fi

echo "Coverage report ready: $REPORT_DIR/index.html"
