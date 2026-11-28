#!/usr/bin/env bash
# Prepares an Ubuntu server for PAKAR: Docker, swap and the /opt/pakar folder. Safe to run twice.
set -euo pipefail

swap_file=/swapfile

# 1. Docker Engine with the compose plugin, started on every boot.
if ! command -v docker >/dev/null; then
  curl -fsSL https://get.docker.com | sudo sh
fi
sudo systemctl enable --now docker
sudo usermod -aG docker "$USER"
docker compose version

# 2. Application folder owned by the current user.
sudo mkdir -p /opt/pakar/assets/models /opt/pakar/assets/videos
sudo chown -R "$USER": /opt/pakar

# 3. Swap, because the machine has only 4 GiB of memory.
if ! sudo swapon --show=NAME --noheadings | grep -qx "$swap_file"; then
  if [ ! -f "$swap_file" ]; then
    sudo fallocate -l 2G "$swap_file"
    sudo chmod 600 "$swap_file"
    sudo mkswap "$swap_file"
  fi
  sudo swapon "$swap_file"
fi
if ! grep -q "^$swap_file " /etc/fstab; then
  echo "$swap_file none swap sw 0 0" | sudo tee -a /etc/fstab >/dev/null
fi

echo "Setup done."
