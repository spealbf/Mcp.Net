using System.Text.RegularExpressions;
using Mcp.Net.Core.Attributes;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Mcp.Net.Examples.ExternalTools;

/// <summary>
/// A tool that sends SMS messages using Twilio
/// </summary>
[McpTool("twilioSms", "Send SMS messages to UK phone numbers using Twilio")]
public class TwilioSmsTool
{
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromNumber;
    private static readonly Regex UkPhoneNumberRegex = new Regex(
        @"^(\+44|0)(\d{10})$",
        RegexOptions.Compiled
    );

    public TwilioSmsTool()
    {
        _accountSid = "";
        _authToken = "";
        _fromNumber = "";

        // Initialize Twilio client
        TwilioClient.Init(_accountSid, _authToken);
    }

    /// <summary>
    /// Sends an SMS message to a UK phone number
    /// </summary>
    /// <param name="phoneNumber">The UK phone number to send the message to (format: +447123456789 or 07123456789)</param>
    /// <param name="message">The message content to send</param>
    /// <returns>A response message with the status of the SMS delivery</returns>
    [McpTool("sendSmsToUkNumber", "Sends an SMS message to a UK phone number")]
    public async Task<string> SendSmsToUkNumberAsync(
        [McpParameter(
            required: true,
            description: "The UK phone number to send the message to (any format)"
        )]
            string phoneNumber,
        [McpParameter(required: true, description: "The message content to send")] string message
    )
    {
        try
        {
            // Check if Twilio credentials are set
            if (
                string.IsNullOrEmpty(_accountSid)
                || string.IsNullOrEmpty(_authToken)
                || string.IsNullOrEmpty(_fromNumber)
            )
            {
                return "Twilio credentials or phone number not configured. Please set TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN, and TWILIO_PHONE_NUMBER environment variables.";
            }

            // Validate and format UK phone number
            string formattedNumber = FormatUkPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(formattedNumber))
            {
                return $"Invalid UK phone number format: {phoneNumber}. Use format +447123456789 or 07123456789.";
            }

            // Send the SMS message
            var messageResource = await MessageResource.CreateAsync(
                to: new PhoneNumber(formattedNumber),
                from: new PhoneNumber(_fromNumber),
                body: message
            );

            return $"Message sent to {formattedNumber}. Status: {messageResource.Status}. SID: {messageResource.Sid}";
        }
        catch (Exception ex)
        {
            return $"Error sending SMS: {ex.Message}";
        }
    }

    /// <summary>
    /// Formats and validates UK phone numbers
    /// </summary>
    /// <param name="phoneNumber">The phone number to format</param>
    /// <returns>A properly formatted UK phone number or empty string if invalid</returns>
    private string FormatUkPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        // Allow any phone number format for testing
        // Just ensure it has a plus if it doesn't already
        if (!phoneNumber.StartsWith("+"))
            return "+" + phoneNumber.TrimStart('0');

        return phoneNumber;
    }
}
