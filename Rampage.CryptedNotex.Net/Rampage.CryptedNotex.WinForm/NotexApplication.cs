using Microsoft.Extensions.DependencyInjection;
using Rampage.CryptedNotex.BLL;
using Rampage.DependencyInjection;
using System;

namespace Rampage.CryptedNotex.WinForm
{
    /// <summary>
    /// 应用入口类：承载全部 BLL 服务接口的 [Auto] 引用锚点(框架要求服务接口被 [Auto] 成员引用才注册)，并挂接首运行建表
    /// </summary>
    public class NotexApplication : RampageBasicApplication, IRampageApplication
    {
        /// <summary>
        /// 用户服务锚点，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected IUserService UserService = default!;

        /// <summary>
        /// 分类服务锚点，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected ICategoryService CategoryService = default!;

        /// <summary>
        /// 笔记服务锚点，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected INoteService NoteService = default!;

        /// <summary>
        /// 笔记附件服务锚点，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected INoteAttachmentService NoteAttachmentService = default!;

        /// <summary>
        /// 用户偏好设置服务锚点，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected IUserSettingService UserSettingService = default!;

        /// <summary>
        /// 系统设置服务锚点，由 [Auto] 注入
        /// </summary>
        [Auto]
        protected ISettingService SettingService = default!;

        /// <summary>
        /// 在框架连库校验实体映射之前确保数据库文件与表结构就绪(首运行建表；详见 DAL 的 SchemaInitializer)
        /// </summary>
        public override IServiceCollection InitializeServiceContainer(string[] args)
        {
            DatabaseSchema.EnsureCreated();
            return base.InitializeServiceContainer(args);
        }

        public override void Run(string[] args)
        {
            base.Run(args);
        }
    }
}
