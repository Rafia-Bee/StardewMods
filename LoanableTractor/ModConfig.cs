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

        /// <summary>If true, loaned tractors have a chance each day of having a broken feature that costs stamina to fix.</summary>
        public bool EnableBreakdownChance { get; set; } = true;

        /// <summary>Percentage chance each loan that a tractor feature will be broken.</summary>
        public int BreakdownChancePercent { get; set; } = 10;

        /// <summary>Percentage of current stamina consumed when fixing a broken tractor feature.</summary>
        public int BreakdownStaminaCostPercent { get; set; } = 50;

        /// <summary>If true, weekend rentals (Saturday/Sunday) have a surcharge applied.</summary>
        public bool EnableWeekendSurcharge { get; set; } = true;

        /// <summary>Percentage surcharge applied on weekends.</summary>
        public int WeekendSurchargePercent { get; set; } = 25;
    }
}
