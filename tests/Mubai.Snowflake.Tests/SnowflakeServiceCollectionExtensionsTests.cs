using Microsoft.Extensions.DependencyInjection;
using System;

namespace Mubai.Snowflake.Tests
{
    /// <summary>
    /// 雪花ID服务注册扩展测试类
    /// </summary>
    public class SnowflakeServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddSnowflakeIdGenerator_ShouldThrowException_WhenServicesIsNull()
        {
            IServiceCollection services = null;
            Assert.Throws<ArgumentNullException>(() => services.AddSnowflakeIdGenerator());
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldRegisterServices_WithDefaultConfiguration()
        {
            var services = new ServiceCollection();

            services.AddSnowflakeIdGenerator();

            var serviceProvider = services.BuildServiceProvider();

            // 验证配置已注册
            var config = serviceProvider.GetService<SnowflakeConfiguration>();
            Assert.NotNull(config);

            // 验证生成器已注册
            var generator = serviceProvider.GetService<IIdGenerator>();
            Assert.NotNull(generator);
            Assert.IsType<SnowflakeIdGenerator>(generator);

            // 验证解码器已注册
            var decoder = serviceProvider.GetService<IIdDecoder>();
            Assert.NotNull(decoder);
            Assert.IsType<SnowflakeIdDecoder>(decoder);
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldRegisterServices_WithCustomConfiguration()
        {
            var services = new ServiceCollection();
            var customWorkerId = 42;

            services.AddSnowflakeIdGenerator(config =>
            {
                config.WorkerId = customWorkerId;
                config.Epoch = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            });

            var serviceProvider = services.BuildServiceProvider();

            var config = serviceProvider.GetService<SnowflakeConfiguration>();
            Assert.NotNull(config);
            Assert.Equal(customWorkerId, config.WorkerId);
            Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), config.Epoch);
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldRegisterAsSingleton()
        {
            var services = new ServiceCollection();

            services.AddSnowflakeIdGenerator();

            var serviceProvider = services.BuildServiceProvider();

            // 多次获取应该是同一个实例
            var generator1 = serviceProvider.GetService<IIdGenerator>();
            var generator2 = serviceProvider.GetService<IIdGenerator>();

            Assert.Same(generator1, generator2);
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldValidateConfiguration()
        {
            var services = new ServiceCollection();

            // 配置无效的WorkerId
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                services.AddSnowflakeIdGenerator(config =>
                {
                    config.WorkerId = -1; // 无效值
                });
            });
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldValidateConfiguration_WhenBitsExceedLimit()
        {
            var services = new ServiceCollection();

            // 配置总位数超过63
            Assert.Throws<InvalidOperationException>(() =>
            {
                services.AddSnowflakeIdGenerator(config =>
                {
                    config.TimestampBits = 30;
                    config.WorkerIdBits = 20;
                    config.SequenceBits = 14; // 30+20+14=64 > 63
                });
            });
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldWork_WithRegisteredServices()
        {
            var services = new ServiceCollection();

            services.AddSnowflakeIdGenerator(config =>
            {
                config.WorkerId = 10;
            });

            var serviceProvider = services.BuildServiceProvider();

            // 验证可以正常使用生成器
            var generator = serviceProvider.GetRequiredService<IIdGenerator>();
            long id1 = generator.NewId();
            long id2 = generator.NewId();

            Assert.NotEqual(id1, id2);
            Assert.True(id2 > id1);

            // 验证可以正常使用解码器
            var decoder = serviceProvider.GetRequiredService<IIdDecoder>();
            var workerId = decoder.GetWorkerId(id1);
            Assert.Equal(10, workerId);
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldReturnSameServicesCollection()
        {
            var services = new ServiceCollection();

            var result = services.AddSnowflakeIdGenerator();

            Assert.Same(services, result);
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldWork_WithNullConfigureCallback()
        {
            var services = new ServiceCollection();

            // 传递null作为configure回调应该使用默认配置
            services.AddSnowflakeIdGenerator(null);

            var serviceProvider = services.BuildServiceProvider();
            var generator = serviceProvider.GetService<IIdGenerator>();
            var config = serviceProvider.GetService<SnowflakeConfiguration>();

            Assert.NotNull(generator);
            Assert.NotNull(config);
            Assert.Equal(41, config.TimestampBits);
            Assert.Equal(10, config.WorkerIdBits);
            Assert.Equal(12, config.SequenceBits);
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldAllowMultipleRegistrations()
        {
            var services = new ServiceCollection();

            // 多次注册应该不会抛出异常
            services.AddSnowflakeIdGenerator();
            services.AddSnowflakeIdGenerator(config => config.WorkerId = 2);

            var serviceProvider = services.BuildServiceProvider();

            // 应该获取最后一次注册的配置
            var configs = serviceProvider.GetServices<SnowflakeConfiguration>();
            var generators = serviceProvider.GetServices<IIdGenerator>();

            // 验证有多个注册
            Assert.True(configs.Count() >= 1);
            Assert.True(generators.Count() >= 1);
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldRegisterServices_WithComplexConfiguration()
        {
            var services = new ServiceCollection();
            var customEpoch = new DateTimeOffset(2020, 6, 15, 12, 30, 0, TimeSpan.Zero);

            services.AddSnowflakeIdGenerator(config =>
            {
                config.WorkerId = 100;
                config.Epoch = customEpoch;
                config.TimestampBits = 35;
                config.WorkerIdBits = 12;
                config.SequenceBits = 16; // 35+12+16=63
            });

            var serviceProvider = services.BuildServiceProvider();
            var config = serviceProvider.GetService<SnowflakeConfiguration>();

            Assert.NotNull(config);
            Assert.Equal(100, config.WorkerId);
            Assert.Equal(customEpoch, config.Epoch);
            Assert.Equal(35, config.TimestampBits);
            Assert.Equal(12, config.WorkerIdBits);
            Assert.Equal(16, config.SequenceBits);
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldThrowException_WhenConfigurationIsInvalid()
        {
            var services = new ServiceCollection();

            // 配置无效的WorkerId
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                services.AddSnowflakeIdGenerator(config =>
                {
                    config.WorkerId = 2000; // 超出默认范围
                });
            });
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldWork_WithMinimalConfiguration()
        {
            var services = new ServiceCollection();

            services.AddSnowflakeIdGenerator(config =>
            {
                config.WorkerId = 0; // 最小WorkerId
            });

            var serviceProvider = services.BuildServiceProvider();
            var generator = serviceProvider.GetService<IIdGenerator>();
            var decoder = serviceProvider.GetService<IIdDecoder>();

            Assert.NotNull(generator);
            Assert.NotNull(decoder);

            // 验证可以正常生成和解码ID
            long id = generator.NewId();
            int workerId = decoder.GetWorkerId(id);
            Assert.Equal(0, workerId);
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldWork_WithMaximumConfiguration()
        {
            var services = new ServiceCollection();

            services.AddSnowflakeIdGenerator(config =>
            {
                config.WorkerId = 1023; // 最大WorkerId
                config.TimestampBits = 41;
                config.WorkerIdBits = 10;
                config.SequenceBits = 12; // 41+10+12=63
            });

            var serviceProvider = services.BuildServiceProvider();
            var generator = serviceProvider.GetService<IIdGenerator>();
            var decoder = serviceProvider.GetService<IIdDecoder>();

            Assert.NotNull(generator);
            Assert.NotNull(decoder);

            // 验证可以正常生成和解码ID
            long id = generator.NewId();
            int workerId = decoder.GetWorkerId(id);
            Assert.Equal(1023, workerId);
        }

        [Fact]
        public void AddSnowflakeIdGenerator_ShouldRegisterAllRequiredServices()
        {
            var services = new ServiceCollection();

            services.AddSnowflakeIdGenerator();

            var serviceProvider = services.BuildServiceProvider();

            // 验证所有必需的服务都已注册
            var config = serviceProvider.GetService<SnowflakeConfiguration>();
            var generator = serviceProvider.GetService<IIdGenerator>();
            var decoder = serviceProvider.GetService<IIdDecoder>();

            Assert.NotNull(config);
            Assert.NotNull(generator);
            Assert.NotNull(decoder);
            Assert.IsType<SnowflakeIdGenerator>(generator);
            Assert.IsType<SnowflakeIdDecoder>(decoder);
        }
    }
}

