using System;
using System.Collections.Generic;

namespace Mcp.Net.LLM.Models.Exceptions
{
    /// <summary>
    /// Exception thrown when one or more requested tools cannot be found in the tool registry.
    /// </summary>
    public class ToolNotFoundException : Exception
    {
        /// <summary>
        /// Gets the list of tool IDs that could not be found.
        /// </summary>
        public IReadOnlyList<string> MissingToolIds { get; }

        /// <summary>
        /// Creates a new instance of the <see cref="ToolNotFoundException"/> class.
        /// </summary>
        /// <param name="missingToolIds">The list of tool IDs that could not be found.</param>
        /// <param name="message">The error message.</param>
        public ToolNotFoundException(IEnumerable<string> missingToolIds, string message)
            : base(message)
        {
            MissingToolIds = new List<string>(missingToolIds);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ToolNotFoundException"/> class.
        /// </summary>
        /// <param name="missingToolIds">The list of tool IDs that could not be found.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ToolNotFoundException(
            IEnumerable<string> missingToolIds,
            string message,
            Exception innerException
        )
            : base(message, innerException)
        {
            MissingToolIds = new List<string>(missingToolIds);
        }
    }
}
