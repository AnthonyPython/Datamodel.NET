﻿using System;
using System.Collections;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Datamodel;

namespace Datamodel_Tests
{
    public class DatamodelTests
    {
        protected FileStream Binary_5_File = System.IO.File.OpenRead(@"Resources\taunt05_b5.dmx");
        protected FileStream Binary_4_File = System.IO.File.OpenRead(@"Resources\binary4.dmx");
        protected FileStream KeyValues2_1_File = System.IO.File.OpenRead(@"Resources\taunt05.dmx");

        static DatamodelTests()
        {
            var binary = new byte[16];
            new Random().NextBytes(binary);
            var quat = new Quaternion(1, 2, 3, 4);
            quat.Normalise();

            TestValues = new object[] { "hello_world", 1, 1.5f, true, binary, TimeSpan.FromMinutes(5), System.Drawing.Color.Blue, 
                new Vector2(1,2), new Vector3(1,2,3), new Angle(1,2,3), new Vector4(1,2,3,4), quat, new Matrix(Enumerable.Range(0,4*4).Select(i => (float)i)) };
        }

        private TestContext testContextInstance;
        public TestContext TestContext
        {
            get { return testContextInstance; }
            set { testContextInstance = value; }
        }

        protected string OutPath { get { return System.IO.Path.Combine(TestContext.TestResultsDirectory, TestContext.TestName); } }
        protected string DmxSavePath { get { return OutPath + ".dmx"; } }
        protected string DmxConvertPath { get { return OutPath + "_convert.dmx"; } }

        protected void Cleanup()
        {
            System.IO.File.Delete(DmxSavePath);
            System.IO.File.Delete(DmxConvertPath);
        }

        protected void SaveAndConvert(Datamodel.Datamodel dm, string encoding, int version)
        {
            dm.Save(DmxSavePath, encoding, version);

            var dmxconvert = new System.Diagnostics.Process();
            dmxconvert.StartInfo = new System.Diagnostics.ProcessStartInfo()
            {
                FileName = System.IO.Path.Combine(Properties.Resources.ValveSourceBinaries, "dmxconvert.exe"),
                Arguments = String.Format("-i \"{0}\" -o \"{1}\" -oe {2}", DmxSavePath, DmxConvertPath, encoding),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            Assert.IsTrue(File.Exists(dmxconvert.StartInfo.FileName),String.Format("Could not find dmxconvert at {0}",dmxconvert.StartInfo.FileName));

            dmxconvert.Start();
            var err = dmxconvert.StandardError.ReadToEnd(); // dmxconvert writes to CON instead of STD...this does nothing!
            dmxconvert.WaitForExit();

            if (dmxconvert.ExitCode != 0)
                throw new AssertFailedException("dmxconvert reported an error.");

        }

        /// <summary>
        /// Perform a parallel loop over all elements and attributes
        /// </summary>
        protected void PrintContents(Datamodel.Datamodel dm)
        {
            System.Threading.Tasks.Parallel.ForEach<Datamodel.Element>(dm.AllElements, e =>
            {
                System.Threading.Tasks.Parallel.ForEach<Datamodel.Attribute>(e, a => { ; });
            });
        }

        protected static object[] TestValues;
        protected static Guid RootGuid = Guid.NewGuid();

        protected static void Populate(Datamodel.Datamodel dm, int attr_version)
        {
            dm.Root = dm.CreateElement("root", RootGuid);
            foreach (var value in TestValues)
            {
                if (attr_version < 2 && value is TimeSpan)
                    continue;
                var name = value.GetType().Name;

                dm.Root[name] = value;
                Assert.AreSame(value, dm.Root[name]);

                name += " array";
                var list = value.GetType().MakeListType().GetConstructor(Type.EmptyTypes).Invoke(null) as IList;
                list.Add(value);
                list.Add(value);
                dm.Root[name] = list;
                Assert.AreSame(list, dm.Root[name]);
            }
        }

        protected void ValidatePopulated(int attr_version)
        {
            var dm = Datamodel.Datamodel.Load(DmxConvertPath);
            Assert.AreEqual(RootGuid, dm.Root.ID);
            foreach (var value in TestValues)
            {
                if (attr_version < 2 && value is TimeSpan)
                    continue;
                var name = value.GetType().Name;
                if (value is ICollection)
                    CollectionAssert.AreEqual((ICollection)value, (ICollection)dm.Root[name]);
                else if (value is System.Drawing.Color)
                    Assert.AreEqual(((System.Drawing.Color)value).ToArgb(), dm.Root.Get<System.Drawing.Color>(name).ToArgb());
                else if (value.GetType().IsSubclassOf(typeof(VectorBase)))
                {
                    var vec_value = ((VectorBase)value);
                    var dm_value = ((VectorBase)dm.Root[name]).ToArray();
                    for (int i = 0; i < vec_value.Count(); i++)
                        Assert.AreEqual(vec_value.ElementAt(i), dm_value.ElementAt(i), 0.00001, name);
                }
                else
                    Assert.AreEqual(value, dm.Root[name], name);
            }

            dm.Dispose();
        }

        protected void Create(string encoding, int version, bool memory_save = false)
        {
            var dm = new Datamodel.Datamodel("model", 1); // using "model" to keep dxmconvert happy
            var attr_version = encoding == "keyvalues2" || version >= 5 ? 2 : 1;
            Populate(dm,attr_version);
            
            dm.Root["Arr"] = new System.Collections.ObjectModel.ObservableCollection<int>();
            dm.Root.GetArray<int>("Arr");

            if (memory_save)
                dm.Save(new System.IO.MemoryStream(), encoding, version);
            else
            {
                dm.Save(DmxSavePath, encoding, version);
                SaveAndConvert(dm, encoding, version);
                ValidatePopulated(attr_version);
                Cleanup();
            }
        }
    }

    [TestClass]
    public class Functionality : DatamodelTests
    {

        [TestMethod]
        public void Create_Binary_5()
        {
            Create("binary", 5);
        }
        [TestMethod]
        public void Create_Binary_4()
        {
            Create("binary", 4);
        }
        [TestMethod]
        public void Create_Binary_3()
        {
            Create("binary", 3);
        }
        [TestMethod]
        public void Create_Binary_2()
        {
            Create("binary", 2);
        }

        [TestMethod]
        public void Create_KeyValues2_1()
        {
            Create("keyvalues2", 1);
        }

        void Get_TF2(Datamodel.Datamodel dm)
        {
            dm.Root.Get<Element>("skeleton").GetArray<Element>("children")[0].Any();
        }

        [TestMethod]
        public void TF2_Binary_5()
        {
            var dm = Datamodel.Datamodel.Load(Binary_5_File);
            PrintContents(dm);
            Get_TF2(dm);
            SaveAndConvert(dm, "binary", 5);

            Cleanup();
        }

        [TestMethod]
        public void TF2_Binary_4()
        {
            var dm = Datamodel.Datamodel.Load(Binary_4_File);
            PrintContents(dm);
            Get_TF2(dm);
            SaveAndConvert(dm, "binary", 4);

            Cleanup();
        }

        [TestMethod]
        public void TF2_KeyValues2_1()
        {
            var dm = Datamodel.Datamodel.Load(KeyValues2_1_File);
            PrintContents(dm);
            Get_TF2(dm);
            SaveAndConvert(dm, "keyvalues2", 1);

            Cleanup();
        }
    }

    [TestClass]
    public class Performance : DatamodelTests
    {
        const int Load_Iterations = 10;
        System.Diagnostics.Stopwatch Timer = new System.Diagnostics.Stopwatch();

        void Load(FileStream f)
        {
            long elapsed = 0;
            Timer.Start();
            foreach (var i in Enumerable.Range(0, Load_Iterations + 1))
            {
                Datamodel.Datamodel.Load(f, Datamodel.Codecs.DeferredMode.Disabled);
                if (i > 0)
                {
                    Console.WriteLine(Timer.ElapsedMilliseconds);
                    elapsed += Timer.ElapsedMilliseconds;
                }
                Timer.Restart();
            }
            Timer.Stop();
            Console.WriteLine("Average: {0}ms", elapsed / Load_Iterations);
        }
        [TestMethod]
        public void Perf_Load_Binary5()
        {
            Load(Binary_5_File);
        }

        [TestMethod]
        public void Perf_Load_KeyValues2_1()
        {
            Load(KeyValues2_1_File);
        }

        [TestMethod]
        public void Perf_Create_Binary5()
        {
            foreach (var i in Enumerable.Range(0, 1000))
                Create("binary", 5,true);
        }
    }

    static class Extensions
    {
        public static Type MakeListType(this Type t)
        {
            return typeof(System.Collections.Generic.List<>).MakeGenericType(t);
        }

        public static void Normalise(this Quaternion q)
        {
            float scale = 1.0f / (float)(System.Math.Sqrt(q.W * q.W + (q.X * q.X + q.Y * q.Y + q.Z * q.Z)));
            q.X *= scale;
            q.Y *= scale;
            q.Z *= scale;
            q.W *= scale;
        }
    }
}
