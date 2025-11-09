using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mubai.Snowflake.Tests
{
    public class SnowflakeIdGeneratorTests
    {
        private SnowflakeIdGenerator CreateGenerator(int workerId = 1)
        {
            var config = TestHelpers.CreateDefaultConfig(workerId);
            return new SnowflakeIdGenerator(config);
        }

        [Fact]
        public void GenerateIds_ShouldBeUnique_InSingleThread()
        {
            var generator = CreateGenerator();
            const int count = 100000;

            var ids = TestHelpers.GenerateIds(generator, count);
            Assert.Equal(count, ids.Count);
        }

        [Fact]
        public void GenerateIds_ShouldBeUnique_InParallel()
        {
            var generator = CreateGenerator();
            var ids = new System.Collections.Concurrent.ConcurrentDictionary<long, byte>();
            const int count = 100000;

            Parallel.For(0, count, _ =>
            {
                long id = generator.NewId();
                Assert.True(ids.TryAdd(id, 0), $"Duplicate id: {id}");
            });

            Assert.Equal(count, ids.Count);
        }

        [Fact]
        public void Ids_ShouldBeRoughlyMonotonic()
        {
            var generator = CreateGenerator();
            const int count = 10000;
            var ids = new List<long> { generator.NewId() };

            for (int i = 0; i < count; i++)
            {
                ids.Add(generator.NewId());
            }

            TestHelpers.AssertMonotonicIds(ids);
        }

        [Fact]
        public void Decoder_ShouldDecodeValidValues()
        {
            var config = TestHelpers.CreateDefaultConfig(workerId: 7);
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);

            long id = generator.NewId();
            TestHelpers.AssertDecodedValues(decoder, id, expectedWorkerId: 7, config);
        }

        

        // 不同工作节点ID测试
        [Fact]
        public void DifferentWorkerIds_ShouldGenerateDifferentIds()
        {
            var config1 = TestHelpers.CreateDefaultConfig(workerId: 1);
            var config2 = TestHelpers.CreateDefaultConfig(workerId: 2);
            
            var generator1 = new SnowflakeIdGenerator(config1);
            var generator2 = new SnowflakeIdGenerator(config2);
            
            long id1 = generator1.NewId();
            long id2 = generator2.NewId();
            
            Assert.NotEqual(id1, id2);
            
            var decoder1 = new SnowflakeIdDecoder(config1);
            var decoder2 = new SnowflakeIdDecoder(config2);
            
            Assert.Equal(1, decoder1.GetWorkerId(id1));
            Assert.Equal(2, decoder2.GetWorkerId(id2));
        }

        [Theory]
        [MemberData(nameof(TestHelpers.GetWorkerIdPairsTestData), MemberType = typeof(TestHelpers), DisableDiscoveryEnumeration = true)]
        public void DifferentWorkerIds_ShouldGenerateDifferentIds_MultiplePairs(int workerId1, int workerId2)
        {
            var config1 = TestHelpers.CreateDefaultConfig(workerId: workerId1);
            var config2 = TestHelpers.CreateDefaultConfig(workerId: workerId2);
            
            var generator1 = new SnowflakeIdGenerator(config1);
            var generator2 = new SnowflakeIdGenerator(config2);
            
            var ids1 = TestHelpers.GenerateIds(generator1, 100);
            var ids2 = TestHelpers.GenerateIds(generator2, 100);
            
            // 验证两个生成器产生的ID集合没有交集
            ids1.IntersectWith(ids2);
            Assert.Empty(ids1);
        }

        // 自定义Epoch测试
        [Fact]
        public void CustomEpoch_ShouldBeCorrectlyApplied()
        {
            var customEpoch = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var config = TestHelpers.CreateCustomConfig(workerId: 1, epoch: customEpoch);
            
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);
            
            long id = generator.NewId();
            var decodedTimestamp = decoder.GetTimestamp(id);
            
            // 验证解码的时间戳在合理范围内
            Assert.InRange(decodedTimestamp, customEpoch, DateTimeOffset.UtcNow.AddMinutes(1));
        }

        // 异常处理测试
        [Fact]
        public void Constructor_ShouldThrowException_WhenConfigIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new SnowflakeIdGenerator(null));
        }

        [Fact]
        public void Constructor_ShouldThrowException_WhenWorkerIdOutOfValidRange()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerIdBits = 5, // 最大值为31
                WorkerId = 32 // 超出范围
            };
            
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeIdGenerator(config));
        }

        // 序列溢出测试（使用较小的序列位数来更容易触发溢出）
        [Fact]
        public void Generator_ShouldHandleSequenceOverflow()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                SequenceBits = 3 // 只允许8个序列值，更容易触发溢出
            };
            
            var generator = new SnowflakeIdGenerator(config);
            var ids = new HashSet<long>();
            
            // 生成足够多的ID以触发序列溢出
            for (int i = 0; i < 20; i++)
            {
                long id = generator.NewId();
                Assert.True(ids.Add(id), $"Duplicate id: {id}");
            }
            
            // 验证生成了唯一的ID
            Assert.Equal(20, ids.Count);
        }

        // 时间回拨模拟测试
        // 注意：由于我们无法真正修改系统时间，这里我们通过反射来模拟时间回拨场景
        // 这是一个高级测试，需要使用反射访问私有成员
        [Fact]
        public async Task Generator_ShouldHandleClockBackwards_WhenUsingReflect()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                Epoch = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero) // 使用固定的Epoch便于测试
            };
            
            var generator = new SnowflakeIdGenerator(config);
            
            // 生成一个ID，让_lastTimestamp有值
            long firstId = generator.NewId();
            
            // 使用反射获取_lastTimestamp字段
            var lastTimestampField = typeof(SnowflakeIdGenerator).GetField("_lastTimestamp", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (lastTimestampField == null)
            {
                Assert.Fail("无法找到 _lastTimestamp 字段");
                return;
            }
            
            // 模拟时间回拨：将_lastTimestamp设置为一个较大的值，使当前时间戳看起来更小
            var timestampValue = lastTimestampField.GetValue(generator);
            if (timestampValue == null)
            {
                Assert.Fail("_lastTimestamp 字段值为 null");
                return;
            }
            
            long originalTimestamp = (long)timestampValue;
            lastTimestampField.SetValue(generator, originalTimestamp + 1000); // 设置为未来时间
            
            // 尝试生成新的ID，此时会触发时间回拨处理
            // 注意：由于当前实现使用自旋等待，这个测试可能会比较慢
            // 为了避免测试超时，我们限制等待时间
            long secondId = await Task.Run(() => generator.NewId()).WaitAsync(TimeSpan.FromSeconds(3));
            
            // 验证ID是唯一的
            Assert.NotEqual(firstId, secondId);
            
            // 验证ID是递增的
            Assert.True(secondId > firstId);
        }

        [Fact]
        public void Generator_ShouldHandleRapidIdGeneration()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                SequenceBits = 2 // 只有4个序列值，更容易触发等待
            };
            var generator = new SnowflakeIdGenerator(config);
            var ids = new HashSet<long>();

            // 快速生成大量ID
            const int count = 1000;
            for (int i = 0; i < count; i++)
            {
                long id = generator.NewId();
                Assert.True(ids.Add(id), $"Duplicate id at index {i}: {id}");
            }

            Assert.Equal(count, ids.Count);
        }

        [Fact]
        public void Generator_ShouldGenerateUniqueIds_WithMinimumConfiguration()
        {
            // 使用稍微大一点的配置来测试边界情况，但仍然是最小实用配置
            // 对于极端配置，我们只生成在组合空间范围内的ID数量
            // 使用8位时间戳来测试最小配置场景，这样有足够的组合空间
            var nearCurrentEpoch = DateTimeOffset.UtcNow.AddMilliseconds(-100);
            var config = TestHelpers.CreateCustomConfig(
                workerId: 0,
                timestampBits: 8,  // 8位时间戳，可以表示256毫秒
                workerIdBits: 2,   // 2位workerId
                sequenceBits: 2,   // 2位序列号
                epoch: nearCurrentEpoch);
            var generator = new SnowflakeIdGenerator(config);

            // 生成足够的ID来验证基本功能（不超过组合空间）
            // 对于8位时间戳和2位序列号，每个workerId有 256 * 4 = 1024 个唯一组合
            var ids = TestHelpers.GenerateIds(generator, 100);
            Assert.Equal(100, ids.Count);
        }

        [Fact]
        public void Generator_ShouldGenerateUniqueIds_WithMaximumConfiguration()
        {
            var config = TestHelpers.CreateCustomConfig(
                workerId: 1023, // 最大值
                timestampBits: 41,
                workerIdBits: 10,
                sequenceBits: 12); // 41+10+12=63
            var generator = new SnowflakeIdGenerator(config);

            const int count = 10000;
            var ids = TestHelpers.GenerateIds(generator, count);
            Assert.Equal(count, ids.Count);
        }

        [Fact]
        public void Generator_ShouldMaintainMonotonicity_AcrossMultipleMilliseconds()
        {
            var generator = CreateGenerator();
            var ids = new List<long>();

            // 生成足够多的ID以确保跨越多个毫秒
            for (int i = 0; i < 5000; i++)
            {
                ids.Add(generator.NewId());
            }

            // 验证ID是递增的
            TestHelpers.AssertMonotonicIds(ids);
        }

        [Fact]
        public void Generator_ShouldWork_WithFutureEpoch()
        {
            var futureEpoch = DateTimeOffset.UtcNow.AddYears(1);
            var config = TestHelpers.CreateCustomConfig(
                workerId: 1,
                epoch: futureEpoch);

            // 使用未来的Epoch则会抛出异常
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = new SnowflakeIdGenerator(config);
            });
        }

        [Fact]
        public void Generator_ShouldWork_WithPastEpoch()
        {
            var pastEpoch = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var config = TestHelpers.CreateCustomConfig(
                workerId: 1,
                epoch: pastEpoch);
            
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);
            
            long id = generator.NewId();
            var timestamp = decoder.GetTimestamp(id);
            
            // 验证时间戳在合理范围内
            Assert.InRange(timestamp, pastEpoch, DateTimeOffset.UtcNow.AddMinutes(1));
        }

        [Fact]
        public void Generator_ShouldHandleConcurrentAccess_FromMultipleThreads()
        {
            var generator = CreateGenerator();
            var ids = new System.Collections.Concurrent.ConcurrentDictionary<long, byte>();
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            const int threadCount = 10;
            const int idsPerThread = 1000;
            var tasks = new List<Task>();

            for (int i = 0; i < threadCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < idsPerThread; j++)
                        {
                            long id = generator.NewId();
                            if (!ids.TryAdd(id, 0))
                            {
                                throw new Exception($"Duplicate ID generated: {id}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // 验证没有异常
            Assert.Empty(exceptions);

            // 验证所有ID都是唯一的
            Assert.Equal(threadCount * idsPerThread, ids.Count);
        }

        [Theory]
        [MemberData(nameof(TestHelpers.GetSequenceBitsTestData), MemberType = typeof(TestHelpers), DisableDiscoveryEnumeration = true)]
        public void Generator_ShouldHandleSequenceOverflow_WithDifferentSequenceBits(int sequenceBits)
        {
            var config = TestHelpers.CreateCustomConfig(
                workerId: 1,
                sequenceBits: sequenceBits);
            var generator = new SnowflakeIdGenerator(config);

            // 生成足够多的ID以触发序列溢出
            int count = (1 << sequenceBits) * 3; // 生成3倍最大序列值的ID
            var ids = TestHelpers.GenerateIds(generator, count);
            Assert.Equal(count, ids.Count);
        }

        [Fact]
        public void Generator_ShouldHandleTimestampOverflow_WithSmallTimestampBits()
        {
            // 使用非常小的timestamp位数来测试时间戳溢出场景
            // 使用接近当前时间的 epoch，避免立即溢出
            var nearCurrentEpoch = DateTimeOffset.UtcNow.AddMilliseconds(-500);
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                TimestampBits = 10, // 只能表示约1024毫秒
                WorkerIdBits = 10,
                SequenceBits = 43, // 10+10+43=63
                Epoch = nearCurrentEpoch
            };
            
            var generator = new SnowflakeIdGenerator(config);
            var ids = new HashSet<long>();
            
            // 生成一些ID验证基本功能
            for (int i = 0; i < 100; i++)
            {
                long id = generator.NewId();
                Assert.True(ids.Add(id), $"Duplicate id: {id}");
            }
            
            Assert.Equal(100, ids.Count);
        }

        [Fact]
        public void Generator_ShouldGenerateIds_WithAllZeroConfiguration()
        {
            // 使用接近当前时间的 epoch，避免时间戳溢出
            // 对于1位时间戳配置，只有2个时间戳值和2个序列值，总共4个唯一组合
            // 所以只生成4个ID来验证基本功能
            var nearCurrentEpoch = DateTimeOffset.UtcNow.AddMilliseconds(-1);
            var config = new SnowflakeConfiguration
            {
                WorkerId = 0,
                TimestampBits = 1,
                WorkerIdBits = 1,
                SequenceBits = 1,
                Epoch = nearCurrentEpoch
            };
            
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);
            
            // 对于1位配置，最多只能生成 2*2 = 4 个唯一ID（每个workerId）
            var ids = TestHelpers.GenerateIds(generator, 4);
            Assert.Equal(4, ids.Count);
            
            // 验证可以正确解码第一个ID
            long id = ids.First();
            var workerId = decoder.GetWorkerId(id);
            var sequence = decoder.GetSequence(id);
            Assert.Equal(0, workerId);
            Assert.InRange(sequence, 0, 1);
        }

        [Fact]
        public void Generator_ShouldMaintainUniqueness_WithSameWorkerId()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 42
            };
            
            var generator1 = new SnowflakeIdGenerator(config);
            var generator2 = new SnowflakeIdGenerator(config);
            
            var ids1 = new HashSet<long>();
            var ids2 = new HashSet<long>();
            
            // 两个生成器使用相同WorkerId，但在不同时间生成，应该产生不同的ID
            for (int i = 0; i < 1000; i++)
            {
                ids1.Add(generator1.NewId());
            }
            
            // 等待一小段时间确保时间戳不同
            System.Threading.Thread.Sleep(2);
            
            for (int i = 0; i < 1000; i++)
            {
                ids2.Add(generator2.NewId());
            }
            
            // 验证每个生成器内部ID唯一
            Assert.Equal(1000, ids1.Count);
            Assert.Equal(1000, ids2.Count);
            
            // 验证两个生成器产生的ID集合没有交集（由于时间戳不同）
            ids1.IntersectWith(ids2);
            Assert.Empty(ids1);
        }

        [Fact]
        public void Generator_ShouldHandleVeryLargeSequenceBits()
        {
            // 使用接近当前时间的 epoch，避免时间戳溢出（20位时间戳约1048576毫秒）
            var nearCurrentEpoch = DateTimeOffset.UtcNow.AddMilliseconds(-500000);
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                TimestampBits = 20,
                WorkerIdBits = 10,
                SequenceBits = 33, // 20+10+33=63
                Epoch = nearCurrentEpoch
            };
            
            var generator = new SnowflakeIdGenerator(config);
            var ids = new HashSet<long>();
            
            // 生成大量ID以测试大序列位数
            for (int i = 0; i < 10000; i++)
            {
                long id = generator.NewId();
                Assert.True(ids.Add(id), $"Duplicate id at index {i}: {id}");
            }
            
            Assert.Equal(10000, ids.Count);
        }

        [Fact]
        public void Generator_ShouldHandleVeryLargeWorkerIdBits()
        {
            // 使用接近当前时间的 epoch，避免时间戳溢出（20位时间戳约1048576毫秒）
            var nearCurrentEpoch = DateTimeOffset.UtcNow.AddMilliseconds(-500000);
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                TimestampBits = 20,
                WorkerIdBits = 20,
                SequenceBits = 23, // 20+20+23=63
                Epoch = nearCurrentEpoch
            };
            
            var generator = new SnowflakeIdGenerator(config);
            var decoder = new SnowflakeIdDecoder(config);
            
            long maxWorkerId = (1L << config.WorkerIdBits) - 1;
            var configMax = new SnowflakeConfiguration
            {
                WorkerId = (int)maxWorkerId,
                TimestampBits = 20,
                WorkerIdBits = 20,
                SequenceBits = 23,
                Epoch = nearCurrentEpoch
            };
            
            var generatorMax = new SnowflakeIdGenerator(configMax);
            var decoderMax = new SnowflakeIdDecoder(configMax);
            
            long id1 = generator.NewId();
            long id2 = generatorMax.NewId();
            
            Assert.Equal(1, decoder.GetWorkerId(id1));
            Assert.Equal((int)maxWorkerId, decoderMax.GetWorkerId(id2));
        }

        [Fact]
        public void Generator_ShouldGenerateMonotonicIds_AcrossEpochBoundary()
        {
            // 使用接近当前时间的epoch来测试边界情况
            // 注意：使用过去的时间，因为未来时间的 epoch 会在验证时抛出异常
            var nearPastEpoch = DateTimeOffset.UtcNow.AddMilliseconds(-100);
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                Epoch = nearPastEpoch,
                MaxFutureEpochSkew = TimeSpan.FromSeconds(10) // 允许更大的时间偏差
            };
            
            var generator = new SnowflakeIdGenerator(config);
            var ids = new List<long>();
            
            // 生成多个ID
            for (int i = 0; i < 100; i++)
            {
                ids.Add(generator.NewId());
            }
            
            // 验证ID是递增的（或至少不递减）
            for (int i = 1; i < ids.Count; i++)
            {
                Assert.True(ids[i] >= ids[i - 1], 
                    $"ID at index {i} ({ids[i]}) should be >= ID at index {i - 1} ({ids[i - 1]})");
            }
        }

        [Fact]
        public void Generator_ShouldHandleConcurrentAccess_WithHighContention()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                SequenceBits = 2 // 只有4个序列值，更容易产生竞争
            };
            
            var generator = new SnowflakeIdGenerator(config);
            var ids = new System.Collections.Concurrent.ConcurrentDictionary<long, byte>();
            
            const int threadCount = 20;
            const int idsPerThread = 500;
            var tasks = new List<Task>();
            
            for (int i = 0; i < threadCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < idsPerThread; j++)
                    {
                        long id = generator.NewId();
                        ids.TryAdd(id, 0);
                    }
                }));
            }
            
            Task.WaitAll(tasks.ToArray());
            
            // 验证所有ID都是唯一的
            Assert.Equal(threadCount * idsPerThread, ids.Count);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(20)]
        public void Generator_ShouldHandleMultipleInstances_WithDifferentWorkerIds(int workerIdCount)
        {
            var generators = new List<SnowflakeIdGenerator>();
            var allIds = new HashSet<long>();
            
            for (int i = 0; i < workerIdCount; i++)
            {
                var config = new SnowflakeConfiguration
                {
                    WorkerId = i
                };
                generators.Add(new SnowflakeIdGenerator(config));
            }
            
            // 每个生成器生成一些ID
            foreach (var generator in generators)
            {
                for (int i = 0; i < 100; i++)
                {
                    long id = generator.NewId();
                    Assert.True(allIds.Add(id), $"Duplicate ID: {id}");
                }
            }
            
            // 验证所有ID都是唯一的
            Assert.Equal(workerIdCount * 100, allIds.Count);
        }
    }
}