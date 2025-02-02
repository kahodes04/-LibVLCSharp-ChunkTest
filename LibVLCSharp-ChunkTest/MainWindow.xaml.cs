using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using LibVLCSharp.Shared;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace LibVLCSharp_ChunkTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly LibVLC libVlc = new LibVLC(enableDebugLogs: true);
        private BlockingStreamMediaInput? chunkStream;
        public MainWindow()
        {
            InitializeComponent();
            MediaPlayer mediaPlayer = new MediaPlayer(libVlc);
            Player.MediaPlayer = mediaPlayer;
        }
        void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            chunkStream = new BlockingStreamMediaInput();
            _ = Task.Run(async () => await this.StartTestStreamLocal(chunkStream));

            if (!Player.MediaPlayer.IsPlaying)
            {
                Player.MediaPlayer.Play(new Media(libVlc, chunkStream));
            }
        }

        private async Task StartTestStreamLocal(BlockingStreamMediaInput blockingStreamMediaInput, CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < 12; i++)
            {
                var chunk = await File.ReadAllBytesAsync($"chunks/bunnyChunk{i}.ts");
                blockingStreamMediaInput.AddChunk(chunk);
                Thread.Sleep(1000);
            }

        }
        private async Task StartTestStream(BlockingStreamMediaInput blockingStreamMediaInput, CancellationToken cancellationToken = default)
        {
            var chunks = await GetPlaylistList();
            DateTime playlistUpdateTime = DateTime.Now;

            //pre buffer
            var chunk4 = await GetTestChunk($"https://live-hls-abr-cdn.livepush.io/live/bigbuckbunnyclip/tracks-v2a1/{chunks[3]}");
            var chunk3 = await GetTestChunk($"https://live-hls-abr-cdn.livepush.io/live/bigbuckbunnyclip/tracks-v2a1/{chunks[2]}");
            var chunk2 = await GetTestChunk($"https://live-hls-abr-cdn.livepush.io/live/bigbuckbunnyclip/tracks-v2a1/{chunks[1]}");

            blockingStreamMediaInput.AddChunk(chunk2);
            blockingStreamMediaInput.AddChunk(chunk3);
            blockingStreamMediaInput.AddChunk(chunk4);

            Console.WriteLine($"DPHandler:StartTestStream - Prebuffered 3 chunks");

            string lastChunkName = chunks[^1];
            //finish prebuffer

            int targetDuration = 5;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    //check if need to wait before refreshing playlist
                    var elapsedTime = DateTime.Now - playlistUpdateTime;

                    if (elapsedTime.TotalSeconds < targetDuration)
                    {
                        var totalWaitTime = targetDuration - elapsedTime.TotalSeconds;
                        int waitTimeMs = (int)Math.Ceiling(totalWaitTime * 1000); // Convert to milliseconds

                        Console.WriteLine($"DPHandler:StartTestStream - Waiting {waitTimeMs} ms before updating playlist.");
                        await Task.Delay(waitTimeMs, cancellationToken);
                    }


                    //refresh playlist (chunk path buffer list)
                    chunks = await GetPlaylistList();
                    playlistUpdateTime = DateTime.Now;
                    int startIndex = chunks.LastIndexOf(lastChunkName) + 1;
                    for (int i = startIndex; i < chunks.Count; i++)
                    {
                        var chunkBytes = await GetTestChunk($"https://live-hls-abr-cdn.livepush.io/live/bigbuckbunnyclip/tracks-v2a1/{chunks[i]}");
                        Console.WriteLine($"DPHandler:StartTestStream - Downloaded chunk: {chunkBytes.Length}B");
                        blockingStreamMediaInput.AddChunk(chunkBytes);
                    }
                    //Set last chunk in list as last chunk loaded
                    lastChunkName = chunks[^1];
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DPHandler:StartLibVLCStream - Exception in stream loop: {ex.Message}");
                }
            }

            Console.WriteLine($"DPHandler:StartLibVLCStream - Finished writing chunks, waiting...");

        }
        private async Task<List<string>> GetPlaylistList()
        {
            var clientHandler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            var client = new HttpClient(clientHandler);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://live-hls-abr-cdn.livepush.io/live/bigbuckbunnyclip/tracks-v1a1/mono.m3u8"),
                Headers =
            {
                { "User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:134.0) Gecko/20100101 Firefox/134.0" },
                { "Accept", "*/*" },
                { "Accept-Language", "en-US,en;q=0.5" },
                { "Origin", "https://livepush.io" },
                { "Connection", "keep-alive" },
                { "Referer", "https://livepush.io/" },
                { "Sec-Fetch-Dest", "empty" },
                { "Sec-Fetch-Mode", "cors" },
                { "Sec-Fetch-Site", "same-site" },
            },
            };
            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                List<string> tsPaths = new List<string>();
                string pattern = @"\b\S+\.ts\b"; // Regex pattern to match .ts file paths

                // Split the content into lines
                string[] lines = body.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    if (Regex.IsMatch(line, pattern))
                    {
                        tsPaths.Add(line.Trim());
                    }
                }
                return tsPaths;
            }
        }
        private async Task<byte[]> GetTestChunk(string url)
        {
            var clientHandler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            var client = new HttpClient(clientHandler);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url),
                Headers =
            {
                { "User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:134.0) Gecko/20100101 Firefox/134.0" },
                { "Accept", "*/*" },
                { "Accept-Language", "en-US,en;q=0.5" },
                { "Origin", "https://livepush.io" },
                { "Connection", "keep-alive" },
                { "Referer", "https://livepush.io/" },
                { "Sec-Fetch-Dest", "empty" },
                { "Sec-Fetch-Mode", "cors" },
                { "Sec-Fetch-Site", "same-site" },
            },
            };
            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsByteArrayAsync();
                return body;
            }
        }
    }
}