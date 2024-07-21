using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Text;

namespace FunctionApp1
{
    public class HttpTrigger1
    {
        private readonly ILogger _logger;

        public HttpTrigger1(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HttpTrigger1>();
        }

        [Function("HttpTrigger1")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")]
              [FromQuery] string voice
            , [FromQuery] string style
            , [FromQuery] string role
            , [FromQuery] string rate
            , [FromQuery] string pitch
            , [FromQuery] string text
            )
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            string subscriptionKey = "4be9d883f1bf406d85cd3b6401d3b3";
            string ttsEndpoint = "https://eastus.tts.speech.microsoft.com/cognitiveservices/v1";
            string accessTokenEndpoint = "https://eastus.api.cognitive.microsoft.com/sts/v1.0/issueToken";
            string azureSpeechResourceName = "TextToSpeech";
            
            string vName = voice;
            string voiceName = "Microsoft Server Speech Text to Speech Voice (en-US, " + vName + ")";
            string voiceStyle = style;
            string vrole = role;
            string vrate = rate;
            string vpitch = pitch;

            string accessToken = string.Empty;
            string vtext = string.Empty;

            if (!string.IsNullOrEmpty(text))
            {
                vtext = text;
            }
            else
            {
                return new BadRequestObjectResult("Please pass text you wish to generate on the query string or in the request body using param: text");
            }

            try
            {
                accessToken = await FetchTokenAsync(accessTokenEndpoint, subscriptionKey).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult("Failed to obtain access token.");
            }

            string body = @"<speak 
                                    version='1.0' 
                                    xmlns='https://www.w3.org/2001/10/synthesis' 
                                    xmlns:mstts='https://www.w3.org/2001/mstts' 
                                    xml:lang='en-US'>
                                    <voice 
                                        name='" + voiceName + @"'>
                                            <mstts:express-as role='" + vrole + "' style='" + voiceStyle + @"'> 
                                                <prosody rate='" + vrate + "' pitch='" + vpitch + "' >"
                                                + vtext +
                                            @"</prosody> 
                                            </mstts:express-as>
                                    </voice>
                                </speak>";

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage())
                {
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(ttsEndpoint);
                    request.Content = new StringContent(body, Encoding.UTF8, "application/ssml+xml");
                    request.Headers.Add("Authorization", "Bearer " + accessToken);
                    request.Headers.Add("Connection", "Keep-Alive");
                    request.Headers.Add("User-Agent", azureSpeechResourceName);
                    request.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");

                    using (var response = await client.SendAsync(request).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var dataStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            string connectionString = "DefaultEndpointsProtocol=https;AccountName=texttospeechdata;AccountKey=TUUE79AVlH3xjTCZDp3G3VNIetAOQSVIJzt6aLjm49bXTazqFiWnAclW6F+tTddVcw==;EndpointSuffix=core.windows.net";
                            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                            CloudBlobClient client1 = storageAccount.CreateCloudBlobClient();
                            CloudBlobContainer container = client1.GetContainerReference("texttospeechcontainer");
                            await container.CreateIfNotExistsAsync();
                            CloudBlockBlob outputBlob = container.GetBlockBlobReference(Guid.NewGuid().ToString("n") + ".mp3");
                            await outputBlob.UploadFromStreamAsync(dataStream);
                            return new OkObjectResult(outputBlob.Uri.AbsoluteUri);
                        }
                    }
                }
            }
        }

        public static async Task<string> FetchTokenAsync(string tokenFetchUri, string subscriptionKey)
        {
            using (var client = new HttpClient())
            {
                Console.WriteLine(subscriptionKey);
                Console.WriteLine(tokenFetchUri);

                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                UriBuilder uriBuilder = new UriBuilder(tokenFetchUri);

                var result = await client.PostAsync(uriBuilder.Uri.AbsoluteUri, null).ConfigureAwait(false);
                return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
    }
}
