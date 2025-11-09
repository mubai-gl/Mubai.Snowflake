using System;
using Microsoft.Extensions.DependencyInjection;

namespace Mubai.Snowflake
{
    /// <summary>
    /// 依赖注入扩展。
    /// </summary>
    public static class SnowflakeServiceCollectionExtensions
    {
        /// <summary>
        /// 注册雪花 ID 生成器及其配置。
        /// </summary>
        /// <param name="services">DI 容器。</param>
        /// <param name="configure">配置回调。</param>
        /// <returns></returns>
        public static IServiceCollection AddSnowflakeIdGenerator(
            this IServiceCollection services,
            Action<SnowflakeConfiguration> configure = null)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));

            var config = new SnowflakeConfiguration();
            configure?.Invoke(config);
            config.Validate();

            services.AddSingleton(config);
            services.AddSingleton<IIdGenerator, SnowflakeIdGenerator>();
            services.AddSingleton<IIdDecoder, SnowflakeIdDecoder>();

            return services;
        }
    }
}
