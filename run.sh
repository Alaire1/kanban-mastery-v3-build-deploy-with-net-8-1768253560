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

run_with_log_profile() {
    if [[ "${SHOW_VERBOSE_LOGS:-0}" -eq 1 ]]; then
        "$@"
    else
        "$@" 2>&1 | awk '
            BEGIN { skip_next = 0 }
            {
                if (skip_next && $0 ~ /^[[:space:]]{2,}/) {
                    next
                }

                skip_next = 0

                if ($0 ~ /^(info|warn): Microsoft\.EntityFrameworkCore\.Update\[/) {
                    skip_next = 1
                    next
                }

                if ($0 ~ /^(info|warn): Microsoft\.AspNetCore\.HttpsPolicy\.HttpsRedirectionMiddleware\[/) {
                    skip_next = 1
                    next
                }

                print
            }
        '
    fi
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
    run_with_log_profile dotnet-ef migrations add "$name" \
        --project KanbanApi \
        --startup-project KanbanApi

    log_info "Updating database..."
    run_with_log_profile dotnet-ef database update \
        --project KanbanApi \
        --startup-project KanbanApi

    log_success "Migration complete!"
}


run_tests() {
    local skip_build="${1:-0}"

    if [[ "$skip_build" -ne 1 ]]; then
        build_project
    fi

    log_info "Running tests..."
    run_with_log_profile dotnet test

    log_success "Tests complete!"
}

run_migration_and_tests() {
    local name="${1:-}"

    run_migration "$name"
    run_tests 1
}

show_help() {
    cat <<EOF
Usage: ./run.sh [COMMAND] [OPTIONS]

Commands:
  build, b          Build the project only
  mig, m [name]     Run migration workflow
  test, t           Run tests
    mt, tm [name]     Run migrations, update DB, then run tests
  help, h           Show this help

Options:
    --verbose-logs, -v  Show full framework logs (default for m/t is reduced logs)

Examples:
  ./run.sh build
  ./run.sh b
  ./run.sh mig AddUserEmail
    ./run.sh mig AddUserEmail --verbose-logs
  ./run.sh test
    ./run.sh test -v
    ./run.sh mt
    ./run.sh mt AddUserEmail -v
EOF
}

# ──────────────────────────────
# Main
# ──────────────────────────────
SHOW_VERBOSE_LOGS=0
command="${1:-}"
shift || true

args=()
for arg in "$@"; do
    case "$arg" in
        --verbose-logs|-v)
            SHOW_VERBOSE_LOGS=1
            ;;
        *)
            args+=("$arg")
            ;;
    esac
done

case "$command" in
    build|b)
        run_build
        ;;
    mig|m)
        run_migration "${args[0]:-}"
        ;;
    test|t)
        run_tests
        ;;
    mt|tm)
        run_migration_and_tests "${args[0]:-}"
        ;;
    help|h|"")
        show_help
        ;;
    *)
        log_error "Unknown command: $command"
        show_help
        exit 1
        ;;
esac
