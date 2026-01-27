#!/usr/bin/env bash
set -euo pipefail

# ──────────────────────────────
# Colors
# ──────────────────────────────
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

# ──────────────────────────────
# Helpers
# ──────────────────────────────
log_info() {
    echo -e "${BLUE}→ $1${NC}"
}

log_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

log_error() {
    echo -e "${RED}$1${NC}"
}

build_project() {
    log_info "Building project..."
    dotnet build
}

# ──────────────────────────────
# Commands
# ──────────────────────────────
run_build() {
    build_project
    log_success "Build complete!"
}

run_migration() {
    local name="${1:-Migration_$(date +%Y%m%d_%H%M%S)}"

    build_project

    log_info "Creating migration: $name"
    dotnet ef migrations add "$name"

    log_info "Updating database..."
    dotnet ef database update

    log_success "Migration complete!"
}

run_tests() {
    build_project

    log_info "Running tests..."
    dotnet test

    log_success "Tests complete!"
}

show_help() {
    cat <<EOF
Usage: ./run.sh [COMMAND] [OPTIONS]

Commands:
  build, b          Build the project only
  mig, m [name]     Run migration workflow
  test, t           Run tests
  help, h           Show this help

Examples:
  ./run.sh build
  ./run.sh b
  ./run.sh mig AddUserEmail
  ./run.sh test
EOF
}

# ──────────────────────────────
# Main
# ──────────────────────────────
case "${1:-}" in
    build|b)
        run_build
        ;;
    mig|m)
        run_migration "${2:-}"
        ;;
    test|t)
        run_tests
        ;;
    help|h|"")
        show_help
        ;;
    *)
        log_error "Unknown command: $1"
        show_help
        exit 1
        ;;
esac
