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
                this.Monitor.Log("GMCM not found — config UI not registered.", LogLevel.Debug);
                return;
            }

            try
            {
                gmcm.Register(
                    mod: this.Manifest,
                    reset: () =>
                    {
                        var fresh = new ModConfig();
                        config.LoanCostPerDay = fresh.LoanCostPerDay;
                        config.MaxLoanDays = fresh.MaxLoanDays;
                        config.DefaultLoanDays = fresh.DefaultLoanDays;
                        config.ChargeUpfront = fresh.ChargeUpfront;
                        config.AllowLoanWithGarage = fresh.AllowLoanWithGarage;
                        config.ShowMailDaily = fresh.ShowMailDaily;
                        config.DismissServicePermanently = fresh.DismissServicePermanently;
                        config.RequireMinimumGold = fresh.RequireMinimumGold;
                        config.EnableLateReturnPenalty = fresh.EnableLateReturnPenalty;
                        config.LateReturnPenalty = fresh.LateReturnPenalty;
                        config.EnableSpeedReduction = fresh.EnableSpeedReduction;
                    },
                    save: () => this.Helper.WriteConfig(config)
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
                    min: 0,
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

                gmcm.AddNumberOption(
                    mod: this.Manifest,
                    getValue: () => config.DefaultLoanDays,
                    setValue: value => config.DefaultLoanDays = value,
                    name: () => this.Helper.Translation.Get("config.default_loan_days.name"),
                    tooltip: () => this.Helper.Translation.Get("config.default_loan_days.tooltip"),
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

                gmcm.AddBoolOption(
                    mod: this.Manifest,
                    getValue: () => config.ShowMailDaily,
                    setValue: value => config.ShowMailDaily = value,
                    name: () => this.Helper.Translation.Get("config.show_mail_daily.name"),
                    tooltip: () => this.Helper.Translation.Get("config.show_mail_daily.tooltip")
                );

                gmcm.AddBoolOption(
                    mod: this.Manifest,
                    getValue: () => config.DismissServicePermanently,
                    setValue: value => config.DismissServicePermanently = value,
                    name: () => this.Helper.Translation.Get("config.dismiss_service.name"),
                    tooltip: () => this.Helper.Translation.Get("config.dismiss_service.tooltip")
                );

                gmcm.AddBoolOption(
                    mod: this.Manifest,
                    getValue: () => config.RequireMinimumGold,
                    setValue: value => config.RequireMinimumGold = value,
                    name: () => this.Helper.Translation.Get("config.require_minimum_gold.name"),
                    tooltip: () => this.Helper.Translation.Get("config.require_minimum_gold.tooltip")
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

                this.Monitor.Log("GMCM config registered.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error registering GMCM config: {ex}", LogLevel.Error);
            }
        }
    }
}
