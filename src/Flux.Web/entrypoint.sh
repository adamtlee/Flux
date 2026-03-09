#!/bin/sh
set -e

# Ensure data dir exists and has correct permissions
mkdir -p /app/data
chown -R www-data:www-data /app/data || true

# Start the API in background
echo "Starting Flux.Api..."
dotnet /app/Flux.Api.dll &
API_PID=$!

# Start nginx in foreground (keeps container alive)
echo "Starting nginx..."
nginx -g 'daemon off;'

# Wait for background process if nginx exits
wait $API_PID
