using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using Mubai.Snowflake;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Mubai.Snowflake.PerformanceTests
{
    /// <summary>
    /// 雪花ID生成器性能测试主程序
    /// 使用 BenchmarkDotNet 进行性能基准测试
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // 确保控制台输出编码正确
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                
                // 运行所有性能测试
                // 可以通过命令行参数过滤特定测试，例如：
                // --filter "*IdGenerationBenchmarks*"  // 只运行ID生成测试
                // --filter "*ConcurrentIdGenerationBenchmarks*"  // 只运行并发测试
                // --job short  // 使用短时间配置（快速测试）
                
                var config = DefaultConfig.Instance
                    .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest))
                    .WithOptions(ConfigOptions.DisableOptimizationsValidator)
                    .AddExporter(MarkdownExporter.GitHub)  // 导出Markdown格式结果
                    .AddLogger(ConsoleLogger.Default);      // 确保控制台输出

                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine("雪花ID生成器性能测试");
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine();
                Console.WriteLine("开始运行性能测试...");
                Console.WriteLine("测试结果将显示在下方，并保存到 BenchmarkDotNet.Artifacts 目录");
                Console.WriteLine();
                Console.WriteLine("提示: 如果控制台窗口关闭太快，请从命令行运行程序");
                Console.WriteLine();

                var summaries = BenchmarkRunner.Run(typeof(Program).Assembly, config, args);

                // 显示测试结果摘要
                Console.WriteLine();
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine("性能测试完成！");
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine();
                
                if (summaries != null && summaries.Length > 0)
                {
                    var summary = summaries[0];
                    if (summary != null)
                    {
                        Console.WriteLine($"测试结果已保存到: {summary.ResultsDirectoryPath}");
                        Console.WriteLine($"报告文件位置: {summary.LogFilePath}");
                    }
                }
                else
                {
                    Console.WriteLine("测试结果已保存到 BenchmarkDotNet.Artifacts 目录");
                }
                
                Console.WriteLine();
                Console.WriteLine("提示: 详细结果请查看控制台输出或 BenchmarkDotNet.Artifacts 目录中的报告文件");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("错误: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
            }
            finally
            {
                // 等待用户按键，防止控制台窗口立即关闭
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
        }
    }

    /// <summary>
    /// ID生成性能测试 - 单线程
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class IdGenerationBenchmarks
    {
        private SnowflakeIdGenerator _generator = null!;

        [GlobalSetup]
        public void Setup()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1
            };
            _generator = new SnowflakeIdGenerator(config);
        }

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("SingleThread")]
        public long GenerateSingleId()
        {
            return _generator.NewId();
        }

        [Benchmark]
        [BenchmarkCategory("SingleThread")]
        public long[] Generate1000Ids()
        {
            var ids = new long[1000];
            for (int i = 0; i < 1000; i++)
            {
                ids[i] = _generator.NewId();
            }
            return ids;
        }

        [Benchmark]
        [BenchmarkCategory("SingleThread")]
        public long[] Generate10000Ids()
        {
            var ids = new long[10000];
            for (int i = 0; i < 10000; i++)
            {
                ids[i] = _generator.NewId();
            }
            return ids;
        }

        [Benchmark]
        [BenchmarkCategory("SingleThread")]
        public long[] Generate100000Ids()
        {
            var ids = new long[100000];
            for (int i = 0; i < 100000; i++)
            {
                ids[i] = _generator.NewId();
            }
            return ids;
        }
    }

    /// <summary>
    /// ID生成性能测试 - 多线程并发
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class ConcurrentIdGenerationBenchmarks
    {
        private SnowflakeIdGenerator _generator = null!;

        [GlobalSetup]
        public void Setup()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1
            };
            _generator = new SnowflakeIdGenerator(config);
        }

        [Benchmark]
        [BenchmarkCategory("MultiThread")]
        public long GenerateIds_4Threads()
        {
            const int idsPerThread = 10000;
            var ids = new ConcurrentBag<long>();
            var tasks = new Task[4];

            for (int i = 0; i < 4; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < idsPerThread; j++)
                    {
                        ids.Add(_generator.NewId());
                    }
                });
            }

            Task.WaitAll(tasks);
            return ids.Count;
        }

        [Benchmark]
        [BenchmarkCategory("MultiThread")]
        public long GenerateIds_8Threads()
        {
            const int idsPerThread = 10000;
            var ids = new ConcurrentBag<long>();
            var tasks = new Task[8];

            for (int i = 0; i < 8; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < idsPerThread; j++)
                    {
                        ids.Add(_generator.NewId());
                    }
                });
            }

            Task.WaitAll(tasks);
            return ids.Count;
        }

        [Benchmark]
        [BenchmarkCategory("MultiThread")]
        public long GenerateIds_16Threads()
        {
            const int idsPerThread = 10000;
            var ids = new ConcurrentBag<long>();
            var tasks = new Task[16];

            for (int i = 0; i < 16; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < idsPerThread; j++)
                    {
                        ids.Add(_generator.NewId());
                    }
                });
            }

            Task.WaitAll(tasks);
            return ids.Count;
        }
    }

    /// <summary>
    /// ID解码性能测试
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class IdDecodingBenchmarks
    {
        private SnowflakeIdGenerator _generator = null!;
        private SnowflakeIdDecoder _decoder = null!;
        private long[] _testIds = null!;

        [GlobalSetup]
        public void Setup()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1
            };
            _generator = new SnowflakeIdGenerator(config);
            _decoder = new SnowflakeIdDecoder(config);

            // 预生成测试ID
            _testIds = new long[10000];
            for (int i = 0; i < 10000; i++)
            {
                _testIds[i] = _generator.NewId();
            }
        }

        [Benchmark]
        [BenchmarkCategory("Decoding")]
        public DateTimeOffset DecodeTimestamp()
        {
            return _decoder.GetTimestamp(_testIds[0]);
        }

        [Benchmark]
        [BenchmarkCategory("Decoding")]
        public int DecodeWorkerId()
        {
            return _decoder.GetWorkerId(_testIds[0]);
        }

        [Benchmark]
        [BenchmarkCategory("Decoding")]
        public int DecodeSequence()
        {
            return _decoder.GetSequence(_testIds[0]);
        }

        [Benchmark]
        [BenchmarkCategory("Decoding")]
        public void DecodeAllFields()
        {
            long id = _testIds[0];
            var timestamp = _decoder.GetTimestamp(id);
            var workerId = _decoder.GetWorkerId(id);
            var sequence = _decoder.GetSequence(id);
        }

        [Benchmark]
        [BenchmarkCategory("Decoding")]
        public void Decode10000Ids()
        {
            foreach (var id in _testIds)
            {
                _decoder.GetTimestamp(id);
                _decoder.GetWorkerId(id);
                _decoder.GetSequence(id);
            }
        }
    }

    /// <summary>
    /// 不同配置下的性能对比
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class ConfigurationBenchmarks
    {
        private SnowflakeIdGenerator _defaultGenerator = null!;
        private SnowflakeIdGenerator _customGenerator = null!;

        [GlobalSetup]
        public void Setup()
        {
            // 默认配置
            var defaultConfig = new SnowflakeConfiguration
            {
                WorkerId = 1
            };
            _defaultGenerator = new SnowflakeIdGenerator(defaultConfig);

            // 自定义配置
            var customConfig = new SnowflakeConfiguration
            {
                WorkerId = 1,
                TimestampBits = 39,
                WorkerIdBits = 12,
                SequenceBits = 12
            };
            _customGenerator = new SnowflakeIdGenerator(customConfig);
        }

        [Benchmark]
        [BenchmarkCategory("Configuration")]
        public long DefaultConfiguration()
        {
            return _defaultGenerator.NewId();
        }

        [Benchmark]
        [BenchmarkCategory("Configuration")]
        public long CustomConfiguration()
        {
            return _customGenerator.NewId();
        }
    }

    /// <summary>
    /// 高并发场景性能测试
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class HighConcurrencyBenchmarks
    {
        private SnowflakeIdGenerator _generator = null!;

        [GlobalSetup]
        public void Setup()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                SequenceBits = 2 // 较小的序列位数，更容易产生竞争
            };
            _generator = new SnowflakeIdGenerator(config);
        }

        [Benchmark]
        [BenchmarkCategory("HighConcurrency")]
        public long HighContention_20Threads()
        {
            const int idsPerThread = 5000;
            var ids = new ConcurrentBag<long>();
            var tasks = new Task[20];

            for (int i = 0; i < 20; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < idsPerThread; j++)
                    {
                        ids.Add(_generator.NewId());
                    }
                });
            }

            Task.WaitAll(tasks);
            return ids.Count;
        }

        [Benchmark]
        [BenchmarkCategory("HighConcurrency")]
        public long HighContention_50Threads()
        {
            const int idsPerThread = 2000;
            var ids = new ConcurrentBag<long>();
            var tasks = new Task[50];

            for (int i = 0; i < 50; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < idsPerThread; j++)
                    {
                        ids.Add(_generator.NewId());
                    }
                });
            }

            Task.WaitAll(tasks);
            return ids.Count;
        }
    }

    /// <summary>
    /// 生成和解码综合性能测试
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class GenerateAndDecodeBenchmarks
    {
        private SnowflakeIdGenerator _generator = null!;
        private SnowflakeIdDecoder _decoder = null!;

        [GlobalSetup]
        public void Setup()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1
            };
            _generator = new SnowflakeIdGenerator(config);
            _decoder = new SnowflakeIdDecoder(config);
        }

        [Benchmark]
        [BenchmarkCategory("GenerateAndDecode")]
        public void GenerateAndDecode_1000()
        {
            for (int i = 0; i < 1000; i++)
            {
                long id = _generator.NewId();
                _decoder.GetTimestamp(id);
                _decoder.GetWorkerId(id);
                _decoder.GetSequence(id);
            }
        }

        [Benchmark]
        [BenchmarkCategory("GenerateAndDecode")]
        public void GenerateAndDecode_10000()
        {
            for (int i = 0; i < 10000; i++)
            {
                long id = _generator.NewId();
                _decoder.GetTimestamp(id);
                _decoder.GetWorkerId(id);
                _decoder.GetSequence(id);
            }
        }
    }

    /// <summary>
    /// 吞吐量测试 - 每秒生成ID数量
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class ThroughputBenchmarks
    {
        private SnowflakeIdGenerator _generator = null!;

        [GlobalSetup]
        public void Setup()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1
            };
            _generator = new SnowflakeIdGenerator(config);
        }

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Throughput")]
        public long Throughput_1Million()
        {
            const long count = 1000000;
            long lastId = 0;
            for (long i = 0; i < count; i++)
            {
                lastId = _generator.NewId();
            }
            return lastId;
        }

        [Benchmark]
        [BenchmarkCategory("Throughput")]
        public long Throughput_10Million()
        {
            const long count = 10000000;
            long lastId = 0;
            for (long i = 0; i < count; i++)
            {
                lastId = _generator.NewId();
            }
            return lastId;
        }

        [Benchmark]
        [BenchmarkCategory("Throughput")]
        public long Throughput_100Million()
        {
            const long count = 100000000;
            long lastId = 0;
            for (long i = 0; i < count; i++)
            {
                lastId = _generator.NewId();
            }
            return lastId;
        }
    }
}
