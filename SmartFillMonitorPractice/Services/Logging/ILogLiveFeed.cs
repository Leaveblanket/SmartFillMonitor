using System;
using System.Collections.Generic;

namespace SmartFillMonitor.Services.Logging
{
    /// <summary>
    /// 提供运行期实时日志缓冲与推送能力，供界面实时展示最新日志。
    /// </summary>
    public interface ILogLiveFeed
    {
        /// <summary>
        /// 实时日志缓冲区容量。
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// 获取当前日志缓冲快照。
        /// </summary>
        IReadOnlyList<string> GetSnapshot();

        /// <summary>
        /// 追加一条实时日志消息。
        /// </summary>
        void Publish(string message);

        /// <summary>
        /// 清空实时日志缓冲。
        /// </summary>
        void Reset();

        /// <summary>
        /// 当有新日志追加时发布。
        /// </summary>
        event EventHandler<string>? LogAppended;

        /// <summary>
        /// 当日志被整体重置时发布。
        /// </summary>
        event EventHandler? ResetRequested;
    }
}
