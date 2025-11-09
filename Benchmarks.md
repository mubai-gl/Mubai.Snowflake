# Mubai.Snowflake Performance Benchmark Summary

> Environment: Windows 11 / .NET 8.0 / BenchmarkDotNet v0.13.10

---

## 1. Single-Thread ID Generation Performance (`IdGenerationBenchmarks`)

> Each benchmark method generates **N IDs in a loop**.  
> The table shows:  
> - **Total invocation time** (Mean)  
> - **Average time per ID**  
> - **Approximate single-thread throughput**

| Benchmark         | IDs per invocation | Mean (total)       | Avg time per ID | Approx. throughput (single thread) |
|-------------------|--------------------|--------------------|-----------------|------------------------------------|
| GenerateSingleId  | 1                  | 241.5 ns           | 241.5 ns        | ≈ 4.14 M IDs/s                     |
| Generate1000Ids   | 1,000              | 243,896.0 ns       | 243.9 ns        | ≈ 4.10 M IDs/s                     |
| Generate10000Ids  | 10,000             | 2,437,215.1 ns     | 243.7 ns        | ≈ 4.10 M IDs/s                     |
| Generate100000Ids | 100,000            | 24,403,598.3 ns    | 244.0 ns        | ≈ 4.10 M IDs/s                     |

- On a single thread, **each ID takes about 244 ns** (~0.244 μs). The numbers stay very stable across different batch sizes.
- The corresponding single-thread throughput is about **4.1M IDs/s per thread**, which lines up with the standalone throughput benchmarks later on.

---

## 2. Multi-Threaded Concurrent Generation (`ConcurrentIdGenerationBenchmarks`)

> These benchmarks start multiple threads in the same process to generate a batch of IDs concurrently, then measure the total time for the whole call.  
> The exact number of IDs generated per invocation is defined in the benchmark source code.

| Benchmark             | Threads | Mean (total) | Gen0    | Gen1    | Gen2    | Allocated |
|-----------------------|---------|--------------|--------:|--------:|--------:|----------:|
| GenerateIds_4Threads  | 4       | 9.762 ms     | 46.8750 | 46.8750 | 31.2500 | 1 MB      |
| GenerateIds_8Threads  | 8       | 20.357 ms    | 93.7500 | 62.5000 | 62.5000 | 2 MB      |
| GenerateIds_16Threads | 16      | 39.928 ms    | 230.7692| 230.7692|153.8462 | 4 MB      |

- As the number of threads increases, **total time grows roughly linearly**, which is expected due to increasing lock contention and GC pressure.
- Memory allocations per benchmark invocation also scale with thread count, but the maximum here is only about 4 MB, which is still very healthy.

---

## 3. ID Decoding Performance (`IdDecodingBenchmarks`)

> These benchmarks evaluate the cost of **decoding Snowflake IDs**:  
> decoding the timestamp / workerId / sequence individually, decoding all fields at once, and batch decoding.

### 3.1 Single-ID Decoding

| Benchmark       | Description                  | Mean (total) | Avg time per ID | Approx. throughput      |
|-----------------|------------------------------|--------------|-----------------|-------------------------|
| DecodeTimestamp | Decode timestamp             | 3.2194 ns    | 3.22 ns         | ≈ 311 M IDs/s           |
| DecodeWorkerId  | Decode workerId              | 0.3065 ns    | 0.31 ns         | ≈ 3.26 B IDs/s          |
| DecodeSequence  | Decode sequence              | 0.4583 ns    | 0.46 ns         | ≈ 2.18 B IDs/s          |
| DecodeAllFields | Decode all fields at once    | 3.6209 ns    | 3.62 ns         | ≈ 276 M IDs/s           |

> Throughput is derived from `1e9 / avgTime`, mainly as an order-of-magnitude reference.

### 3.2 Batch Decoding

| Benchmark      | IDs per invocation | Mean (total)   | Avg time per ID | Approx. throughput |
|----------------|--------------------|----------------|-----------------|--------------------|
| Decode10000Ids | 10,000             | 37,079.3452 ns | 3.71 ns         | ≈ 270 M IDs/s      |

- Compared with ID generation (~244 ns), **decoding is about two orders of magnitude cheaper**.
- This makes it very attractive for log analysis / operational tooling: you can freely decode IDs at scale for troubleshooting without worrying about performance.

---

## 4. Performance of Different Configuration Paths (`ConfigurationBenchmarks`)

> These benchmarks measure the overhead of obtaining the default vs. custom configuration to make sure configuration lookup never becomes a bottleneck.

| Benchmark            | Scenario        | Mean       | Allocated |
|----------------------|-----------------|------------|----------:|
| DefaultConfiguration | Default config  | 241.2 ns   | -         |
| CustomConfiguration  | Custom config   | 241.2 ns   | -         |

- Whether you use the default or a custom configuration, **getting the configuration object costs around 240 ns**, which is effectively zero in a real system.
- Recommended pattern: **register a singleton generator and decoder once at app startup**, and then ignore configuration overhead at runtime.

---

## 5. High-Concurrency Stress Tests (`HighConcurrencyBenchmarks`)

> These benchmarks focus on **stability and memory usage** under high contention rather than precise throughput.  
> Each benchmark runs for roughly 25 seconds, continuously generating IDs under heavy contention.

| Benchmark                | Threads | Mean (run duration) | Approx. memory allocations |
|--------------------------|---------|---------------------|----------------------------|
| HighContention_20Threads | 20      | 25.00 s             | 2.51 MB                    |
| HighContention_50Threads | 50      | 25.00 s             | 1.57 MB                    |

- Under 20- and 50-thread high-contention scenarios, the tests **stably run for the full 25-second window** without abnormal exits or extreme jitter.
- Memory usage stays in the ~1.5–2.5 MB range, indicating no obvious memory leaks or runaway allocations under sustained high concurrency.

---

## 6. Combined Generate + Decode Benchmarks (`GenerateAndDecodeBenchmarks`)

> These benchmarks bundle **“generate ID + immediately decode”** into a single operation to simulate an end-to-end flow.  
> They help answer the question:  
> **“From generating an ID to parsing out timestamp and workerId, how much time does the full pipeline take?”**

| Benchmark              | IDs per invocation | Mean (total) | Avg time per ID | Approx. throughput (single thread) |
|------------------------|--------------------|-------------:|-----------------|------------------------------------|
| GenerateAndDecode_1000 | 1,000              | 243.9 μs     | 243.9 ns        | ≈ 4.10 M IDs/s                     |
| GenerateAndDecode_10000| 10,000             | 2,437.5 μs   | 243.75 ns       | ≈ 4.10 M IDs/s                     |

- The average cost for the **“generate + decode” combo** is almost identical to “generation only”: **about 244 ns per ID**.
- This means operational tools can safely decode IDs right after generation without worrying about extra overhead.

---

## 7. Straightforward Throughput Tests: IDs per Second (`ThroughputBenchmarks`)

> These are the most intuitive throughput benchmarks: generate 1M / 10M / 100M IDs on a single thread,  
> then derive **IDs per second** from the total duration.

| Benchmark             | IDs per invocation | Mean (total) | Approx. throughput         |
|-----------------------|--------------------|-------------:|----------------------------|
| Throughput_1Million   | 1,000,000          | 243.9 ms     | ≈ 4.10 M IDs/s             |
| Throughput_10Million  | 10,000,000         | 2,440.8 ms   | ≈ 4.10 M IDs/s             |
| Throughput_100Million | 100,000,000        | 24,413.7 ms  | ≈ 4.10 M IDs/s             |

> Example calculation:  
> `1,000,000 / (243.9 ms / 1000) ≈ 4,100,041 IDs/s`

- Single-thread throughput remains stable at **about 4.1M IDs/s** across test sizes.
- Combined with the multi-thread benchmarks, you can expect that, on a multi-core machine with reasonable multi-threading / multi-process scaling, the ID service will easily support the needs of most medium-to-large systems.

---

## 8. Summary

- **Generation performance**: ~244 ns per ID on a single thread, ~4.1M IDs/s throughput.  
- **Decoding performance**: 1–4 ns per ID; throughput in the hundreds of millions to billions of IDs per second.  
- **Configuration & initialization**: ~240 ns; effectively zero overhead.  
- **High-concurrency stability**: Under 20 / 50 threads with high contention, the benchmarks can run stably for the full 25-second window without issues.  
- **End-to-end pipeline**: generating + decoding together still stays at ~244 ns per ID.
