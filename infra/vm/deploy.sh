#!/usr/bin/env bash
# Pulls the images named in .env and starts PAKAR from /opt/pakar. Safe to run again after a change.
set -euo pipefail

cd "${PAKAR_DIR:-/opt/pakar}"

for file in compose.yml .env assets/models/best.onnx; do
  if [ ! -f "$file" ]; then
    echo "Missing $PWD/$file" >&2
    exit 1
  fi
done

# Secrets stay readable by the owner only; the containers need to read the model and videos.
chmod 600 .env
chmod -R a+rX assets

docker compose -f compose.yml pull
docker compose -f compose.yml up -d --no-build --remove-orphans --wait --wait-timeout 300
docker compose -f compose.yml ps

echo "Running at $(grep '^SITE_URL=' .env | cut -d= -f2-)"
