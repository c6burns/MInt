using System;
using System.Reflection;

namespace MInt
{
    public static class MIntstrument
    {
        public static void Go(string[] targetPaths)
        {
            foreach (string targetPath in targetPaths)
            {
               OPInject.AddTarget(targetPath);
            }
            OPInject.InstrumentTargets();
        }
    }
}