using System;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace MInt
{
    public static class OPInject
    {
        public static List<Assembly> TargetAssemblies { get; private set; }
        public static Assembly MStatsAssembly { get; set; }
        public static List<string> MethodSignatures { get; private set; }

        static OPInject()
        {
            TargetAssemblies = new List<Assembly>();
            MethodSignatures = new List<string>();
        }

        public static void AddTarget(string targetPath)
        {
            AddTarget(Assembly.LoadFrom(targetPath));
        }

        public static void AddTargets(string[] targetPaths)
        {
            foreach (string targetPath in targetPaths)
            {
                AddTarget(Assembly.LoadFrom(targetPath));
            }
        }

        public static void AddTarget(Assembly target)
        {
            if (target == null) return;
            TargetAssemblies.Add(target);
        }

        public static void ClearTargets()
        {
            TargetAssemblies.Clear();
        }

        public static void Setup(Assembly mStatsAssembly, Assembly targetAssemblies)
        {
        }

        public static MethodDefinition ResolveMethod(TypeDefinition typeDef, string methodName)
        {
            foreach (MethodDefinition method in typeDef.Methods)
            {
                //Console.WriteLine("{0} = {1}", method.Name, methodName);
                if (method.Name == methodName)
                {
                    return method;
                }
            }
            return null;
        }

        public static void InstrumentTargets()
        {
            if (MStatsAssembly == null) return;

            ReaderParameters readParams = new ReaderParameters();
            using (AssemblyDefinition mstatsAsmDef = AssemblyDefinition.ReadAssembly(MStatsAssembly.Location, readParams))
            {
                foreach (Assembly targetAsm in TargetAssemblies)
                {
                    using (AssemblyDefinition targetAsmDef = AssemblyDefinition.ReadAssembly(targetAsm.Location, readParams))
                    {
                        InstrumentTarget(mstatsAsmDef, targetAsmDef);
                    }
                }
            }
        }

        static void InstrumentTarget(AssemblyDefinition mstatsAsmDef, AssemblyDefinition targetAsmDef)
        {
            ModuleDefinition mstatsModDef = mstatsAsmDef.MainModule;
            ModuleDefinition targetModDef = targetAsmDef.MainModule;

            TypeDefinition mstatsTD = mstatsModDef.GetType("MInt.MStats");
            TypeReference mstatsTR = targetModDef.ImportReference(mstatsTD);

            MethodDefinition setupMD = ResolveMethod(mstatsTD, "Setup");
            MethodDefinition startSpanMD = ResolveMethod(mstatsTD, "StartSpan");
            MethodDefinition endSpanMD = ResolveMethod(mstatsTD, "EndSpan");

            MethodReference setupMR = targetModDef.ImportReference(setupMD);
            MethodReference startSpanMR = targetModDef.ImportReference(startSpanMD);
            MethodReference endSpanMR = targetModDef.ImportReference(endSpanMD);
            
            foreach (TypeDefinition td in targetModDef.Types)
            {
                if (td.Methods.Count == 0) continue;

                Console.WriteLine("--------------------------------------------------------------------");
                foreach (MethodDefinition md in td.Methods)
                {
                    //string methodSig = md.FullName;
                    string methodSig = MethodSignature(md);

                    if (OPInject.ILMethodHasCall(md, setupMR))
                    {
                        Console.WriteLine("{0} -- skipping intrumentation (setup call)", methodSig);
                        continue;
                    }

                    long methodID = MethodSignatures.Count;
                    MethodSignatures.Add(methodSig);
                    OPInject.ILMethodIntrument(md, methodID, startSpanMR, endSpanMR);

                    Console.WriteLine("{0} -- instrumentation ID: {1}", methodSig, methodID);
                }
                Console.Write("\n");
            }
        }

        public static string MethodSignature(MethodReference mr)
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.Append(mr.Name);
            sb.Append("(");
            int pcount = 0;
            foreach (ParameterDefinition pd in mr.Parameters)
            {
                if (pcount > 0) sb.Append(", ");
                sb.Append(pd.ParameterType.Name);
                pcount++;
            }
            sb.Append(")");
            return sb.ToString();
        }

        public static bool ILMethodHasCall(MethodDefinition md, MethodReference mr)
        {
            foreach (Instruction inst in md.Body.Instructions)
            {
                if (inst.Operand == null) continue;
                if (inst.OpCode.Code == Code.Call)
                {
                    MethodReference opMR = inst.Operand as MethodReference;
                    if (opMR.FullName == mr.FullName) return true;
                }
            }
            return false;
        }

        public static void ILMethodIntrument(MethodDefinition md, long methodID, MethodReference startMR, MethodReference endMR)
        {
            Mono.Cecil.Cil.MethodBody mb = md.Body;
            ILProcessor ilp = mb.GetILProcessor();

            if (mb.Instructions.Count == 0) ilp.Append(ilp.Create(OpCodes.Nop));

            // create a local to trap the ulong ret val
            ilp.InsertBefore(mb.Instructions[0], ilp.Create(OpCodes.Call, startMR));
            ilp.InsertBefore(mb.Instructions[0], ilp.Create(OpCodes.Ldc_I8, methodID));

            foreach (Instruction inst in md.Body.Instructions)
            {
                switch (inst.OpCode.Code)
                {
                    case Code.Ret:
                    case Code.Throw:
                    case Code.Rethrow:
                        ilp.InsertBefore(inst, ilp.Create(OpCodes.Call, startMR));
                        ilp.InsertBefore(inst, ilp.Create(OpCodes.Ldc_I8, methodID));
                        break;
                }
                Console.WriteLine(inst.OpCode.Name);
            }
        }
    }
}