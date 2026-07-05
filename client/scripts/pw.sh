#!/usr/bin/env sh
# Run Playwright inside the pinned container so local runs match CI byte-for-byte
# and visual baselines are deterministic. Usage: sh scripts/pw.sh test tests/e2e
# Tests live at the repo root (../tests relative to client/), so the container
# mounts the repo root and runs pnpm against the client/ workspace inside it.
set -eu
PW_IMAGE="mcr.microsoft.com/playwright:v1.61.1-noble"
REPO_ROOT=$(cd "$PWD/.." && pwd)
exec docker run --rm --init \
  -v "$REPO_ROOT":/work -w /work/client \
  -e HOME=/work -e CI="${CI:-}" \
  -e PNPM_HOME=/work/.pnpm \
  --user "$(id -u):$(id -g)" \
  "$PW_IMAGE" \
  sh -c "CI=true corepack pnpm install --frozen-lockfile --store-dir /tmp/pnpm-store && corepack pnpm exec playwright $*"
