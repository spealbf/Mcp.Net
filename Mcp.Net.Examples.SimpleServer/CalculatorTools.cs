using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mcp.Net.Core.Attributes;

namespace Mcp.Net.Examples.SimpleServer
{
    /// <summary>
    /// Provides basic calculator functions as MCP tools
    /// </summary>
    public class CalculatorTools
    {
        [McpTool("calculator.add", "Add two numbers together")]
        public static double Add(
            [McpParameter(required: true, description: "First number")] double a,
            [McpParameter(required: true, description: "Second number")] double b
        )
        {
            return a + b;
        }

        [McpTool("calculator.subtract", "Subtract one number from another")]
        public static double Subtract(
            [McpParameter(required: true, description: "Number to subtract from")] double a,
            [McpParameter(required: true, description: "Number to subtract")] double b
        )
        {
            return a - b;
        }

        [McpTool("calculator.multiply", "Multiply two numbers")]
        public static double Multiply(
            [McpParameter(required: true, description: "First number")] double a,
            [McpParameter(required: true, description: "Second number")] double b
        )
        {
            return a * b;
        }

        [McpTool("calculator.divide", "Divide one number by another")]
        public static CalculationResult Divide(
            [McpParameter(required: true, description: "Dividend (number to be divided)")] double a,
            [McpParameter(required: true, description: "Divisor (number to divide by)")] double b
        )
        {
            if (b == 0)
            {
                return new CalculationResult
                {
                    Success = false,
                    ErrorMessage = "Cannot divide by zero",
                    Result = 0, // Use 0 instead of NaN which can't be serialized to JSON
                };
            }

            return new CalculationResult
            {
                Success = true,
                Result = a / b,
                Operation = $"{a} ÷ {b}",
            };
        }

        [McpTool("calculator.power", "Raise a number to a power")]
        public static double Power(
            [McpParameter(required: true, description: "Base number")] double baseNumber,
            [McpParameter(required: true, description: "Exponent")] double exponent
        )
        {
            return Math.Pow(baseNumber, exponent);
        }
    }

    public class CalculationResult
    {
        public bool Success { get; set; } = true;
        public double Result { get; set; }
        public string? Operation { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
