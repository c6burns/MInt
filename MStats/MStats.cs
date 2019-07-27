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
        const long MAX_THREAD_HEAP_SIZE = 1024L * 1024L * 1024L;
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
            ulong nextIdx;
            MSpan* _mSpan;
            Stopwatch _sw;

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
        static string[] _methodMap;
        static IntPtr _mSpanHeapHandle;
        static MSpan* _mSpanHeap;

        static MStats()
        {
        }

        public static void Setup()
        {
            if (_ready) return;

            _ready = true;
            _mSpanHeapHandle = Marshal.AllocHGlobal((IntPtr)MAX_HEAP_SIZE);
            _mSpanHeap = (MSpan*)_mSpanHeapHandle.ToPointer();

            _mThreadMap = new ConcurrentDictionary<int, int>();

            for (int i = 0; i < _mThread.Length; i++)
            {
                _mThread[i] = new MThread(_mSpanHeap + (MAX_THREAD_HEAP_SIZE * i));
            }
        }

        public static ulong StartSpan(long methodID)
        {
            return GetMapThread().StartSpan(methodID);
        }

        public static void EndSpan(ulong mSpanID, long customData = 0)
        {
            GetMapThread().EndSpan(mSpanID, customData);
        }

        static MThread GetMapThread()
        {
            if (_mThreadMap.TryGetValue(Thread.CurrentThread.ManagedThreadId, out int idx))
            {
                return _mThread[idx];
            }

            return AddMapThread();
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
