using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rampage.CryptedNotex.WinForm.Internal
{
    /// <summary>
    /// 把耗时工作放到线程池，并串行化，避免解密/保存/搜索同时打到 SQLite。
    /// 调用方在 UI 线程 await，界面可以继续重绘忙碌状态。
    /// </summary>
    internal static class UiWork
    {
        private static readonly SemaphoreSlim GATE = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 排队执行并在 UI 上下文返回结果
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="work">后台工作(勿访问控件)</param>
        /// <returns>工作结果</returns>
        public static async Task<T> RunAsync<T>(Func<T> work)
        {
            await GATE.WaitAsync();
            try
            {
                return await Task.Run(work);
            }
            finally
            {
                GATE.Release();
            }
        }
    }
}
