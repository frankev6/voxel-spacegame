using System;
using System.Globalization;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Commands;
using NUnit.Framework;

namespace Game.Tests.EditMode.App
{
    public sealed class DevArgsTests
    {
        [Test] public void Parse_QuotedEscapedPositional()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.IsTrue(DevArgs.TryParse("ReCoRd start NAME=\"a \\\"quote\\\" \\\\ file\" x=1.25 n=-4 u=18446744073709551615 yes=1 no=false", out var command, out var args, out var error), error);
                Assert.AreEqual("record", command); CollectionAssert.AreEqual(new[] { "start" }, args.Positional);
                Assert.AreEqual("a \"quote\" \\ file", args.GetString("name", null));
                Assert.AreEqual(1.25, args.GetDouble("x", 0)); Assert.AreEqual(-4, args.GetLong("n", 0));
                Assert.AreEqual(ulong.MaxValue, args.GetULong("u", 0)); Assert.IsTrue(args.GetBool("yes", false)); Assert.IsFalse(args.GetBool("no", true));
                Assert.IsTrue(args.Has("NAME")); Assert.IsTrue(args.TryGetString("name", out _));
                CollectionAssert.Contains(args.Keys, "name"); Assert.AreEqual(42, args.GetLong("missing", 42));
                Assert.Throws<FormatException>(() => args.GetLong("name", 0));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
        [Test] public void Parse_UnterminatedQuote_Error()
        {
            Assert.IsFalse(DevArgs.TryParse("help cmd=\"missing", out _, out _, out var error));
            StringAssert.Contains("unterminated quote", error); StringAssert.Contains("column 10", error);
        }
    }
    public sealed class DevRegistryTests
    {
        [Test] public void DuplicateName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DevCommandRegistry(new HelpDevCommand(), new HelpDevCommand()));
        }
    }
}
