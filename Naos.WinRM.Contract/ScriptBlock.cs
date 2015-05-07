// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ScriptBlock.cs" company="Naos">
//   Copyright 2015 Naos
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Naos.WinRM.Contract
{
    /// <summary>
    /// Representation of a command to run on a remote machine.
    /// </summary>
    public class ScriptBlock
    {
        /// <summary>
        /// Gets or sets the text of the script block (MUST include the opening and closing curly braces i.e. { LS C:\Temp\ }).
        /// </summary>
        public string ScriptText { get; set; }
    }
}
