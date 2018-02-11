using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

using Pose.Exceptions;
using Pose.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static System.Console;
using System.Diagnostics;

namespace Pose.Tests
{
    [TestClass]
    public class ShimTests
    {
        [TestMethod]
        public void TestReplace()
        {
            Shim shim = Shim.Replace(() => Console.WriteLine(""));

            Assert.AreEqual(typeof(Console).GetMethod("WriteLine", new[] { typeof(string) }), shim.Original);
            Assert.IsNull(shim.Replacement);
        }

        [TestMethod]
        public void TestReplaceWithInstanceVariable()
        {
            ShimTests shimTests = new ShimTests();
            Shim shim = Shim.Replace(() => shimTests.TestReplace());

            Assert.AreEqual(typeof(ShimTests).GetMethod("TestReplace"), shim.Original);
            Assert.AreSame(shimTests, shim.Instance);
            Assert.IsNull(shim.Replacement);
        }

        [TestMethod]
        public void TestShimReplaceWithInvalidSignature()
        {
            ShimTests shimTests = new ShimTests();
            Shim shim = Shim.Replace(() => shimTests.TestReplace());
            Assert.ThrowsException<InvalidShimSignatureException>(
                () => Shim.Replace(() => shimTests.TestReplace()).With(() => { }));
            Assert.ThrowsException<InvalidShimSignatureException>(
                () => Shim.Replace(() => Console.WriteLine(Is.A<string>())).With(() => { }));
        }

        [TestMethod]
        public void TestShimReplaceWith()
        {
            ShimTests shimTests = new ShimTests();
            Action action = new Action(() => { });
            Action<ShimTests> actionInstance = new Action<ShimTests>((s) => { });

            Shim shim = Shim.Replace(() => Console.WriteLine()).With(action);
            Shim shim1 = Shim.Replace(() => shimTests.TestReplace()).With(actionInstance);

            Assert.AreEqual(typeof(Console).GetMethod("WriteLine", Type.EmptyTypes), shim.Original);
            Assert.AreEqual(action, shim.Replacement);

            Assert.AreEqual(typeof(ShimTests).GetMethod("TestReplace"), shim1.Original);
            Assert.AreSame(shimTests, shim1.Instance);
            Assert.AreEqual(actionInstance, shim1.Replacement);
        }

        [TestMethod]
        public void TestReplacePropertyGetter()
        {
            Shim shim = Shim.Replace(() => Thread.CurrentThread.CurrentCulture);

            Assert.AreEqual(typeof(Thread).GetProperty(nameof(Thread.CurrentCulture), typeof(CultureInfo)).GetMethod, shim.Original);
            Assert.IsNull(shim.Replacement);
        }

        [TestMethod]
        public void TestReplacePropertySetter()
        {
            Shim shim = Shim.Replace(() => Is.A<Thread>().CurrentCulture, true);

            Assert.AreEqual(typeof(Thread).GetProperty(nameof(Thread.CurrentCulture), typeof(CultureInfo)).SetMethod, shim.Original);
            Assert.IsNull(shim.Replacement);
        }        
        
        [TestMethod]
        public void TestReplacePropertySetterAction()
        {
            var getterExecuted = false;
            var getterShim = Shim.Replace(() => Is.A<Thread>().CurrentCulture)
                .With((Thread t) =>
                {
                    getterExecuted = true;
                    return t.CurrentCulture;
                });
            var setterExecuted = false;
            var setterShim = Shim.Replace(() => Is.A<Thread>().CurrentCulture, true)
                .With((Thread t, CultureInfo value) =>
                {
                    setterExecuted = true;
                    t.CurrentCulture = value;
                });

            var currentCultureProperty = typeof(Thread).GetProperty(nameof(Thread.CurrentCulture), typeof(CultureInfo));
            Assert.AreEqual(currentCultureProperty.GetMethod, getterShim.Original);
            Assert.AreEqual(currentCultureProperty.SetMethod, setterShim.Original);

            PoseContext.Isolate(() =>
            {
                var oldCulture = Thread.CurrentThread.CurrentCulture;
                Thread.CurrentThread.CurrentCulture = oldCulture;
            }, getterShim, setterShim);

            Assert.IsTrue(getterExecuted, "Getter not executed");
            Assert.IsTrue(setterExecuted, "Setter not executed");
        }

        [TestMethod]
        public void TestReplaceWithMethodInfo()
        {
            var action = new Action<string>((string value) => 
            {
                Debug.WriteLine(value);
            });

            var writeLog = typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) });

            Shim shim = Shim.Replace(typeof(Console), writeLog).With(action);

            Assert.AreEqual(writeLog, shim.Original);
            Assert.AreSame(typeof(Console), shim.Type);
            Assert.AreEqual(action, shim.Replacement);
        }        

        [TestMethod]
        public void TestIsolateShimWithMethodInfo()
        {
            var called = false;

            var action = new Action<string>((string value) =>
            {
                called = true;
            });

            var writeLog = typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) });

            Shim shim = Shim.Replace(typeof(Console), writeLog).With(action);

            PoseContext.Isolate(() => 
            {
                Console.WriteLine("Teste");
            }, shim);

            Assert.AreEqual(true, called);
        }

        private static class SomeStaticClass
        {
            private static void SomePrivateMethod() { }

            public static void CallSomePrivateMethod()
            {
                SomePrivateMethod();
            }
        }

        [TestMethod]
        public void TestIsolateShimWithPrivateStaticMethod()
        {
            var called = false;

            var action = new Action(() =>
            {
                called = true;
            });

            var somePrivateMethod = typeof(SomeStaticClass).GetMethod("SomePrivateMethod", BindingFlags.Static | BindingFlags.NonPublic);

            Shim shim = Shim.Replace(typeof(SomeStaticClass), somePrivateMethod).With(action);

            PoseContext.Isolate(() =>
            {
                SomeStaticClass.CallSomePrivateMethod();
            }, shim);

            Assert.AreEqual(true, called);
        }

        private class SomePrivateClass
        {
            private void SomePrivateMethod() { }

            public void CallPrivateMethod()
            {
                this.SomePrivateMethod();
            }
        }

        [TestMethod]
        public void TestIsolateShimWithPrivateInstanceMethod()
        {   
            var called = false;

            var action = new Action<SomePrivateClass>((SomePrivateClass @this) =>
            {
                called = true;
            });
                        
            var somePrivateMethod = typeof(SomePrivateClass).GetMethod("SomePrivateMethod", BindingFlags.Instance | BindingFlags.NonPublic);

            Shim shim = Shim.Replace(typeof(SomePrivateClass), somePrivateMethod).With(action);

            PoseContext.Isolate(() =>
            {
                var instanceOfSomePrivateClass = new SomePrivateClass();
                instanceOfSomePrivateClass.CallPrivateMethod();
            }, shim);

            Assert.AreEqual(true, called);
        }
    }
}
