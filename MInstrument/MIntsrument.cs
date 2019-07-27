using System;
using System.Reflection;

namespace MInt
{
    public static class MIntstrument
    {
        public static void Go(string mstatsPath, string targetPath)
        {
            OPInject.MStatsAssembly = Assembly.LoadFrom(mstatsPath);

            OPInject.AddTarget(targetPath);
            //Assembly[] targetAsms = new Assembly[targetPaths.Length];
            //foreach (Assembly targetAsm in targetAsms)
            //{
            //    OPInject.AddTarget(targetAsm);
            //}

            OPInject.InstrumentTargets();
        }
    }
}