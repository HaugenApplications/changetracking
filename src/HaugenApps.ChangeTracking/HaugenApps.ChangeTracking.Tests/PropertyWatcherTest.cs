using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HaugenApps.ChangeTracking.Tests
{
    [TestClass]
    public class PropertyWatcherTest
    {
        class BasicPOCO
        {
            public string StringValue { get; set; }
            public int IntValue { get; set; }
        }

        [TestMethod]
        public void Generic_NoHistory_ChangeOneProperty()
        {
            var propWatch = new PropertyWatcher<BasicPOCO>(false);

            propWatch.Set(c => c.StringValue, "New value!");

            var ret = propWatch.GetValues().ToArray();

            Assert.AreEqual(1, ret.Length, "Length was incorrect.");
            Assert.AreEqual("StringValue", ret[0].Key.Name, "Name was incorrect.");
            Assert.AreEqual("New value!", ret[0].Value, "Value was incorrect.");
        }

        [TestMethod]
        public void Generic_NoHistory_INotifyPropertyChanged()
        {
            var propWatch = new PropertyWatcher<BasicPOCO>(false);

            bool called = false;

            propWatch.PropertyChanged += (Sender, Args) => called = true;

            propWatch.Set(c => c.StringValue, "New value!");

            Assert.IsTrue(called, "PropertyChanged was not called.");
        }

        [TestMethod]
        public void Generic_WithHistory_ChangeOneProperty()
        {
            var propWatch = new PropertyWatcher<BasicPOCO>(true);

            propWatch.Set(c => c.StringValue, "First new value!");
            propWatch.Set(c => c.StringValue, "Second new value!");

            var ret = propWatch.GetHistory(c => c.StringValue).ToArray();

            Assert.AreEqual(2, ret.Length);
            Assert.AreEqual("First new value!", ret[0]);
            Assert.AreEqual("Second new value!", ret[1]);
        }

        [TestMethod]
        public void Generic_WithHistory_ChangeOneProperty_ClearAllHistory()
        {
            var propWatch = new PropertyWatcher<BasicPOCO>(true);

            propWatch.Set(c => c.StringValue, "Zeroth new value!");

            propWatch.ClearHistory();

            propWatch.Set(c => c.StringValue, "First new value!");
            propWatch.Set(c => c.StringValue, "Second new value!");

            var ret = propWatch.GetHistory(c => c.StringValue).ToArray();

            Assert.AreEqual(2, ret.Length);
            Assert.AreEqual("First new value!", ret[0]);
            Assert.AreEqual("Second new value!", ret[1]);
        }
    }
}
