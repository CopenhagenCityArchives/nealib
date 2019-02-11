using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HardHorn.Archiving;
using HardHorn.Analysis;

namespace LibHardHornTest
{
    /// <summary>
    /// Summary description for ForeignKeyTest
    /// </summary>
    [TestClass]
    public class ForeignKeyTestTest
    {
        public ForeignKeyTestTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        [DeploymentItem(@"..\..\TestResources", @"TestResources")]
        public void TestErrorsBlanks()
        {
            var AV = ArchiveVersion.Load(@"TestResources\AVID.FKEY.1.1", new TestLogger());

            int blankCount = 0;
            int errorCount = 0;
            var fkeyTest = new ForeignKeyTest(AV.TableIndex.Tables, notification => {
                switch (notification.Type)
                {
                    case HardHorn.Utility.NotificationType.ForeignKeyTestBlank:
                        var blankNoti = notification as HardHorn.Utility.ForeignKeyTestBlankNotification;
                        blankCount += blankNoti.Count.Value;
                        break;
                    case HardHorn.Utility.NotificationType.ForeignKeyTestError:
                        var errorNoti = notification as HardHorn.Utility.ForeignKeyTestErrorNotification;
                        errorCount += errorNoti.Count.Value;
                        break;
                    default:
                        Assert.Fail($"Unexpected notification type {notification.Type}.");
                        break;
                }
            });

            while (fkeyTest.MoveNextTable())
            {
                fkeyTest.InitializeReferencedValueLoading();
                while (fkeyTest.MoveNextForeignKey())
                {
                    while (fkeyTest.ReadReferencedForeignKeyValue()) { }
                }
                fkeyTest.InitializeTableTest();
                while (fkeyTest.ReadForeignKeyValue()) { }
            }

            Assert.AreEqual(1, blankCount, "Wrong number of blank references found.");
            Assert.AreEqual(1, errorCount, "Wrong number of foreign key reference errors found.");
        }
    }
}
