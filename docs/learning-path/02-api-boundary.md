# 02: Put HTTP at the edge

Version 0.2 moves job storage behind an HTTP boundary without disturbing the
local G-code preview.

## Follow one save request

1. `MainViewModel` creates a `CreateJobRequest`.
2. `MachineOpsApiClient` serialises that DTO and posts it to `/api/jobs`.
3. `JobsController` turns the request into a `CreateJobCommand`.
4. `CreateJobHandler` parses and validates the program, then calls
   `IJobRepository`.
5. The controller maps the saved domain object to `JobDto`.

The DTO is not the domain object. It is a stable message designed for transport.
The controller knows about HTTP status codes. The application handler knows
whether a job is valid. The repository knows how to retain it.

## Why the repository is volatile here

This release uses an in-process repository so the API can be run and tested
before a database is introduced. The interface already sits in Application,
which means the Dapper adapter in the next release can replace the volatile
adapter at composition time. The use case and controller do not need to change.

## Try it

Run the API:

```shell
dotnet run --project src/Yottaverse.MachineOps.Api --urls http://localhost:5080
```

Then run the desktop app in another terminal and choose **Save job**. Stop the
API and try again to see the offline behaviour. Local inspection still works.
