using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Pose.IL;

namespace Pose
{
    public static class PoseContext
    {
        internal static Shim[] Shims { private set; get; }
        internal static Dictionary<MethodBase, DynamicMethod> StubCache { private set; get; }

        public static void Isolate(Action entryPoint, params Shim[] shims)
        {
            if (shims == null || shims.Length == 0)
            {
                entryPoint.Invoke();
                return;
            }

            Shims = shims;
            StubCache = new Dictionary<MethodBase, DynamicMethod>();

            var delegateType = typeof(Action<>).MakeGenericType(entryPoint.Target.GetType());

            var rewriter = MethodRewriter.CreateRewriter(entryPoint.Method);

            var rewroteEntryPoint = (MethodInfo)(rewriter.Rewrite());

            var rewroteEntryPointDelegate = rewroteEntryPoint.CreateDelegate(delegateType);

            rewroteEntryPointDelegate.DynamicInvoke(entryPoint.Target);
        }
    }
}