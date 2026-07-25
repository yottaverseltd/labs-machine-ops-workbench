# 03: Replace the adapter, keep the use case

Version 0.3 moves durable job storage into Infrastructure. The controller and
application handler from version 0.2 are unchanged.

## The dependency direction

Application owns `IJobRepository` because the use case defines what storage
must do. Infrastructure implements that port with `DapperJobRepository`. The
API composition root chooses the implementation and owns the connection string.

This direction matters. Core and Application can be tested without PostgreSQL,
while Infrastructure tests use a real disposable database.

## Why SQL is explicit

Dapper keeps the persistence code close to SQL. Every selected column is
aliased to the row model, every parameter is named, and JSON is limited to
toolpath and diagnostic value collections. Job identity, state, timestamps,
and query indexes remain normal relational columns.

## Migrations

`DatabaseMigrator` loads ordered embedded SQL resources and records each
successful version in `schema_versions`. A migration and its version record are
committed in one transaction. Running the migrator again is safe.

## Try it

Start PostgreSQL:

```shell
docker compose up -d postgres
```

Then run the API. It applies pending migrations before listening:

```shell
dotnet run --project src/Yottaverse.MachineOps.Api
```

Readiness is available at `http://localhost:5080/health/ready`.
