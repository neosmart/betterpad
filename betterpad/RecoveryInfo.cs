using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace betterpad
{
    [Serializable]
    class RecoveryInfo
    {
        public string FilePath;
        public string Text;
        public int Position;
    }
}
