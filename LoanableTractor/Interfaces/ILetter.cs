namespace LoanableTractor.Interfaces
{
    /// <summary>Represents a letter from the Mail Framework Mod.</summary>
    public interface ILetter
    {
        /// <summary>The unique ID for this letter.</summary>
        string Id { get; set; }

        /// <summary>The letter text content.</summary>
        string Text { get; set; }

        /// <summary>The background index for the letter display.</summary>
        int WhichBG { get; set; }

        /// <summary>The text color index.</summary>
        int? TextColor { get; set; }
    }
}
