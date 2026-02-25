using System;

namespace LoanableTractor.Interfaces
{
    /// <summary>API interface for the Mail Framework Mod by Digus.</summary>
    public interface IMailFrameworkModApi
    {
        /// <summary>Register a letter to be delivered when conditions are met.</summary>
        /// <param name="letter">The letter to register.</param>
        /// <param name="condition">Condition check called each day to determine if the letter should be delivered.</param>
        /// <param name="callback">Callback invoked after the letter is read by the player.</param>
        void RegisterLetter(ILetter letter, Func<ILetter, bool> condition, Action<ILetter> callback = null);

        /// <summary>Create a new letter instance.</summary>
        /// <param name="id">Unique ID for the letter.</param>
        /// <param name="text">Letter text content.</param>
        /// <param name="recipe">Recipe to include, or null.</param>
        /// <param name="callback">Callback on letter read.</param>
        /// <param name="whichBG">Background index for the letter.</param>
        ILetter CreateLetter(string id, string text, string recipe = null, Action<ILetter> callback = null, int whichBG = 0);
    }
}
