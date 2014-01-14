using Common.Logging;
using Roslyn.Compilers;
using Roslyn.Scripting;
using Roslyn.Scripting.CSharp;
using ScriptCs.Contracts;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;

namespace ScriptCs.Engine.Roslyn {

    /// <summary>
    /// Temporary proof-of-concept engine, related to the implementation of
    /// issue https://github.com/scriptcs/scriptcs/issues/23.
    /// </summary>
    public class RoslynScriptExecutableEngine : RoslynScriptEngine {
        const string RoslynAssemblyNameCharacter = "ℛ";
        const string CompiledScriptClass = "Submission#0";
        const string CompiledScriptMethod = "<Factory>";

        public RoslynScriptExecutableEngine(IScriptHostFactory scriptHostFactory, ILog logger)
            : base(scriptHostFactory, logger) {
        }

        protected override ScriptResult Execute(string code, Session session) {
            Guard.AgainstNullArgument("session", session);

            var result = new ScriptResult();
            try {
                var submission = session.CompileSubmission<object>(code);
                var loaded = BuildExecutable(session, submission);
                try {
                    InvokeEntrypoint(session, loaded, result);
                } catch (Exception ex) {
                    result.ExecuteExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                }
            } catch (Exception ex) {
                result.CompileExceptionInfo = ExceptionDispatchInfo.Capture(ex);
            }

            return result;
        }

        private Assembly BuildExecutable(Session session, Submission<object> submission) {
            var name = new AssemblyName(RoslynAssemblyNameCharacter + Guid.NewGuid().ToString());
            var fileName = Path.ChangeExtension(FileName, ".exe");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave);
            var scriptModule = assemblyBuilder.DefineDynamicModule(Path.GetFileNameWithoutExtension(fileName), fileName, true);

            submission.Compilation.Emit(scriptModule);

            var engineType = scriptModule.ResolveType(scriptModule.GetTypeToken(typeof(ScriptEngine)).Token);
            var engineCtor = engineType.GetConstructor(new[] { typeof(MetadataFileProvider), typeof(IAssemblyLoader) });
            var createSession = engineType.GetMethod("CreateSession", Type.EmptyTypes);
            var sessionType = scriptModule.ResolveType(scriptModule.GetTypeToken(typeof(Session)).Token);
            var factory = scriptModule.GetType(CompiledScriptClass).GetMethod(CompiledScriptMethod, BindingFlags.Static | BindingFlags.Public);

            var entrypoint = scriptModule.DefineGlobalMethod("Main", MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig, typeof(void), Type.EmptyTypes);
            var il = entrypoint.GetILGenerator();
            il.DeclareLocal(sessionType);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Newobj, engineCtor);
            il.Emit(OpCodes.Call, createSession);
            il.Emit(OpCodes.Stloc_0);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Call, factory);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);

            scriptModule.CreateGlobalFunctions();
            assemblyBuilder.SetEntryPoint(entrypoint.GetBaseDefinition());

            assemblyBuilder.Save(fileName);

            return assemblyBuilder;
        }

        private void InvokeEntrypoint(Session session, Assembly assembly, ScriptResult scriptResult) {
            var type = assembly.GetType(CompiledScriptClass);
            var method = type.GetMethod(CompiledScriptMethod, BindingFlags.Static | BindingFlags.Public);

            try {
                scriptResult.ReturnValue = method.Invoke(null, new[] { session });
            } catch (Exception executeException) {
                var ex = executeException.InnerException ?? executeException;
                scriptResult.ExecuteExceptionInfo = ExceptionDispatchInfo.Capture(ex);
            }
        }
    }
}
