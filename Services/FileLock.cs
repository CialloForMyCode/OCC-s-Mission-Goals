using System;
using System.Threading;

namespace OCCMissionGoals.Services;

/// <summary>
/// 跨进程互斥锁。GUI 与 CLI 是独立进程，通过同一个命名 Mutex 串行化对
/// 数据文件（versions/*.json）与 project.json 的「读-改-写」，防止并发覆盖丢失、
/// 读到半截文件，以及条目编号（NextEntryId）重复。
/// </summary>
public sealed class FileLock : IDisposable
{
    private const string MutexName = @"Local\OCCMissionGoals.DataFileLock";

    private readonly Mutex _mutex;
    private bool _disposed;

    private FileLock(Mutex mutex) => _mutex = mutex;

    /// <summary>获取跨进程锁（阻塞直到可用）。</summary>
    public static FileLock Acquire()
    {
        var mutex = new Mutex(false, MutexName);
        try
        {
            mutex.WaitOne();
        }
        catch (AbandonedMutexException)
        {
            // 上一持有者异常退出（如进程被杀），锁已被系统释放，视为获取成功继续。
        }
        return new FileLock(mutex);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _mutex.ReleaseMutex(); }
        catch (ApplicationException) { /* 未持有时不释放 */ }
        _mutex.Dispose();
    }
}
