using Microsoft.Extensions.DependencyInjection;
using Rampage.CryptedNotex.BLL;
using Rampage.DependencyInjection;

namespace Rampage.CryptedNotex.TestConsole
{
    /// <summary>
    /// E2E 测试宿主：同 NotexApplication 的服务锚点与建表挂钩,但不加载 WinForm
    /// </summary>
    public class NotexTestApplication : RampageBasicApplication, IRampageApplication
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
        /// 建表先于映射校验
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

        /// <summary>
        /// 供测试直接解析服务
        /// </summary>
        public T GetRequiredService<T>() where T : class
        {
            return RampageScope.CurrentProvider!.GetRequiredService<T>();
        }
    }
}
