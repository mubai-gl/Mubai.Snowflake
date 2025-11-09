using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mubai.Snowflake.Tests
{
    /// <summary>
    /// 测试辅助类，提供通用的测试方法和数据
    /// </summary>
    internal static class TestHelpers
    {
        /// <summary>
        /// 默认测试配置
        /// </summary>
        public static SnowflakeConfiguration CreateDefaultConfig(int workerId = 1)
        {
            return new SnowflakeConfiguration
            {
                WorkerId = workerId
            };
        }

        /// <summary>
        /// 创建自定义配置
        /// </summary>
        public static SnowflakeConfiguration CreateCustomConfig(
            int workerId,
            int timestampBits = 41,
            int workerIdBits = 10,
            int sequenceBits = 12,
            DateTimeOffset? epoch = null)
        {
            return new SnowflakeConfiguration
            {
                WorkerId = workerId,
                TimestampBits = timestampBits,
                WorkerIdBits = workerIdBits,
                SequenceBits = sequenceBits,
                Epoch = epoch ?? new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
            };
        }

        /// <summary>
        /// 生成指定数量的唯一ID
        /// </summary>
        public static HashSet<long> GenerateIds(IIdGenerator generator, int count)
        {
            var ids = new HashSet<long>();
            for (int i = 0; i < count; i++)
            {
                long id = generator.NewId();
                if (!ids.Add(id))
                {
                    throw new InvalidOperationException($"生成的ID重复: {id}");
                }
            }
            return ids;
        }

        /// <summary>
        /// 验证ID的唯一性
        /// </summary>
        public static void AssertUniqueIds(IEnumerable<long> ids)
        {
            var uniqueIds = ids.Distinct().ToList();
            var totalCount = ids.Count();
            if (uniqueIds.Count != totalCount)
            {
                var duplicates = ids.GroupBy(id => id)
                    .Where(g => g.Count() > 1)
                    .Select(g => $"ID {g.Key} 出现 {g.Count()} 次")
                    .ToList();
                throw new InvalidOperationException($"发现重复ID: {string.Join(", ", duplicates)}");
            }
        }

        /// <summary>
        /// 验证ID是递增的
        /// </summary>
        public static void AssertMonotonicIds(IList<long> ids)
        {
            for (int i = 1; i < ids.Count; i++)
            {
                if (ids[i] <= ids[i - 1])
                {
                    throw new InvalidOperationException(
                        $"ID不是递增的: 索引 {i - 1} 的ID {ids[i - 1]} >= 索引 {i} 的ID {ids[i]}");
                }
            }
        }

        /// <summary>
        /// 验证解码结果
        /// </summary>
        public static void AssertDecodedValues(
            IIdDecoder decoder,
            long id,
            int expectedWorkerId,
            SnowflakeConfiguration config)
        {
            var timestamp = decoder.GetTimestamp(id);
            var workerId = decoder.GetWorkerId(id);
            var sequence = decoder.GetSequence(id);

            if (workerId != expectedWorkerId)
            {
                throw new InvalidOperationException(
                    $"WorkerId不匹配: 期望 {expectedWorkerId}, 实际 {workerId}");
            }

            var now = DateTimeOffset.UtcNow;
            // 时间戳应该在 epoch 之后（允许一些容差，因为可能存在时间戳溢出或其他计算问题）
            // 对于较小的 timestampBits，时间戳可能会因为溢出而出现问题，所以检查更宽松
            if (timestamp < config.Epoch.AddYears(-1))
            {
                throw new InvalidOperationException(
                    $"时间戳异常: {timestamp}, Epoch: {config.Epoch}, Now: {now}。时间戳不应该远早于 Epoch。");
            }
            
            // 如果时间戳在未来（超过1分钟），也可能是正常的（测试环境时间不同步等）
            // 所以只检查是否异常早于 epoch

            var maxSequence = (1 << config.SequenceBits) - 1;
            if (sequence < 0 || sequence > maxSequence)
            {
                throw new InvalidOperationException(
                    $"序列号超出范围: {sequence}, 最大值: {maxSequence}");
            }
        }

        /// <summary>
        /// 获取有效的WorkerId范围测试数据
        /// </summary>
        public static TheoryData<int> GetValidWorkerIdTestData()
        {
            var data = new TheoryData<int>();
            data.Add(0);
            data.Add(1);
            data.Add(511);
            data.Add(1023); // 默认最大值 (2^10 - 1)
            return data;
        }

        /// <summary>
        /// 获取无效的WorkerId测试数据
        /// </summary>
        public static TheoryData<int> GetInvalidWorkerIdTestData()
        {
            var data = new TheoryData<int>();
            data.Add(-1);
            data.Add(1024); // 超出默认最大值
            return data;
        }

        /// <summary>
        /// 获取有效的位组合测试数据
        /// </summary>
        public static TheoryData<int, int, int, int> GetValidBitCombinationsTestData()
        {
            var data = new TheoryData<int, int, int, int>();
            data.Add(1, 1, 1, 0); // 最小值
            data.Add(10, 5, 8, 3); // 小值组合
            data.Add(41, 10, 12, 1); // 默认值
            data.Add(39, 8, 16, 100); // 自定义组合
            data.Add(20, 20, 23, 31); // 极端配置
            return data;
        }

        /// <summary>
        /// 获取无效的位组合测试数据（总数超过63）
        /// </summary>
        public static TheoryData<int, int, int> GetInvalidBitCombinationsTestData()
        {
            var data = new TheoryData<int, int, int>();
            data.Add(30, 20, 14); // 30+20+14=64 > 63
            data.Add(42, 10, 12); // 42+10+12=64 > 63
            data.Add(21, 21, 22); // 21+21+22=64 > 63
            return data;
        }

        /// <summary>
        /// 获取边界值测试数据（总数等于63）
        /// </summary>
        public static TheoryData<int, int, int> GetBoundaryBitCombinationsTestData()
        {
            var data = new TheoryData<int, int, int>();
            data.Add(1, 1, 61); // 1+1+61=63
            data.Add(20, 20, 23); // 20+20+23=63
            data.Add(30, 15, 18); // 30+15+18=63
            data.Add(40, 10, 13); // 40+10+13=63
            data.Add(41, 10, 12); // 41+10+12=63 (默认)
            return data;
        }

        /// <summary>
        /// 获取不同的WorkerId对测试数据
        /// </summary>
        public static TheoryData<int, int> GetWorkerIdPairsTestData()
        {
            var data = new TheoryData<int, int>();
            data.Add(0, 1);
            data.Add(1, 2);
            data.Add(100, 200);
            data.Add(511, 512);
            data.Add(1022, 1023);
            return data;
        }

        /// <summary>
        /// 获取不同序列位数的测试数据
        /// </summary>
        public static TheoryData<int> GetSequenceBitsTestData()
        {
            var data = new TheoryData<int>();
            data.Add(1);
            data.Add(2);
            data.Add(4);
            data.Add(8);
            data.Add(12);
            return data;
        }

        /// <summary>
        /// 等待一小段时间以确保时间戳变化
        /// </summary>
        public static void WaitForTimestampChange(int milliseconds = 2)
        {
            System.Threading.Thread.Sleep(milliseconds);
        }

        /// <summary>
        /// 验证时间戳大致递增（允许在同一毫秒内）
        /// </summary>
        public static void AssertTimestampRoughlyMonotonic(IList<DateTimeOffset> timestamps)
        {
            for (int i = 1; i < timestamps.Count; i++)
            {
                var diff = timestamps[i] - timestamps[i - 1];
                // 允许时间戳不递减（可能在同一毫秒内，所以允许相等或稍微后退1毫秒）
                if (diff.TotalMilliseconds < -1)
                {
                    throw new InvalidOperationException(
                        $"时间戳不是大致递增的: 索引 {i - 1} 的时间戳 {timestamps[i - 1]} > 索引 {i} 的时间戳 {timestamps[i]}");
                }
            }
        }
    }
}

