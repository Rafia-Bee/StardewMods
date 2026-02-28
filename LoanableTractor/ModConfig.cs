namespace LoanableTractor
{
    /// <summary>Mod configuration options, editable via config.json or Generic Mod Config Menu.</summary>
    internal class ModConfig
    {
        /// <summary>Gold cost per day to loan the tractor.</summary>
        public int LoanCostPerDay { get; set; } = 500;

        /// <summary>Maximum number of days the tractor can be loaned.</summary>
        public int MaxLoanDays { get; set; } = 1;

        /// <summary>If true, the full duration fee is charged when loaning. If false, charged daily.</summary>
        public bool ChargeUpfront { get; set; } = true;

        /// <summary>If true, allow loaning even when the player already owns a tractor garage.</summary>
        public bool AllowLoanWithGarage { get; set; } = false;

        /// <summary>If true, a penalty is applied if the tractor is returned after 2:00 AM.</summary>
        public bool EnableLateReturnPenalty { get; set; } = true;

        /// <summary>Bonus gold charged for late return (passing out with tractor).</summary>
        public int LateReturnPenalty { get; set; } = 250;

        /// <summary>If true, the loaned tractor has a 10% chance each day to be slower than the usual tractor.</summary>
        public bool EnableSpeedReduction { get; set; } = false;

        /// <summary>If true, weekend rentals (Saturday/Sunday) have a surcharge applied.</summary>
        public bool EnableWeekendSurcharge { get; set; } = true;

        /// <summary>Percentage surcharge applied on weekends.</summary>
        public int WeekendSurchargePercent { get; set; } = 25;
    }
}
