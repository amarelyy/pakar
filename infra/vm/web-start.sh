#!/bin/sh
# Points the web app at the public address, then starts Caddy.
set -eu
: "${SITE_URL:?SITE_URL is required}"
sed -i "s#\"BaseUrl\": *\"[^\"]*\"#\"BaseUrl\": \"${SITE_URL%/}/\"#" /srv/appsettings.json
exec caddy run --config /etc/caddy/Caddyfile --adapter caddyfile
