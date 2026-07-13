using System;

namespace Starfall.Core.Replay
{
    /// <summary>
    /// Replay 序列化/反序列化异常。含文件路径便于诊断（类比 Data 层的 DefinitionException）。
    /// </summary>
    public class ReplayException : Exception
    {
        public string FilePath { get; }

        public ReplayException(string message, string filePath, Exception? inner = null)
            : base($"[{filePath}] {message}", inner)
        {
            FilePath = filePath;
        }
    }
}