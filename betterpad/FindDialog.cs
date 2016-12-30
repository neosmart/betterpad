using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterpad
{
    public partial class FindDialog : Form
    {
        RichTextBox _textBox;
        public RichTextBoxFinds Options { get; private set; } = RichTextBoxFinds.None;

        public FindDialog(RichTextBox text)
        {
            _textBox = text;
            InitializeComponent();
        }

        public string SearchTerm { get; private set; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            if (keyData == Keys.Enter)
            {
                SearchTerm = SearchBox.Text;
                DialogResult = !string.IsNullOrEmpty(SearchTerm) ? DialogResult.OK : DialogResult.Cancel;
                Close();
                return true;
            }

            // Call the base class
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void chkMatchCase_CheckedChanged(object sender, EventArgs e)
        {
            if ((sender as CheckBox).Checked)
            {
                Options |= RichTextBoxFinds.MatchCase;
            }
            else
            {
                Options &= ~RichTextBoxFinds.MatchCase;
            }
        }

        private void chkWholeWord_CheckedChanged(object sender, EventArgs e)
        {
            if ((sender as CheckBox).Checked)
            {
                Options |= RichTextBoxFinds.WholeWord;
            }
            else
            {
                Options &= ~RichTextBoxFinds.WholeWord;
            }
        }
    }
}
