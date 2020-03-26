namespace ShapeMaker
{
    partial class ColorWindow
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
            this.CancelButton = new System.Windows.Forms.Button();
            this.OkButton = new System.Windows.Forms.Button();
            this.pdnColor1 = new ShapeMaker.PdnColor();
            this.SuspendLayout();
            // 
            // CancelButton
            // 
            this.CancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.CancelButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.CancelButton.Location = new System.Drawing.Point(324, 322);
            this.CancelButton.Name = "CancelButton";
            this.CancelButton.Size = new System.Drawing.Size(75, 24);
            this.CancelButton.TabIndex = 2;
            this.CancelButton.Text = "Cancel";
            this.CancelButton.UseVisualStyleBackColor = true;
            // 
            // OkButton
            // 
            this.OkButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.OkButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.OkButton.Location = new System.Drawing.Point(243, 322);
            this.OkButton.Name = "OkButton";
            this.OkButton.Size = new System.Drawing.Size(75, 24);
            this.OkButton.TabIndex = 1;
            this.OkButton.Text = "OK";
            this.OkButton.UseVisualStyleBackColor = true;
            // 
            // pdnColor1
            // 
            this.pdnColor1.Color = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.pdnColor1.Location = new System.Drawing.Point(12, 12);
            this.pdnColor1.Name = "pdnColor1";
            this.pdnColor1.ShowAlpha = true;
            this.pdnColor1.Size = new System.Drawing.Size(385, 300);
            this.pdnColor1.TabIndex = 0;
            // 
            // ColorWindow
            // 
            this.AcceptButton = this.OkButton;
            this.ClientSize = new System.Drawing.Size(411, 358);
            this.Controls.Add(this.OkButton);
            this.Controls.Add(this.CancelButton);
            this.Controls.Add(this.pdnColor1);
            this.Name = "ColorWindow";
            this.Text = "Choose a Color";
            this.ResumeLayout(false);

        }

        #endregion

        private PdnColor pdnColor1;
        private new System.Windows.Forms.Button CancelButton;
        private System.Windows.Forms.Button OkButton;
    }
}

