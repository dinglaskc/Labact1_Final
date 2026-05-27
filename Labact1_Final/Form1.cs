using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Labact1_Final
{
    public partial class Form1 : Form
    {
        // Rates & Constants 
        private const decimal CAR_RATE        = 50m;
        private const decimal MOTORCYCLE_RATE = 30m;
        private const decimal VAN_RATE        = 70m;
        private const decimal SERVICE_CHARGE  = 20m;
        private const int     OVERTIME_LIMIT  = 8;
        private const decimal OVERTIME_MULT   = 1.5m;
        private const decimal SENIOR_DISC     = 0.20m;
        private const decimal EMPLOYEE_DISC   = 0.15m;

        // ── Internal data 
        private class ParkingRecord
        {
            public string  PlateNumber  { get; set; }
            public string  VehicleType  { get; set; }
            public decimal HoursParked  { get; set; }
            public string  AssignedSlot { get; set; }
            public string  Discount     { get; set; }
        }

        private struct FeeResult
        {
            public decimal BaseRate, StandardFee, OvertimeFee;
            public decimal OvertimeHours, ServiceCharge, DiscountAmount, Total;
        }

       
        private Dictionary<string, string> slotOccupancy = new Dictionary<string, string>();

        private Dictionary<string, Button> slotButtons = new Dictionary<string, Button>();

        private ParkingRecord currentRecord = null;

        //  CONSTRUCTOR
        public Form1()
        {
            InitializeComponent();
            SetupComboBoxes();
            SetupSlotButtons();
            WireButtonEvents();
        }

        //  SETUP HELPERS

        private void SetupComboBoxes()
        {
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(new object[] { "Car", "Motorcycle", "Van" });
            comboBox1.SelectedIndex = -1;

            comboBox2.Items.Clear();
            comboBox2.Items.AddRange(new object[] { "None", "Senior Citizen (20%)", "Employee (15%)" });
            comboBox2.SelectedIndex = 0;
        }

        private void SetupSlotButtons()
        {

            Button[][] columns = new Button[][]
            {
                new Button[] { button6,  button7,  button8,  button11, button10, button9,  button12 },
                new Button[] { button19, button18, button17, button16, button15, button14, button13 },
                new Button[] { button26, button25, button24, button23, button22, button21, button20 },
                new Button[] { button33, button32, button31, button30, button29, button28, button27 },
                new Button[] { button40, button39, button38, button37, button36, button35, button34 },
            };

            char[] rows = { 'A', 'B', 'C', 'D', 'E', 'F', 'G' };

            for (int col = 0; col < 5; col++)
            {
                for (int row = 0; row < 7; row++)
                {
                    string slotId = rows[row].ToString() + (col + 1);
                    Button btn = columns[col][row];
                    btn.Tag = slotId;
                    btn.BackColor = Color.LawnGreen;
                    btn.Click += SlotButton_Click;
                    slotButtons[slotId] = btn;
                }
            }
        }

        private void WireButtonEvents()
        {
            button1.Click += Button1_RegisterVehicle;
            button2.Click += Button2_UpdateStatus;
            button3.Click += Button3_ProcessPayment;
            button4.Click += Button4_GenerateReceipt;
            button5.Click += Button5_ClearForm;
        }
       
        //  SLOT BUTTON CLICK

        private void SlotButton_Click(object sender, EventArgs e)
        {
            string slotId = ((Button)sender).Tag.ToString();

            if (slotOccupancy.ContainsKey(slotId))
            {
                MessageBox.Show($"Slot {slotId} is occupied by {slotOccupancy[slotId]}.",
                                "Slot Occupied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            textBox3.Text = slotId;
        }

        //  BUTTON 1 — Register Vehicle

        private void Button1_RegisterVehicle(object sender, EventArgs e)
        {
            string plate = textBox1.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(plate))
            { ShowError("Please enter a plate number."); return; }

            if (comboBox1.SelectedIndex < 0)
            { ShowError("Please select a vehicle type."); return; }

            if (!decimal.TryParse(textBox2.Text.Trim(), out decimal hours) || hours <= 0)
            { ShowError("Please enter a valid number of hours."); return; }

            string slot = textBox3.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(slot))
            { ShowError("Please click a parking slot from the grid first."); return; }

            if (slotOccupancy.ContainsKey(slot))
            { ShowError($"Slot {slot} is already occupied. Choose another."); return; }

            currentRecord = new ParkingRecord
            {
                PlateNumber  = plate,
                VehicleType  = comboBox1.SelectedItem.ToString(),
                HoursParked  = hours,
                AssignedSlot = slot,
                Discount     = comboBox2.SelectedItem != null ? comboBox2.SelectedItem.ToString() : "None"
            };

            slotOccupancy[slot] = plate;
            SetSlotColor(slot, Color.Red);
            RefreshTransactionPanel(currentRecord);

            MessageBox.Show($"Vehicle {plate} registered in slot {slot}.",
                            "Registered", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        //  BUTTON 2 — Update Status

        private void Button2_UpdateStatus(object sender, EventArgs e)
        {
            if (currentRecord == null)
            { ShowError("No active vehicle registered."); return; }

            if (decimal.TryParse(textBox2.Text.Trim(), out decimal h) && h > 0)
                currentRecord.HoursParked = h;

            currentRecord.Discount = comboBox2.SelectedItem != null ? comboBox2.SelectedItem.ToString() : "None";
            RefreshTransactionPanel(currentRecord);

            MessageBox.Show("Status updated.", "Updated",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        //  BUTTON 3 — Process Payment

        private void Button3_ProcessPayment(object sender, EventArgs e)
        {
            if (currentRecord == null)
            { ShowError("No active vehicle to process payment for."); return; }

            var fees = CalculateFees(currentRecord);
            textBox4.Text = fees.Total.ToString("F2");

            slotOccupancy.Remove(currentRecord.AssignedSlot);
            SetSlotColor(currentRecord.AssignedSlot, Color.LawnGreen);

            MessageBox.Show(
                $"Payment processed!\n\n" +
                $"Plate   : {currentRecord.PlateNumber}\n" +
                $"Vehicle : {currentRecord.VehicleType}\n" +
                $"Slot    : {currentRecord.AssignedSlot}\n" +
                $"Hours   : {currentRecord.HoursParked}\n" +
                $"Total   : \u20b1{fees.Total:F2}",
                "Payment Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        //  BUTTON 4 — Generate Receipt

        private void Button4_GenerateReceipt(object sender, EventArgs e)
        {
            if (currentRecord == null)
            { ShowError("No transaction to generate a receipt for."); return; }

            var fees = CalculateFees(currentRecord);
            string receipt = BuildReceipt(currentRecord, fees);

          
            listView1.Clear();
            listView1.View = View.Details;
            listView1.Columns.Add("Parking Receipt", listView1.Width - 4, HorizontalAlignment.Left);
            listView1.Font = new Font("Courier New", 7f);

            foreach (string line in receipt.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                listView1.Items.Add(new ListViewItem(line));
        }

        //  BUTTON 5 — Clear Form

        private void Button5_ClearForm(object sender, EventArgs e)
        {
            textBox1.Clear();           // Plate Number
            textBox2.Clear();           // Hours Parked
            textBox3.Clear();           // Assigned Slot
            textBox4.Clear();           // Pay Amount
            comboBox1.SelectedIndex = -1;
            comboBox2.SelectedIndex = 0;
            listView1.Clear();

            label6.Text  = "\u2014";   // Plate Number value
            label7.Text  = "\u2014";   // Vehicle Info value
            label9.Text  = "\u2014";   // Duration value
            label11.Text = "\u2014";   // Overtime Fee value

            label19.Text = "\u2014";   // Standard Fee value
            label17.Text = "\u2014";   // Service Charge value
            label15.Text = "\u2014";   // Total value

            currentRecord = null;
        }

     

        private void groupBox5_Enter(object sender, EventArgs e) { }
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e) { }


        //  BUSINESS LOGIC

        private FeeResult CalculateFees(ParkingRecord rec)
        {
            decimal rate;
            if (rec.VehicleType == "Motorcycle")
                rate = MOTORCYCLE_RATE;
            else if (rec.VehicleType == "Van")
                rate = VAN_RATE;
            else
                rate = CAR_RATE;

            decimal normalHrs = Math.Min(rec.HoursParked, OVERTIME_LIMIT);
            decimal otHrs     = Math.Max(0, rec.HoursParked - OVERTIME_LIMIT);
            decimal stdFee    = normalHrs * rate;
            decimal otFee     = otHrs * rate * OVERTIME_MULT;
            decimal subtotal  = stdFee + otFee + SERVICE_CHARGE;

            decimal discRate;
            if (rec.Discount.StartsWith("Senior"))
                discRate = SENIOR_DISC;
            else if (rec.Discount.StartsWith("Employee"))
                discRate = EMPLOYEE_DISC;
            else
                discRate = 0m;

            decimal discAmt   = Math.Round((stdFee + otFee) * discRate, 2);
            decimal total     = subtotal - discAmt;

            return new FeeResult
            {
                BaseRate       = rate,
                StandardFee    = stdFee,
                OvertimeFee    = otFee,
                OvertimeHours  = otHrs,
                ServiceCharge  = SERVICE_CHARGE,
                DiscountAmount = discAmt,
                Total          = total
            };
        }

        private void RefreshTransactionPanel(ParkingRecord rec)
        {
            var f = CalculateFees(rec);

            label6.Text  = rec.PlateNumber;
            label7.Text  = rec.VehicleType;
            label9.Text  = rec.HoursParked + " hr/s";
            label11.Text = f.OvertimeFee > 0
                           ? $"\u20b1{f.OvertimeFee:F2} ({f.OvertimeHours}h)"
                           : "None";

            label19.Text = $"\u20b1{f.StandardFee:F2}";
            label17.Text = $"\u20b1{f.ServiceCharge:F2}";
            label15.Text = $"\u20b1{f.Total:F2}";

            textBox4.Text = f.Total.ToString("F2");
        }

        private string BuildReceipt(ParkingRecord rec, FeeResult f)
        {
            string line = new string('-', 34);
            string disc;
            if (rec.Discount.StartsWith("Senior"))
                disc = "Senior Citizen (20%)";
            else if (rec.Discount.StartsWith("Employee"))
                disc = "Employee (15%)";
            else
                disc = "None";

            return
                " SMART PARKING MANAGEMENT SYSTEM\r\n" +
                "      OFFICIAL PARKING RECEIPT\r\n" +
                line + "\r\n" +
                $" Date    : {DateTime.Now:MM/dd/yyyy hh:mm tt}\r\n" +
                $" Receipt : {new Random().Next(10000, 99999)}\r\n" +
                line + "\r\n" +
                " VEHICLE INFORMATION\r\n" +
                $" Plate   : {rec.PlateNumber}\r\n" +
                $" Type    : {rec.VehicleType}\r\n" +
                $" Slot    : {rec.AssignedSlot}\r\n" +
                $" Hours   : {rec.HoursParked} hr/s\r\n" +
                (f.OvertimeHours > 0 ? $" OT Hrs  : {f.OvertimeHours} hr/s\r\n" : "") +
                line + "\r\n" +
                " FEE BREAKDOWN\r\n" +
                $" Rate    : P{f.BaseRate:F2}/hr\r\n" +
                $" Parking : P{f.StandardFee:F2}\r\n" +
                (f.OvertimeFee > 0 ? $" Overtime: P{f.OvertimeFee:F2}\r\n" : "") +
                $" Svc Chg : P{f.ServiceCharge:F2}\r\n" +
                (f.DiscountAmount > 0 ? $" Discount: -P{f.DiscountAmount:F2} ({disc})\r\n" : "") +
                line + "\r\n" +
                $" TOTAL   : P{f.Total:F2}\r\n" +
                line + "\r\n" +
                " Thank you! Please drive safely.\r\n" +
                line;
        }

        private void SetSlotColor(string slotId, Color color)
        {
            if (slotButtons.TryGetValue(slotId, out Button btn))
                btn.BackColor = color;
        }

        private void ShowError(string msg) =>
            MessageBox.Show(msg, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
