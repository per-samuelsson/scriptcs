using Common.Logging;
using Roslyn.Scripting;
using ScriptCs.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ScriptCs.Engine.Roslyn {

    /// <summary>
    /// Temporary proof-of-concept engine, related to the implementation of
    /// issue https://github.com/scriptcs/scriptcs/issues/23.
    /// </summary>
    public class RoslynScriptExecutableEngine : RoslynScriptEngine {

        public RoslynScriptExecutableEngine(IScriptHostFactory scriptHostFactory, ILog logger)
            : base(scriptHostFactory, logger) {
        }

        protected override ScriptResult Execute(string code, Session session) {
            // TODO:
            // Implement our thing

            throw new NotImplementedException();
        }
    }
}
