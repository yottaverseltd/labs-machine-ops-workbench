# UI to API request

Follow `SaveJobCommand` for the smallest complete request path.

The Avalonia button binds to a generated `IAsyncRelayCommand`. The view model
owns busy and error presentation, but it does not construct an `HttpRequestMessage`.
It calls `IMachineOpsApiClient.CreateJobAsync` with a contract DTO.

`MachineOpsApiClient` owns the HTTP path, JSON serialisation, status handling,
and empty-response check. This keeps transport details out of presentation
logic and lets a view-model test substitute a small fake.

At the server, `JobsController` owns status codes:

- model validation failures become HTTP 400;
- invalid G-code becomes HTTP 422 with diagnostics in Problem Details;
- a saved job becomes HTTP 201 with a location header.

The controller delegates parsing, time, identity creation, and persistence to
`CreateJobHandler`. It maps the result explicitly. There is no reflection-based
mapping library, so contract changes are visible in code review.

Cancellation crosses each boundary. ASP.NET supplies a request token, the
handler passes it to the repository, and Npgsql receives it in the Dapper
command definition.
