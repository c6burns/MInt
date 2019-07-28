using System;

namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            MInt.MStats.Setup();
            PrintHelloWorld();
            ThrowError(12344);
        }

        static void PrintHelloWorld()
        {
            PrintString("Hello World!");
        }

        static void PrintString(string msg)
        {
            Console.WriteLine(msg);
        }

        static int ThrowError(int durp)
        {
            int abc = 123;
            if (abc == durp) throw new SystemException();
            int def = 456;
            return def;
        }
    }
}
