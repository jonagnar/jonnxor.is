#!/usr/bin/env sh
# Run Playwright inside the pinned container so local runs match CI byte-for-byte
# and visual baselines are deterministic. Usage: sh scripts/pw.sh test tests/e2e
set -eu
PW_IMAGE="mcr.microsoft.com/playwright:v1.61.1-noble"
exec docker run --rm --init \
  -v "$PWD":/work -w /work \
  -e HOME=/work -e CI="${CI:-}" \
  -e PNPM_HOME=/work/.pnpm \
  --user "$(id -u):$(id -g)" \
  "$PW_IMAGE" \
  sh -c "CI=true corepack pnpm install --frozen-lockfile --store-dir /tmp/pnpm-store && corepack pnpm exec playwright $*"
