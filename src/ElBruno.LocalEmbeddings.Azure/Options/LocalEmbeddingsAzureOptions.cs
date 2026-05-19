namespace ElBruno.LocalEmbeddings.Azure.Options;

/// <summary>
/// Configuration options for Azure OpenAI fallback integration.
/// </summary>
public class LocalEmbeddingsAzureOptions
{
    /// <summary>
    /// Gets or sets the Azure OpenAI endpoint URL (e.g., https://my-resource.openai.azure.com).
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the Azure OpenAI API key for authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the deployment name of the embedding model in Azure OpenAI.
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of fallback attempts before giving up.
    /// </summary>
    public int MaxFallbackAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the timeout in milliseconds for Azure OpenAI requests.
    /// </summary>
    public int TimeoutMilliseconds { get; set; } = 30_000;

    /// <summary>
    /// Gets or sets whether to log fallback events. Defaults to true.
    /// </summary>
    public bool LogFallbackEvents { get; set; } = true;

    /// <summary>
    /// Validates the options are properly configured.
    /// </summary>
    /// <returns>A list of validation errors, empty if valid.</returns>
    public IList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            errors.Add("Endpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            errors.Add("ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(DeploymentName))
        {
            errors.Add("DeploymentName is required.");
        }

        if (MaxFallbackAttempts < 1)
        {
            errors.Add("MaxFallbackAttempts must be at least 1.");
        }

        if (TimeoutMilliseconds < 1000)
        {
            errors.Add("TimeoutMilliseconds must be at least 1000.");
        }

        return errors;
    }
}
