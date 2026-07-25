# Upgrade and rollback

## Before upgrading

1. Stop new simulated runs.
2. Export diagnostics if the current history matters.
3. Back up the `machineops-postgres` volume or its database.
4. Record the current image and desktop versions.
5. Verify the checksum of the new release.

## Upgrade

Desktop packages replace application files and keep PostgreSQL outside the
desktop installation. For Compose, pull or build the tagged images and run:

```shell
docker compose up -d --wait
```

The API applies ordered forward migrations before listening for requests.
`schema_versions` records each applied script and checksum.

## Rollback

Application rollback is done by reinstalling the prior desktop package and
starting the prior API image. Database migrations are forward-only because a
blind down migration can destroy operational evidence.

If a release changed the schema incompatibly, restore the matching database
backup before starting the older API. The v1.0 migrations are additive, but an
older binary has not been certified against a newer schema.

Do not remove the Compose volume unless permanent deletion of local history is
intended.
