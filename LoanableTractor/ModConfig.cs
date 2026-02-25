namespace LoanableTractor
{
    /// <summary>Mod configuration options, editable via config.json or Generic Mod Config Menu.</summary>
    internal class ModConfig
    {
        /// <summary>Gold cost per day to loan the tractor.</summary>
        public int LoanCostPerDay { get; set; } = 500;

        /// <summary>Maximum number of days the tractor can be loaned.</summary>
        public int MaxLoanDays { get; set; } = 1;

        /// <summary>Default number of days selected when loaning.</summary>
        public int DefaultLoanDays { get; set; } = 1;

        /// <summary>If true, the full duration fee is charged when loaning. If false, charged daily.</summary>
        public bool ChargeUpfront { get; set; } = true;

        /// <summary>If true, allow loaning even when the player already owns a tractor garage.</summary>
        public bool AllowLoanWithGarage { get; set; } = false;

        /// <summary>If true, the Joja mail reappears daily. If false, only sent once.</summary>
        public bool ShowMailDaily { get; set; } = true;

        /// <summary>If true, the player has opted out of the Joja tractor mail entirely.</summary>
        public bool DismissServicePermanently { get; set; } = false;

        /// <summary>If true, the mail only appears if the player has enough gold to pay the fee.</summary>
        public bool RequireMinimumGold { get; set; } = true;

        /// <summary>If true, a penalty is applied if the tractor is returned after 2:00 AM.</summary>
        public bool EnableLateReturnPenalty { get; set; } = true;

        /// <summary>Bonus gold charged for late return (passing out with tractor).</summary>
        public int LateReturnPenalty { get; set; } = 250;

        /// <summary>If true, the loaned tractor moves slightly slower than an owned tractor.</summary>
        public bool EnableSpeedReduction { get; set; } = false;
    }
}
