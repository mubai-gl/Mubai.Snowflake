# Mubai.Snowflake

English | [简体中文](./README.zh-cn.md)

## Vision

Provide a Snowflake ID component for .NET that is:

- **Simple to adopt**: one DI registration, business code only cares about `IIdGenerator.NewId()`.
- **Safe in production**: clear defensive behavior for misconfiguration, clock skew/rollback, and extreme bit layouts.
- **Able to evolve**: covers everything from single-instance deployment to multi-node, multi-region, and cluster-level worker allocation.

In short, a **Snowflake ID library** suitable as ID infrastructure for distributed systems, with a smooth path toward more complex architectures.

---

## Version Overview

| Version        | Description                                                                                         |
|----------------|-----------------------------------------------------------------------------------------------------|
| v1.0.0 (current) | Flexible bit layout config, clock safety, thread safety, extreme-config fallback, decoding, DI support |
| v1.1.0        | Strongly-typed ID wrapper, EF Core integration, sample projects                                      |
| v1.2.0        | Fix JS `Number` precision issues and unify ID representation between backend and frontend           |
| v1.3.0        | Optional high-performance implementation and observability, keeping the default simple and reliable  |
| v2.0.0        | Cluster-aware & multi-region solution                                                                |

---

## Current Version: v1.0.0 – Base Implementation (Shipped)

### Core Capabilities

- [x] **Flexible bit layout configuration**
  - [x] Default layout: `0 | 41-bit timestamp | 10-bit workerId | 12-bit sequence`
  - [x] Custom bit widths for each field, as long as the total is ≤ 63 bits

- [x] **Clock safety**
  - [x] Configurable epoch (default: 2025-01-01)
  - [x] Built-in detection for “future timestamps” to prevent misconfigured clocks
  - [x] Clear exceptions when runtime clock anomalies are detected (rollback / moving too far ahead)

- [x] **Thread safety**
  - [x] Stable generation under multi-threaded workloads
  - [x] Proper handling of sequence overflow and clock rollback edge cases

- [x] **Extreme-configuration fallback**
  - [x] When the timestamp field overflows, a composite counter kicks in so IDs remain usable in stress / extreme test scenarios

- [x] **Decoding**
  - [x] Decode timestamp, workerId, and sequence from a Snowflake ID
  - [x] Consistent configuration between encoding and decoding

- [x] **Dependency injection support**
  - [x] Single-line registration in DI:

    ```csharp
    services.AddSnowflakeIdGenerator(options =>
    {
        options.WorkerId = 1;
    });
    ```

  - [x] Business code injects and uses `IIdGenerator` directly

### Performance

- **Generation**: single thread ~244 ns per ID, throughput ~4.1M IDs/s  
- **Decoding**: 1–4 ns per ID, throughput in the hundreds of millions to billions of IDs/s  
- **Configuration & initialization**: ~240 ns, effectively zero overhead  
- **High-concurrency stability**: 20 / 50 threads under high contention, stable for a 25-second run, no abnormal behavior  
- **End-to-end pipeline**: generate + decode still stays around ~244 ns per ID

For details, see the [v1 benchmark results](./Benchmarks.md).

### Explicit Non-Goals / Not Yet Implemented

- No EF Core integration yet (no `ValueConverter` / strongly-typed ID mapping).
- No JSON converters yet (no unified solution for string IDs / JS precision issues).
- No lock-free “high performance” implementation yet; the default focuses on simplicity and safety, using `lock`.
- No cluster-level worker ID allocation (e.g., Redis / DB lease).

All of these are planned in later versions.

---

## v1.1 – Application Integration: EF Core & Strongly-Typed IDs

> Goal: make application code more type-safe and avoid “raw `long` IDs everywhere”.

**Planned work:**

- **Strongly-typed ID wrapper**
  - Define a `SnowflakeId` struct / record struct that holds an internal `long` value.
  - Provide explicit / implicit conversions to and from `long` / `string`.

- **EF Core integration**
  - Provide a `ValueConverter` for `SnowflakeId`.
  - Offer extension methods to simplify configuration.

- **Sample project**
  - A simple WebAPI / Minimal API sample that demonstrates:
    - Using `SnowflakeId` as a primary key.
    - Combining `SnowflakeIdGenerator` with EF Core.

---

## v1.2 – JSON & Frontend Compatibility

> Goal: solve JS `Number` precision issues and unify how IDs are represented end to end.

**Planned work:**

- **System.Text.Json converter**
  - By default, serialize Snowflake IDs as `string` values in JSON.

- **Optional Newtonsoft.Json support**
  - For projects still using Newtonsoft.Json, provide a matching `JsonConverter`.

- **Frontend docs & examples**
  - Show how to handle IDs as `string` in TypeScript / frontend code to avoid casting to `number`.
  - Examples: displaying IDs on the UI, passing IDs in URLs, table pagination, etc.

---

## v1.3 – Performance Optimization & Diagnostics

> Goal: provide an **optional** high-performance implementation and better observability, while keeping the default implementation simple and robust.

**Planned work:**

- **Optional high-performance implementation**
  - Introduce a lock-free version, e.g. `HighPerfSnowflakeIdGenerator`:
    - Use `Interlocked` / CAS / `SpinWait`.
    - Eliminate the global `lock` overhead as much as possible.
  - Choose implementation via configuration at registration time.

- **Benchmark project improvements**
  - Use BenchmarkDotNet to compare:
    - v1.0 generator vs. the high-performance implementation.
    - Snowflake vs. `Guid.NewGuid()`.
  - Present typical environment results in tables within the README.

- **Diagnostics & observability**
  - Provide logging hooks or counters for:
    - Clock rollback events.
    - `WaitUntilNextMillis` invocations.
    - Usage of composite counter logic in extreme configurations.

---

## v2.0 – Cluster Awareness & Multi-Region Strategy

> Goal: support large-scale multi-region / multi-datacenter systems with worker ID allocation and high availability strategies.

**Planned work:**

- **Worker ID leases & centralized allocation**
  - Obtain worker IDs from Redis / a database with time-bound leases.
  - Register worker IDs with a central service at startup; automatically reclaim leases when a process crashes.

- **Multi-region bit splitting**
  - Split `WorkerIdBits` into:
    - High bits: `RegionId`
    - Low bits: `MachineId`
  - Provide configuration support for this split.

- **Availability strategies**
  - Define clear behavior for:
    - Worker ID acquisition failures.
    - Lease conflicts.
    - Central service downtime.
  - Potential strategies:
    - Fail-fast with clear diagnostics.
    - Optional fallback to single-node mode.
    - Emit warning logs.

- **Docs & guidance**
  - Provide deployment guidelines for multi-region setups:
    - How to plan epochs, bit allocation, and worker ID distribution.

---

## Backlog (Unscheduled Ideas)

> Ideas that have come up but are not yet scheduled for any of the versions above.

- **Monitoring dashboard example**
  - Provide a sample based on Prometheus / OpenTelemetry showing:
    - IDs generated per second.
    - Clock rollback counts.
    - Composite counter usage under extreme configurations.

- **Security & obfuscation**
  - Provide an optional obfuscation scheme for IDs exposed externally.

- **Language interoperability**
  - In documentation, recommend compatible Snowflake implementations in other languages  
    to make it easier to integrate in polyglot stacks.
