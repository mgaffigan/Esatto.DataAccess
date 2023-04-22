using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Esatto.DataAccess
{
    public static class TransactionExtensions
    {
        public static void ForceMSDTC(this Transaction txn)
        {
            // per https://social.msdn.microsoft.com/Forums/en-US/32e54787-bf41-464e-8c6c-cd9fe838ea2c/transaction-aborted-with-adonet-since-installing-sql-server-2008?forum=adodotnetdataproviders
            TransactionInterop.GetTransmitterPropagationToken(txn);
        }
    }
}
