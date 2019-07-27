using System;
using Mono.Cecil;

namespace MInt
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Usage();
                return;
            }

            MIntstrument.Go(args[0], args[1]);
        }

        static void Usage()
        {
            Console.WriteLine("\nMInstrument is an automatic instrumenter for C# dlls\n\nUsage:\nMinstrument /path/to/MStats.dll /path/to/target/assembly [/path/to/next/assembly] ...\n\n\n");
        }
    }
}
