using System;
using System.Collections.Generic;
using System.Linq;

namespace Mubai.Snowflake.Tests
{
    /// <summary>
    /// 集成测试：验证生成器与解码器的完整工作流程
    /// </summary>
    public class IntegrationTests
    {
        [Fact]
        public void GeneratorAndDecoder_ShouldWorkTogether_WithDefaultConfiguration()
        {
            var config = TestHelpers.CreateDefaultConfig(workerId: 42);
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            // 生成多个ID并验证可以正确解码
            for (int i = 0; i < 1000; i++)
            {
                long id = generator.NewId();
                TestHelpers.AssertDecodedValues(decoder, id, expectedWorkerId: 42, config);
            }
        }

        [Fact]
        public void GeneratorAndDecoder_ShouldWorkTogether_WithCustomConfiguration()
        {
            // 使用接近当前时间的 epoch，避免时间戳溢出（35位时间戳约34359738368毫秒）
            var nearCurrentEpoch = DateTimeOffset.UtcNow.AddMilliseconds(-1000000);
            var config = TestHelpers.CreateCustomConfig(
                workerId: 100,
                timestampBits: 35,
                workerIdBits: 12,
                sequenceBits: 16,
                epoch: nearCurrentEpoch);

            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            var ids = new HashSet<long>();
            var timestamps = new List<DateTimeOffset>();

            // 生成多个ID
            for (int i = 0; i < 5000; i++)
            {
                long id = generator.NewId();
                Assert.True(ids.Add(id), $"Duplicate ID: {id}");

                // 验证 WorkerId 和 Sequence
                var workerId = decoder.GetWorkerId(id);
                var sequence = decoder.GetSequence(id);
                Assert.Equal(100, workerId);
                Assert.InRange(sequence, 0, (1 << config.SequenceBits) - 1);
                
                // 验证时间戳
                var timestamp = decoder.GetTimestamp(id);
                timestamps.Add(timestamp);
                // 时间戳应该不早于 epoch（允许一些容差）
                Assert.True(timestamp >= config.Epoch.AddYears(-1), 
                    $"时间戳 {timestamp} 不应该远早于 Epoch {config.Epoch}");
            }

            // 验证所有ID都是唯一的
            Assert.Equal(5000, ids.Count);

            // 验证时间戳大致递增（允许一些波动）
            TestHelpers.AssertTimestampRoughlyMonotonic(timestamps);
        }

        [Fact]
        public void GeneratorAndDecoder_ShouldMaintainConsistency_AcrossMultipleGenerations()
        {
            var config = TestHelpers.CreateDefaultConfig(workerId: 7);
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            var generatedData = new List<(long Id, DateTimeOffset Timestamp, int WorkerId, int Sequence)>();

            // 生成大量ID并记录解码结果
            for (int i = 0; i < 10000; i++)
            {
                long id = generator.NewId();
                var timestamp = decoder.GetTimestamp(id);
                var workerId = decoder.GetWorkerId(id);
                var sequence = decoder.GetSequence(id);

                generatedData.Add((id, timestamp, workerId, sequence));
            }

            // 验证所有ID都是唯一的
            TestHelpers.AssertUniqueIds(generatedData.Select(d => d.Id));

            // 验证所有WorkerId都正确
            Assert.All(generatedData, d => Assert.Equal(7, d.WorkerId));

            // 验证可以重新解码得到相同结果
            foreach (var data in generatedData)
            {
                var timestamp = decoder.GetTimestamp(data.Id);
                var workerId = decoder.GetWorkerId(data.Id);
                var sequence = decoder.GetSequence(data.Id);

                Assert.Equal(data.Timestamp, timestamp);
                Assert.Equal(data.WorkerId, workerId);
                Assert.Equal(data.Sequence, sequence);
            }
        }

        [Fact]
        public void GeneratorAndDecoder_ShouldWork_WithDifferentEpochs()
        {
            var epoch1 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var config1 = TestHelpers.CreateCustomConfig(workerId: 1, epoch: epoch1);

            var epoch2 = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var config2 = TestHelpers.CreateCustomConfig(workerId: 1, epoch: epoch2);

            var generator1 = new SnowflakeIdGenerator(config1);
            var decoder1 = new SnowflakeIdDecoder(config1);

            var generator2 = new SnowflakeIdGenerator(config2);
            var decoder2 = new SnowflakeIdDecoder(config2);

            long id1 = generator1.NewId();
            long id2 = generator2.NewId();

            var timestamp1 = decoder1.GetTimestamp(id1);
            var timestamp2 = decoder2.GetTimestamp(id2);

            // 验证两个系统都能正常工作
            Assert.InRange(timestamp1, epoch1, DateTimeOffset.UtcNow.AddMinutes(1));
            Assert.InRange(timestamp2, epoch2, DateTimeOffset.UtcNow.AddMinutes(1));

            // 验证WorkerId都正确
            Assert.Equal(1, decoder1.GetWorkerId(id1));
            Assert.Equal(1, decoder2.GetWorkerId(id2));
        }

        [Fact]
        public void GeneratorAndDecoder_ShouldHandleRapidGeneration_WithCorrectDecoding()
        {
            var config = TestHelpers.CreateCustomConfig(
                workerId: 5,
                sequenceBits: 4); // 较小的序列位数，更容易触发等待

            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            var results = new List<(long Id, int Sequence)>();

            // 快速生成大量ID
            for (int i = 0; i < 10000; i++)
            {
                long id = generator.NewId();
                int sequence = decoder.GetSequence(id);
                results.Add((id, sequence));
            }

            // 验证所有ID都是唯一的
            TestHelpers.AssertUniqueIds(results.Select(r => r.Id));

            // 验证序列号在有效范围内
            Assert.All(results, r => 
                Assert.InRange(r.Sequence, 0, (1 << config.SequenceBits) - 1));

            // 验证WorkerId都正确
            Assert.All(results, r => 
                Assert.Equal(5, decoder.GetWorkerId(r.Id)));
        }

        [Fact]
        public void GeneratorAndDecoder_ShouldWork_WithExtremeConfigurations()
        {
            // 测试最小配置 - 使用接近当前时间的 epoch，避免时间戳溢出
            // 对于1位配置，只有4个唯一组合，所以只生成1个ID验证基本功能
            var nearCurrentEpoch = DateTimeOffset.UtcNow.AddMilliseconds(-1);
            var minConfig = TestHelpers.CreateCustomConfig(
                workerId: 0,
                timestampBits: 1,
                workerIdBits: 1,
                sequenceBits: 1,
                epoch: nearCurrentEpoch);

            var minGenerator = new SnowflakeIdGenerator(minConfig);
            var minDecoder = new SnowflakeIdDecoder(minConfig);

            long minId = minGenerator.NewId();
            TestHelpers.AssertDecodedValues(minDecoder, minId, expectedWorkerId: 0, minConfig);

            // 测试最大配置
            var maxConfig = TestHelpers.CreateCustomConfig(
                workerId: 1023,
                timestampBits: 41,
                workerIdBits: 10,
                sequenceBits: 12);

            var maxGenerator = new SnowflakeIdGenerator(maxConfig);
            var maxDecoder = new SnowflakeIdDecoder(maxConfig);

            long maxId = maxGenerator.NewId();
            TestHelpers.AssertDecodedValues(maxDecoder, maxId, expectedWorkerId: 1023, maxConfig);
        }

        [Fact]
        public void GeneratorAndDecoder_ShouldMaintainIdUniqueness_WithMultipleWorkers()
        {
            const int workerCount = 10;
            var generators = new List<SnowflakeIdGenerator>();
            var decoders = new List<SnowflakeIdDecoder>();
            var allIds = new HashSet<long>();

            // 创建多个工作节点
            for (int i = 0; i < workerCount; i++)
            {
                var config = TestHelpers.CreateDefaultConfig(workerId: i);
                generators.Add(new SnowflakeIdGenerator(config));
                decoders.Add(new SnowflakeIdDecoder(config));
            }

            // 每个工作节点生成ID
            for (int i = 0; i < workerCount; i++)
            {
                for (int j = 0; j < 1000; j++)
                {
                    long id = generators[i].NewId();
                    Assert.True(allIds.Add(id), $"Duplicate ID: {id}");

                    // 验证可以正确解码
                    TestHelpers.AssertDecodedValues(decoders[i], id, expectedWorkerId: i, 
                        TestHelpers.CreateDefaultConfig(workerId: i));
                }
            }

            // 验证所有ID都是唯一的
            Assert.Equal(workerCount * 1000, allIds.Count);
        }
    }
}

