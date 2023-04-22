using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Esatto.DataAccess
{
    /// <summary>
    /// Event args for reporting status
    /// </summary>
    [ImmutableObject(true)]
    public class ProgressReportEventArgs : EventArgs
    {

        /// <summary>
        /// row being processed
        /// </summary>
        public int Progress { get; private set; }

        /// <summary>
        /// total number of rows to process
        /// </summary>
        public int Total { get; private set; }

        /// <summary>
        /// Text set by the sproc
        /// </summary>
        public string Status { get; private set; }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="total"></param>
        public ProgressReportEventArgs(int progress, int total, string status)
        {
            if (!(progress <= total && progress >= 0))
            {
                throw new ArgumentException("Contract assertion not met: progress <= total && progress >= 0", nameof(progress));
            }

            Progress = progress;
            Total = total;
            Status = status;
        }

    }
}
