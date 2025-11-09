namespace Mubai.Snowflake
{
    /// <summary>
    /// ID 生成器接口。
    /// </summary>
    public interface IIdGenerator
    {
        /// <summary>
        /// 生成一个新的雪花 ID。
        /// </summary>
        long NewId();
    }
}
