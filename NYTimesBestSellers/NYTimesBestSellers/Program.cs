using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NYTimesBestSellers
{
    class Result
    {
        public List<string> Titles { get; set; }
    }

    class Program
    {
        private static readonly DateTime FirstDateTime = new DateTime(2011, 2, 13);
        private static readonly HttpClient Client = new HttpClient();

        static async Task Main()
        {
            var loopDateTime = FirstDateTime;
            var dateTimes = new List<DateTime>();
            while (loopDateTime <= new DateTime(2022, 6, 26))
            {
                dateTimes.Add(loopDateTime);

                loopDateTime = loopDateTime.AddDays(7);
            }

            var results = new List<Result>();
            foreach (var dateTime in dateTimes)
            {
                var fictionResult = await MakeRequest(dateTime, "combined-print-and-e-book-fiction");
                var nonFictionResult = await MakeRequest(dateTime, "combined-print-and-e-book-nonfiction");
                results.Add(fictionResult);
                results.Add(nonFictionResult);
            }

            var books = new Dictionary<string, (int Points, int Count)>();
            const int maxPoints = 15;
            foreach (var result in results)
            {
                var titles = result.Titles;
                if (titles.Count > maxPoints)
                {
                    throw new Exception($"Max book titles is at least {titles.Count}");
                }

                if (titles.Count == 0)
                {
                    throw new Exception("That's odd.");
                }

                for (var i = 0; i < titles.Count; i++)
                {
                    var title = titles[i];
                    var points = maxPoints - i;
                    if (books.ContainsKey(title))
                    {
                        books[title] = (books[title].Points + points, books[title].Count + 1);
                    }
                    else
                    {
                        books[title] = (points, 1);
                    }
                }
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Title,Points,Count");
            foreach (var (key, (points, count)) in books)
            {
                var escapedKey = key.Contains(",") ? $"\"{key}\"" : key;
                stringBuilder.AppendLine($"{escapedKey},{points},{count}");
            }

            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "nyt.csv");
            await File.WriteAllTextAsync(filePath, stringBuilder.ToString());
        }

        public static async Task<Result> MakeRequest(DateTime dateTime, string category)
        {
            var url =
                $"https://www.nytimes.com/books/best-sellers/{dateTime.Year}/{dateTime.Month:D2}/{dateTime.Day:D2}/{category}/";

            HttpResponseMessage response = null;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                response = await Client.GetAsync(url);
            }
            catch (Exception)
            {
            }

            var j = 1;
            while (response == null || !response.IsSuccessStatusCode)
            {
                Thread.Sleep(j * 1000);
                try
                {
                    response = await Client.GetAsync(url);
                }
                catch (Exception)
                {
                }

                j *= 2;
            }
            Console.WriteLine($"{dateTime}\t{j}\t{stopwatch.ElapsedMilliseconds}");

            var responseBody = await response.Content.ReadAsStringAsync();

            const string pattern = @"itemProp=""name"">([^<]+)<\/h3>";
            var titles = new List<string>();
            foreach (Match match in Regex.Matches(responseBody, pattern))
            {
                var title = match.Groups[1].Value;
                titles.Add(title);
            }

            return new Result
            {
                Titles = titles,
            };
        }
    }
}
