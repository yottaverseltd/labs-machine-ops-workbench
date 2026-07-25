# Importing a job

There are two useful boundaries in job import: immediate local inspection and
durable server storage.

## Local inspection

1. `GCodeFilePicker` returns a name and text. It has no parsing rules.
2. `MainViewModel.LoadProgram` calls the Core `GCodeParser`.
3. The parser removes comments, tracks units and positioning mode, and creates
   immutable `ToolpathSegment` values.
4. A problem becomes a `GCodeDiagnostic` with its source line, severity, code,
   and useful message.
5. `ToolpathView` renders the segment list without changing it.

The preview still works when the API, PostgreSQL, and simulator are stopped.
That is an intentional product capability and a useful failure boundary.

## Saving the same program

1. `SaveJobCommand` creates a `CreateJobRequest`.
2. `MachineOpsApiClient` serialises the request to `POST /api/jobs`.
3. `JobsController` applies HTTP validation and calls `CreateJobHandler`.
4. The handler parses the source again. The server never trusts a client-only
   validation result.
5. `MachiningJob.Create` gives the job an identity and creation time.
6. `DapperJobRepository.AddAsync` writes the job and its derived data in one
   PostgreSQL transaction.
7. The API maps the saved aggregate to `JobDto`.

Parsing twice is deliberate. The desktop gives fast feedback. The server
protects durable state for every possible client.
