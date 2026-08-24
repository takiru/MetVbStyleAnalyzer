using System;
using System.IO;
using System.Threading;

namespace VSIXProject1
{
    /// <summary>
    /// ファイルが一時的に他のプロセス (VSのライブ解析など) にロックされている場合に備えて、
    /// 短い間隔でリトライするための簡易ヘルパー。
    /// </summary>
    internal static class FileRetry
    {
        public static void Execute(Action action, int maxAttempts = 5, int delayMilliseconds = 150)
        {
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(delayMilliseconds);
                }
            }
        }
    }
}