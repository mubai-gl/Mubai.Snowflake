# Mubai.Snowflake

## 愿景（Vision）

为 .NET 提供一个：

- **简单易接入**：DI 一行注册，业务代码只关心 `IIdGenerator.NewId()`；  
- **工程上安全**：对错误配置、时钟回拨、极端 bit 配置有明确防御行为；  
- **可渐进增强**：从单实例到多节点、多 Region、集群级 Worker 分配都能覆盖；

的 **雪花 ID 组件**，适合作为分布式系统的 ID 基础设施，并能平滑过渡到更复杂架构。

---

## 版本总览

| 版本              | 功能说明     |
|------------------------|------------------|
| v1.0.0(当前版本)   | 灵活的位布局配置、时钟安全机制、线程安全、极端配置兜底、解码能力、支持依赖注入      | 
| v1.1.0  | 增加：强类型 ID 封装、EF Core 集成、示例项目      | 
| v1.2.0  | 解决 JS Number 精度问题，统一前后端 ID 表达方式      | 
| v1.3.0  | 在保持默认实现简单可靠的前提下，提供一套「可选的高性能实现」和可观测能力。     | 
| v2.0.0  | 集群感知 & 多 Region 方案     | 

## 当前版本：v1.0.0 – 基础实现（已完成）
### 核心能力
- [x] **灵活的位布局配置**
  - [x] 默认配置：`0 | 41-bit timestamp | 10-bit workerId | 12-bit sequence`
  - [x] 支持自定义各字段位数，总和 ≤ 63 即可

- [x] **时钟安全机制**
  - [x] 可配置 Epoch（默认：2025-01-01）
  - [x] 内置未来时间偏差检测，防止配置错误
  - [x] 运行时时钟异常（回拨/超前）有明确异常提示

- [x] **线程安全**
  - [x] 在多线程下，能够稳定地生成
  - [x] 处理序列号溢出、时钟回拨等边界情况

- [x] **极端配置兜底**
  - [x] 时间戳溢出时启用组合计数器，保证极端测试场景下的可用性

- [x] **解码能力**
  - [x] 支持从 ID 反解析出时间、WorkerId、序列号
  - [x] 编码/解码配置一致

- [x] **依赖注入支持**
  - [x] 一行代码完成服务注册
      ```csharp
    services.AddSnowflakeIdGenerator(options =>
    {
        options.WorkerId = 1;
    });
    ```
  - [x] 业务代码直接注入 `IIdGenerator` 使用

### 非目标 / 明确还没做的

- 没有 EF Core 集成（ValueConverter / 强类型 ID）。  
- 没有 JSON 转换器（前端 string 化、JS 精度问题还没统一方案）。  
- 没有高性能无锁实现，当前以简单、安全的 `lock` 为主。
- 没有集群级 WorkerId 分配（Redis/DB 租约等）。  

这些全部放到后续版本。

---

## v1.1 – 应用集成：EF Core & 强类型 ID

> 目标：让业务代码更类型安全、更不容易「到处裸 long」。

**计划工作：**

- **强类型 ID 封装**
  - 定义 `SnowflakeId` 结构体 / record struct，内部持有 `long` 值。
  - 提供与 `long`/`string` 的显式/隐式转换。

- **EF Core 集成**
  - 为 `SnowflakeId` 提供 `ValueConverter`。
  - 提供扩展方法简化配置。

- **示例项目**
  - 提供一个简单的 WebAPI / Minimal API 示例，展示：
    - 使用 `SnowflakeId` 做主键；
    - 与 `SnowflakeIdGenerator` 组合使用。

---

## v1.2 – JSON & 前端兼容性

> 目标：解决 JS Number 精度问题，统一前后端 ID 表达方式。

**计划工作：**

- **System.Text.Json 转换器**
  - 默认以 `string` 形式序列化 Snowflake ID。

- **Newtonsoft.Json 支持（可选）**
  - 若用户项目仍使用 Newtonsoft，提供对应的 `JsonConverter`。

- **前端配套文档 & 示例**
  - 展示在 TypeScript/前端里如何处理 ID（string 类型），避免强转 number；
  - 示例：前端展示、URL 传参、表格分页等场景。

---

## v1.3 – 性能优化 & 诊断

> 目标：在保持默认实现简单可靠的前提下，提供一套「可选的高性能实现」和可观测能力。

**计划工作：**

- **高性能实现（可选）**
  - 引入无锁版本 `HighPerfSnowflakeIdGenerator`：
    - 使用 `Interlocked` / CAS / `SpinWait` 实现；
    - 尽量消除全局 `lock` 的开销。
  - 通过配置注册实现

- **Benchmark 项目完善**
  - 使用 BenchmarkDotNet 对比：
    - v1.0 Generator vs 高性能版本；
    - Snowflake vs `Guid.NewGuid()`。
  - 在 README 中以表格形式展示典型环境下的性能。

- **诊断和观测**
  - 针对以下事件提供日志 Hook 或计数器接口：
    - 时间回拨次数；
    - 进入 `WaitUntilNextMillis` 的次数；
    - 开启组合计数器逻辑的次数（极端配置下）。

---

## v2.0 – 集群感知 & 多 Region 方案

> 目标：适用于多 Region / 多机房的大型系统，提供 WorkerId 分配策略和可用性保障。

**计划工作：**

- **WorkerId 租约与集中分配**
  - 支持从 Redis / 数据库获取 WorkerId，带租约过期机制；
  - 进程启动时向中心注册 WorkerId，异常退出后租约自动回收。

- **多 Region bit 拆分**
  - 在 `WorkerIdBits` 内部拆分：
    - 高位：RegionId；
    - 低位：MachineId。
  - 提供配置支持。

- **可用性保障策略**
  - 针对 WorkerId 获取失败、租约冲突、中心服务不可用，定义清晰策略：
    - Fail-fast；
    - 降级为单机模式（可选）
    - 打印告警日志。

- **文档 & 指南**
  - 提供多 Region 部署指南：如何规划 Epoch、bit 分配、WorkerId 分配。

---

## Backlog（未排期事项）

> 目前想到但不一定排到上述版本中的需求 / 想法。

- **监控面板示例**
  - 提供一个基于 Prometheus / OpenTelemetry 的示例：
    - 每秒 ID 生成量；
    - 时间回拨计数；
    - 极端配置下组合计数器使用情况。

- **安全与混淆**
  - 对外暴露 ID 时提供一种混淆方案

- **语言互通**
  - 在文档里给出其他语言 Snowflake 实现的推荐或兼容性说明，方便多语言技术栈对接。
