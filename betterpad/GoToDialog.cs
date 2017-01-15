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
    public partial class GoToDialog : Form
    {
        public int LineNumber { get; private set; }

        public GoToDialog()
        {
            InitializeComponent();

            KeyEventHandler handler = (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Parse();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };

            KeyUp += handler;
            txtLine.KeyDown += handler;
        }

        private void Parse()
        {
            if (int.TryParse(txtLine.Text, out int temp))
            {
                LineNumber = temp;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                System.Media.SystemSounds.Beep.Play();
                txtLine.SelectAll();
                txtLine.Focus();
            }
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            Parse();
        }

        public void SelectAll()
        {
            txtLine.SelectAll();
            txtLine.Focus();
        }
    }
}
