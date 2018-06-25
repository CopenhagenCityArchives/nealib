using System;
using HardHorn.Archiving;
using HardHorn.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;

namespace LibHardHornTest
{
    [TestClass]
    public class TableTest
    {
        [TestMethod]
        [DeploymentItem(@"..\..\TestResources", @"TestResources")]
        public void TestTableReplace()
        {
            var archiveVersion = ArchiveVersion.Load(@"TestResources\AVID.OVERFLOW.1.1", new TestLogger());

            var table1 = archiveVersion.Tables.First();
            var pattern = new Regex(@"^(\d\d\d\d-\d\d-\d\d)T\d\d:\d\d:\d\d(?:.\d+)?$");
            var replacement = "$1";
            var column4 = table1.Columns.ToList()[3];
            var replacementOperation = new ReplacementOperation(table1, column4, pattern, replacement);
            var stream = new MemoryStream();
            var originalTableReader = new TableReader(table1);
            Post[,] posts;
            originalTableReader.Read(out posts, 3);
            var replacer = new TableReplacer(table1, new ReplacementOperation[] { replacementOperation }, stream);
            replacer.WriteHeader();
            replacer.Write(posts, 3);
            replacer.WriteFooter();
            replacer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            var replacedTableReader = new TableReader(table1, stream);
            replacedTableReader.Read(out posts, 3);
            var assertPattern = new Regex(@"^\d\d\d\d-\d\d-\d\d$");
            for (int i = 0; i < 3; i++)
            {
                var post = posts[i, 3];
                Assert.IsTrue(assertPattern.IsMatch(post.Data));
            }
        }

        public void AssertReplacePost(string data, bool isNull, Regex pattern, string replacement, string expected, int expectedReplacements = 1)
        {
            AssertReplacePost(new Post(data, isNull), pattern, replacement, expected, expectedReplacements);
        }

        public void AssertReplacePost(Post post, Regex pattern, string replacement, string expected, int expectedReplacements = 1)
        {
            int count = post.ReplacePattern(pattern, replacement);
            Assert.AreEqual(expected, post.Data);
            Assert.AreEqual(expectedReplacements, count);
        }

        [TestMethod]
        public void TestReplacePost()
        {
            var pattern = new Regex(@"^(\d\d)-(\d\d)-(\d\d\d\d)$", RegexOptions.Compiled);
            var replacement = "$3-$2-$1";
            AssertReplacePost("12-01-2000", false, pattern, replacement, "2000-01-12", 1);
            AssertReplacePost("12-1-2000", false, pattern, replacement, "12-1-2000", 0); // does not match

            pattern = new Regex(@"^([a-zA-Z]+)/([a-zA-Z0-9]+)/(\d+)$", RegexOptions.Compiled);
            replacement = "'$2'.'$1': $3";
            AssertReplacePost("ABC/aB01/123", false, pattern, replacement, "'aB01'.'ABC': 123");
            AssertReplacePost("qza/00000Z/9909", false, pattern, replacement, "'00000Z'.'qza': 9909");
            AssertReplacePost("111/00000Z/9909", false, pattern, replacement, "111/00000Z/9909", 0);

            pattern = new Regex(@"AAA(\d)", RegexOptions.Compiled);
            replacement = "BBB$1";
            AssertReplacePost("AAA1/AAA2/AAA3", false, pattern, replacement, "BBB1/BBB2/BBB3", 4);
        }
    }
}
