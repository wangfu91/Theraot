﻿using System.Reflection;
using NUnit.Framework;
using System;
using System.Threading;
using Theraot.Threading.Needles;

namespace Tests.Theraot.Threading.Needles
{
    [TestFixture]
    internal class LazyNeedleTest
    {
        [Test]
        public void ConstructorWithNull()
        {
            Assert.Throws(typeof(ArgumentNullException), () => new LazyNeedle<int>(null));
        }

        [Test]
        public void ConstructorWithTargetValue()
        {
            var needle = new LazyNeedle<int>(5);
            Assert.AreEqual(needle.Value, 5);
        }

        [Test]
        public void ConstructorWithValueFactory()
        {
            var needle = new LazyNeedle<int>(() => 5);
            Assert.AreEqual(needle.Value, 5);
        }

        [Test]
        public void DefaultConstructor()
        {
            //Not nullable
            var a = new LazyNeedle<int>();
            Assert.IsTrue(a.IsAlive);   // Not nullable is always alive
            Assert.AreEqual(a.Value, default(int));
            //Nullable
            var b = new LazyNeedle<int?>();
            Assert.IsFalse(b.IsAlive);
            Assert.Throws(typeof(NullReferenceException), () => GC.KeepAlive(b.Value));
            //Object
            var c = new LazyNeedle<int?>();
            Assert.IsFalse(c.IsAlive);
            Assert.Throws(typeof(NullReferenceException), () => GC.KeepAlive(c.Value));
        }

        [Test]
        public void FixedHashCode()
        {
            //No initial value
            var a = new LazyNeedle<int>();
            var hashcode = a.GetHashCode();
            a.Value = 5;
            Assert.AreEqual(hashcode, a.GetHashCode());
            a.Value = 6;
            Assert.AreEqual(hashcode, a.GetHashCode());
            //Initial value
            var b = new LazyNeedle<int>(5);
            hashcode = 5.GetHashCode();
            b.Value = 5;
            Assert.AreEqual(hashcode, b.GetHashCode());
            b.Value = 6;
            Assert.AreEqual(hashcode, b.GetHashCode());
            //ValueFactory
            var c = new LazyNeedle<int>(() => 5);
            hashcode = c.GetHashCode();
            c.Initialize();
            Assert.IsTrue(c.IsCompleted);
            Assert.AreEqual(c.Value, 5);
            Assert.AreEqual(hashcode, c.GetHashCode());
        }

        [Test]
        public void InitializeOnlyOnce()
        {
            var control = 0;
            var threadDone = 0;
            var needle = new LazyNeedle<int>(() =>
            {
                Interlocked.Increment(ref control); return 5;
            });
            var manual = new ManualResetEvent(false);
            var threadA = new Thread(() =>
            {
                manual.WaitOne();
                needle.Initialize();
                Interlocked.Increment(ref threadDone);
            });
            var threadB = new Thread(() =>
            {
                manual.WaitOne();
                needle.Initialize();
                Interlocked.Increment(ref threadDone);
            });
            var threadC = new Thread(() =>
            {
                manual.WaitOne();
                needle.Initialize();
                Interlocked.Increment(ref threadDone);
            });
            threadA.Start();
            threadB.Start();
            threadC.Start();
            manual.Set();
            threadA.Join();
            threadB.Join();
            threadC.Join();
            needle.Initialize();
            needle.Initialize();
            Assert.IsTrue(needle.IsCompleted);
            Assert.AreEqual(needle.Value, 5);
            Assert.AreEqual(control, 1);
            Assert.AreEqual(threadDone, 3);
            manual.Close();
        }

        [Test]
        public void IsCompletedAndIsAlive()
        {
            //Not Nullable
            var a = new LazyNeedle<int>(() => 5);
            Assert.IsTrue(a.IsAlive);   // Not nullable is always alive
            Assert.IsFalse(a.IsCompleted);
            Assert.AreEqual(a.Value, 5);
            Assert.IsTrue(a.IsAlive);
            Assert.IsTrue(a.IsCompleted);
            //Nullable
            var b = new LazyNeedle<int?>(() => null);
            Assert.IsFalse(b.IsAlive);
            Assert.IsFalse(b.IsCompleted);
            Assert.AreEqual(b.Value, null);
            Assert.IsFalse(b.IsAlive);
            Assert.IsTrue(b.IsCompleted);
            //Object
            var c = new LazyNeedle<string>(() => null);
            Assert.IsFalse(c.IsAlive);
            Assert.IsFalse(c.IsCompleted);
            Assert.AreEqual(c.Value, null);
            Assert.IsFalse(c.IsAlive);
            Assert.IsTrue(c.IsCompleted);
        }

        [Test]
        public void SetTheValue()
        {
            //Alive version
            //Not nullable
            var a = (new LazyNeedle<int>());
            Assert.IsTrue(a.IsAlive);   // Not nullable is always alive
            Assert.IsTrue(a.IsCompleted);   // Nothing to run
            a.Value = 5;
            Assert.AreEqual(a.Value, 5);
            Assert.IsTrue(a.IsAlive);
            Assert.IsTrue(a.IsCompleted);
            //Nullable
            var b = (new LazyNeedle<int?>());
            Assert.IsFalse(b.IsAlive);
            Assert.IsTrue(b.IsCompleted);   // Nothing to run
            b.Value = 5;
            Assert.AreEqual(b.Value, 5);
            Assert.IsTrue(b.IsAlive);
            Assert.IsTrue(b.IsCompleted);
            //object
            var c = (new LazyNeedle<string>());
            Assert.IsFalse(c.IsAlive);
            Assert.IsTrue(c.IsCompleted);   // Nothing to run
            c.Value = String.Empty;
            Assert.AreEqual(c.Value, String.Empty);
            Assert.IsTrue(c.IsAlive);
            Assert.IsTrue(c.IsCompleted);
            //Not Alive Version
            //Nullable
            b.Value = null;
            Assert.AreEqual(b.Value, null);
            Assert.IsFalse(b.IsAlive);
            Assert.IsTrue(b.IsCompleted);
            //object
            c.Value = null;
            Assert.AreEqual(c.Value, null);
            Assert.IsFalse(c.IsAlive);
            Assert.IsTrue(c.IsCompleted);
        }

        [Test]
        public void ValueFactoryReentry()
        {
            LazyNeedle<int>[] needle = { null };
            needle[0] = new LazyNeedle<int>(() => ReferenceEquals(needle[0], null) ? 0 : needle[0].Value);
            Assert.Throws(typeof(InvalidOperationException), needle[0].Initialize);
        }

        [Test]
        public void Waiting()
        {
            var completedWas = false;
            var threadDone = 0;
            var control = 0;
            var needle = new LazyNeedle<int>(() =>
            {
                Interlocked.Increment(ref control); return 5;
            });
            var threadA = new Thread(() =>
            {
                completedWas = needle.IsCompleted;
                needle.Wait();
                needle.Initialize();
                Interlocked.Increment(ref threadDone);
            });
            var threadB = new Thread(() => Assert.AreEqual(needle.Value, 5));
            threadA.Start();
            threadB.Start();
            threadA.Join();
            threadB.Join();
            needle.Initialize();
            Assert.IsTrue(needle.IsCompleted);
            Assert.AreEqual(needle.Value, 5);
            Assert.AreEqual(control, 1);
            Assert.AreEqual(threadDone, 1);
            Assert.AreEqual(completedWas, false);
        }

        [Test]
        public void WaitingAlreadyCompleted()
        {
            var needle = new LazyNeedle<int>(5);
            Assert.IsTrue(needle.IsCompleted);
            Assert.AreEqual(needle.Value, 5);
            needle.Wait();
            Assert.IsTrue(needle.IsCompleted);
            Assert.AreEqual(needle.Value, 5);
            needle.Initialize();
            Assert.IsTrue(needle.IsCompleted);
            Assert.AreEqual(needle.Value, 5);
        }

        [Test]
        public void WaitingNested()
        {
            LazyNeedle<int>[] needle = { null };
            needle[0] = new LazyNeedle<int>(() =>
            {
                if (ReferenceEquals(needle[0], null))
                {
                    return 0;
                }
                else
                {
                    needle[0].Wait();
                    return 0;
                }
            });
            Assert.Throws(typeof(InvalidOperationException), needle[0].Initialize);
        }

        [Test]
        public void CacheException()
        {
            int count = 0;

            // No Cache
            var a = new LazyNeedle<int>(() =>
            {
                if (count == 0)
                {
                    count++;
                    throw new InvalidOperationException();
                }
                else return count;
            });
            Assert.Throws(typeof(InvalidOperationException), () => GC.KeepAlive(a.Value));
            Assert.AreEqual(a.Value, 1);

            // Cache
            count = 0;
            a = new LazyNeedle<int>(() =>
            {
                if (count == 0)
                {
                    count++;
                    throw new InvalidOperationException();
                }
                else return count;
            }, true);
            Assert.Throws(typeof(InvalidOperationException), () => GC.KeepAlive(a.Value));
            Assert.Throws(typeof(InvalidOperationException), () => GC.KeepAlive(a.Value)); // Did cache
        }
    }
}