namespace Rampage.CryptedNotex.Model.Enums
{
    /// <summary>
    /// 笔记状态；数据库 note.status 列取值：0=正常;1=回收站
    /// </summary>
    public enum NoteStatus
    {
        /// <summary>
        /// 正常(0)
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 回收站(1)：软删除，可恢复或彻底删除
        /// </summary>
        Trashed = 1
    }
}
