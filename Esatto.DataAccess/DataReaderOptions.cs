using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.DataAccess
{
    [Flags]
    public enum DataReaderOptions
    {
        None = 0,
        TrimStringValues = 0x0001,
        Interpret19000101AsNull = 0x0002,
        DefaultParametersToSbcs = 0x0004
    }
}
