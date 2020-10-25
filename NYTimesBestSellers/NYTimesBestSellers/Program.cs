using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MoreLinq.Extensions;

namespace NYTimesBestSellers
{
    class Result
    {
        public bool Failed { get; set; }
        public List<string> Titles { get; set; }
        public DateTime DateTime { get; set; }
    }

    public static class BatchExtension
    {
        public static async Task<List<TResult>> BatchRequests<TResult>(this IEnumerable<Task<TResult>> requests, int batchSize)
        {
            var results = new List<TResult>();

            foreach (var requestsBatch in requests.Batch(batchSize))
            {
                results.AddRange(await Task.WhenAll(requestsBatch));
            }

            return results;
        }
    }

    class Program
    {
        private static readonly DateTime FirstDateTime = new DateTime(2011, 2, 13);
        private static readonly HttpClient Client = new HttpClient();

        static async Task Main()
        {
            var loopDateTime = FirstDateTime;
            var dateTimes = new List<DateTime>();
            while (loopDateTime <= new DateTime(2020, 11, 2))
            {
                dateTimes.Add(loopDateTime);

                loopDateTime = loopDateTime.AddDays(7);
                // if (loopDateTime == new DateTime(2011, 4, 3))
                // {
                //     loopDateTime = loopDateTime.AddDays(1);
                // }
            }

            var results = new List<Result>();
            foreach (var dateTime in dateTimes)
            {
                var result = await MakeRequest(dateTime);
                results.Add(result);
            }

            var books = new Dictionary<string, int>();
            var max = 0;
            const int maxPoints = 15;
            foreach (var result in results.Where(result => !result.Failed))
            {
                var titles = result.Titles;
                max = titles.Count;
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
                        books[title] = books[title] + points;
                    }
                    else
                    {
                        books[title] = points;
                    }
                }
            }

            var stringBuilder = new StringBuilder();
            foreach (var (key, value) in books)
            {
                var key2 = key.Contains(",") ? $"\"{key}\"" : key;
                stringBuilder.AppendLine($"{key2},{value}");
            }

            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "nyt-non.csv");
            await File.WriteAllTextAsync(filePath, stringBuilder.ToString());

            Console.WriteLine("Failed:");
            foreach (var result in results.Where(result => result.Failed))
            {
                Console.WriteLine($"{result.DateTime}");
            }
            Console.WriteLine($"Max: {max}");
        }

        public static async Task<Result> MakeRequest(DateTime dateTime)
        {
            var url =
                $"https://www.nytimes.com/books/best-sellers/{dateTime.Year}/{dateTime.Month:D2}/{dateTime.Day:D2}/combined-print-and-e-book-nonfiction/";
            var response = await Client.GetAsync(url);

            var j = 1;
            while (!response.IsSuccessStatusCode)
            {
                if (j > 16)
                {
                    ;
                }
                Thread.Sleep(j * 1000);
                response = await Client.GetAsync(url);
                j *= 2;
            }
            Console.WriteLine(dateTime);

            if (!response.IsSuccessStatusCode)
            {
                return new Result
                {
                    Failed = true,
                    DateTime = dateTime,
                };
            }
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
