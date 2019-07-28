using System;
using Mono.Cecil;

namespace MInt
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Usage();
                return;
            }

            MIntstrument.Go(args);
        }

        static void Usage()
        {
            Console.WriteLine("\nMInstrument is an automatic instrumenter for C# dlls\n\nUsage:\nMinstrument /path/to/target/assembly [/path/to/next/assembly] ...\n\n\n");
        }
    }
}
