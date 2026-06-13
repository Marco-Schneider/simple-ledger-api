# Simple Ledger API

[![.NET Build & Test](https://github.com/Marco-Schneider/simple-ledger-api/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/Marco-Schneider/simple-ledger-api/actions/workflows/dotnet.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/en-us/aspnet/core/)

A simple banking API built with **ASP.NET Core (.NET 8)** developed as part of a software engineering take-home assignment. The implementation prioritises correctness, thread safety, and testability over speculative abstraction.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
  - [Project structure](#project-structure)
  - [Design philosophy](#design-philosophy)
  - [Domain model](#domain-model)
- [API reference](#api-reference)
  - [POST /reset](#post-reset)
  - [GET /balance](#get-balance)
  - [POST /event](#post-event)
- [Key engineering decisions](#key-engineering-decisions)
  - [Concurrency & data integrity](#concurrency--data-integrity)
  - [Result\<T\> pattern | No exception-driven flow control](#resultt-pattern--no-exception-driven-flow-control)
  - [JSON serialisation contract](#json-serialisation-contract)
- [Testing strategy](#testing-strategy)
  - [Unit tests | state-based, not interaction-based](#unit-tests--state-based-not-interaction-based)
  - [Integration tests | Lifecycle-isolated end-to-end](#integration-tests--lifecycle-isolated-end-to-end)
- [How to run locally](#how-to-run-locally)
  - [Prerequisites](#prerequisites)
  - [Running the API](#running-the-api)
  - [Exposing to the internet via ngrok](#exposing-to-the-internet-via-ngrok)
- [Running the tests](#running-the-tests)
- [Development process](#development-process)
- [Technical debt & trade-Offs](#technical-debt--trade-offs)

---

## Overview

The Simple Ledger API manages financial accounts with three core operations: **deposit**, **withdrawal**, and **transfer**. All state is held in-memory using a thread-safe singleton store. The API is designed to satisfy an automated grading script that strictly validates HTTP status codes, response bodies, and JSON field conventions.

---

## Architecture

### Project Structure

```
SimpleLedgerAPI/
├── Controllers/
│   └── LedgerController.cs       # Single unified controller for all ledger operations
├── Domain/
│   ├── Account.cs                # Immutable account record
│   ├── EventRequest.cs           # Inbound event payload
│   ├── EventResponse.cs          # Outbound event payload
│   └── Result.cs                 # Generic Result<T>
├── Services/
│   ├── IAccountService.cs        # Service contract
│   └── AccountService.cs         # Business logic & concurrency management
├── Store/
│   ├── IAccountStore.cs          # Store abstraction
│   └── InMemoryAccountStore.cs   # ConcurrentDictionary-backed in-memory store
└── Program.cs                    # DI wiring & JSON serialisation configuration

SimpleLedgerAPI.Test/
├── Unit/
│   ├── AccountServiceTests.cs    # Service-layer state-based unit tests
│   └── AccountStoreTests.cs      # Store-layer unit tests
└── Integration/
    └── LedgerControllerTests.cs  # End-to-end lifecycle test via WebApplicationFactory
```

### Design philosophy

This is a deliberate **single-controller design**. A unified `LedgerController` handles all ledger operations via a single `/event` dispatch endpoint, keeping the API surface clean and the routing mental model trivial. Splitting this into `DepositController`, `WithdrawController`, etc. would be architectural ceremony for its own sake.

Similarly, the `AccountService` intentionally uses an **Anemic Domain Model** pattern. The business logic (balance arithmetic, guard clauses) lives in the service methods rather than inside the `Account` record. For a three-operation API, introducing a Rich Domain Model with domain events, value objects, and aggregate roots would be over-engineering that creates abstraction overhead without a proportional benefit in this context.

The `Reset()` method is intentionally placed on `IAccountService` rather than extracted into a separate `IAdminService` interface. Creating a dedicated admin interface purely to satisfy the Interface Segregation Principle would add indirection and cognitive overhead without a meaningful architectural payoff at this scale.

### Domain model

| Type | Kind | Purpose |
|---|---|---|
| `Account` | `record` | Immutable value object: `(string Id, decimal Balance)` |
| `EventRequest` | `record` | Inbound payload: `(string Type, string? Origin, string? Destination, decimal Amount)` |
| `EventResponse` | `record` | Outbound payload: `(Account? Origin, Account? Destination)` |
| `Result<T>` | `record` | Discriminated union for operation outcomes; carries `Data` on success or `ErrorMessage` on failure |

The use of C# `record` types throughout the domain enforces **immutability by convention**, mutations always produce a new instance via `with` expressions, making state transitions explicit and auditable.

---

## API reference

All endpoints run on `http://localhost:5164` by default (HTTP profile).

### POST /reset

Clears all account state. Returns `200 OK` with the plain-text body `OK`.

```http
POST /reset HTTP/1.1
```

```
200 OK
OK
```

---

### GET /balance

Retrieves the current balance of an account.

```http
GET /balance?account_id={id}
```

**Success (account exists):**
```
200 OK
20
```

**Failure (account not found):**
```
404 Not Found
0
```

> The body `0` on a 404 is a deliberate contract requirement of the grading script and is not an error response structure.

---

### POST /event

Dispatches a ledger event. The `type` field routes to the appropriate operation.

**Request body:**
```json
{
  "type": "deposit | withdraw | transfer",
  "origin": "account_id",
  "destination": "account_id",
  "amount": 10
}
```

`origin` is required for `withdraw` and `transfer`, `destination` is required for `deposit` and `transfer`.

---

#### Deposit

Creates the account if it does not exist, then credits the specified amount.

**Request:**
```json
{ "type": "deposit", "destination": "100", "amount": 10 }
```

**Response `201 Created`:**
```json
{ "destination": { "id": "100", "balance": 10 } }
```

---

#### Withdraw

Debits the specified amount from an existing account. Fails with `404` if the account does not exist or has insufficient funds.

**Request:**
```json
{ "type": "withdraw", "origin": "100", "amount": 5 }
```

**Response `201 Created`:**
```json
{ "origin": { "id": "100", "balance": 5 } }
```

**Failure (non-existent account or insufficient funds):**
```
404 Not Found
0
```

---

#### Transfer

Atomically moves funds between two accounts. The destination account is created if it does not exist. Fails with `404` if the origin account does not exist or has insufficient funds.

**Request:**
```json
{ "type": "transfer", "origin": "100", "destination": "300", "amount": 5 }
```

**Response `201 Created`:**
```json
{
  "origin": { "id": "100", "balance": 0 },
  "destination": { "id": "300", "balance": 5 }
}
```

---

## Key engineering decisions

### Concurrency & data integrity

The application registers both `AccountService` and `InMemoryAccountStore` as **singletons** in the DI container, meaning a single shared instance serves all concurrent HTTP requests for the lifetime of the process.

To protect read-modify-write cycles from data races, `AccountService` maintains a `ConcurrentDictionary<string, object>` of per-account lock objects:

```csharp
private readonly ConcurrentDictionary<string, object> _accountLocks = new();

private object GetAccountLock(string accountId)
    => _accountLocks.GetOrAdd(accountId, _ => new object());
```

Every mutating operation (`Deposit`, `Withdraw`, `Transfer`) acquires the lock for its target account(s) before touching the store. This gives per-account granularity rather than a coarse global lock, avoiding unnecessary serialisation of operations on unrelated accounts.

#### Deadlock prevention in `Transfer`

The `Transfer` operation must lock **two** accounts simultaneously. Naive nested locking introduces the classic deadlock risk: Thread A holds lock on account `"1"` and waits for `"2"`, while Thread B holds `"2"` and waits for `"1"`.

This is prevented by enforcing a **consistent, global lock-acquisition order** based on lexicographical comparison of account IDs:

```csharp
bool isOriginFirst = string.CompareOrdinal(originId, destinationId) < 0;
string firstLockId  = isOriginFirst ? originId      : destinationId;
string secondLockId = isOriginFirst ? destinationId : originId;

lock (GetAccountLock(firstLockId))
{
    lock (GetAccountLock(secondLockId))
    {
        // atomic transfer
    }
}
```

Regardless of which direction a transfer flows, both threads will always attempt to acquire the locks in the same alphabetical order, making a circular wait impossible. This is validated by the `TransferShouldHaveAtomicityMaintainingBalanceBetweenAccounts` stress test, which fires 100 concurrent bidirectional transfers and verifies that the combined balance is always conserved.

---

### Result\<T\> pattern | No exception-driven flow control

Business rule violations (non-existent account, insufficient funds) are **expected outcomes**, not exceptional ones. Using `try/catch` to signal these conditions is semantically incorrect and carries measurable runtime overhead.

Instead, the service layer communicates outcomes through a lightweight `Result<T>` discriminated union:

```csharp
public record Result<T>(bool IsSuccess, T? Data = default, string? ErrorMessage = null)
{
    public static Result<T> Success(T data) => new(true, data);
    public static Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
}
```

The controller inspects `result.IsSuccess` and maps to the appropriate HTTP status code. This keeps the service layer free of HTTP concerns and makes all failure paths explicit, enumerable, and testable without needing to `Assert.Throws`.

---

### JSON serialisation contract

The grading script parses response JSON with strict expectations. Two serialisation behaviours are configured globally in `Program.cs`:

| Setting | Value | Rationale |
|---|---|---|
| `DefaultIgnoreCondition` | `WhenWritingNull` | Omits `null` properties (`origin`/`destination`) from the response, preventing schema mismatches |
| Property naming | `camelCase` (ASP.NET Core default) | Matches the expected lowercase field names (`id`, `balance`, `origin`, `destination`) |

The `EventResponse` record uses nullable properties (`Account? Origin`, `Account? Destination`) so that, for example, a `withdraw` response only serialises the `origin` key, and a `deposit` response only serialises the `destination` key, exactly as the grading script expects.

---

## Testing strategy

### Unit tests | State-Based, not interaction-based

The unit tests in `AccountServiceTests` use the **real `InMemoryAccountStore`** as a test double (a Fake), rather than a mock generated by a library like Moq.

This is a deliberate choice driven by the nature of the system under test. The ledger's correctness is entirely about **state outcomes**: did the balance change to the right value?

This approach provides stronger guarantees with simpler test setup:

```csharp
// State is directly observable via the real store
Assert.Equal(50m, _accountStore.GetAccount("100")!.Balance);
```

Tests use `[Theory]` with `[InlineData]` to cover boundary conditions (exact balance withdrawal, zero-balance accounts, overdraft attempts) in a data-driven, non-repetitive way.

### Integration tests | Lifecycle-isolated end-to-end

The integration tests use `WebApplicationFactory<Program>` to spin up a real in-memory instance of the full ASP.NET Core pipeline: controllers, middleware, DI, and serialisation, against a real `HttpClient`.

A **single integration test method** (`TheFullTestSuitShouldRunThroughTheAPIAndGetMatchingResult`) orchestrates the complete state machine of the API in sequence:

1. `POST /reset` → verify `200 OK` with body `"OK"`
2. `GET /balance?account_id=1234` → verify `404 Not Found` with body `"0"`
3. `POST /event` (deposit) → verify `201 Created`, correct `destination` shape, `null` `origin`
4. `POST /event` (withdraw) → verify `201 Created`, correct `origin` balance
5. `POST /event` (transfer) → verify `201 Created`, both `origin` and `destination` balances

Consolidating this into a single test method is **intentional**. Because `WebApplicationFactory` with `IClassFixture` shares one application instance per test class, multiple independent `[Fact]` methods would share state and produce non-deterministic results depending on execution order. A single sequential test is the architecturally correct model for validating a stateful system, and it precisely mirrors how the grading script itself runs.

---

## How to run locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [ngrok](https://ngrok.com/download) (optional, for external access)

### Running the API

Clone the repository and run the API using the **`http` profile** to avoid self-signed HTTPS certificate issues:

```bash
git clone <repository-url>
cd SimpleLedgerAPI

dotnet run --project SimpleLedgerAPI --launch-profile http
```

The API will start at **`http://localhost:5164`**.

A Swagger UI is available at `http://localhost:5164/swagger` when running in the `Development` environment.

> **Why the `http` profile?**  
> The `https` profile requires a trusted local development certificate. Automated graders and tools like `ngrok` interact over plain HTTP, so using the HTTP profile eliminates the certificate chain as a potential failure point.

---

### Exposing to the internet via ngrok

Cloud deployment was deliberately deferred to prioritise rapid iteration against the automated grader. `ngrok` provides a production-grade public HTTPS tunnel to the local process without any infrastructure configuration:

**1. Start the API** (as described above).

**2. In a second terminal, start the ngrok tunnel:**

```bash
ngrok http 5164
```

**3. Copy the public forwarding URL** from the ngrok output, for example:

```
Forwarding  https://a1b2c3d4.ngrok-free.app -> http://localhost:5164
```

**4. Use the ngrok URL** as the base URL in the grading script.

All requests made to the `ngrok-free.app` URL are transparently proxied to your local API over HTTP, bypassing the self-signed certificate entirely. ngrok's own TLS termination provides the HTTPS layer to the outside world.

---

## Running the Tests

```bash
dotnet test
```

To run with verbose output and see individual test display names:

```bash
dotnet test --logger "console;verbosity=detailed"
```

Expected output: all tests pass, including the concurrent transfer stress test.

---

## Development Process

This project was developed iteratively using **GitHub** as the primary platform for planning, tracking, and reviewing work.

- **Issues** were used to define and scope individual pieces of work — each one documents the context, constraints, and intent behind a feature or decision before any code was written.
- **Pull Requests** contain detailed descriptions of the changes introduced, the reasoning behind implementation choices, alternatives considered, and references back to the issues they resolve. They serve as a written record of the decision-making process throughout the project's lifecycle.

For a deeper look into how the project evolved — including design discussions, trade-off analyses, and implementation notes — the [issues](https://github.com/Marco-Schneider/simple-ledger-api/issues) and [pull requests](https://github.com/Marco-Schneider/simple-ledger-api/pulls) on this repository are the most detailed source of context.

---

## Technical Debt & Trade-Offs

The following decisions carry known trade-offs that are acceptable for the scope of this challenge but would require revisiting in a production system.

| Decision | Trade-Off | Production Mitigation |
|---|---|---|
| In-memory store (Singleton) | All state is lost on process restart; no horizontal scaling | Replace `IAccountStore` with a database-backed implementation (e.g., PostgreSQL via EF Core) |
| `Reset()` on `IAccountService` | Violates ISP; admin operations mixed with business operations | Extract `IAdminService` or protect behind an admin-only controller with auth |
| Anemic Domain Model | Balance arithmetic in the service layer; domain object carries no self-protection | Introduce a Rich Domain Model with invariant enforcement on `Account` if business rules grow in complexity |
| No authentication / authorisation | All endpoints are publicly accessible | Add JWT bearer auth or API key middleware; scope `Reset` and write operations to authenticated principals |
| `ngrok` for external access | Requires the developer's machine to be running; ephemeral tunnel URL changes on restart | Deploy to a managed PaaS (e.g., Azure Web App, Azure Container Apps, Railway) |
| HTTP profile on port `5164` | Differs from the `5000` convention sometimes expected by graders | Override with `ASPNETCORE_URLS=http://+:5000` or `dotnet run --urls http://+:5000` if the grader hardcodes port 5000 |
