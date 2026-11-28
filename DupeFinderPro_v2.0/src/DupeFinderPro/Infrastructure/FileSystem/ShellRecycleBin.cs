using System.Runtime.InteropServices;
using DupeFinderPro.Domain.Interfaces;

namespace DupeFinderPro.Infrastructure.FileSystem;

/// <summary>
/// 셸의 IFileOperation으로 휴지통에 보낸다. 셸이 "휴지통에 넣지 못하고 영구 삭제할 것"이라고 알리면 그 삭제를 취소한다.
/// 2026-11-26 실측: 이전에 쓰던 VB FileSystem.DeleteFile(SendToRecycleBin)은 네트워크 경로(\\localhost\F$\...)의 파일을
/// 경고 없이 영구 삭제했고, 이 방식은 같은 파일을 지우지 않고 남겼다. 로컬 NTFS 파일은 둘 다 휴지통으로 갔다.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class ShellRecycleBin
{
    private const uint FofSilent = 0x4, FofNoConfirmation = 0x10, FofAllowUndo = 0x40, FofNoErrorUi = 0x400,
                       FofNoConnectedElements = 0x2000, FofxRecycleOnDelete = 0x80000;
    private const uint TsfDeleteRecycleIfPossible = 0x80;
    private const int EAbort = unchecked((int)0x80004004);
    private static readonly Guid FileOperationClsid = new("3ad05575-8857-4850-9277-11b85bdb8e09");
    private static readonly Guid ShellItemIid = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    /// <summary>IFileOperation은 STA 스레드에서 써야 한다. 호출마다 전용 스레드에서 실행한다.</summary>
    public static Task<RecycleOutcome> RecycleAsync(string path)
    {
        var done = new TaskCompletionSource<RecycleOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { done.SetResult(Recycle(path)); }
            catch (Exception ex) { done.SetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    private static RecycleOutcome Recycle(string path)
    {
        var sink = new Sink();
        var operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(FileOperationClsid, throwOnError: true)!)!;
        var item = IntPtr.Zero;
        try
        {
            // 확인·진행·오류 창을 띄우지 않는다. 창이 뜨면 작업 스레드가 사용자의 응답을 기다리며 멈춘다.
            Marshal.ThrowExceptionForHR(operation.SetOperationFlags(
                FofSilent | FofNoConfirmation | FofAllowUndo | FofNoErrorUi | FofNoConnectedElements | FofxRecycleOnDelete));
            var iid = ShellItemIid;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out item));
            Marshal.ThrowExceptionForHR(operation.Advise(sink, out var cookie));
            try
            {
                Marshal.ThrowExceptionForHR(operation.DeleteItem(item, IntPtr.Zero));
                var hr = operation.PerformOperations();
                if (sink.RefusedNotRecyclable)
                    return RecycleOutcome.NotRecyclable;
                Marshal.ThrowExceptionForHR(hr);
                Marshal.ThrowExceptionForHR(sink.DeleteResult);
                return RecycleOutcome.Recycled;
            }
            finally
            {
                operation.Unadvise(cookie);
            }
        }
        finally
        {
            if (item != IntPtr.Zero)
                Marshal.Release(item);
            Marshal.ReleaseComObject(operation);
        }
    }

    [ComVisible(true)]
    private sealed class Sink : IFileOperationProgressSink
    {
        public bool RefusedNotRecyclable { get; private set; }
        public int DeleteResult { get; private set; }

        public int PreDeleteItem(uint flags, IntPtr item)
        {
            if ((flags & TsfDeleteRecycleIfPossible) != 0)
                return 0;
            RefusedNotRecyclable = true;
            return EAbort; // 휴지통에 못 넣으면 영구 삭제하지 않는다
        }

        public int PostDeleteItem(uint flags, IntPtr item, int hrDelete, IntPtr created)
        {
            DeleteResult = hrDelete < 0 && !RefusedNotRecyclable ? hrDelete : 0;
            return 0;
        }

        public int StartOperations() => 0;
        public int FinishOperations(int hrResult) => 0;
        public int PreRenameItem(uint f, IntPtr i, string n) => 0;
        public int PostRenameItem(uint f, IntPtr i, string n, int hr, IntPtr c) => 0;
        public int PreMoveItem(uint f, IntPtr i, IntPtr d, string n) => 0;
        public int PostMoveItem(uint f, IntPtr i, IntPtr d, string n, int hr, IntPtr c) => 0;
        public int PreCopyItem(uint f, IntPtr i, IntPtr d, string n) => 0;
        public int PostCopyItem(uint f, IntPtr i, IntPtr d, string n, int hr, IntPtr c) => 0;
        public int PreNewItem(uint f, IntPtr d, string n) => 0;
        public int PostNewItem(uint f, IntPtr d, string n, string t, uint a, int hr, IntPtr i) => 0;
        public int UpdateProgress(uint total, uint soFar) => 0;
        public int ResetTimer() => 0;
        public int PauseTimer() => 0;
        public int ResumeTimer() => 0;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IntPtr item);

    // shobjidl_core.h의 메서드 순서를 그대로 따른다.
    [ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        [PreserveSig] int Advise(IFileOperationProgressSink sink, out uint cookie);
        [PreserveSig] int Unadvise(uint cookie);
        [PreserveSig] int SetOperationFlags(uint flags);
        [PreserveSig] int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        [PreserveSig] int SetProgressDialog(IntPtr dialog);
        [PreserveSig] int SetProperties(IntPtr properties);
        [PreserveSig] int SetOwnerWindow(IntPtr owner);
        [PreserveSig] int ApplyPropertiesToItem(IntPtr item);
        [PreserveSig] int ApplyPropertiesToItems(IntPtr items);
        [PreserveSig] int RenameItem(IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr sink);
        [PreserveSig] int RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string newName);
        [PreserveSig] int MoveItem(IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr sink);
        [PreserveSig] int MoveItems(IntPtr items, IntPtr folder);
        [PreserveSig] int CopyItem(IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string copyName, IntPtr sink);
        [PreserveSig] int CopyItems(IntPtr items, IntPtr folder);
        [PreserveSig] int DeleteItem(IntPtr item, IntPtr sink);
        [PreserveSig] int DeleteItems(IntPtr items);
        [PreserveSig] int NewItem(IntPtr folder, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name,
                                  [MarshalAs(UnmanagedType.LPWStr)] string template, IntPtr sink);
        [PreserveSig] int PerformOperations();
        [PreserveSig] int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }

    [ComImport, Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperationProgressSink
    {
        [PreserveSig] int StartOperations();
        [PreserveSig] int FinishOperations(int hrResult);
        [PreserveSig] int PreRenameItem(uint f, IntPtr i, [MarshalAs(UnmanagedType.LPWStr)] string n);
        [PreserveSig] int PostRenameItem(uint f, IntPtr i, [MarshalAs(UnmanagedType.LPWStr)] string n, int hr, IntPtr c);
        [PreserveSig] int PreMoveItem(uint f, IntPtr i, IntPtr d, [MarshalAs(UnmanagedType.LPWStr)] string n);
        [PreserveSig] int PostMoveItem(uint f, IntPtr i, IntPtr d, [MarshalAs(UnmanagedType.LPWStr)] string n, int hr, IntPtr c);
        [PreserveSig] int PreCopyItem(uint f, IntPtr i, IntPtr d, [MarshalAs(UnmanagedType.LPWStr)] string n);
        [PreserveSig] int PostCopyItem(uint f, IntPtr i, IntPtr d, [MarshalAs(UnmanagedType.LPWStr)] string n, int hr, IntPtr c);
        [PreserveSig] int PreDeleteItem(uint flags, IntPtr item);
        [PreserveSig] int PostDeleteItem(uint flags, IntPtr item, int hrDelete, IntPtr created);
        [PreserveSig] int PreNewItem(uint f, IntPtr d, [MarshalAs(UnmanagedType.LPWStr)] string n);
        [PreserveSig] int PostNewItem(uint f, IntPtr d, [MarshalAs(UnmanagedType.LPWStr)] string n,
                                      [MarshalAs(UnmanagedType.LPWStr)] string t, uint a, int hr, IntPtr i);
        [PreserveSig] int UpdateProgress(uint total, uint soFar);
        [PreserveSig] int ResetTimer();
        [PreserveSig] int PauseTimer();
        [PreserveSig] int ResumeTimer();
    }
}
