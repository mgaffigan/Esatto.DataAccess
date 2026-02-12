using Microsoft.Extensions.Logging.Debug;
using System.Transactions;

namespace Esatto.DataAccess.Tests;

[TestClass]
public sealed class XactAbortTests
{
    [TestMethod]
    public void OriginalExceptionOccurs()
    {
        var con = new DbConf(new DebugLoggerProvider().CreateLogger("TEST"),
            @"Server=(local);Integrated Security=true;");
        using var txn = new TransactionScope();
        var command = new DbCommand(con, @"SET XACT_ABORT ON;

IF @@TRANCOUNT < 1 THROW 50000, 'No transaction', 1;

CREATE TABLE #T (A INT);
INSERT INTO #T VALUES (1);

INSERT INTO #T VALUES ('A');

INSERT INTO #T VALUES (2);

");

        try
        {
            command.Execute();
            Assert.Fail("Expected exception");
        }
        catch (Exception ex)
        {
            Assert.AreEqual("Conversion failed when converting the varchar value 'A' to data type int.", ex.Message);
        }
    }
}
