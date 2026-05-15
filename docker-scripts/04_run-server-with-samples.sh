#!/bin/bash
################################################################################
# 04 — Run published Docker MCP image with this repo's sample-docs mounted
#
# Launches `ghcr.io/groupdocs-watermark/watermark-net-mcp` as an interactive MCP
# server with the project's sample-docs/ folder mounted at /data. Use this for
# ad-hoc smoke testing of the published container artifact — the existing
# 02/03 scripts only exercise the dnx/NuGet path, never the Docker image.
#
# Two modes:
#   - Default (no args): run server interactively. Press Ctrl+C to stop.
#   - --smoke           : pipe an initialize + tools/list JSON-RPC pair through
#                         stdin and verify both tools are advertised. Returns 0
#                         on success.
#
# Prerequisites:
#   - Docker daemon running (Docker Desktop on Windows / macOS)
#   - Drive containing this repo shared with Docker Desktop (WSL2 = automatic)
#
# Usage:
#   ./04_run-server-with-samples.sh [OPTIONS]
#
# Options:
#   --image-tag TAG         Image tag (default: latest). Pin to e.g. "26.5.0"
#                           for reproducible runs. Each release pushes both the
#                           version tag AND :latest.
#   --no-pull               Skip the implicit `--pull always` (use cached image)
#   --smoke                 Run a JSON-RPC smoke test instead of an interactive
#                           server. Exits non-zero if the server doesn't
#                           advertise both `search_watermarks` and `add_watermark`.
#   --license PATH          Mount a GroupDocs license file (enables
#                           add_watermark; without it the server runs in
#                           evaluation mode)
#   --help                  Show this help message
#
# Examples:
#   ./04_run-server-with-samples.sh                      # interactive, latest
#   ./04_run-server-with-samples.sh --smoke              # CI-style smoke check
#   ./04_run-server-with-samples.sh --image-tag 26.5.0   # pinned
#   ./04_run-server-with-samples.sh --license /path/to/GroupDocs.Total.lic
#
################################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
SAMPLE_DOCS_DIR="$PROJECT_ROOT/sample-docs"
IMAGE_REPO="ghcr.io/groupdocs-watermark/watermark-net-mcp"

# Defaults
IMAGE_TAG="latest"
PULL_FLAG="--pull always"
RUN_MODE="interactive"
LICENSE_PATH=""

source "$SCRIPT_DIR/helpers.sh"

show_help() {
    grep '^#' "$0" | grep -E "^\s*#\s+(Usage|Options|Examples|Prerequisites|04 |Two modes|Launches)" | sed 's/^# \?//'
}

# Parse args
while [[ $# -gt 0 ]]; do
    case $1 in
        --image-tag) IMAGE_TAG="$2"; shift 2 ;;
        --no-pull) PULL_FLAG=""; shift ;;
        --smoke) RUN_MODE="smoke"; shift ;;
        --license)
            LICENSE_PATH="$2"
            [ ! -f "$LICENSE_PATH" ] && log_error "License file not found: $LICENSE_PATH" && exit 1
            shift 2
            ;;
        --help) show_help; exit 0 ;;
        *) log_error "Unknown option: $1"; show_help; exit 1 ;;
    esac
done

log_header "MCP Docker Smoke ($IMAGE_REPO:$IMAGE_TAG)"

# Pre-flight
check_docker_daemon || exit 1

if [ ! -d "$SAMPLE_DOCS_DIR" ]; then
    log_error "sample-docs directory not found: $SAMPLE_DOCS_DIR"
    exit 1
fi

local_count=$(find "$SAMPLE_DOCS_DIR" -type f -not -name '.gitkeep' 2>/dev/null | wc -l)
if [ "$local_count" -eq 0 ]; then
    log_warning "sample-docs is empty — drop test files in $SAMPLE_DOCS_DIR before running"
else
    log_success "Found $local_count sample document(s) in $SAMPLE_DOCS_DIR"
fi

# Build the docker run argv as an array so multi-token flags survive quoting.
docker_args=(run --rm -i)

# `--pull always` is opt-out via --no-pull. Runs an explicit registry probe each
# launch so :latest stays current; subsequent runs are cached.
if [ -n "$PULL_FLAG" ]; then
    # shellcheck disable=SC2206  # intentional word-splitting on PULL_FLAG
    docker_args+=($PULL_FLAG)
fi

# Mount the project's sample-docs folder at /data so the server's default
# storage path picks them up automatically. Read-only — server writes
# nothing to /data when only running search_watermarks / non-licensed add_watermark.
docker_args+=(-v "$SAMPLE_DOCS_DIR:/data:ro")

# Optional license mount + env.
if [ -n "$LICENSE_PATH" ]; then
    docker_args+=(-v "$LICENSE_PATH:/license/GroupDocs.Total.lic:ro")
    docker_args+=(-e "GROUPDOCS_LICENSE_PATH=/license/GroupDocs.Total.lic")
    log_info "License mounted — add_watermark will work in licensed mode"
else
    log_info "No license — add_watermark will return evaluation-mode error (search_watermarks works fine)"
fi

docker_args+=("$IMAGE_REPO:$IMAGE_TAG")

if [ "$RUN_MODE" = "smoke" ]; then
    log_section "Running JSON-RPC smoke test (initialize + tools/list)"
    set +e
    response=$(
        (
            echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"04-smoke","version":"1"}}}'
            echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'
            echo '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
            sleep 2
        ) | docker "${docker_args[@]}" 2>/dev/null
    )
    set -e

    if echo "$response" | grep -q '"search_watermarks"' && echo "$response" | grep -q '"add_watermark"'; then
        log_success "Both tools advertised — smoke test PASSED"
        exit 0
    else
        log_error "Smoke test FAILED — expected tools not found in response"
        echo "Response was:"
        echo "$response"
        exit 1
    fi
fi

# Interactive mode
log_section "Starting interactive MCP server"
log_info "Press Ctrl+C to stop. The server reads JSON-RPC from stdin."
log_info "From a separate shell, you can pipe an MCP client at this process."
echo ""

exec docker "${docker_args[@]}"
