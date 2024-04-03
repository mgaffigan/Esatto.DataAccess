using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esatto.DataAccess;
using System.Threading;
using System.Transactions;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Esatto.DataAccess
{
    public class ServiceBrokerQueueListener : IDisposable
    {
        DbConf con = null;
        ILogger log = null;
        Thread thListener = null;
        DbCommand spReader = null;
        bool isStarted = false;

        public ServiceBrokerQueueListener(DbConf con, DbCommand proc, ILogger log)
        {
            if (con == null)
                throw new ArgumentNullException("con");
            if (proc == null)
                throw new ArgumentNullException("proc");
            if (log == null)
                throw new ArgumentNullException("log");

            if (proc.Timeout <= 30)
            {
                throw new ArgumentOutOfRangeException("proc.Timeout", "Polling command has "
                    + "a timeout under 31 seconds.  Usually service broker timeouts should "
                    + "be much longer.");
            }

            this.con = con;
            this.spReader = proc;
            this.log = log;
        }

        ~ServiceBrokerQueueListener()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (isStarted)
                    Stop();
            }
        }

        public void Start()
        {
            if (isStarted)
                throw new InvalidOperationException("Already started");
            isStarted = true;

            thListener = new Thread(new ParameterizedThreadStart(thStart));
            thListener.Start();
        }

        private void thStart(object o)
        {
            while (isStarted)
            {
                try
                {
                    using (TransactionScope tsBroker = new TransactionScope(TransactionScopeOption.Required, new TimeSpan(1, 0, 0)))
                    {
                        var sp = this.spReader;
                        // force MSDTC since a call to another SQL server or to MSMQ from the reader will fail
                        TransactionInterop.GetTransmitterPropagationToken(Transaction.Current);
                        log.LogDebug("Waiting for message for procedure {0}", sp.CommandText);

                        //call will wait till a message is available
                        using (var reader = sp.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                OnMessageReceived(reader);
                            }
                        }

                        tsBroker.Complete();
                    }
                }
                catch (Exception ex)
                {
                    //if the exception is anthing other than us exiting, via sqlcommand.cancel
                    //TODO: get code or some manner of identification other than message
                    if (!ex.Message.Contains("Operation cancelled"))
                    {
                        log.LogWarning(ex, "Exception while receiving a message");
                    }

                    if (isStarted)
                    {
                        Thread.Sleep(5000); // prevent the system from killing the network in the face of repeated failure.
                    }
                }
            }
        }

        protected virtual void OnMessageReceived(ResultSet reader)
        {
            MessageReceived?.Invoke(reader);
        }

        public void Stop()
        {
            if (thListener == null || !isStarted)
                return;

            isStarted = false;
            try
            {
                if (spReader != null)
                    spReader.Cancel();
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to gracefully shut down sb reader");
            }
            thListener.Join();
            thListener = null;
        }

        public delegate void MessageReceivedHandler(ResultSet rs);

        public event MessageReceivedHandler MessageReceived;
    }
}