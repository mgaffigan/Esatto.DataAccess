using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Runtime.Serialization;
using System.IO;
using System.Xml.Serialization;

namespace Esatto.DataAccess
{
    /// <summary>
    /// Holds progress  information returned from dbo.UpdateProgress
    /// must be public due to xmlseralizer
    /// </summary>
    [Serializable]
    public class ProgressReport
    {

        internal ProgressReport()
        {
        }

        #region properties

        /// <summary>
        /// DB set source of the progress information
        /// (Application Name)
        /// </summary>
        public string DBSource { get; set; }

        /// <summary>
        /// Status set by the sproc
        /// </summary>
        [XmlAttribute]
        public string Status { get; set; }

        /// <summary>
        /// Total number of items being processed
        /// </summary>
        [XmlAttribute]
        public int Total { get; set; }

        /// <summary>
        /// Current item number
        /// </summary>
        [XmlAttribute]
        public int Progress { get; set; }

        /// <summary>
        /// percentage complete
        /// </summary>
        public double PercentComplete
        {
            get
            {
                if (Total > 0)
                {
                    return (((double)Progress / (double)Total) * 100.0);
                }
                else
                {
                    return 100.0;
                }
            }
        }

        #endregion

        #region parsing

        /// <summary>
        /// attemps to parse and construct a new class
        /// </summary>
        /// <param name="xml">string to attempt parse on</param>
        /// <param name="progressInfo">new constructed class</param>
        /// <returns>false when the input is not in a valid format</returns>
        public static bool TryParse(string xml, out ProgressReport progressInfo)
        {
            if (string.IsNullOrWhiteSpace(xml)
                || !xml.StartsWith("<ProgressReport"))
            {
                progressInfo = null;
                return false;
            }

            try
            {
                progressInfo = FromXml(xml);
                return true;
            }
            catch
            {
                progressInfo = null;
                return false;
            }
        }


        public string ToXml()
        {
            var xs = new XmlSerializer(typeof(ProgressReport));
            using (var stream = new StringWriter())
            {
                xs.Serialize(stream, this);

                return stream.ToString();
            }
        }

        public static ProgressReport FromXml(string xmlText)
        {
            if (xmlText == null)
            {
                throw new ArgumentNullException(nameof(xmlText), "Contract assertion not met: xmlText != null");
            }

            var xs = new XmlSerializer(typeof(ProgressReport));
            using (var sr = new StringReader(xmlText))
            {
                return xs.Deserialize(sr) as ProgressReport;
            }
        }

        #endregion
    }
}
