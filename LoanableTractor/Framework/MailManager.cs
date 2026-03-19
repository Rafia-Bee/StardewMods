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

        /// <summary>The mail ID for Pierre's takeover letter after Community Center completion.</summary>
        public const string PierreTransitionMailId = "RafiaBee.LoanableTractor_PierreTransition";

        /// <summary>The mail ID for the late return penalty notice.</summary>
        public const string LateReturnMailId = "RafiaBee.LoanableTractor_LateReturn";

        /*********
         ** Fields
         *********/
        private readonly IMonitor Monitor;
        private readonly IModHelper Helper;
        private readonly ModConfig Config;
        private readonly TractorLoanManager LoanManager;

        /// <summary>The penalty amount to embed in the late return mail, or 0 if none pending.</summary>
        private int PendingLateReturnPenalty;

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

        /// <summary>Queue a late return penalty mail for delivery the next morning.</summary>
        public void QueueLateReturnMail(int penalty)
        {
            this.PendingLateReturnPenalty = penalty;
            Game1.player.mailForTomorrow.Add(LateReturnMailId);
            this.Helper.GameContent.InvalidateCache("Data/mail");
        }

        /// <summary>Check conditions and deliver the introductory mail at the start of each day.</summary>
        public void TryDeliverIntroMail()
        {
            try
            {
                if (!this.Config.EnableMailboxLoan)
                    return;

                if (Game1.player.mailReceived.Contains(IntroMailId))
                {
                    this.TryDeliverPierreTransitionMail();
                    return;
                }

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

        /// <summary>Send Pierre's takeover letter if the Community Center was just completed.</summary>
        private void TryDeliverPierreTransitionMail()
        {
            if (!TractorLoanManager.IsCommunityCenterComplete())
                return;

            if (Game1.player.mailReceived.Contains(PierreTransitionMailId))
                return;

            if (Game1.player.mailbox.Contains(PierreTransitionMailId))
                return;

            this.Helper.GameContent.InvalidateCache("Data/mail");
            Game1.player.mailbox.Add(PierreTransitionMailId);
        }

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

                string transitionText = this.Helper.Translation.Get("mail.pierre.transition");
                data.Data[PierreTransitionMailId] = transitionText;

                if (this.PendingLateReturnPenalty > 0)
                {
                    string lateKey = ccComplete ? "mail.late.return.pierre" : "mail.late.return";
                    string lateText = this.Helper.Translation.Get(lateKey, new { penalty = this.PendingLateReturnPenalty });
                    data.Data[LateReturnMailId] = lateText;
                }
            });
        }
    }
}
