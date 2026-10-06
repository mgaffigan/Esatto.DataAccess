using Microsoft.Extensions.Logging.Debug;
using System.Transactions;

namespace Esatto.DataAccess.Tests;

[TestClass]
public sealed class XactAbortTests
{
    private const string ConversionError = "Conversion failed when converting the varchar value 'A' to data type int.";

    private static DbConf CreateConnection() => new(new DebugLoggerProvider().CreateLogger("TEST"),
        @"Server=(local);Integrated Security=true;");

    [TestMethod]
    public void OriginalExceptionOccurs()
    {
        var con = CreateConnection();
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
            Assert.AreEqual(ConversionError, ex.Message);
        }
    }

    [TestMethod]
    public void ErrorAfterLastResultSetOccurs()
    {
        var con = CreateConnection();
        using var txn = new TransactionScope();
        var command = new DbCommand(con, @"SET XACT_ABORT ON;

IF @@TRANCOUNT < 1 THROW 50000, 'No transaction', 1;

CREATE TABLE #T (A INT);
INSERT INTO #T OUTPUT INSERTED.A VALUES (1);

INSERT INTO #T VALUES ('A');

");

        try
        {
            using (var reader = command.ExecuteReader())
            {
                CollectionAssert.AreEqual(new[] { 1 }, reader.GetList(rs => rs.GetInt("A")));
            }
            Assert.Fail("Expected exception");
        }
        catch (Exception ex)
        {
            Assert.AreEqual(ConversionError, ex.Message);
        }
    }
}
