#!/bin/sh
set -eu
for database in catalog_db ordering_db inventory_db payment_db identity_db; do
  exists="$(psql -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$database'")"
  if [ "$exists" != "1" ]; then
    createdb "$database"
  fi
done
