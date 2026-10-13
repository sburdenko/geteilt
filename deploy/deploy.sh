#!/bin/sh
# The one deploy command. Run on the server from the repository's deploy/ directory:
#
#   TAG=<image tag> ./deploy.sh
#
# PULL=0 skips pulling, for a rehearsal on a laptop with images built locally (see README).
#
# Order matters: migrations first, then IBANs from before encryption
# are encrypted, and only then does the new API take requests. Each step stops the deploy if it
# fails, and the last one waits until the API reports ready — which it does not while any IBAN is
# still stored in plain text.
set -eu

cd "$(dirname "$0")"
compose="docker compose --env-file .env"

if [ "${PULL:-1}" = "1" ]; then
  $compose pull api web
fi
# --wait: up alone returns once the container exists, and a fresh Postgres takes a few seconds
# more before it accepts the migration's connection.
$compose up -d --wait postgres

# The old API keeps serving until the new one replaces it; the database already refuses a plain
# IBAN from it, so the overlap cannot store one.
$compose run --rm --no-deps api migrate
$compose run --rm --no-deps api protect-ibans

$compose up -d api web

echo "Waiting for the API to report ready..."
attempts=0
until $compose exec -T web wget -q -O /dev/null "http://api:8080/api/health/ready"; do
  attempts=$((attempts + 1))
  if [ "$attempts" -ge 30 ]; then
    echo "The API did not become ready. Logs: $compose logs api" >&2
    exit 1
  fi
  sleep 2
done

echo "Deployed and ready."
