using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceBuilder.Services;

namespace InvoiceBuilder.Modules.AiInvoiceDrafting;

public class GeminiInvoiceDraftService(HttpClient http, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly JsonElement ResponseSchema = JsonSerializer.Deserialize<JsonElement>("""
        {
            "type": "OBJECT",
            "properties": {
                "customerName": { "type": "STRING" },
                "invoiceDate": { "type": "STRING", "description": "ISO 8601 date, e.g. 2026-07-28" },
                "dueDate": { "type": "STRING", "description": "ISO 8601 date, e.g. 2026-08-11" },
                "taxRate": { "type": "NUMBER" },
                "notes": { "type": "STRING" },
                "lineItems": {
                    "type": "ARRAY",
                    "items": {
                        "type": "OBJECT",
                        "properties": {
                            "description": { "type": "STRING" },
                            "quantity": { "type": "NUMBER" },
                            "unitPrice": { "type": "NUMBER" }
                        },
                        "required": ["description", "quantity", "unitPrice"]
                    }
                }
            },
            "required": ["customerName", "lineItems"]
        }
        """);

    public async Task<DraftInvoiceResult> DraftAsync(
        string request, List<string> knownCustomerNames, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Ai:Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Missing configuration: Ai:Gemini:ApiKey. Set it via the Ai__Gemini__ApiKey environment variable.");
        }

        var model = configuration["Ai:Gemini:Model"] ?? "gemini-flash-latest";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var knownCustomersText = knownCustomerNames.Count > 0
            ? string.Join(", ", knownCustomerNames.Select(n => $"\"{n}\""))
            : "(none)";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = $"""
                                Today's date is {today:yyyy-MM-dd}. Extract structured invoice details from the
                                following request. If the invoice date isn't mentioned, use today's date. If the
                                due date isn't mentioned, leave it out. If a tax rate isn't mentioned, use 0. Keep
                                line item descriptions professional and concise. Every line item must have a
                                unitPrice greater than 0 — if the request doesn't give you enough information to
                                compute a dollar amount for something (e.g. "2% of their outstanding balance"
                                without stating the balance), do not invent a line item for it; instead describe
                                what's missing in the notes field so a human can fill it in.

                                Existing customers on file: {knownCustomersText}. If the request's customer name
                                is clearly one of these (even if worded or capitalized differently, e.g. "ACME
                                Corp" vs "Acme Corporation"), set customerName to that EXACT existing string.
                                Otherwise use the name as given in the request.

                                Request: {request}
                                """,
                        },
                    },
                },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = ResponseSchema,
            },
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };

        using var response = await http.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    ? "Gemini's free-tier rate limit was hit — wait a moment and try again."
                    : $"Gemini request failed ({(int)response.StatusCode}): {body}");
        }

        var envelope = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, cancellationToken);
        var json = envelope?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Gemini returned an empty response.");
        }

        var parsed = JsonSerializer.Deserialize<GeminiDraft>(json, JsonOptions)
            ?? throw new InvalidOperationException("Could not parse Gemini's response.");

        return new DraftInvoiceResult
        {
            CustomerName = parsed.CustomerName ?? "",
            InvoiceDate = ParseDate(parsed.InvoiceDate) ?? today,
            DueDate = ParseDate(parsed.DueDate),
            TaxRate = parsed.TaxRate,
            Notes = parsed.Notes,
            LineItems = (parsed.LineItems ?? [])
                .Select(li => new DraftLineItem
                {
                    Description = li.Description ?? "",
                    Quantity = li.Quantity <= 0 ? 1 : li.Quantity,
                    UnitPrice = li.UnitPrice,
                })
                .ToList(),
        };
    }

    private static DateOnly? ParseDate(string? value)
        => DateOnly.TryParse(value, out var date) ? date : null;

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart>? Parts { get; set; }
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class GeminiDraft
    {
        public string? CustomerName { get; set; }
        public string? InvoiceDate { get; set; }
        public string? DueDate { get; set; }
        public decimal TaxRate { get; set; }
        public string? Notes { get; set; }
        public List<GeminiLineItem>? LineItems { get; set; }
    }

    private class GeminiLineItem
    {
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
