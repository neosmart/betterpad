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
    public partial class ReplaceDialog : Form
    {
        RichTextBox _textBox;
        public RichTextBoxFinds Options { get; private set; } = RichTextBoxFinds.None;

        public ReplaceDialog(RichTextBox text)
        {
            _textBox = text;
            InitializeComponent();
        }

        public string SearchTerm => txtFind.Text;
        public string ReplaceTerm => txtReplace.Text;

        public Func<string, bool> FindCallback;
        public Func<string, string, bool> ReplaceCallback;
        public Action<string> Status;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                btnCancel.PerformClick();
                return true;
            }
            if (keyData == Keys.Enter)
            {
                btnFind.PerformClick();
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

        private void btnFind_Click(object sender, EventArgs e)
        {
            if (!FindCallback(SearchTerm))
            {
                System.Media.SystemSounds.Beep.Play();
                Status("No matches found!");
            }
        }

        private void btnReplace_Click(object sender, EventArgs e)
        {
            if (ReplaceCallback(SearchTerm, ReplaceTerm))
            {
                //move to next match
                FindCallback(SearchTerm);
            }
            else
            {
                System.Media.SystemSounds.Beep.Play();
                Status("No matches found!");
            }
        }

        private void btnReplaceAll_Click(object sender, EventArgs e)
        {
            int replacements = 0;
            while (ReplaceCallback(SearchTerm, ReplaceTerm))
            {
                //move to next match
                ++replacements;
                if (!FindCallback(SearchTerm))
                {
                    break;
                }
            }

            if (replacements == 0)
            {
                System.Media.SystemSounds.Beep.Play();
            }
            Status($"Replaced {replacements} occurrence(s)");
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
