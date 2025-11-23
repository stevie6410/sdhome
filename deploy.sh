#!/usr/bin/env bash
set -euo pipefail

cd /home/signals  # adjust if needed

echo "[deploy] Fetching latest code..."
git fetch origin
git reset --hard origin/main

echo "[deploy] Pulling latest images..."
docker compose pull

echo "[deploy] Building Signals image..."
docker compose build signals

echo "[deploy] Applying stack..."
docker compose up -d --remove-orphans

echo "[deploy] Deployment complete."