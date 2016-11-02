﻿namespace ShapeMaker
{
    partial class Shortcuts
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
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.rt3 = new System.Windows.Forms.RichTextBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.rt1 = new System.Windows.Forms.RichTextBox();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.rt2 = new System.Windows.Forms.RichTextBox();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Location = new System.Drawing.Point(0, 3);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(362, 401);
            this.tabControl1.TabIndex = 4;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.rt3);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(354, 375);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Keyboard Shortcuts";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // rt3
            // 
            this.rt3.BackColor = System.Drawing.SystemColors.Window;
            this.rt3.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rt3.Location = new System.Drawing.Point(8, 6);
            this.rt3.Margin = new System.Windows.Forms.Padding(0);
            this.rt3.Name = "rt3";
            this.rt3.ReadOnly = true;
            this.rt3.ShortcutsEnabled = false;
            this.rt3.Size = new System.Drawing.Size(339, 363);
            this.rt3.TabIndex = 7;
            this.rt3.Text = "";
            this.rt3.WordWrap = false;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.rt1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(354, 375);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Mouse & Keyboard";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // rt1
            // 
            this.rt1.BackColor = System.Drawing.Color.White;
            this.rt1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rt1.Location = new System.Drawing.Point(8, 6);
            this.rt1.Margin = new System.Windows.Forms.Padding(0);
            this.rt1.Name = "rt1";
            this.rt1.ReadOnly = true;
            this.rt1.ShortcutsEnabled = false;
            this.rt1.Size = new System.Drawing.Size(339, 363);
            this.rt1.TabIndex = 6;
            this.rt1.Text = "";
            this.rt1.WordWrap = false;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.rt2);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(354, 375);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Misc.";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // rt2
            // 
            this.rt2.BackColor = System.Drawing.Color.White;
            this.rt2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rt2.Location = new System.Drawing.Point(8, 6);
            this.rt2.Name = "rt2";
            this.rt2.ReadOnly = true;
            this.rt2.ShortcutsEnabled = false;
            this.rt2.Size = new System.Drawing.Size(339, 363);
            this.rt2.TabIndex = 7;
            this.rt2.Text = "";
            this.rt2.WordWrap = false;
            // 
            // Shortcuts
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.ClientSize = new System.Drawing.Size(363, 407);
            this.Controls.Add(this.tabControl1);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Shortcuts";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Shortcuts";
            this.Load += new System.EventHandler(this.Shortcuts_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.RichTextBox rt1;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.RichTextBox rt2;
        private System.Windows.Forms.RichTextBox rt3;
    }
}