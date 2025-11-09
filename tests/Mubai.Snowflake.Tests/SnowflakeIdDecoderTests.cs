using System;
using System.Collections.Generic;
using System.Linq;

namespace Mubai.Snowflake.Tests
{
    /// <summary>
    /// 雪花ID解码器测试类
    /// </summary>
    public class SnowflakeIdDecoderTests
    {
        [Fact]
        public void Constructor_ShouldThrowException_WhenConfigIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new SnowflakeIdDecoder(null));
        }

        [Fact]
        public void Constructor_ShouldThrowException_WhenConfigIsInvalid()
        {
            var config = TestHelpers.CreateDefaultConfig(workerId: 1);
            config.TimestampBits = 0; // 无效配置

            Assert.Throws<InvalidOperationException>(() => new SnowflakeIdDecoder(config));
        }

        [Fact]
        public void GetTimestamp_ShouldReturnCorrectTimestamp()
        {
            var epoch = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var config = TestHelpers.CreateCustomConfig(workerId: 1, epoch: epoch);
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            var beforeGeneration = DateTimeOffset.UtcNow;
            long id = generator.NewId();
            var afterGeneration = DateTimeOffset.UtcNow;

            var decodedTimestamp = decoder.GetTimestamp(id);

            // 验证解码的时间戳在生成前后之间
            Assert.InRange(decodedTimestamp, beforeGeneration.AddSeconds(-1), afterGeneration.AddSeconds(1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(31)]
        [InlineData(511)]
        [InlineData(1023)]
        public void GetWorkerId_ShouldReturnCorrectWorkerId(int workerId)
        {
            var config = TestHelpers.CreateDefaultConfig(workerId);
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            long id = generator.NewId();
            int decodedWorkerId = decoder.GetWorkerId(id);

            Assert.Equal(workerId, decodedWorkerId);
        }

        [Fact]
        public void GetSequence_ShouldReturnCorrectSequence()
        {
            var config = TestHelpers.CreateCustomConfig(
                workerId: 1,
                sequenceBits: 3); // 使用较小的序列位数便于测试
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            // 在同一毫秒内快速生成多个ID
            var sequences = new List<int>();
            var ids = new List<long>();

            for (int i = 0; i < 10; i++)
            {
                long id = generator.NewId();
                ids.Add(id);
                sequences.Add(decoder.GetSequence(id));
            }

            // 验证序列号在有效范围内
            foreach (var seq in sequences)
            {
                Assert.InRange(seq, 0, (1 << config.SequenceBits) - 1);
            }

            // 验证所有ID都是唯一的
            TestHelpers.AssertUniqueIds(ids);
        }

        [Fact]
        public void Decoder_ShouldWorkWithCustomConfiguration()
        {
            var config = TestHelpers.CreateCustomConfig(
                workerId: 15,
                timestampBits: 39,
                workerIdBits: 8,
                sequenceBits: 16,
                epoch: new DateTimeOffset(2021, 6, 1, 0, 0, 0, TimeSpan.Zero));

            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            long id = generator.NewId();
            TestHelpers.AssertDecodedValues(decoder, id, expectedWorkerId: 15, config);
        }

        [Fact]
        public void Decoder_ShouldHandleZeroId()
        {
            var config = TestHelpers.CreateDefaultConfig(workerId: 0);
            var decoder = new SnowflakeIdDecoder(config);

            // 解码零ID（虽然实际不会生成，但测试解码器的健壮性）
            long zeroId = 0;
            var timestamp = decoder.GetTimestamp(zeroId);
            var workerId = decoder.GetWorkerId(zeroId);
            var sequence = decoder.GetSequence(zeroId);

            Assert.Equal(0, workerId);
            Assert.Equal(0, sequence);
            // 时间戳应该是epoch时间
            Assert.True(timestamp >= config.Epoch);
        }

        [Fact]
        public void Decoder_ShouldHandleMaximumValues()
        {
            var config = TestHelpers.CreateCustomConfig(
                workerId: 1023, // 最大值 (2^10 - 1)
                timestampBits: 41,
                workerIdBits: 10,
                sequenceBits: 12);
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            // 生成一个ID并验证可以正确解码
            long id = generator.NewId();
            TestHelpers.AssertDecodedValues(decoder, id, expectedWorkerId: 1023, config);
        }

        [Fact]
        public void Decoder_ShouldBeConsistent_AcrossMultipleDecodes()
        {
            var config = TestHelpers.CreateDefaultConfig(workerId: 5);
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            long id = generator.NewId();

            // 多次解码同一ID应该得到相同的结果
            var timestamp1 = decoder.GetTimestamp(id);
            var workerId1 = decoder.GetWorkerId(id);
            var sequence1 = decoder.GetSequence(id);

            var timestamp2 = decoder.GetTimestamp(id);
            var workerId2 = decoder.GetWorkerId(id);
            var sequence2 = decoder.GetSequence(id);

            Assert.Equal(timestamp1, timestamp2);
            Assert.Equal(workerId1, workerId2);
            Assert.Equal(sequence1, sequence2);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(511)]
        public void Decoder_ShouldWorkWithDifferentWorkerIds(int workerId)
        {
            var config = TestHelpers.CreateDefaultConfig(workerId);
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            long id = generator.NewId();
            int decodedWorkerId = decoder.GetWorkerId(id);

            Assert.Equal(workerId, decodedWorkerId);
        }

        [Fact]
        public void Decoder_ShouldHandleNegativeId()
        {
            var config = TestHelpers.CreateDefaultConfig(workerId: 1);
            var decoder = new SnowflakeIdDecoder(config);

            // 测试负数ID（虽然实际不会生成，但测试解码器的健壮性）
            long negativeId = -1L;
            var timestamp = decoder.GetTimestamp(negativeId);
            var workerId = decoder.GetWorkerId(negativeId);
            var sequence = decoder.GetSequence(negativeId);

            // 验证解码不会抛出异常，但结果可能不合理
            Assert.NotNull(timestamp);
            // 验证结果在合理范围内（至少不会崩溃）
            Assert.True(workerId >= 0 && workerId <= int.MaxValue);
            Assert.True(sequence >= 0 && sequence <= int.MaxValue);
        }

        [Fact]
        public void Decoder_ShouldHandleMaximumLongValue()
        {
            var config = TestHelpers.CreateDefaultConfig(workerId: 1);
            var decoder = new SnowflakeIdDecoder(config);

            // 测试最大long值
            long maxId = long.MaxValue;
            var timestamp = decoder.GetTimestamp(maxId);
            var workerId = decoder.GetWorkerId(maxId);
            var sequence = decoder.GetSequence(maxId);

            // 验证解码不会抛出异常
            Assert.NotNull(timestamp);
            Assert.InRange(workerId, 0, int.MaxValue);
            Assert.InRange(sequence, 0, int.MaxValue);
        }

        [Fact]
        public void Decoder_ShouldHandleCrossConfigurationDecoding()
        {
            // 使用配置1生成ID
            var config1 = TestHelpers.CreateCustomConfig(
                workerId: 5,
                timestampBits: 41,
                workerIdBits: 10,
                sequenceBits: 12);
            var generator1 = new SnowflakeIdGenerator(config1);
            long id1 = generator1.NewId();

            // 使用配置2解码（不同配置）
            var config2 = TestHelpers.CreateCustomConfig(
                workerId: 10,
                timestampBits: 39,
                workerIdBits: 8,
                sequenceBits: 16);
            var decoder2 = new SnowflakeIdDecoder(config2);

            // 使用不同配置解码应该不会抛出异常，但结果可能不正确
            var timestamp = decoder2.GetTimestamp(id1);
            var workerId = decoder2.GetWorkerId(id1);
            var sequence = decoder2.GetSequence(id1);

            // 验证解码不会抛出异常
            Assert.NotNull(timestamp);
            Assert.InRange(workerId, 0, int.MaxValue);
            Assert.InRange(sequence, 0, int.MaxValue);
        }

        [Fact]
        public void Decoder_ShouldHandleIdsFromDifferentEpochs()
        {
            var epoch1 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var config1 = TestHelpers.CreateCustomConfig(workerId: 1, epoch: epoch1);
            var generator1 = new SnowflakeIdGenerator(config1);
            var decoder1 = new SnowflakeIdDecoder(config1);
            long id1 = generator1.NewId();

            var epoch2 = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var config2 = TestHelpers.CreateCustomConfig(workerId: 1, epoch: epoch2);
            var decoder2 = new SnowflakeIdDecoder(config2);

            // 使用不同epoch的解码器解码
            var timestamp1 = decoder1.GetTimestamp(id1);
            var timestamp2 = decoder2.GetTimestamp(id1);

            // 验证两个解码器都能正常工作，但结果不同
            Assert.NotNull(timestamp1);
            Assert.NotNull(timestamp2);
            Assert.NotEqual(timestamp1, timestamp2);
        }

        [Theory]
        [InlineData(1, 1, 1, 0)] // 最小配置
        [InlineData(41, 10, 12, 1)] // 默认配置
        [InlineData(39, 8, 16, 100)] // 自定义配置
        [InlineData(20, 5, 38, 31)] // 极端配置
        public void Decoder_ShouldCorrectlyDecodeAllComponents(
            int timestampBits, int workerIdBits, int sequenceBits, int workerId)
        {
            var config = TestHelpers.CreateCustomConfig(
                workerId: workerId,
                timestampBits: timestampBits,
                workerIdBits: workerIdBits,
                sequenceBits: sequenceBits);

            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            long id = generator.NewId();
            TestHelpers.AssertDecodedValues(decoder, id, expectedWorkerId: workerId, config);
        }

        [Fact]
        public void Decoder_ShouldHandleRapidIdGeneration_WithCorrectSequence()
        {
            var config = TestHelpers.CreateCustomConfig(
                workerId: 1,
                sequenceBits: 4); // 16个序列值
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            var sequences = new List<int>();
            var ids = new List<long>();

            // 快速生成多个ID
            for (int i = 0; i < 50; i++)
            {
                long id = generator.NewId();
                ids.Add(id);
                sequences.Add(decoder.GetSequence(id));
            }

            // 验证所有ID都是唯一的
            TestHelpers.AssertUniqueIds(ids);

            // 验证序列号在有效范围内
            foreach (var seq in sequences)
            {
                Assert.InRange(seq, 0, (1 << config.SequenceBits) - 1);
            }
        }
    }
}

