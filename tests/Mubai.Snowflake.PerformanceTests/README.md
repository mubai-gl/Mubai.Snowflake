# Mubai.Snowflake 性能测试

本项目使用 BenchmarkDotNet 对雪花ID生成器进行性能基准测试。

## 运行性能测试

### 运行所有测试

```bash
dotnet run --project tests/Mubai.Snowflake.PerformanceTests/Mubai.Snowflake.PerformanceTests.csproj -c Release
```

### 运行特定测试类

```bash
# 只运行单线程生成测试
dotnet run --project tests/Mubai.Snowflake.PerformanceTests/Mubai.Snowflake.PerformanceTests.csproj -c Release -- --filter "*IdGenerationBenchmarks*"

# 只运行多线程并发测试
dotnet run --project tests/Mubai.Snowflake.PerformanceTests/Mubai.Snowflake.PerformanceTests.csproj -c Release -- --filter "*ConcurrentIdGenerationBenchmarks*"

# 只运行解码测试
dotnet run --project tests/Mubai.Snowflake.PerformanceTests/Mubai.Snowflake.PerformanceTests.csproj -c Release -- --filter "*IdDecodingBenchmarks*"
```

### 快速测试（使用短时间配置）

```bash
dotnet run --project tests/Mubai.Snowflake.PerformanceTests/Mubai.Snowflake.PerformanceTests.csproj -c Release -- --job short
```

## 测试类别

### 1. IdGenerationBenchmarks - 单线程ID生成性能
- `GenerateSingleId`: 生成单个ID的性能
- `Generate1000Ids`: 生成1000个ID的性能
- `Generate10000Ids`: 生成10000个ID的性能
- `Generate100000Ids`: 生成100000个ID的性能

### 2. ConcurrentIdGenerationBenchmarks - 多线程并发ID生成性能
- `GenerateIds_4Threads`: 4个线程并发生成ID
- `GenerateIds_8Threads`: 8个线程并发生成ID
- `GenerateIds_16Threads`: 16个线程并发生成ID

### 3. IdDecodingBenchmarks - ID解码性能
- `DecodeTimestamp`: 解码时间戳的性能
- `DecodeWorkerId`: 解码WorkerId的性能
- `DecodeSequence`: 解码序列号的性能
- `DecodeAllFields`: 解码所有字段的性能
- `Decode10000Ids`: 解码10000个ID的性能

### 4. ConfigurationBenchmarks - 不同配置下的性能对比
- `DefaultConfiguration`: 默认配置（41位时间戳，10位WorkerId，12位序列号）
- `CustomConfiguration`: 自定义配置（39位时间戳，12位WorkerId，12位序列号）

### 5. HighConcurrencyBenchmarks - 高并发场景性能测试
- `HighContention_20Threads`: 20个线程高竞争场景
- `HighContention_50Threads`: 50个线程高竞争场景

### 6. GenerateAndDecodeBenchmarks - 生成和解码综合性能
- `GenerateAndDecode_1000`: 生成并解码1000个ID
- `GenerateAndDecode_10000`: 生成并解码10000个ID

### 7. ThroughputBenchmarks - 吞吐量测试
- `Throughput_1Million`: 生成100万个ID的吞吐量
- `Throughput_10Million`: 生成1000万个ID的吞吐量

## 性能指标

BenchmarkDotNet 会报告以下性能指标：
- **Mean**: 平均执行时间
- **Error**: 误差范围
- **StdDev**: 标准差
- **Median**: 中位数执行时间
- **Min/Max**: 最小/最大执行时间
- **Gen0/Gen1/Gen2**: 垃圾回收次数
- **Allocated**: 内存分配量

## 注意事项

1. 性能测试应该在 Release 模式下运行以获得准确的性能数据
2. 建议关闭其他应用程序以减少干扰
3. 多线程测试结果可能会受到CPU核心数的影响
4. 测试结果会保存在 `BenchmarkDotNet.Artifacts` 目录下

