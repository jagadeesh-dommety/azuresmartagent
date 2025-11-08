using OpenAI;
using OpenAI.Embeddings;

public class AzureOpenAIClient
{
    private readonly EmbeddingClient embeddingClient;

    public AzureOpenAIClient(string endpointUrl, string apiKey, string model)
    {
        var endpoint = new Uri(endpointUrl);

        OpenAIClient client = new(
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAIClientOptions()
            {
                Endpoint = endpoint
            });

        embeddingClient = client.GetEmbeddingClient(model);
    }

    public OpenAIEmbeddingCollection GenerateEmbeddings(string[] inputs)
    {
        return embeddingClient.GenerateEmbeddings(inputs);
    }
}