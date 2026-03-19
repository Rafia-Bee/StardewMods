using System;
using LoanableTractor.Interfaces;
using StardewModdingAPI;

namespace LoanableTractor.Framework
{
    /// <summary>Registers configuration options with Generic Mod Config Menu.</summary>
    internal class ConfigMenuHelper
    {
        /*********
         ** Fields
         *********/
        private readonly IMonitor Monitor;
        private readonly IModHelper Helper;
        private readonly IManifest Manifest;

        /*********
         ** Public Methods
         *********/
        public ConfigMenuHelper(IMonitor monitor, IModHelper helper, IManifest manifest)
        {
            this.Monitor = monitor;
            this.Helper = helper;
            this.Manifest = manifest;
        }

        /// <summary>Register all config options with GMCM if available.</summary>
        public void Register(ModConfig config)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
            {
                this.Monitor.Log("GMCM not found — config UI not registered.", LogLevel.Trace);
                return;
            }

            try
            {
                gmcm.Register(
                    mod: this.Manifest,
                    reset: () =>
                    {
                        var fresh = new ModConfig();
                        config.LoanTractorKeybind = fresh.LoanTractorKeybind;
                        config.EnableMailboxLoan = fresh.EnableMailboxLoan;
                        config.LoanCostPerDay = fresh.LoanCostPerDay;
                        config.MaxLoanDays = fresh.MaxLoanDays;
                        config.ChargeUpfront = fresh.ChargeUpfront;
                        config.AllowLoanWithGarage = fresh.AllowLoanWithGarage;
                        config.EnableLateReturnPenalty = fresh.EnableLateReturnPenalty;
                        config.LateReturnPenalty = fresh.LateReturnPenalty;
                        config.EnableSpeedReduction = fresh.EnableSpeedReduction;
                        config.EnableBreakdownChance = fresh.EnableBreakdownChance;
                        config.BreakdownChancePercent = fresh.BreakdownChancePercent;
                        config.BreakdownStaminaCostPercent = fresh.BreakdownStaminaCostPercent;
                        config.EnableWeekendSurcharge = fresh.EnableWeekendSurcharge;
                        config.WeekendSurchargePercent = fresh.WeekendSurchargePercent;
                    },
                    save: () => this.Helper.WriteConfig(config)
                );

                // --- General Settings ---
                gmcm.AddSectionTitle(
                    mod: this.Manifest,
                    text: () => "General Settings"
                );

                gmcm.AddKeybindList(
                    mod: this.Manifest,
                    getValue: () => config.LoanTractorKeybind,
                    setValue: value => config.LoanTractorKeybind = value,
                    name: () => this.Helper.Translation.Get("config.loan_tractor_keybind.name"),
                    tooltip: () => this.Helper.Translation.Get("config.loan_tractor_keybind.tooltip")
                );

                gmcm.AddBoolOption(
                    mod: this.Manifest,
                    getValue: () => config.EnableMailboxLoan,
                    setValue: value => config.EnableMailboxLoan = value,
                    name: () => this.Helper.Translation.Get("config.enable_mailbox_loan.name"),
                    tooltip: () => this.Helper.Translation.Get("config.enable_mailbox_loan.tooltip")
                );

                // --- Loan Settings ---
                gmcm.AddSectionTitle(
                    mod: this.Manifest,
                    text: () => "Loan Settings"
                );

                gmcm.AddNumberOption(
                    mod: this.Manifest,
                    getValue: () => config.LoanCostPerDay,
                    setValue: value => config.LoanCostPerDay = value,
                    name: () => this.Helper.Translation.Get("config.loan_cost_per_day.name"),
                    tooltip: () => this.Helper.Translation.Get("config.loan_cost_per_day.tooltip"),
                    min: 100,
                    max: 50000,
                    interval: 50
                );

                gmcm.AddNumberOption(
                    mod: this.Manifest,
                    getValue: () => config.MaxLoanDays,
                    setValue: value => config.MaxLoanDays = value,
                    name: () => this.Helper.Translation.Get("config.max_loan_days.name"),
                    tooltip: () => this.Helper.Translation.Get("config.max_loan_days.tooltip"),
                    min: 1,
                    max: 28
                );

                gmcm.AddBoolOption(
                    mod: this.Manifest,
                    getValue: () => config.ChargeUpfront,
                    setValue: value => config.ChargeUpfront = value,
                    name: () => this.Helper.Translation.Get("config.charge_upfront.name"),
                    tooltip: () => this.Helper.Translation.Get("config.charge_upfront.tooltip")
                );

                // --- Behavior Settings ---
                gmcm.AddSectionTitle(
                    mod: this.Manifest,
                    text: () => "Behavior Settings"
                );

                gmcm.AddBoolOption(
                    mod: this.Manifest,
                    getValue: () => config.AllowLoanWithGarage,
                    setValue: value => config.AllowLoanWithGarage = value,
                    name: () => this.Helper.Translation.Get("config.allow_loan_with_garage.name"),
                    tooltip: () => this.Helper.Translation.Get("config.allow_loan_with_garage.tooltip")
                );

                // --- Penalty Settings ---
                gmcm.AddSectionTitle(
                    mod: this.Manifest,
                    text: () => "Penalty Settings"
                );

                gmcm.AddBoolOption(
                    mod: this.Manifest,
                    getValue: () => config.EnableLateReturnPenalty,
                    setValue: value => config.EnableLateReturnPenalty = value,
                    name: () => this.Helper.Translation.Get("config.enable_late_return_penalty.name"),
                    tooltip: () => this.Helper.Translation.Get("config.enable_late_return_penalty.tooltip")
                );

                gmcm.AddNumberOption(
                    mod: this.Manifest,
                    getValue: () => config.LateReturnPenalty,
                    setValue: value => config.LateReturnPenalty = value,
                    name: () => this.Helper.Translation.Get("config.late_return_penalty.name"),
                    tooltip: () => this.Helper.Translation.Get("config.late_return_penalty.tooltip"),
                    min: 0,
                    max: 10000,
                    interval: 50
                );

                gmcm.AddBoolOption(
                    mod: this.Manifest,
                    getValue: () => config.EnableSpeedReduction,
                    setValue: value => config.EnableSpeedReduction = value,
                    name: () => this.Helper.Translation.Get("config.enable_speed_reduction.name"),
                    tooltip: () => this.Helper.Translation.Get("config.enable_speed_reduction.tooltip")
                );

                gmcm.AddBoolOption(
                    mod: this.Manifest,
                    getValue: () => config.EnableBreakdownChance,
                    setValue: value => config.EnableBreakdownChance = value,
                    name: () => this.Helper.Translation.Get("config.enable_breakdown_chance.name"),
                    tooltip: () => this.Helper.Translation.Get("config.enable_breakdown_chance.tooltip")
                );

                gmcm.AddNumberOption(
                    mod: this.Manifest,
                    getValue: () => config.BreakdownChancePercent,
                    setValue: value => config.BreakdownChancePercent = value,
                    name: () => this.Helper.Translation.Get("config.breakdown_chance_percent.name"),
                    tooltip: () => this.Helper.Translation.Get("config.breakdown_chance_percent.tooltip"),
                    min: 1,
                    max: 100,
                    interval: 1
                );

                gmcm.AddNumberOption(
                    mod: this.Manifest,
                    getValue: () => config.BreakdownStaminaCostPercent,
                    setValue: value => config.BreakdownStaminaCostPercent = value,
                    name: () => this.Helper.Translation.Get("config.breakdown_stamina_cost_percent.name"),
                    tooltip: () => this.Helper.Translation.Get("config.breakdown_stamina_cost_percent.tooltip"),
                    min: 1,
                    max: 100,
                    interval: 5
                );

                // --- Pricing Settings ---
                gmcm.AddSectionTitle(
                    mod: this.Manifest,
                    text: () => "Pricing Settings"
                );

                gmcm.AddBoolOption(
                    mod: this.Manifest,
                    getValue: () => config.EnableWeekendSurcharge,
                    setValue: value => config.EnableWeekendSurcharge = value,
                    name: () => this.Helper.Translation.Get("config.enable_weekend_surcharge.name"),
                    tooltip: () => this.Helper.Translation.Get("config.enable_weekend_surcharge.tooltip")
                );

                gmcm.AddNumberOption(
                    mod: this.Manifest,
                    getValue: () => config.WeekendSurchargePercent,
                    setValue: value => config.WeekendSurchargePercent = value,
                    name: () => this.Helper.Translation.Get("config.weekend_surcharge_percent.name"),
                    tooltip: () => this.Helper.Translation.Get("config.weekend_surcharge_percent.tooltip"),
                    min: 0,
                    max: 100,
                    interval: 5
                );

                this.Monitor.Log("GMCM config registered.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error registering GMCM config: {ex}", LogLevel.Error);
            }
        }
    }
}
