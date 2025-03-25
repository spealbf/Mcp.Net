using Mcp.Net.Core.Attributes;

namespace Mcp.Net.Examples.ExternalTools;

[McpTool("Math", "Mathematical operations")]
public class MathTools
{
    [McpTool("square", "Square a number")]
    public double Square(
        [McpParameter(required: true, description: "The number to square")] double number
    )
    {
        return number * number;
    }

    [McpTool("factorial", "Calculate the factorial of a number")]
    public long Factorial(
        [McpParameter(
            required: true,
            description: "The number to calculate factorial for (max 20)"
        )]
            int number
    )
    {
        if (number < 0)
            return 0;
        if (number > 20)
            return -1; // Avoid overflow

        long result = 1;
        for (int i = 2; i <= number; i++)
        {
            result *= i;
        }
        return result;
    }
}
