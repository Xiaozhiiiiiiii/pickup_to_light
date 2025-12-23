namespace SmartCap.Bridge
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.txtHubIp = new System.Windows.Forms.TextBox();
            this.txtOrderId = new System.Windows.Forms.TextBox();
            this.numCount = new System.Windows.Forms.NumericUpDown();
            this.numIntervalMs = new System.Windows.Forms.NumericUpDown();
            this.numTimeoutMs = new System.Windows.Forms.NumericUpDown();
            this.lblHubIp = new System.Windows.Forms.Label();
            this.lblOrderId = new System.Windows.Forms.Label();
            this.lblCount = new System.Windows.Forms.Label();
            this.lblInterval = new System.Windows.Forms.Label();
            this.lblTimeout = new System.Windows.Forms.Label();
            this.btnRun = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnExportCsv = new System.Windows.Forms.Button();
            this.lblKpi = new System.Windows.Forms.Label();
            this.lstLog = new System.Windows.Forms.ListBox();
            this.lblScenario = new System.Windows.Forms.Label();
            this.lblCriteria = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numIntervalMs)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTimeoutMs)).BeginInit();
            this.SuspendLayout();
            // 
            // lblScenario
            // 
            this.lblScenario.AutoSize = true;
            this.lblScenario.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblScenario.Location = new System.Drawing.Point(16, 9);
            this.lblScenario.Name = "lblScenario";
            this.lblScenario.Size = new System.Drawing.Size(77, 19);
            this.lblScenario.TabIndex = 100;
            this.lblScenario.Text = "Scenario";
            // 
            // lblCriteria
            // 
            this.lblCriteria.AutoSize = true;
            this.lblCriteria.Location = new System.Drawing.Point(16, 32);
            this.lblCriteria.Name = "lblCriteria";
            this.lblCriteria.Size = new System.Drawing.Size(47, 15);
            this.lblCriteria.TabIndex = 101;
            this.lblCriteria.Text = "Criteria";
            // 
            // txtHubIp
            // 
            this.txtHubIp.Location = new System.Drawing.Point(92, 58);
            this.txtHubIp.Name = "txtHubIp";
            this.txtHubIp.Size = new System.Drawing.Size(200, 23);
            this.txtHubIp.TabIndex = 0;
            this.txtHubIp.Text = "10.0.60.96";
            // 
            // txtOrderId
            // 
            this.txtOrderId.Location = new System.Drawing.Point(92, 90);
            this.txtOrderId.Name = "txtOrderId";
            this.txtOrderId.Size = new System.Drawing.Size(200, 23);
            this.txtOrderId.TabIndex = 1;
            this.txtOrderId.Text = "L1-011";
            // 
            // numCount
            // 
            this.numCount.Location = new System.Drawing.Point(388, 58);
            this.numCount.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            this.numCount.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numCount.Name = "numCount";
            this.numCount.Size = new System.Drawing.Size(90, 23);
            this.numCount.TabIndex = 2;
            this.numCount.Value = new decimal(new int[] { 200, 0, 0, 0 });
            // 
            // numIntervalMs
            // 
            this.numIntervalMs.Location = new System.Drawing.Point(388, 90);
            this.numIntervalMs.Maximum = new decimal(new int[] { 600000, 0, 0, 0 });
            this.numIntervalMs.Name = "numIntervalMs";
            this.numIntervalMs.Size = new System.Drawing.Size(90, 23);
            this.numIntervalMs.TabIndex = 3;
            this.numIntervalMs.Value = new decimal(new int[] { 100, 0, 0, 0 });
            // 
            // numTimeoutMs
            // 
            this.numTimeoutMs.Location = new System.Drawing.Point(388, 122);
            this.numTimeoutMs.Maximum = new decimal(new int[] { 600000, 0, 0, 0 });
            this.numTimeoutMs.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numTimeoutMs.Name = "numTimeoutMs";
            this.numTimeoutMs.Size = new System.Drawing.Size(90, 23);
            this.numTimeoutMs.TabIndex = 4;
            this.numTimeoutMs.Value = new decimal(new int[] { 5000, 0, 0, 0 });
            // 
            // lblHubIp
            // 
            this.lblHubIp.AutoSize = true;
            this.lblHubIp.Location = new System.Drawing.Point(16, 61);
            this.lblHubIp.Name = "lblHubIp";
            this.lblHubIp.Size = new System.Drawing.Size(45, 15);
            this.lblHubIp.TabIndex = 10;
            this.lblHubIp.Text = "Hub IP";
            // 
            // lblOrderId
            // 
            this.lblOrderId.AutoSize = true;
            this.lblOrderId.Location = new System.Drawing.Point(16, 93);
            this.lblOrderId.Name = "lblOrderId";
            this.lblOrderId.Size = new System.Drawing.Size(50, 15);
            this.lblOrderId.TabIndex = 11;
            this.lblOrderId.Text = "OrderId";
            // 
            // lblCount
            // 
            this.lblCount.AutoSize = true;
            this.lblCount.Location = new System.Drawing.Point(312, 61);
            this.lblCount.Name = "lblCount";
            this.lblCount.Size = new System.Drawing.Size(41, 15);
            this.lblCount.TabIndex = 12;
            this.lblCount.Text = "Count";
            // 
            // lblInterval
            // 
            this.lblInterval.AutoSize = true;
            this.lblInterval.Location = new System.Drawing.Point(312, 93);
            this.lblInterval.Name = "lblInterval";
            this.lblInterval.Size = new System.Drawing.Size(67, 15);
            this.lblInterval.TabIndex = 13;
            this.lblInterval.Text = "Interval ms";
            // 
            // lblTimeout
            // 
            this.lblTimeout.AutoSize = true;
            this.lblTimeout.Location = new System.Drawing.Point(312, 125);
            this.lblTimeout.Name = "lblTimeout";
            this.lblTimeout.Size = new System.Drawing.Size(75, 15);
            this.lblTimeout.TabIndex = 14;
            this.lblTimeout.Text = "Timeout ms";
            // 
            // btnRun
            // 
            this.btnRun.Location = new System.Drawing.Point(500, 58);
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(120, 27);
            this.btnRun.TabIndex = 5;
            this.btnRun.Text = "Start Stress";
            this.btnRun.UseVisualStyleBackColor = true;
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(500, 90);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(120, 27);
            this.btnStop.TabIndex = 6;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            // 
            // btnExportCsv
            // 
            this.btnExportCsv.Location = new System.Drawing.Point(500, 122);
            this.btnExportCsv.Name = "btnExportCsv";
            this.btnExportCsv.Size = new System.Drawing.Size(120, 27);
            this.btnExportCsv.TabIndex = 7;
            this.btnExportCsv.Text = "Export CSV";
            this.btnExportCsv.UseVisualStyleBackColor = true;
            // 
            // lblKpi
            // 
            this.lblKpi.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblKpi.Location = new System.Drawing.Point(16, 160);
            this.lblKpi.Name = "lblKpi";
            this.lblKpi.Size = new System.Drawing.Size(760, 18);
            this.lblKpi.TabIndex = 15;
            this.lblKpi.Text = "KPI: -";
            // 
            // lstLog
            // 
            this.lstLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstLog.FormattingEnabled = true;
            this.lstLog.ItemHeight = 15;
            this.lstLog.Location = new System.Drawing.Point(16, 188);
            this.lstLog.Name = "lstLog";
            this.lstLog.Size = new System.Drawing.Size(760, 259);
            this.lstLog.TabIndex = 8;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(792, 468);
            this.Controls.Add(this.lblCriteria);
            this.Controls.Add(this.lblScenario);
            this.Controls.Add(this.lstLog);
            this.Controls.Add(this.lblKpi);
            this.Controls.Add(this.btnExportCsv);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnRun);
            this.Controls.Add(this.lblTimeout);
            this.Controls.Add(this.lblInterval);
            this.Controls.Add(this.lblCount);
            this.Controls.Add(this.lblOrderId);
            this.Controls.Add(this.lblHubIp);
            this.Controls.Add(this.numTimeoutMs);
            this.Controls.Add(this.numIntervalMs);
            this.Controls.Add(this.numCount);
            this.Controls.Add(this.txtOrderId);
            this.Controls.Add(this.txtHubIp);
            this.Name = "Form1";
            this.Text = "LEDhub MQTT";
            ((System.ComponentModel.ISupportInitialize)(this.numCount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numIntervalMs)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTimeoutMs)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox txtHubIp;
        private System.Windows.Forms.TextBox txtOrderId;
        private System.Windows.Forms.NumericUpDown numCount;
        private System.Windows.Forms.NumericUpDown numIntervalMs;
        private System.Windows.Forms.NumericUpDown numTimeoutMs;
        private System.Windows.Forms.Label lblHubIp;
        private System.Windows.Forms.Label lblOrderId;
        private System.Windows.Forms.Label lblCount;
        private System.Windows.Forms.Label lblInterval;
        private System.Windows.Forms.Label lblTimeout;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnExportCsv;
        private System.Windows.Forms.Label lblKpi;
        private System.Windows.Forms.ListBox lstLog;

        private System.Windows.Forms.Label lblScenario;
        private System.Windows.Forms.Label lblCriteria;
    }
}
