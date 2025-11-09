using System;

namespace Mubai.Snowflake
{
    /// <summary>
    /// 雪花 ID 解码器接口。
    /// </summary>
    public interface IIdDecoder
    {
        /// <summary>
        /// 从 ID 解出生成时间（UTC）。
        /// </summary>
        DateTimeOffset GetTimestamp(long id);

        /// <summary>
        /// 从 ID 解出 WorkerId。
        /// </summary>
        int GetWorkerId(long id);

        /// <summary>
        /// 从 ID 解出序列号。
        /// </summary>
        int GetSequence(long id);
    }
}
