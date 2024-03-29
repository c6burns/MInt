﻿using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace MInt
{
    public static class OPInject
    {
        public static bool LogToConsole { get; set; }
        public static List<string> TargetAssemblies { get; set; }
        public static List<string> ReferenceAssemblies { get; set; }
        public static List<string> MethodSignatures { get; set; }

        static HashSet<string> _refPathsHash = new HashSet<string>();
        
        static OPInject()
        {
            TargetAssemblies = new List<string>();
            ReferenceAssemblies = new List<string>();
            MethodSignatures = new List<string>();

#if DEBUG
            LogToConsole = true;
#endif
        }

        public static void AddTargets(string[] targetPaths)
        {
            foreach (string targetPath in targetPaths)
            {
                AddTarget(targetPath);
            }
        }

        public static void AddTarget(string targetPath)
        {
            using (AssemblyDefinition targetDef = AssemblyDefinition.ReadAssembly(targetPath))
            {
                foreach (AssemblyNameReference asmRefName in targetDef.MainModule.AssemblyReferences)
                {
                    Console.WriteLine("Adding Reference: {0}", asmRefName.FullName);
                    //Assembly.
                    //AddReference(asmName);
                }
                TargetAssemblies.Add(targetPath);
            }
        }

        //public static void AddReference(AssemblyName refAsm)
        //{
        //    if (refAsm == null) return;
        //    ReferenceAssemblies.Add(refAsm);
        //}

        public static void ClearTargets()
        {
            TargetAssemblies.Clear();
        }

        public static void Setup(Assembly mStatsAssembly, Assembly targetAssemblies)
        {
        }

        public static MethodDefinition ResolveMethod(TypeDefinition td, string methodName)
        {
            foreach (MethodDefinition method in td.Methods)
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
            DefaultAssemblyResolver asmResolver = new DefaultAssemblyResolver();
            _refPathsHash.Clear();
            foreach (string refAsm in ReferenceAssemblies)
            {
                string refPath = Path.GetDirectoryName(refAsm);
                if (_refPathsHash.Add(refPath))
                {
                    Console.WriteLine(refAsm);
                    asmResolver.AddSearchDirectory(refPath);
                }
            }

            ReaderParameters readParams = new ReaderParameters() {
                ReadSymbols = true,
                ReadWrite = true,
                AssemblyResolver = asmResolver,
            };
            WriterParameters writeParams = new WriterParameters() { WriteSymbols = true };
            foreach (string targetAsm in TargetAssemblies)
            {
                using (AssemblyDefinition targetAsmDef = AssemblyDefinition.ReadAssembly(targetAsm, readParams))
                {
                    InstrumentTarget(targetAsmDef);
                    targetAsmDef.Write(writeParams);
                }
            }
        }

        static void InstrumentTarget(AssemblyDefinition targetAsmDef)
        {
            ModuleDefinition targetModDef = targetAsmDef.MainModule;

            TypeReference tmpTR;
            if (!targetModDef.TryGetTypeReference("MInt.MStats", out tmpTR))
            {
                throw new SystemException("{0} does not reference MInt.MStats");
            }
            TypeDefinition mstatsTD = tmpTR.Resolve();
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

            Instruction startInst;
            List<Instruction> endInst = new List<Instruction>();

            Console.WriteLine(" -- StartMSpan");
            foreach (Instruction inst in mb.Instructions)
            {
                switch (inst.OpCode.Code)
                {
                    case Code.Ret:
                    case Code.Throw:
                    case Code.Rethrow:
                        endInst.Add(inst);
                        Console.WriteLine(" -- EndMSpan");
                        break;
                }
                Console.WriteLine(inst.OpCode.Name);
            }

            mb.InitLocals = true;
            VariableDefinition vd = new VariableDefinition(startMR.ReturnType);
            mb.Variables.Add(vd);

            ILProcessor ilp = mb.GetILProcessor();

            startInst = mb.Instructions[0];
            if (startInst.OpCode.Code != Code.Nop)
            {
                ilp.InsertBefore(startInst, ilp.Create(OpCodes.Nop));
                startInst = mb.Instructions[0];
            }

            ilp.InsertBefore(startInst, ilp.Create(OpCodes.Nop));
            ilp.InsertBefore(startInst, ilp.Create(OpCodes.Ldc_I8, methodID));
            ilp.InsertBefore(startInst, ilp.Create(OpCodes.Call, startMR));
            ilp.InsertBefore(startInst, ilp.Create(OpCodes.Stloc, vd));

            foreach (Instruction inst in endInst)
            {
                ilp.InsertBefore(inst, ilp.Create(OpCodes.Ldloc, vd));
                ilp.InsertBefore(inst, ilp.Create(OpCodes.Call, endMR));
            }
            ilp.Body.OptimizeMacros();
        }
    }
}