using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;


namespace MInt
{
    public static unsafe class MStats
    {
        const int MAX_THREADS = 8;
        const long MAX_THREAD_HEAP_SIZE = 1024L * 1024L * 1L;
        const int MSPAN_SIZE = 32;
        const long MAX_HEAP_SIZE = MAX_THREAD_HEAP_SIZE * MAX_THREADS;

        [StructLayout(LayoutKind.Explicit, Size = MSPAN_SIZE)]
        struct MSpan
        {
            [FieldOffset(0)] public long methodID;
            [FieldOffset(8)] public long startMS;
            [FieldOffset(16)] public long endMS;
            [FieldOffset(24)] public long custom0;
        }

        class MThread
        {
            internal ulong nextIdx;
            internal MSpan* _mSpan;
            internal Stopwatch _sw;

            private MThread() { }

            public MThread(MSpan* mSpan)
            {
                nextIdx = 0;
                _mSpan = mSpan;
                _sw = Stopwatch.StartNew();
            }

            public ulong StartSpan(long methodID)
            {
                ulong idx = nextIdx++;
                MSpan* s = _mSpan + idx;
                s->methodID = methodID;
                s->startMS = _sw.ElapsedMilliseconds;
                return idx;
            }

            public void EndSpan(ulong mSpanID, long customData = 0)
            {
                MSpan* s = _mSpan + mSpanID;
                s->endMS = _sw.ElapsedMilliseconds;
                s->custom0 = customData;
            }
        }

        static bool _ready = false;
        static int _mThreadIdx = 0;
        static MThread[] _mThread = new MThread[MAX_THREADS];
        static ConcurrentDictionary<int, int> _mThreadMap;
        static ThreadLocal<int> _threadID;
        static string[] _methodMap;
        static IntPtr _mSpanHeapHandle;
        static MSpan* _mSpanHeap;

        static MStats()
        {
        }

        static void OnAppExit(object sender, EventArgs e)
        {
            Console.WriteLine("MStats.OnAppExit");

            foreach (MThread t in _mThread)
            {
                for (int i = 0; i < (int)t.nextIdx; i++)
                {
                    MSpan* s = t._mSpan + i;
                    Console.WriteLine("t: {0}, mID: {1}, start: {2}, end: {3}", i, s->methodID, s->startMS, s->endMS);
                }
            }
        }

        public static void Setup()
        {
            if (_ready) return;

            Console.WriteLine("MStats.Setup");
            _ready = true;

            _mSpanHeapHandle = Marshal.AllocHGlobal((IntPtr)MAX_HEAP_SIZE);
            _mSpanHeap = (MSpan*)_mSpanHeapHandle.ToPointer();

            _mThreadIdx = 0;
            _threadID = new ThreadLocal<int>();
            _mThreadMap = new ConcurrentDictionary<int, int>();

            for (int i = 0; i < _mThread.Length; i++)
            {
                _mThread[i] = new MThread(_mSpanHeap + (MAX_THREAD_HEAP_SIZE * i));
            }

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnAppExit);
        }

        public static ulong StartSpan(long methodID)
        {
            Console.WriteLine("StartSpan({0})", methodID);
            return GetMapThread().StartSpan(methodID);
        }

        public static void EndSpan(ulong mSpanID, long customData = 0)
        {
            Console.WriteLine("EndSpan({0})", mSpanID);
            GetMapThread().EndSpan(mSpanID, customData);
        }

        static MThread GetMapThread()
        {
            if (!_threadID.IsValueCreated) _threadID.Value = Interlocked.Increment(ref _mThreadIdx);
            return _mThread[_threadID.Value];

            // if (_mThreadMap.TryGetValue(Thread.CurrentThread.ManagedThreadId, out int idx))
            // {
            //     return _mThread[idx];
            // }

            // return AddMapThread();
        }

        static MThread AddMapThread()
        {
            if (_mThreadMap.Count < MAX_THREADS)
            {
                int idx = Interlocked.Increment(ref _mThreadIdx);
                if (_mThreadMap.TryAdd(Thread.CurrentThread.ManagedThreadId, idx))
                {
                    return _mThread[idx];
                }
            }

            return null;
        }
    }
}
