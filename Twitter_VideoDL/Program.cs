/*
MIT License

Copyright (c) 2021 SantiSC

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

/*
    DISCLAIMER: I wrote this with the purpose of learning and practising. I'm not responsible of how you use this project. Please consider copyright before downloading 
    anything from twitter.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;

namespace Twitter_VideoDL
{
    class Program
    {
        private const string playerUrl = "https://twitter.com/i/videos/tweet/";
        private const string apiUrl = "https://api.twitter.com/1.1/videos/tweet/config/";
        private const string mediaUrl = "https://video.twimg.com";
        private const string guestTokenUrl = "https://api.twitter.com/1.1/guest/activate.json";

        static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                // Get the url
                string tweetUrl = args[0];
                Console.Write($"Downloading: {tweetUrl}");

                // Split the url in parts (3 is the author, 5 is the id)
                string[] urlParts = tweetUrl.Split('/');

                // Create the tweet instance
                Tweet tweet = new Tweet(tweetUrl, urlParts[3], urlParts[5]);

                // Get the token
                string token = GetToken(tweet);

                // Get the playlist/m3u8 file url
                string playlistUrl = GetPlaylistUrl(tweet, token);

                // Check if it's a gif or a playlist
                if (playlistUrl.EndsWith(".mp4") && !playlistUrl.Contains(".m3u8"))
                {
                    using (WebClient webClient = new WebClient())
                    {
                        if (!File.Exists($"{tweet.ID}.mp4"))
                        {
                            webClient.DownloadFile(playlistUrl, $"{tweet.ID}.mp4");
                        }
                    }
                }
                // It's a playlist
                else
                {
                    // Get the playlist file content
                    string playlistContent = GetPlaylistContent(playlistUrl);

                    // Split the content lines
                    string[] playListLines = playlistContent.Split('\n');

                    // The higher quality file is usually at the end of the file
                    string lastLine = playListLines[playListLines.Length - 2]; // -2 Cause the last line of the array is empty

                    // Prepare ffmpeg
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = "ffmpeg.exe",
                        Arguments = $"-hide_banner -loglevel error -i {mediaUrl + lastLine} -c copy -bsf:a aac_adtstoasc {tweet.ID}.mp4"
                    };

                    // Create the ffmpeg process
                    Process cmdProcess = new Process
                    {
                        StartInfo = startInfo
                    };

                    // Start the process
                    cmdProcess.Start();
                    cmdProcess.WaitForExit();
                }

                Console.WriteLine(" -> Download completed");
            }

            Console.ReadLine();
        }

        private static string GetToken(Tweet tweet)
        {
            // Get the video url
            string videoUrl = playerUrl + tweet.ID;

            // Request the html
            WebRequest htmlRequest = WebRequest.Create(videoUrl);

            // Get the response
            HttpWebResponse htmlResponse = (HttpWebResponse)htmlRequest.GetResponse();

            // Get the html content
            Stream htmlResponseStream = htmlResponse.GetResponseStream();
            string responseHtml = new StreamReader(htmlResponseStream).ReadToEnd();

            // Close the html response
            htmlResponse.Close();

            // Get the JS url
            int srcIndex = responseHtml.IndexOf("src=") + 5;
            int srcEndIndex = responseHtml.IndexOf(".js") + 3;
            int urlLength = srcEndIndex - srcIndex;

            string jsUrl = responseHtml.Substring(srcIndex, urlLength);

            // Request the js
            WebRequest jsRequest = WebRequest.Create(jsUrl);

            // Get the response
            HttpWebResponse jsResponse = (HttpWebResponse)jsRequest.GetResponse();

            // Get the js content
            Stream jsResponseStream = jsResponse.GetResponseStream();
            string responseJS = new StreamReader(jsResponseStream).ReadToEnd();

            // Close the js response
            jsResponse.Close();

            // Extract the bearer token
            int bearerIndex = responseJS.IndexOf("r={authorization:") + 18;
            int bearerEndIndex = responseJS.IndexOf('"', bearerIndex);
            int tokenLength = bearerEndIndex - bearerIndex;

            string bearerToken = responseJS.Substring(bearerIndex, tokenLength);

            return bearerToken;
        }

        private static string GetGuestToken(string bearerToken)
        {
            // Request the json
            WebRequest jsonRequest = WebRequest.Create(guestTokenUrl);
            jsonRequest.Headers.Add("Authorization", bearerToken);
            jsonRequest.Method = "POST";

            // Get the response
            HttpWebResponse jsonResponse = (HttpWebResponse)jsonRequest.GetResponse();

            // Get the json content
            Stream jsonResponseStream = jsonResponse.GetResponseStream();
            string responseJson = new StreamReader(jsonResponseStream).ReadToEnd();

            // Close the json response
            jsonResponse.Close();

            // Create a JObject to parse the json without needing to create a new class
            JObject o = JObject.Parse(responseJson);

            // Return the guest token
            Console.WriteLine(o["guest_token"].ToString());
            return o["guest_token"].ToString();
        }

        private static string GetPlaylistUrl(Tweet tweet, string Token)
        {
            // Get the playlist url
            string playlistUrl = apiUrl + tweet.ID + ".json";

            // Request the json
            WebRequest jsonRequest = WebRequest.Create(playlistUrl);

            // Add the authorization header
            jsonRequest.Headers.Add("Authorization", Token);

            // Add the guest token
            jsonRequest.Headers.Add("x-guest-token", GetGuestToken(Token));

            // Get the response
            HttpWebResponse jsonResponse = (HttpWebResponse)jsonRequest.GetResponse();

            // Get the json content
            Stream jsonResponseStream = jsonResponse.GetResponseStream();
            string responseJson = new StreamReader(jsonResponseStream).ReadToEnd();

            // Close the json response
            jsonResponse.Close();

            // Create a JObject to parse the json without needing to create a new class
            JObject o = JObject.Parse(responseJson);

            // Extract the m3u8 (playlist) url from the json and return it
            return o["track"]["playbackUrl"].ToString();
        }

        private static string GetPlaylistContent(string playlistUrl)
        {
            // Request the file content
            WebRequest playlistRequest = WebRequest.Create(playlistUrl);

            // Get the response
            HttpWebResponse playlistResponse = (HttpWebResponse)playlistRequest.GetResponse();

            // Get the playlist content
            Stream playlistResponseStream = playlistResponse.GetResponseStream();
            string responsePlaylist = new StreamReader(playlistResponseStream).ReadToEnd();

            // Close the html response
            playlistResponse.Close();

            // Return the file content
            return responsePlaylist;
        }
    }
}
