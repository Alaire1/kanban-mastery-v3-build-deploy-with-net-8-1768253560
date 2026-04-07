# Map short aliases to test class names
get_test_class_name() {
    local alias="$1"
    case "$alias" in
        cards)
            echo "CardsEndpointTests"
            ;;
        columns)
            echo "ColumnsEndpointTests"
            ;;
        boardid)
            echo "BoardIdEndpointTests"
            ;;
        boardmembers)
            echo "BoardMembersTests"
            ;;
        board)
            echo "BoardTests"
            ;;
        auth)
            echo "AuthTests"
            ;;
        *)
            echo "$alias" # fallback to user input
            ;;
    esac
}
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
    local test_filter="${2:-}"

    if [[ "$skip_build" -ne 1 ]]; then
        build_project
    fi

    log_info "Running tests..."
    if [[ -n "$test_filter" ]]; then
        local class_name
        class_name=$(get_test_class_name "$test_filter")
        run_with_log_profile dotnet test --filter "FullyQualifiedName~$class_name"
    else
        run_with_log_profile dotnet test
    fi

    log_success "Tests complete!"
}

run_migration_and_tests() {
    local name="${1:-}"
    local test_filter="${2:-}"

    run_migration "$name"
    run_tests 1 "$test_filter"
}

run_backend() {
    log_info "Starting backend (KanbanApi)..."
    (cd KanbanApi && dotnet run)
}

run_frontend() {
    log_info "Starting frontend (my-kanban)..."
    (cd my-kanban && npm start)
}

run_all_services() {
    log_info "Starting backend and frontend..."

    (cd KanbanApi && dotnet run) &
    local backend_pid=$!

    (cd my-kanban && npm start) &
    local frontend_pid=$!

    cleanup() {
        log_info "Stopping services..."
        kill "$backend_pid" "$frontend_pid" 2>/dev/null || true
        wait "$backend_pid" "$frontend_pid" 2>/dev/null || true
    }

    trap cleanup INT TERM EXIT

    log_success "Backend PID: $backend_pid | Frontend PID: $frontend_pid"
    wait -n "$backend_pid" "$frontend_pid"
}

show_help() {
    cat <<EOF
Usage: ./run.sh [COMMAND] [OPTIONS]

Commands:
  build, b          Build the project only
  mig, m [name]     Run migration workflow
    test, t [test]    Run tests (optionally only for [test] class, or alias: cards, columns, boardid, boardmembers, board, auth)
    mt, tm [name] [test]  Run migrations, update DB, then run tests (optionally only for [test] class or alias)
    api               Run backend only (KanbanApi)
    web               Run frontend only (my-kanban)
    dev               Run backend + frontend together
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
        ./run.sh test CardsEndpointTests
        ./run.sh mt
        ./run.sh mt AddUserEmail -v
        ./run.sh mt AddUserEmail CardsEndpointTests
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
        run_tests 0 "${args[0]:-}"
        ;;
    mt|tm)
        run_migration_and_tests "${args[0]:-}" "${args[1]:-}"
        ;;
    api)
        run_backend
        ;;
    web)
        run_frontend
        ;;
    dev)
        run_all_services
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
