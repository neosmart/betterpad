namespace betterpad
{
    partial class ReplaceDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.txtFind = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.chkWholeWord = new System.Windows.Forms.CheckBox();
            this.chkMatchCase = new System.Windows.Forms.CheckBox();
            this.btnReplace = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.txtReplace = new System.Windows.Forms.TextBox();
            this.btnReplaceAll = new System.Windows.Forms.Button();
            this.btnFind = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtFind
            // 
            this.txtFind.Location = new System.Drawing.Point(6, 28);
            this.txtFind.Name = "txtFind";
            this.txtFind.Size = new System.Drawing.Size(332, 29);
            this.txtFind.TabIndex = 0;
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(369, 205);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(140, 39);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.chkWholeWord);
            this.groupBox1.Controls.Add(this.chkMatchCase);
            this.groupBox1.Location = new System.Drawing.Point(12, 160);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(350, 84);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            // 
            // chkWholeWord
            // 
            this.chkWholeWord.AutoSize = true;
            this.chkWholeWord.Location = new System.Drawing.Point(156, 28);
            this.chkWholeWord.Name = "chkWholeWord";
            this.chkWholeWord.Size = new System.Drawing.Size(148, 29);
            this.chkWholeWord.TabIndex = 1;
            this.chkWholeWord.Text = "Whole Word";
            this.chkWholeWord.UseVisualStyleBackColor = true;
            this.chkWholeWord.CheckedChanged += new System.EventHandler(this.chkWholeWord_CheckedChanged);
            // 
            // chkMatchCase
            // 
            this.chkMatchCase.AutoSize = true;
            this.chkMatchCase.Location = new System.Drawing.Point(6, 28);
            this.chkMatchCase.Name = "chkMatchCase";
            this.chkMatchCase.Size = new System.Drawing.Size(144, 29);
            this.chkMatchCase.TabIndex = 0;
            this.chkMatchCase.Text = "Match Case";
            this.chkMatchCase.UseVisualStyleBackColor = true;
            this.chkMatchCase.CheckedChanged += new System.EventHandler(this.chkMatchCase_CheckedChanged);
            // 
            // btnReplace
            // 
            this.btnReplace.Location = new System.Drawing.Point(369, 67);
            this.btnReplace.Name = "btnReplace";
            this.btnReplace.Size = new System.Drawing.Size(140, 39);
            this.btnReplace.TabIndex = 4;
            this.btnReplace.Text = "&Replace";
            this.btnReplace.UseVisualStyleBackColor = true;
            this.btnReplace.Click += new System.EventHandler(this.btnReplace_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.txtFind);
            this.groupBox2.Location = new System.Drawing.Point(12, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(344, 72);
            this.groupBox2.TabIndex = 0;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Find";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.txtReplace);
            this.groupBox3.Location = new System.Drawing.Point(12, 90);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(344, 72);
            this.groupBox3.TabIndex = 1;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Replace";
            // 
            // txtReplace
            // 
            this.txtReplace.Location = new System.Drawing.Point(6, 28);
            this.txtReplace.Name = "txtReplace";
            this.txtReplace.Size = new System.Drawing.Size(332, 29);
            this.txtReplace.TabIndex = 0;
            // 
            // btnReplaceAll
            // 
            this.btnReplaceAll.Location = new System.Drawing.Point(369, 112);
            this.btnReplaceAll.Name = "btnReplaceAll";
            this.btnReplaceAll.Size = new System.Drawing.Size(140, 39);
            this.btnReplaceAll.TabIndex = 5;
            this.btnReplaceAll.Text = "Replace &All";
            this.btnReplaceAll.UseVisualStyleBackColor = true;
            this.btnReplaceAll.Click += new System.EventHandler(this.btnReplaceAll_Click);
            // 
            // btnFind
            // 
            this.btnFind.Location = new System.Drawing.Point(369, 22);
            this.btnFind.Name = "btnFind";
            this.btnFind.Size = new System.Drawing.Size(140, 39);
            this.btnFind.TabIndex = 3;
            this.btnFind.Text = "&Find Next";
            this.btnFind.UseVisualStyleBackColor = true;
            this.btnFind.Click += new System.EventHandler(this.btnFind_Click);
            // 
            // ReplaceDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(521, 256);
            this.Controls.Add(this.btnReplaceAll);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.btnReplace);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnFind);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "ReplaceDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Find & Replace";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox chkMatchCase;
        private System.Windows.Forms.Button btnReplace;
        public System.Windows.Forms.TextBox txtFind;
        private System.Windows.Forms.CheckBox chkWholeWord;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        public System.Windows.Forms.TextBox txtReplace;
        private System.Windows.Forms.Button btnReplaceAll;
        private System.Windows.Forms.Button btnFind;
    }
}