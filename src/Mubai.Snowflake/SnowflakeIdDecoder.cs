using System;

namespace Mubai.Snowflake
{
    /// <summary>
    /// 雪花 ID 解码器实现。
    /// 和 SnowflakeConfiguration 使用同一套位分配。
    /// </summary>
    public class SnowflakeIdDecoder : IIdDecoder
    {
        private readonly long _epochMs;
        private readonly int _timestampBits;
        private readonly int _workerIdBits;
        private readonly int _sequenceBits;

        private readonly int _workerIdShift;
        private readonly int _timestampShift;

        private readonly long _sequenceMask;
        private readonly long _workerIdMask;

        public SnowflakeIdDecoder(SnowflakeConfiguration config)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));

            config.Validate();

            _epochMs = config.Epoch.ToUnixTimeMilliseconds();
            _timestampBits = config.TimestampBits;
            _workerIdBits = config.WorkerIdBits;
            _sequenceBits = config.SequenceBits;

            _workerIdShift = _sequenceBits;
            _timestampShift = _sequenceBits + _workerIdBits;

            _sequenceMask = (1L << _sequenceBits) - 1;
            _workerIdMask = ((1L << _workerIdBits) - 1) << _workerIdShift;
        }

        /// <inheritdoc />
        public DateTimeOffset GetTimestamp(long id)
        {
            var timestamp = (id >> _timestampShift);
            var ms = _epochMs + timestamp;
            return DateTimeOffset.FromUnixTimeMilliseconds(ms);
        }

        /// <inheritdoc />
        public int GetWorkerId(long id)
        {
            long worker = (id & _workerIdMask) >> _workerIdShift;
            return (int)worker;
        }

        /// <inheritdoc />
        public int GetSequence(long id)
        {
            long sequence = id & _sequenceMask;
            return (int)sequence;
        }
    }
}
