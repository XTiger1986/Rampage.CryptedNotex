using System;

namespace Rampage.CryptedNotex.BLL
{
    /// <summary>
    /// 区间进度映射：把子操作(如重加密 0~100)映射到总进度的一个区间(如 10~85)，供多步长任务的进度条使用
    /// </summary>
    internal sealed class RangeProgress : IProgress<int>
    {
        private readonly IProgress<int>? _inner;
        private readonly int _from;
        private readonly int _to;

        /// <summary>
        /// 构造区间进度
        /// </summary>
        /// <param name="inner">实际进度接收者(可为 null,此时上报为空操作)</param>
        /// <param name="from">子进度 0 映射到的总进度值</param>
        /// <param name="to">子进度 100 映射到的总进度值</param>
        public RangeProgress(IProgress<int>? inner, int from, int to)
        {
            _inner = inner;
            _from = from;
            _to = to;
        }

        /// <inheritdoc />
        public void Report(int value)
        {
            if (_inner == null)
            {
                return;
            }

            int clamped = value < 0 ? 0 : (value > 100 ? 100 : value);
            _inner.Report(_from + (_to - _from) * clamped / 100);
        }
    }
}
