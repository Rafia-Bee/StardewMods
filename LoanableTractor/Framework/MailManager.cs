using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace LoanableTractor.Framework
{
    /// <summary>
    /// Handles mail delivery using SMAPI's native mail system.
    /// Injects custom mail content into Data/mail and delivers to the player's mailbox.
    /// Switches to Pierre's flavor text after the Community Center is completed.
    /// </summary>
    internal class MailManager
    {
        /*********
         ** Constants
         *********/
        /// <summary>The mail ID for the introductory Joja loan service letter.</summary>
        public const string IntroMailId = "RafiaBee.LoanableTractor_Intro";

        /*********
         ** Fields
         *********/
        private readonly IMonitor Monitor;
        private readonly IModHelper Helper;
        private readonly ModConfig Config;
        private readonly TractorLoanManager LoanManager;

        /*********
         ** Public Methods
         *********/
        public MailManager(IMonitor monitor, IModHelper helper, ModConfig config, TractorLoanManager loanManager)
        {
            this.Monitor = monitor;
            this.Helper = helper;
            this.Config = config;
            this.LoanManager = loanManager;
        }

        /// <summary>Register the asset editor to inject our mail content into Data/mail.</summary>
        public void RegisterAssetEditor()
        {
            this.Helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

        /// <summary>Check conditions and deliver the introductory mail at the start of each day.</summary>
        public void TryDeliverIntroMail()
        {
            try
            {
                if (Game1.player.mailReceived.Contains(IntroMailId))
                    return;

                if (Game1.player.mailbox.Contains(IntroMailId))
                    return;

                if (!this.Config.AllowLoanWithGarage && this.LoanManager.PlayerHasGarage())
                    return;

                this.Helper.GameContent.InvalidateCache("Data/mail");
                Game1.player.mailbox.Add(IntroMailId);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error delivering intro mail: {ex}", LogLevel.Error);
            }
        }

        /*********
         ** Private Methods
         *********/

        /// <summary>Inject our custom mail content into the Data/mail asset.</summary>
        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo("Data/mail"))
                return;

            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>();
                bool ccComplete = TractorLoanManager.IsCommunityCenterComplete();
                string mailKey = ccComplete ? "mail.pierre.intro" : "mail.joja.intro";
                string mailText = this.Helper.Translation.Get(mailKey);
                data.Data[IntroMailId] = mailText;
            });
        }
    }
}
