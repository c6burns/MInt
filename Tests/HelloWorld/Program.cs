using System;
using System.Threading;

namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            MInt.MStats.Setup();
            PrintHelloWorld();
        }

        static void PrintHelloWorld()
        {
            Thread.Sleep(150);
            PrintString("Hello World!");
        }

        static void PrintString(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
