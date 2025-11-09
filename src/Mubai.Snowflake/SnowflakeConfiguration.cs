using System;

namespace Mubai.Snowflake
{
    /// <summary>
    /// 雪花 ID 生成配置。
    /// </summary>
    public class SnowflakeConfiguration
    {
        /// <summary>
        /// 自定义 Epoch（时间起点），默认 2025-01-01 00:00:00 UTC。
        /// </summary>
        public DateTimeOffset Epoch { get; set; } =
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// 时间戳占用 bit 数，默认 41。
        /// </summary>
        public int TimestampBits { get; set; } = 41;

        /// <summary>
        /// WorkerId 占用 bit 数，默认 10。
        /// </summary>
        public int WorkerIdBits { get; set; } = 10;

        /// <summary>
        /// 同一毫秒内序列号占用 bit 数，默认 12。
        /// </summary>
        public int SequenceBits { get; set; } = 12;

        /// <summary>
        /// 当前节点的 WorkerId（0..(2^WorkerIdBits-1)）。
        /// 建议从配置或环境变量读取。
        /// </summary>
        public int WorkerId { get; set; }

        /// <summary>
        /// 允许 Epoch 比当前 UTC 时间超前的最大偏差（时钟误差容忍度）。
        /// 默认 1 分钟，不建议设太大；如果对时间很严格，可以设为 TimeSpan.Zero。
        /// </summary>
        public TimeSpan MaxFutureEpochSkew { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 校验配置合法性。
        /// </summary>
        public void Validate()
        {
            if (TimestampBits <= 0)
                throw new InvalidOperationException("TimestampBits must be positive.");

            if (WorkerIdBits <= 0)
                throw new InvalidOperationException("WorkerIdBits must be positive.");

            if (SequenceBits <= 0)
                throw new InvalidOperationException("SequenceBits must be positive.");

            if (TimestampBits + WorkerIdBits + SequenceBits > 63)
                throw new InvalidOperationException(
                    $"Total bits (TimestampBits + WorkerIdBits + SequenceBits) must be <= 63. " +
                    $"Current = {TimestampBits + WorkerIdBits + SequenceBits}.");

            long maxWorkerId = (1L << WorkerIdBits) - 1;
            if (WorkerId < 0 || WorkerId > maxWorkerId)
                throw new ArgumentOutOfRangeException(
                    nameof(WorkerId),
                    WorkerId,
                    $"WorkerId must be between 0 and {maxWorkerId}.");

            if (MaxFutureEpochSkew < TimeSpan.Zero)
            {
                throw new InvalidOperationException("MaxFutureEpochSkew must be non-negative.");
            }

            // Epoch 不能比当前时间超前超过 MaxFutureEpochSkew（默认 1 分钟）
            var nowUtc = DateTimeOffset.UtcNow;
            var delta = Epoch - nowUtc;

            if (delta > MaxFutureEpochSkew)
            {
                throw new InvalidOperationException(
                    $"Epoch {Epoch:o} is too far in the future. " +
                    $"Current UTC time is {nowUtc:o}. " +
                    $"Allowed future skew is {MaxFutureEpochSkew.TotalSeconds} seconds.");
            }
        }
    }
}
