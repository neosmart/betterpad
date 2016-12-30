using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    class FindStatus
    {
        public enum SearchDirection
        {
            Forward,
            Reverse
        };
        public SearchDirection Direction = SearchDirection.Forward;
        public string SearchTerm;
        public RichTextBoxFinds Options;
        public int StartPosition;
        public int FindCount = 0;
    }
}
