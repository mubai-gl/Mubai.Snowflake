namespace Mubai.Snowflake.Tests
{
    /// <summary>
    /// 配置验证测试
    /// </summary>
    public class ConfigurationTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Configuration_ShouldThrowException_WhenTimestampBitsIsZeroOrNegative(int timestampBits)
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                TimestampBits = timestampBits
            };

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Configuration_ShouldThrowException_WhenWorkerIdBitsIsZeroOrNegative(int workerIdBits)
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                WorkerIdBits = workerIdBits
            };

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Configuration_ShouldThrowException_WhenSequenceBitsIsZeroOrNegative(int sequenceBits)
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                SequenceBits = sequenceBits
            };

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Theory]
        [MemberData(nameof(TestHelpers.GetInvalidBitCombinationsTestData), MemberType = typeof(TestHelpers), DisableDiscoveryEnumeration = true)]
        public void Configuration_ShouldThrowException_WhenTotalBitsExceeds63(
            int timestampBits, int workerIdBits, int sequenceBits)
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                TimestampBits = timestampBits,
                WorkerIdBits = workerIdBits,
                SequenceBits = sequenceBits
            };

            // 验证总位数超过63
            Assert.True(timestampBits + workerIdBits + sequenceBits > 63,
                $"总位数应该超过63，实际为 {timestampBits + workerIdBits + sequenceBits}");

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Fact]
        public void Configuration_ShouldSucceed_WhenTotalBitsEquals63()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1,
                TimestampBits = 41,
                WorkerIdBits = 10,
                SequenceBits = 12 // 41+10+12=63 <= 63
            };

            // 不应该抛出异常
            config.Validate();
        }

        [Theory]
        [MemberData(nameof(TestHelpers.GetInvalidWorkerIdTestData), MemberType = typeof(TestHelpers), DisableDiscoveryEnumeration = true)]
        public void Configuration_ShouldThrowException_WhenWorkerIdOutOfRange(int workerId)
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = workerId
            };
            Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
        }

        [Theory]
        [MemberData(nameof(TestHelpers.GetValidWorkerIdTestData), MemberType = typeof(TestHelpers), DisableDiscoveryEnumeration = true)]
        public void Configuration_ShouldSucceed_WhenWorkerIdInValidRange(int workerId)
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = workerId
            };

            // 不应该抛出异常
            var exception = Record.Exception(() => config.Validate());
            Assert.Null(exception);
        }

        [Fact]
        public void Configuration_ShouldThrowException_WhenWorkerIdExceedsCustomMaxValue()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerIdBits = 5, // 最大值是 2^5 - 1 = 31
                WorkerId = 32 // 超出范围
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
        }

        [Fact]
        public void Configuration_ShouldUseDefaultValues()
        {
            var config = TestHelpers.CreateDefaultConfig(workerId: 1);

            Assert.Equal(41, config.TimestampBits);
            Assert.Equal(10, config.WorkerIdBits);
            Assert.Equal(12, config.SequenceBits);
            Assert.Equal(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), config.Epoch);
            
            // 验证默认配置是有效的
            var exception = Record.Exception(() => config.Validate());
            Assert.Null(exception);
        }

        [Fact]
        public void Configuration_ShouldAllowCustomEpoch()
        {
            var customEpoch = new DateTimeOffset(2020, 6, 15, 12, 30, 0, TimeSpan.Zero);
            var config = TestHelpers.CreateCustomConfig(
                workerId: 1,
                epoch: customEpoch);

            var exception = Record.Exception(() => config.Validate());
            Assert.Null(exception);
            Assert.Equal(customEpoch, config.Epoch);
        }

        [Theory]
        [MemberData(nameof(TestHelpers.GetValidBitCombinationsTestData), MemberType = typeof(TestHelpers), DisableDiscoveryEnumeration = true)]
        public void Configuration_ShouldSucceed_WithValidBitCombinations(
            int timestampBits, int workerIdBits, int sequenceBits, int workerId)
        {
            var config = new SnowflakeConfiguration
            {
                TimestampBits = timestampBits,
                WorkerIdBits = workerIdBits,
                SequenceBits = sequenceBits,
                WorkerId = workerId
            };

            // 验证总位数不超过63
            Assert.True(timestampBits + workerIdBits + sequenceBits <= 63, 
                $"总位数 {timestampBits + workerIdBits + sequenceBits} 超过 63");

            // 验证WorkerId在有效范围内
            long maxWorkerId = (1L << workerIdBits) - 1;
            Assert.True(workerId >= 0 && workerId <= maxWorkerId,
                $"WorkerId {workerId} 超出范围 [0, {maxWorkerId}]");

            // 不应该抛出异常
            var exception = Record.Exception(() => config.Validate());
            Assert.Null(exception);
        }


        [Theory]
        [MemberData(nameof(TestHelpers.GetBoundaryBitCombinationsTestData), MemberType = typeof(TestHelpers), DisableDiscoveryEnumeration = true)]
        public void Configuration_ShouldSucceed_WhenTotalBitsIsExactly63(
            int timestampBits, int workerIdBits, int sequenceBits)
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 0,
                TimestampBits = timestampBits,
                WorkerIdBits = workerIdBits,
                SequenceBits = sequenceBits
            };

            // 验证总位数等于63
            Assert.Equal(63, timestampBits + workerIdBits + sequenceBits);

            // 不应该抛出异常
            var exception = Record.Exception(() => config.Validate());
            Assert.Null(exception);
        }

        [Fact]
        public void Configuration_ShouldThrowException_WhenWorkerIdIsNegative_WithCustomBits()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerIdBits = 5, // 最大值是31
                WorkerId = -1
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => config.Validate());
        }

        [Fact]
        public void Configuration_ShouldSucceed_WhenWorkerIdIsZero()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 0
            };

            config.Validate();
        }

        [Fact]
        public void Configuration_ShouldSucceed_WhenWorkerIdIsMaximum_WithDefaultBits()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1023 // 2^10 - 1
            };

            config.Validate();
        }

        [Theory]
        [InlineData(1, 1)] // 最大值是1 (2^1 - 1 = 1)
        [InlineData(2, 3)] // 最大值是3 (2^2 - 1 = 3)
        [InlineData(3, 7)] // 最大值是7 (2^3 - 1 = 7)
        [InlineData(4, 15)] // 最大值是15 (2^4 - 1 = 15)
        [InlineData(5, 31)] // 最大值是31 (2^5 - 1 = 31)
        public void Configuration_ShouldSucceed_WhenWorkerIdIsMaximum_WithCustomBits(
            int workerIdBits, int maxWorkerId)
        {
            var config = new SnowflakeConfiguration
            {
                WorkerIdBits = workerIdBits,
                WorkerId = maxWorkerId
            };

            // 验证最大值计算正确
            long expectedMax = (1L << workerIdBits) - 1;
            Assert.Equal(expectedMax, maxWorkerId);

            var exception = Record.Exception(() => config.Validate());
            Assert.Null(exception);
        }

        [Fact]
        public void Configuration_ShouldUseDefaultEpoch_WhenNotSet()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1
            };

            var expectedEpoch = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.Equal(expectedEpoch, config.Epoch);
        }

        [Fact]
        public void Configuration_ShouldAllowEpochModification()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1
            };

            var customEpoch = new DateTimeOffset(2023, 6, 15, 12, 30, 45, TimeSpan.Zero);
            config.Epoch = customEpoch;

            Assert.Equal(customEpoch, config.Epoch);
            config.Validate(); // 应该仍然有效
        }

        [Fact]
        public void Configuration_ShouldAllowBitModification()
        {
            var config = new SnowflakeConfiguration
            {
                WorkerId = 1
            };

            config.TimestampBits = 30;
            config.WorkerIdBits = 15;
            config.SequenceBits = 18; // 30+15+18=63

            config.Validate(); // 应该仍然有效
        }
    }
}
