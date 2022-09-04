﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace C0bW3b
{
    public class Scraper
    {
        public static List<Thread> RunningScrapers = new List<Thread>();
        public static List<ScrapeHit> ScrapeHits = new List<ScrapeHit>();

        public class ScrapeHit
        {
            public string Dork;
            public string Url;
            public string Html;
            public List<string> Matches = new List<string>();
            public WebProxy Proxy;

            public ScrapeHit(string dork, string url, string html, List<string> matches, WebProxy proxy)
            {
                this.Dork = dork;
                this.Url = url;
                this.Html = html;

                if (matches != null)
                    this.Matches = matches;
                else
                    this.Matches = new List<string>();

                this.Proxy = proxy;
            }
        }

        public static void Start(int threads, bool proxyless, bool regexmatches, bool allowduplicates, bool logfullurl, int minmatch)
        {
            for (int i = 0; i < threads; i++)
            {
                Thread t = new Thread(() => Scrape(proxyless, regexmatches, allowduplicates, logfullurl, minmatch));
                RunningScrapers.Add(t);
                t.Start();
            }
        }

        public static void Scrape(bool proxyless, bool regexmatches, bool allowduplicates, bool logfullurl, int minmatch)
        {
            while (true)
            {
                try
                {
                    string dork = GUI.Dorks[new Random().Next(GUI.Dorks.Length)];
                    string useragent = UserAgents.Agents[new Random().Next(UserAgents.Agents.Length)];
                    WebProxy proxy = null;
                    List<ScrapeHit> results = new List<ScrapeHit>();

                    if (!proxyless)
                        proxy = GUI.Proxies[new Random().Next(GUI.Proxies.Length)];

                    try { results.AddRange(Google(dork, useragent, proxyless, allowduplicates, logfullurl, proxy)); } catch { }
                    try { results.AddRange(Bing(dork, useragent, proxyless, allowduplicates, logfullurl, proxy)); } catch { }

                    if (results != null && results.Count > 0)
                    {
                        foreach (ScrapeHit result in results)
                        {
                            try
                            {
                                result.Html = AnalyseURL(result);

                                foreach (string match in GUI.Matches)
                                {
                                    if (!regexmatches)
                                    {
                                        if (result.Html.Contains(match))
                                            result.Matches.Add(match);
                                    }
                                    else
                                    {
                                        Regex regex = new Regex(match);
                                        foreach (Match rmatch in regex.Matches(result.Html))
                                            result.Matches.Add(rmatch.Groups[1].Value);
                                    }
                                }

                                if (result.Matches.Count >= minmatch)
                                {
                                    if (allowduplicates || ScrapeHits.FindAll(x => x.Url == result.Url).Count == 0)
                                    {
                                        ScrapeHits.Add(result);
                                        GUI.instance.Hits++;
                                        GUI.instance.AddScrapeHit(result);
                                    }
                                }
                                else
                                    GUI.instance.Bad++;
                            }
                            catch { GUI.instance.Retries++; }
                        }
                    }
                }
                catch { GUI.instance.Retries++; }
            }
        }

        public static string AnalyseURL(ScrapeHit hit)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://" + hit.Url);
            request.Proxy = hit.Proxy;

            request.UserAgent = UserAgents.Agents[new Random().Next(UserAgents.Agents.Length)];
            request.Timeout = 2000;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream());
            string html = reader.ReadToEnd();
            reader.Close();
            response.Close();

            return html;
        }

        public static List<ScrapeHit> Google(string dork, string useragent, bool proxyless, bool allowduplicates, bool logfullurl, WebProxy proxy = null)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://www.google.com/search?q=" + dork);
                if (!proxyless)
                    request.Proxy = proxy;

                request.UserAgent = useragent;
                request.Timeout = 2000;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream());
                string html = reader.ReadToEnd();
                reader.Close();
                response.Close();


                // get result urls
                List<ScrapeHit> results = new List<ScrapeHit>();
                try
                {
                    string searches = html.Split(new string[] { "id=\"rso\">" }, StringSplitOptions.None)[1].Split(new string[] { "<div id=\"bottomads\">" }, StringSplitOptions.None)[0];

                    // get all urls from search results using regex and add to list´
                    Regex regex = new Regex(@"<a href=""(.*?)"">");
                    foreach (Match match in regex.Matches(searches))
                    {
                        try
                        {
                            string url = match.Groups[1].Value;

                            if (url.Contains("https://"))
                                results.Add(new ScrapeHit(dork, logfullurl ? url.Replace("https://", "").Split('"')[0] : new Uri(url).Host, null, null, proxy));
                        }
                        catch { GUI.instance.Retries++; }
                    }
                }
                catch { GUI.instance.Retries++; }

                return results;
            }
            catch { GUI.instance.Retries++; }

            return new List<ScrapeHit>();
        }

        public static List<ScrapeHit> Bing(string dork, string useragent, bool proxyless, bool allowduplicates, bool logfullurl, WebProxy proxy = null)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://bing.com/search?q=" + dork);
                if (!proxyless)
                    request.Proxy = proxy;

                request.UserAgent = useragent;
                request.Timeout = 2000;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream());
                string html = reader.ReadToEnd();
                reader.Close();
                response.Close();

                List<ScrapeHit> results = new List<ScrapeHit>();
                try
                {
                    string searches = html.Split(new string[] { "<ol id=\"b_results\" class=\"\">" }, StringSplitOptions.None)[1].Split(new string[] { "<aside aria-label=\"Additional Results\">" }, StringSplitOptions.None)[0];

                    // get all urls from search results using regex and add to list
                    Regex regex = new Regex(@"<a href=""(.*?)"">");
                    foreach (Match match in regex.Matches(searches))
                    {
                        try
                        {
                            string url = match.Groups[1].Value;
                            if (url.Contains("https://"))
                                results.Add(new ScrapeHit(dork, logfullurl ? url.Replace("https://", "").Split('"')[0] : new Uri(url).Host, null, null, proxy));
                        }
                        catch { GUI.instance.Retries++; }
                    }
                }
                catch { GUI.instance.Retries++; }
                return results;
            }
            catch { GUI.instance.Retries++; }
            return new List<ScrapeHit>();
        }
    }
}