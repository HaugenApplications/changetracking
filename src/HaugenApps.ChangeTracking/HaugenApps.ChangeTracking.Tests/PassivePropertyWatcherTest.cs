using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HaugenApps.ChangeTracking.Tests
{
    [TestClass]
    public class PassivePropertyWatcherTest
    {
        public class BasicPOCO
        {
            public virtual string StringValue { get; set; }
            public virtual int IntValue { get; set; }
        }
        public class BasicPOCOWithConstructor : BasicPOCO
        {
            public BasicPOCOWithConstructor(int val)
            {
                this.IntValue = val;
            }
        }

        [TestMethod]
        public void Generic_NoHistory_ChangeOneProperty_WithConstructor()
        {
            var type = PassivePropertyWatcher.GetWrapperType<BasicPOCOWithConstructor>();
            var inst = (BasicPOCOWithConstructor)type.GetConstructor(new[] { typeof(int) }).Invoke(new object[] { 12 });

            var propWatcher = PassivePropertyWatcher.GetPropertyWatcher(inst);

            propWatcher.Clear();

            inst.StringValue = "New value!";

            var ret = propWatcher.GetValues().ToArray();

            Assert.AreEqual(1, ret.Length, "Length was incorrect.");
            Assert.AreEqual("StringValue", ret[0].Key.Name, "Name was incorrect.");
            Assert.AreEqual("New value!", ret[0].Value, "Value was incorrect.");
        }

        [TestMethod]
        public void Generic_NoHistory_ChangeOneProperty()
        {
            var inst = new PassivePropertyWatcher<BasicPOCO>(false);

            inst.Instance.StringValue = "New value!";

            var ret = inst.PropertyWatcher.GetValues().ToArray();

            Assert.AreEqual(1, ret.Length, "Length was incorrect.");
            Assert.AreEqual("StringValue", ret[0].Key.Name, "Name was incorrect.");
            Assert.AreEqual("New value!", ret[0].Value, "Value was incorrect.");
        }

        [TestMethod]
        public void Generic_NoHistory_ChangeOneProperty_ReadOnly()
        {
            var inst = new PassivePropertyWatcher<BasicPOCO>(false);

            inst.Instance.StringValue = "New value!";

            try
            {
                inst.PropertyWatcher.Set(c => c.StringValue, "Uh oh!");

                Assert.Fail("PropertyWatcher was not read-only.");
            }
            catch
            {

            }
        }

        [TestMethod]
        public void Generic_NoHistory_ChangeOneProperty_Updates()
        {
            var inst = new PassivePropertyWatcher<BasicPOCO>(false);

            var propWatch = inst.PropertyWatcher;

            inst.Instance.StringValue = "New value!";

            var ret = propWatch.GetValues().ToArray();

            Assert.AreEqual(1, ret.Length, "Length was incorrect.");

        }

        [TestMethod]
        public void Generic_WithHistory_ChangeOneProperty()
        {
            var inst = new PassivePropertyWatcher<BasicPOCO>(true);

            inst.Instance.StringValue = "First new value!";
            inst.Instance.StringValue = "Second new value!";

            var ret = inst.PropertyWatcher.GetHistory(c => c.StringValue).ToArray();

            Assert.AreEqual(2, ret.Length, "Length was incorrect.");
            Assert.AreEqual("First new value!", ret[0], "First was incorrect.");
            Assert.AreEqual("Second new value!", ret[1], "Second was incorrect.");
        }
    }
}